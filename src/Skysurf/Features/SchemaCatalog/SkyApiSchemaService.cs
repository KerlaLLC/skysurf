using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using MonkeyCache;
using MonkeyCache.FileStore;
using skysurf.App;
using skysurf.Features.ParameterEntry;

namespace skysurf.Features.SchemaCatalog;

public sealed class SkyApiSchemaService
{
    private const string ApisCacheKey = "sky_apis";
    private const string SpecsCacheKey = "sky_specs";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromDays(7);

    private static readonly HashSet<string> PaginationParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "limit", "offset", "page_size", "page_number", "cursor", "continuation_token", "marker"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SkyApiSchemaService> _logger;
    private readonly IBarrel _cache;
    private readonly object _lock = new();

    private List<SkyApi> _apis = [];
    private Dictionary<string, SkyApiSpec> _specs = new();
    private List<SkyEndpoint> _endpoints = [];

    public SkyApiSchemaService(
        IHttpClientFactory httpClientFactory,
        AppPaths paths,
        ILogger<SkyApiSchemaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cache = Barrel.Create(paths.CacheDirectory);
    }

    public IReadOnlyList<SkyEndpoint> Endpoints
    {
        get
        {
            lock (_lock)
            {
                return _endpoints.ToList();
            }
        }
    }

    public bool TryLoadFromCache()
    {
        try
        {
            if (!_cache.Exists(ApisCacheKey) || !_cache.Exists(SpecsCacheKey))
            {
                return false;
            }

            var apis = _cache.Get<List<SkyApi>>(ApisCacheKey);
            var rawSpecs = _cache.Get<Dictionary<string, string>>(SpecsCacheKey);

            if (apis is not { Count: > 0 } || rawSpecs is null)
            {
                return false;
            }

            var specs = new Dictionary<string, SkyApiSpec>();

            foreach (var (apiId, rawJson) in rawSpecs)
            {
                var parsed = ParseOpenApiSpec(apiId, rawJson);
                if (parsed is not null)
                {
                    specs[apiId] = parsed;
                }
            }

            lock (_lock)
            {
                _apis = apis;
                _specs = specs;
                _endpoints = BuildEndpoints(apis, specs);
            }

            return _endpoints.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load SKY schemas from cache.");
            return false;
        }
    }

    public async Task RefreshAsync(
        IProgress<SchemaRefreshProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var managementApiVersion = await GetManagementApiVersionAsync(client, cancellationToken);
        var apis = await FetchApisAsync(client, managementApiVersion, cancellationToken);
        var specs = new Dictionary<string, SkyApiSpec>();
        var rawSpecs = new Dictionary<string, string>();

        for (var index = 0; index < apis.Count; index++)
        {
            var api = apis[index];
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var (spec, rawJson) = await FetchSpecAsync(client, managementApiVersion, api.Id, cancellationToken);
                specs[api.Id] = spec;
                rawSpecs[api.Id] = rawJson;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch SKY schema for {ApiId}", api.Id);
            }

            progress?.Report(new SchemaRefreshProgress(index + 1, apis.Count, api.Name));
        }

        lock (_lock)
        {
            _apis = apis;
            _specs = specs;
            _endpoints = BuildEndpoints(apis, specs);
        }

        _cache.Add(ApisCacheKey, apis, CacheExpiration);
        _cache.Add(SpecsCacheKey, rawSpecs, CacheExpiration);
    }

    public SkyEndpoint? FindEndpoint(string apiId, string path, string httpMethod)
    {
        lock (_lock)
        {
            return _endpoints.FirstOrDefault(x =>
                x.ApiId.Equals(apiId, StringComparison.OrdinalIgnoreCase)
                && x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)
                && x.HttpMethod.Equals(httpMethod, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task<string> GetManagementApiVersionAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.GetStringAsync("https://developer.sky.blackbaud.com/config.json", cancellationToken);
        using var document = JsonDocument.Parse(response);
        return document.RootElement.GetProperty("managementApiVersion").GetString()
            ?? throw new InvalidOperationException("managementApiVersion not found in SKY config.");
    }

    private async Task<List<SkyApi>> FetchApisAsync(
        HttpClient client,
        string managementApiVersion,
        CancellationToken cancellationToken)
    {
        var url = $"https://developer.sky.blackbaud.com/developer/apisByTags?api-version={managementApiVersion}";
        var response = await client.GetStringAsync(url, cancellationToken);
        using var document = JsonDocument.Parse(response);

        var apis = new List<SkyApi>();
        var seenApiIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in document.RootElement.GetProperty("value").EnumerateArray())
        {
            var api = item.GetProperty("api");
            var id = api.GetProperty("id").GetString();

            if (string.IsNullOrWhiteSpace(id) || !seenApiIds.Add(id))
            {
                continue;
            }

            apis.Add(new SkyApi
            {
                Id = id,
                Name = api.GetProperty("name").GetString() ?? id,
                Description = api.TryGetProperty("description", out var description)
                    ? description.GetString() ?? string.Empty
                    : string.Empty,
                Path = api.TryGetProperty("path", out var path)
                    ? path.GetString() ?? string.Empty
                    : string.Empty
            });
        }

        return apis.OrderBy(x => x.Name).ToList();
    }

    private async Task<(SkyApiSpec Spec, string RawJson)> FetchSpecAsync(
        HttpClient client,
        string managementApiVersion,
        string apiId,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://developer.sky.blackbaud.com/developer/apis/{Uri.EscapeDataString(apiId)}?export=true&api-version={managementApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.oai.openapi+json");

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var spec = ParseOpenApiSpec(apiId, rawJson)
            ?? throw new InvalidOperationException($"Failed to parse SKY schema for {apiId}.");

        return (spec, rawJson);
    }

    private SkyApiSpec? ParseOpenApiSpec(string apiId, string rawJson)
    {
        try
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rawJson));
            var reader = new OpenApiStreamReader();
            var document = reader.Read(stream, out var diagnostic);

            if (diagnostic.Errors.Count > 0)
            {
                _logger.LogWarning(
                    "OpenAPI parse errors for {ApiId}: {Errors}",
                    apiId,
                    string.Join("; ", diagnostic.Errors.Select(x => x.Message)));
            }

            return new SkyApiSpec
            {
                ApiId = apiId,
                Document = document
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse SKY schema for {ApiId}", apiId);
            return null;
        }
    }

    private static List<SkyEndpoint> BuildEndpoints(
        IReadOnlyList<SkyApi> apis,
        IReadOnlyDictionary<string, SkyApiSpec> specs)
    {
        var endpoints = new List<SkyEndpoint>();
        var apiLookup = apis.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var (apiId, spec) in specs)
        {
            if (!apiLookup.TryGetValue(apiId, out var api))
            {
                continue;
            }

            var serverBasePath = string.Empty;
            var serverUrl = spec.Document.Servers?.FirstOrDefault()?.Url;
            if (!string.IsNullOrEmpty(serverUrl)
                && Uri.TryCreate(serverUrl, UriKind.Absolute, out var parsedServerUri))
            {
                serverBasePath = parsedServerUri.AbsolutePath.TrimEnd('/');
            }

            foreach (var (path, pathItem) in spec.Document.Paths)
            {
                foreach (var (operationType, operation) in pathItem.Operations)
                {
                    if (operationType != OperationType.Get)
                    {
                        continue;
                    }

                    var allParams = pathItem.Parameters
                        .Concat(operation.Parameters)
                        .Where(x => x.In == ParameterLocation.Path || x.In == ParameterLocation.Query)
                        .GroupBy(x => $"{x.Name}:{x.In}")
                        .Select(group => group.First())
                        .ToList();

                    var requiredParameters = allParams
                        .Where(x => x.Required)
                        .Select(x => new ParameterPrompt(
                            x.Name,
                            (x.In?.ToString() ?? "query").ToLowerInvariant(),
                            x.Schema?.Type ?? "string",
                            x.Description ?? string.Empty))
                        .OrderBy(x => x.Location)
                        .ThenBy(x => x.Name)
                        .ToList();

                    var optionalParameters = allParams
                        .Where(x => !x.Required && !PaginationParameterNames.Contains(x.Name))
                        .Select(x => new ParameterPrompt(
                            x.Name,
                            (x.In?.ToString() ?? "query").ToLowerInvariant(),
                            x.Schema?.Type ?? "string",
                            x.Description ?? string.Empty))
                        .OrderBy(x => x.Name)
                        .ToList();

                    endpoints.Add(new SkyEndpoint
                    {
                        ApiId = apiId,
                        ApiName = api.Name,
                        Path = serverBasePath + path,
                        HttpMethod = operationType.ToString().ToUpperInvariant(),
                        OperationId = operation.OperationId ?? string.Empty,
                        SchemaModelName = ResolveSchemaModelName(operation),
                        RequiredParameters = requiredParameters,
                        OptionalParameters = optionalParameters
                    });
                }
            }
        }

        return endpoints
            .OrderBy(x => x.ApiName)
            .ThenBy(x => x.Path)
            .ToList();
    }

    private static string ResolveSchemaModelName(OpenApiOperation operation)
    {
        var response = operation.Responses
            .Where(x => x.Key.StartsWith('2'))
            .Select(x => x.Value)
            .FirstOrDefault();

        if (response is null)
        {
            return "Json";
        }

        var schema = response.Content
            .Where(x => x.Key.Contains("json", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value.Schema)
            .FirstOrDefault();

        return ResolveSchemaName(schema) ?? "Json";
    }

    private static string? ResolveSchemaName(OpenApiSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(schema.Reference?.Id))
        {
            return schema.Reference.Id;
        }

        if (schema.Items is not null)
        {
            return ResolveSchemaName(schema.Items);
        }

        if (schema.Properties.TryGetValue("value", out var valueProperty))
        {
            return ResolveSchemaName(valueProperty) ?? ResolveSchemaName(valueProperty.Items);
        }

        foreach (var composedSchema in schema.AllOf.Concat(schema.AnyOf).Concat(schema.OneOf))
        {
            var resolved = ResolveSchemaName(composedSchema);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return schema.Type switch
        {
            "array" => "Array",
            "object" => "Object",
            _ => schema.Type
        };
    }
}

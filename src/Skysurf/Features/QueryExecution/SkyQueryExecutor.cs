using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shared.Authentication;
using Shared.Connections;
using skysurf.Features.SchemaCatalog;

namespace skysurf.Features.QueryExecution;

public sealed class SkyQueryExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SkyAuthenticationService _authenticationService;

    public SkyQueryExecutor(
        IHttpClientFactory httpClientFactory,
        SkyAuthenticationService authenticationService)
    {
        _httpClientFactory = httpClientFactory;
        _authenticationService = authenticationService;
    }

    public async Task<QueryResult> ExecuteAsync(
        ConnectionRecord connection,
        SkyEndpoint endpoint,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await _authenticationService.GetAccessTokenAsync(connection, cancellationToken: cancellationToken);
        var client = _httpClientFactory.CreateClient();
        var baseUrl = "https://api.sky.blackbaud.com";
        var allItems = new List<JsonElement>();
        var url = BuildUrl(baseUrl, endpoint.Path, values);
        var hasMorePages = false;
        JsonElement? singlePayload = null;
        var isCollectionResult = false;

        do
        {
            hasMorePages = false;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("Bb-Api-Subscription-Key", connection.SubscriptionKey);

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"SKY request failed with HTTP {(int)response.StatusCode}: {body}");
            }

            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                isCollectionResult = true;
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    allItems.Add(item.Clone());
                }
            }
            else if (document.RootElement.TryGetProperty("value", out var valueArray)
                && valueArray.ValueKind == JsonValueKind.Array)
            {
                isCollectionResult = true;
                var pageItemCount = valueArray.GetArrayLength();
                foreach (var item in valueArray.EnumerateArray())
                {
                    allItems.Add(item.Clone());
                }

                var nextLink = document.RootElement.TryGetProperty("next_link", out var nextLinkProperty)
                    ? nextLinkProperty.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(nextLink))
                {
                    url = nextLink.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? nextLink
                        : $"{baseUrl.TrimEnd('/')}/{nextLink.TrimStart('/')}";
                    hasMorePages = pageItemCount > 0;
                }
                else if (pageItemCount > 0
                    && document.RootElement.TryGetProperty("page", out var pageProperty)
                    && pageProperty.TryGetInt32(out var currentPage)
                    && document.RootElement.TryGetProperty("count", out var countProperty)
                    && countProperty.TryGetInt32(out var count)
                    && count == pageItemCount)
                {
                    url = BuildUrl(baseUrl, endpoint.Path, values, currentPage + 1);
                    hasMorePages = true;
                }
            }
            else
            {
                singlePayload = document.RootElement.Clone();
            }

            if (hasMorePages)
            {
                await Task.Delay(150, cancellationToken);
            }
        } while (hasMorePages);

        var payload = isCollectionResult
            ? JsonSerializer.SerializeToElement(allItems)
            : singlePayload ?? JsonSerializer.SerializeToElement<object?>(null);
        var itemCount = isCollectionResult ? allItems.Count : singlePayload.HasValue ? 1 : 0;

        return new QueryResult(payload, itemCount);
    }

    private static string BuildUrl(
        string baseUrl,
        string endpointPath,
        IReadOnlyDictionary<string, string> values,
        int? page = null)
    {
        var remainingValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        var resolvedPath = ResolveEndpointPath(endpointPath, remainingValues);
        var builder = new UriBuilder($"{baseUrl.TrimEnd('/')}/{resolvedPath.TrimStart('/')}");
        var queryParts = remainingValues
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")
            .ToList();

        if (page.HasValue)
        {
            queryParts.Add($"page={page.Value}");
        }

        builder.Query = string.Join("&", queryParts);
        return builder.Uri.ToString();
    }

    private static string ResolveEndpointPath(string endpointPath, IDictionary<string, string> values)
    {
        return Regex.Replace(endpointPath, @"\{([^}/]+)\}", match =>
        {
            var parameterName = match.Groups[1].Value;

            if (!values.Remove(parameterName, out var parameterValue) || string.IsNullOrWhiteSpace(parameterValue))
            {
                throw new InvalidOperationException($"Missing required path parameter '{parameterName}'.");
            }

            return Uri.EscapeDataString(parameterValue);
        });
    }
}

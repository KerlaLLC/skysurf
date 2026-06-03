using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using skysurf.Features.SchemaCatalog;

namespace skysurf.Features.QueryExecution;

/// <summary>The last query result kept for a particular endpoint + parameter combination,
/// together with when it was fetched.</summary>
public sealed record CachedQueryResult(QueryResult Result, DateTime RefreshedUtc);

/// <summary>A best-effort, disk-backed cache of query results keyed by endpoint identity plus
/// the exact parameter values used. One JSON file per key (named by the key's SHA-256) so a large
/// payload never forces the whole cache to be rewritten. Reads never throw into the UI — a missing
/// or unreadable entry simply yields <c>null</c>.</summary>
public sealed class QueryResultCacheRepository(string directory)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>Builds the canonical cache key: connection id, then API id, method and path followed
    /// by the parameter values sorted by name. Scoping by connection keeps each connection's results
    /// separate. Order-independent so the same query always maps to the same key.</summary>
    public static string BuildKey(Guid connectionId, SkyEndpoint endpoint, IReadOnlyDictionary<string, string> values)
    {
        var builder = new StringBuilder();
        builder.Append(connectionId.ToString("N")).Append('\n');
        builder.Append(endpoint.ApiId).Append('\n');
        builder.Append(endpoint.HttpMethod).Append('\n');
        builder.Append(endpoint.Path);

        foreach (var pair in values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            builder.Append('\n').Append(pair.Key).Append('=').Append(pair.Value);

        return builder.ToString();
    }

    public CachedQueryResult? TryGet(string key)
    {
        try
        {
            var path = FilePath(key);
            if (!File.Exists(path))
                return null;

            var record = JsonSerializer.Deserialize<CacheRecord>(File.ReadAllText(path), _jsonOptions);
            if (record is null)
                return null;

            var result = new QueryResult(record.Payload.Clone(), record.ItemCount);
            return new CachedQueryResult(result, record.RefreshedUtc);
        }
        catch
        {
            // Best-effort cache: any corruption or read error just behaves as a miss.
            return null;
        }
    }

    public void Save(string key, QueryResult result, DateTime refreshedUtc)
    {
        var record = new CacheRecord
        {
            Key = key,
            ItemCount = result.ItemCount,
            RefreshedUtc = refreshedUtc,
            Payload = result.Payload,
        };

        File.WriteAllText(FilePath(key), JsonSerializer.Serialize(record, _jsonOptions));
    }

    private string FilePath(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Path.Combine(directory, $"{Convert.ToHexString(hash)}.json");
    }

    private sealed class CacheRecord
    {
        public string Key { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public DateTime RefreshedUtc { get; set; }
        public JsonElement Payload { get; set; }
    }
}

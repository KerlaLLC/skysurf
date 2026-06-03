using skysurf.Features.SavedQueries;
using skysurf.Features.SchemaCatalog;

namespace skysurf.Features.QuerySelection;

public sealed class QuerySearchService(SkyApiSchemaService schemaService)
{
    public IReadOnlyList<SearchResult> Search(
        string query,
        IReadOnlyList<SavedQueryRecord> savedQueries,
        bool includeEndpoints = true,
        bool includeSaved = true)
    {
        var trimmedQuery = query.Trim();
        var endpointResults = includeEndpoints
            ? schemaService.Endpoints
                .Select(endpoint => new SearchResult
                {
                    Endpoint = endpoint,
                    Score = Score(trimmedQuery, endpoint.ApiName, endpoint.Path, endpoint.SchemaModelName)
                })
            : Enumerable.Empty<SearchResult>();

        var savedQueryResults = includeSaved
            ? savedQueries
                .Select(savedQuery => new SearchResult
                {
                    SavedQuery = savedQuery,
                    Score = Score(trimmedQuery, savedQuery.Name, savedQuery.ApiName, savedQuery.EndpointPath, savedQuery.SchemaModelName) + 1000
                })
            : Enumerable.Empty<SearchResult>();

        return savedQueryResults
            .Concat(endpointResults)
            .Where(x => string.IsNullOrWhiteSpace(trimmedQuery) || x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.DisplayText)
            .ToList();
    }

    private static int Score(string query, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 1;
        }

        var tokens = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var haystack = string.Join(' ', parts).ToLowerInvariant();
        var score = 0;

        foreach (var token in tokens.Select(x => x.ToLowerInvariant()))
        {
            if (!haystack.Contains(token, StringComparison.Ordinal))
            {
                return 0;
            }

            score += 10;

            if (parts.Any(x => x.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                score += 5;
            }
        }

        if (haystack.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }
}

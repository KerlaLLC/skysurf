using skysurf.Features.SavedQueries;
using skysurf.Features.SchemaCatalog;

namespace skysurf.Features.QuerySelection;

public sealed class SearchResult
{
    public SkyEndpoint? Endpoint { get; init; }
    public SavedQueryRecord? SavedQuery { get; init; }
    public int Score { get; init; }

    public string DisplayText =>
        SavedQuery is not null
            ? $"[Saved] {SavedQuery.Name}  {SavedQuery.ApiName} {SavedQuery.EndpointPath} {SavedQuery.SchemaModelName}"
            : Endpoint is not null
                ? $"{Endpoint.ApiName} {Endpoint.Path} {Endpoint.SchemaModelName}"
                : string.Empty;
}

namespace skysurf.Features.SchemaCatalog;

public sealed record SchemaRefreshProgress(int Current, int Total, string ApiName);

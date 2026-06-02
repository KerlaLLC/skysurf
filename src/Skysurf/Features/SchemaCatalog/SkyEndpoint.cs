using skysurf.Features.ParameterEntry;

namespace skysurf.Features.SchemaCatalog;

public sealed class SkyEndpoint
{
    public string ApiId { get; init; } = string.Empty;
    public string ApiName { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string HttpMethod { get; init; } = "GET";
    public string SchemaModelName { get; init; } = "Json";
    public string OperationId { get; init; } = string.Empty;
    public IReadOnlyList<ParameterPrompt> RequiredParameters { get; init; } = [];
    public IReadOnlyList<ParameterPrompt> OptionalParameters { get; init; } = [];

    public string DisplayText => $"{ApiName} {Path} {SchemaModelName}";
}

using System.Text.Json;

namespace skysurf.Features.SavedQueries;

public sealed class SavedQueryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ApiId { get; set; } = string.Empty;
    public string ApiName { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public string SchemaModelName { get; set; } = "Json";
    public string ParametersJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public IReadOnlyDictionary<string, string> GetParameters()
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

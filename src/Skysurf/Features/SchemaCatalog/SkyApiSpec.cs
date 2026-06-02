using Microsoft.OpenApi.Models;

namespace skysurf.Features.SchemaCatalog;

public sealed class SkyApiSpec
{
    public string ApiId { get; set; } = string.Empty;
    public OpenApiDocument Document { get; set; } = null!;
}

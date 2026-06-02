namespace Shared.Connections;

public sealed class ConnectionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SubscriptionKey { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenValidToUtc { get; set; } = DateTime.UtcNow.AddYears(1);
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

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

    /// <summary>When set, this connection is chosen automatically on launch. Only one
    /// connection may be the default at a time (enforced by the repository).</summary>
    public bool IsDefault { get; set; }

    /// <summary>Last time this connection was activated/used. Used as the launch fallback
    /// when no connection is flagged as default.</summary>
    public DateTime LastUsedUtc { get; set; } = DateTime.MinValue;
}

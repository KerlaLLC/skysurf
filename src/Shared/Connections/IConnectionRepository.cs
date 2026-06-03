namespace Shared.Connections;

public interface IConnectionRepository
{
    IReadOnlyList<ConnectionRecord> List();
    ConnectionRecord? GetByNameOrId(string nameOrId);
    void Add(ConnectionRecord connection);
    void Delete(Guid id);
    void Update(ConnectionRecord connection);
    void UpdateRefreshToken(Guid id, string refreshToken, DateTime refreshTokenValidToUtc);

    /// <summary>Flags the given connection as the default and clears the flag on all others.</summary>
    void SetDefault(Guid id);

    /// <summary>Records that the given connection was just used (sets <see cref="ConnectionRecord.LastUsedUtc"/>).</summary>
    void TouchLastUsed(Guid id);
}

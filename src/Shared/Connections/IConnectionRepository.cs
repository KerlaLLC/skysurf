namespace Shared.Connections;

public interface IConnectionRepository
{
    IReadOnlyList<ConnectionRecord> List();
    ConnectionRecord? GetByNameOrId(string nameOrId);
    void Add(ConnectionRecord connection);
    void Delete(Guid id);
    void Update(ConnectionRecord connection);
    void UpdateRefreshToken(Guid id, string refreshToken, DateTime refreshTokenValidToUtc);
}

using System.Text.Json;
using Hjson;

namespace Shared.Connections;

public sealed class ConnectionRepository(string filePath) : IConnectionRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Lock _lock = new();

    private List<ConnectionRecord> Load()
    {
        if (!File.Exists(filePath))
            return [];

        var hjsonText = File.ReadAllText(filePath);
        var jsonText = HjsonValue.Parse(hjsonText).ToString();
        return JsonSerializer.Deserialize<List<ConnectionRecord>>(jsonText, _jsonOptions) ?? [];
    }

    private void Save(List<ConnectionRecord> records)
    {
        var jsonText = JsonSerializer.Serialize(records, _jsonOptions);
        var hjsonText = HjsonValue.Parse(jsonText).ToString(Stringify.Hjson);
        File.WriteAllText(filePath, hjsonText);
    }

    public IReadOnlyList<ConnectionRecord> List()
    {
        lock (_lock)
            return Load().OrderBy(x => x.Name).ToList();
    }

    public ConnectionRecord? GetByNameOrId(string nameOrId)
    {
        lock (_lock)
        {
            var records = Load();
            if (Guid.TryParse(nameOrId, out var id))
                return records.FirstOrDefault(x => x.Id == id);

            return records.FirstOrDefault(x =>
                x.Name.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Add(ConnectionRecord connection)
    {
        lock (_lock)
        {
            var records = Load();
            records.Add(connection);
            Save(records);
        }
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            var records = Load();
            var index = records.FindIndex(x => x.Id == id);
            if (index < 0)
                return;

            records.RemoveAt(index);
            Save(records);
        }
    }

    public void Update(ConnectionRecord updated)
    {
        lock (_lock)
        {
            var records = Load();
            var connection = records.FirstOrDefault(x => x.Id == updated.Id)
                ?? throw new InvalidOperationException("Connection not found.");

            connection.Name = updated.Name;
            connection.SubscriptionKey = updated.SubscriptionKey;
            connection.ClientId = updated.ClientId;
            connection.ClientSecret = updated.ClientSecret;
            connection.RefreshToken = updated.RefreshToken;
            connection.RefreshTokenValidToUtc = updated.RefreshTokenValidToUtc;
            connection.IsDefault = updated.IsDefault;
            Save(records);
        }
    }

    public void SetDefault(Guid id)
    {
        lock (_lock)
        {
            var records = Load();
            var found = false;
            foreach (var record in records)
            {
                var isTarget = record.Id == id;
                found |= isTarget;
                record.IsDefault = isTarget;
            }

            if (found)
                Save(records);
        }
    }

    public void TouchLastUsed(Guid id)
    {
        lock (_lock)
        {
            var records = Load();
            var connection = records.FirstOrDefault(x => x.Id == id);
            if (connection is null)
                return;

            connection.LastUsedUtc = DateTime.UtcNow;
            Save(records);
        }
    }

    public void UpdateRefreshToken(Guid id, string refreshToken, DateTime refreshTokenValidToUtc)
    {
        lock (_lock)
        {
            var records = Load();
            var connection = records.FirstOrDefault(x => x.Id == id)
                ?? throw new InvalidOperationException("Connection not found.");

            connection.RefreshToken = refreshToken;
            connection.RefreshTokenValidToUtc = refreshTokenValidToUtc;
            Save(records);
        }
    }
}

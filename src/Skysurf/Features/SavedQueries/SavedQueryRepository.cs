using System.Text.Json;
using Hjson;

namespace skysurf.Features.SavedQueries;

public sealed class SavedQueryRepository(string filePath)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Lock _lock = new();

    private List<SavedQueryRecord> Load()
    {
        if (!File.Exists(filePath))
            return [];

        var hjsonText = File.ReadAllText(filePath);
        var jsonText = HjsonValue.Parse(hjsonText).ToString();
        return JsonSerializer.Deserialize<List<SavedQueryRecord>>(jsonText, _jsonOptions) ?? [];
    }

    private void Save(List<SavedQueryRecord> records)
    {
        var jsonText = JsonSerializer.Serialize(records, _jsonOptions);
        var hjsonText = HjsonValue.Parse(jsonText).ToString(Stringify.Hjson);
        File.WriteAllText(filePath, hjsonText);
    }

    public IReadOnlyList<SavedQueryRecord> List()
    {
        lock (_lock)
            return Load()
                .OrderBy(x => x.Name)
                .ThenBy(x => x.EndpointPath)
                .ToList();
    }

    public void Add(SavedQueryRecord savedQuery)
    {
        lock (_lock)
        {
            var records = Load();
            records.Add(savedQuery);
            Save(records);
        }
    }

    public void Update(SavedQueryRecord savedQuery)
    {
        lock (_lock)
        {
            var records = Load();
            var index = records.FindIndex(x => x.Id == savedQuery.Id);
            if (index < 0)
            {
                records.Add(savedQuery);
            }
            else
            {
                records[index] = savedQuery;
            }

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
}

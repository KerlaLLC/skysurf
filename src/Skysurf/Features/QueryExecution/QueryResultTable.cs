using System.Text.Json;

namespace skysurf.Features.QueryExecution;

public enum QueryResultContentKind
{
    Empty,
    Scalar,
    Object,
    Array
}

public sealed record QueryResultTable(
    IReadOnlyList<QueryResultColumn> Columns,
    IReadOnlyList<QueryResultRow> Rows,
    string EmptyMessage);

public sealed record QueryResultColumn(string Name);

public sealed record QueryResultRow(
    int Index,
    string Json,
    IReadOnlyList<QueryResultCell> Cells);

public sealed record QueryResultCell(
    string ColumnName,
    string DisplayValue,
    QueryResultContentKind ContentKind,
    JsonElement? Value)
{
    public bool HasNestedContent => ContentKind is QueryResultContentKind.Array or QueryResultContentKind.Object;

    public string GetCopyText()
    {
        if (Value is null)
        {
            return string.Empty;
        }

        var element = Value.Value;
        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : QueryResultNormalizer.Serialize(element, writeIndented: true);
    }
}

internal static class QueryResultNormalizer
{
    public static QueryResultTable BuildTable(JsonElement payload)
    {
        return payload.ValueKind switch
        {
            JsonValueKind.Array => BuildArrayTable(payload),
            JsonValueKind.Object => BuildObjectTable(payload),
            JsonValueKind.Undefined => new QueryResultTable([], [], "No result payload was returned."),
            _ => BuildScalarTable(payload)
        };
    }

    public static QueryResultContentKind GetContentKind(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => element.GetArrayLength() == 0 ? QueryResultContentKind.Empty : QueryResultContentKind.Array,
            JsonValueKind.Object => QueryResultContentKind.Object,
            JsonValueKind.Null or JsonValueKind.Undefined => QueryResultContentKind.Empty,
            _ => QueryResultContentKind.Scalar
        };
    }

    public static string Serialize(JsonElement element, bool writeIndented)
    {
        return JsonSerializer.Serialize(element, new JsonSerializerOptions
        {
            WriteIndented = writeIndented
        });
    }

    private static QueryResultTable BuildArrayTable(JsonElement payload)
    {
        var items = payload.EnumerateArray().Select(x => x.Clone()).ToList();
        if (items.Count == 0)
        {
            return new QueryResultTable([], [], "No rows were returned.");
        }

        if (items.All(x => x.ValueKind == JsonValueKind.Object))
        {
            return BuildObjectRowsTable(items);
        }

        var rows = items
            .Select((item, index) => new QueryResultRow(
                index,
                Serialize(item, writeIndented: true),
                [CreateCell("Value", item)]))
            .ToList();

        return new QueryResultTable([new QueryResultColumn("Value")], rows, string.Empty);
    }

    private static QueryResultTable BuildObjectTable(JsonElement payload)
    {
        var columns = payload.EnumerateObject()
            .Select(x => new QueryResultColumn(x.Name))
            .ToList();

        if (columns.Count == 0)
        {
            var fallbackRow = new QueryResultRow(0, Serialize(payload, writeIndented: true), [CreateCell("Value", payload)]);
            return new QueryResultTable([new QueryResultColumn("Value")], [fallbackRow], string.Empty);
        }

        var cells = payload.EnumerateObject()
            .Select(x => CreateCell(x.Name, x.Value))
            .ToList();

        var row = new QueryResultRow(0, Serialize(payload, writeIndented: true), cells);
        return new QueryResultTable(columns, [row], string.Empty);
    }

    private static QueryResultTable BuildScalarTable(JsonElement payload)
    {
        var row = new QueryResultRow(0, Serialize(payload, writeIndented: true), [CreateCell("Value", payload)]);
        return new QueryResultTable([new QueryResultColumn("Value")], [row], string.Empty);
    }

    private static QueryResultTable BuildObjectRowsTable(IReadOnlyList<JsonElement> items)
    {
        var columnNames = new List<string>();
        var seenColumns = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            foreach (var property in item.EnumerateObject())
            {
                if (seenColumns.Add(property.Name))
                {
                    columnNames.Add(property.Name);
                }
            }
        }

        if (columnNames.Count == 0)
        {
            var fallbackRows = items
                .Select((item, index) => new QueryResultRow(
                    index,
                    Serialize(item, writeIndented: true),
                    [CreateCell("Value", item)]))
                .ToList();

            return new QueryResultTable([new QueryResultColumn("Value")], fallbackRows, string.Empty);
        }

        var columns = columnNames.Select(x => new QueryResultColumn(x)).ToList();
        var rows = new List<QueryResultRow>(items.Count);

        for (var rowIndex = 0; rowIndex < items.Count; rowIndex++)
        {
            var item = items[rowIndex];
            var cells = new List<QueryResultCell>(columnNames.Count);

            foreach (var columnName in columnNames)
            {
                cells.Add(item.TryGetProperty(columnName, out var propertyValue)
                    ? CreateCell(columnName, propertyValue)
                    : new QueryResultCell(columnName, string.Empty, QueryResultContentKind.Empty, null));
            }

            rows.Add(new QueryResultRow(rowIndex, Serialize(item, writeIndented: true), cells));
        }

        return new QueryResultTable(columns, rows, string.Empty);
    }

    private static QueryResultCell CreateCell(string columnName, JsonElement value)
    {
        var clonedValue = value.Clone();
        var contentKind = GetContentKind(clonedValue);
        return new QueryResultCell(columnName, GetDisplayValue(clonedValue), contentKind, clonedValue);
    }

    private static string GetDisplayValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Array => $"[{value.GetArrayLength()} item(s)]",
            JsonValueKind.Object => $"{{{value.EnumerateObject().Count()} field(s)}}",
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Null => "(null)",
            JsonValueKind.Undefined => string.Empty,
            _ => value.GetRawText()
        };
    }
}
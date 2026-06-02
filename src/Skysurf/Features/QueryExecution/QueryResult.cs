using System.Text.Json;

namespace skysurf.Features.QueryExecution;

public sealed class QueryResult
{
	public QueryResult(JsonElement payload, int itemCount)
	{
		Payload = payload.Clone();
		ItemCount = itemCount;
		ContentKind = QueryResultNormalizer.GetContentKind(Payload);
		Table = QueryResultNormalizer.BuildTable(Payload);
		Json = QueryResultNormalizer.Serialize(Payload, writeIndented: true);
	}

	public JsonElement Payload { get; }

	public string Json { get; }

	public int ItemCount { get; }

	public QueryResultContentKind ContentKind { get; }

	public QueryResultTable Table { get; }
}

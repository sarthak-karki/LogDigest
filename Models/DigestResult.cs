namespace LogDigest.Models;

public class DigestResult
{
    public required string Summary { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required int TotalLogs { get; init; }
    public required int UniqueErrors { get; init; }
}

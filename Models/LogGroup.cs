namespace LogDigest.Models;

public class LogGroup
{
    public required string Service { get; init; }
    public required string Message { get; init; }
    public required string Level { get; init; }
    public int Count { get; set; }
    public List<string> Samples { get; init; } = [];
    public List<ErrorInfo> Errors { get; init; } = [];
    public Dictionary<string, string> Attributes { get; init; } = [];
}

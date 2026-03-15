namespace LogDigest.Models;

public class ErrorInfo
{
    public string? Kind { get; init; }
    public string? Message { get; init; }
    public string? Stack { get; init; }

    public override string ToString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Kind)) parts.Add(Kind);
        if (!string.IsNullOrEmpty(Message)) parts.Add(Message);
        if (!string.IsNullOrEmpty(Stack)) parts.Add($"\n{Stack}");
        return string.Join(": ", parts);
    }
}

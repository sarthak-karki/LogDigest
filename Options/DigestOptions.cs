namespace LogDigest.Options;

public class DigestOptions
{
    public int Days { get; set; } = 1;
    public string[] Levels { get; set; } = ["warn", "error"];
    public string[] Services { get; set; } = [];
    public bool PromptOnly { get; set; } = false;

    public DateTimeOffset From => DateTimeOffset.UtcNow.AddDays(-Days);
    public DateTimeOffset To => DateTimeOffset.UtcNow;

    public string LevelsDisplay => string.Join(", ", Levels);
    public string ServicesDisplay => Services.Length == 0 ? "all" : string.Join(", ", Services);
}

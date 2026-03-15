using System.Text;
using LogDigest.Models;
using LogDigest.Options;

namespace LogDigest.Services;

public static class OutputWriter
{
    private const string DigestsDir = "digests";

    public static async Task WriteAsync(List<LogGroup> groups, DigestOptions options)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var content = BuildContent(groups, options, timestamp);

        Directory.CreateDirectory(DigestsDir);
        var filename = $"digest-{timestamp:yyyyMMdd-HHmmss}.md";
        var path = Path.Combine(DigestsDir, filename);

        await File.WriteAllTextAsync(path, content);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Saved to {path}");
        Console.WriteLine("Upload this file to Claude for analysis.");
        Console.ResetColor();
    }

    private static string BuildContent(List<LogGroup> groups, DigestOptions options, DateTimeOffset timestamp)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Log Extract — {timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine($"**Window:** last {options.Days} day(s)");
        sb.AppendLine($"**Levels:** {options.LevelsDisplay}");
        sb.AppendLine($"**Services:** {options.ServicesDisplay}");
        sb.AppendLine($"**Total logs:** {groups.Sum(g => g.Count)} | **Unique groups:** {groups.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        var byService = groups.GroupBy(g => g.Service).OrderBy(g => g.Key);

        foreach (var service in byService)
        {
            sb.AppendLine($"## {service.Key}");
            sb.AppendLine();

            foreach (var group in service)
            {
                sb.AppendLine($"- **[{group.Level.ToUpperInvariant()}] {group.Count}x** — {group.Message}");
                foreach (var sample in group.Samples)
                {
                    sb.AppendLine($"  - `{sample}`");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}

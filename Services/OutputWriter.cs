using LogDigest.Models;
using LogDigest.Options;

namespace LogDigest.Services;

public static class OutputWriter
{
    private const string DigestsDir = "digests";

    public static async Task WriteAsync(DigestResult result, DigestOptions options)
    {
        PrintToConsole(result, options);
        await CopyToClipboardAsync(result.Summary);
        await SaveToFileAsync(result, options);
    }

    private static void PrintToConsole(DigestResult result, DigestOptions options)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("  LogDigest Summary");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Window: last {options.Days} day(s) | Levels: {options.LevelsDisplay} | Services: {options.ServicesDisplay}");
        Console.WriteLine($"  Total logs: {result.TotalLogs} | Unique groups: {result.UniqueErrors}");
        Console.WriteLine($"  Generated: {result.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine(result.Summary);
        Console.WriteLine();
    }

    private static async Task CopyToClipboardAsync(string text)
    {
        try
        {
            await TextCopy.ClipboardService.SetTextAsync(text);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Copied to clipboard.");
            Console.ResetColor();
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Could not copy to clipboard (no display environment?).");
            Console.ResetColor();
        }
    }

    private static async Task SaveToFileAsync(DigestResult result, DigestOptions options)
    {
        Directory.CreateDirectory(DigestsDir);
        var filename = $"digest-{result.GeneratedAt:yyyyMMdd-HHmmss}.md";
        var path = Path.Combine(DigestsDir, filename);

        var content = $"""
            # Log Digest — {result.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC

            **Window:** last {options.Days} day(s)
            **Levels:** {options.LevelsDisplay}
            **Services:** {options.ServicesDisplay}
            **Total logs:** {result.TotalLogs} | **Unique groups:** {result.UniqueErrors}

            ---

            {result.Summary}
            """;

        await File.WriteAllTextAsync(path, content);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Saved to {path}");
        Console.ResetColor();
    }
}

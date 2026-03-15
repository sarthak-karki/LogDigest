using System.CommandLine;
using LogDigest.Options;
using LogDigest.Services;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var daysOption = new Option<int>("--days", () => 1, "Number of days to look back");
var levelsOption = new Option<string[]>("--levels", () => ["warn", "error"], "Log severity levels to include")
{
    AllowMultipleArgumentsPerToken = true
};
var servicesOption = new Option<string[]>("--services", () => [], "Filter to specific services (default: all)")
{
    AllowMultipleArgumentsPerToken = true
};

var rootCommand = new RootCommand("LogDigest — Datadog log summary for Slack")
{
    daysOption,
    levelsOption,
    servicesOption
};

rootCommand.SetHandler(async (int days, string[] levels, string[] services) =>
{
    var options = new DigestOptions
    {
        Days = days,
        Levels = ParseCommaSeparated(levels),
        Services = ParseCommaSeparated(services)
    };

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"Fetching {options.LevelsDisplay} logs from the last {options.Days} day(s) for {options.ServicesDisplay}...");
    Console.ResetColor();

    var extractor = new DatadogLogExtractor(config);
    var groups = await extractor.ExtractAsync(options);

    if (groups.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("No matching logs found. All clear!");
        Console.ResetColor();
        return;
    }

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"Found {groups.Sum(g => g.Count)} logs in {groups.Count} groups. Summarising with Claude...");
    Console.ResetColor();

    var summariser = new AiSummariser(config);
    var result = await summariser.SummariseAsync(groups, options);

    await OutputWriter.WriteAsync(result, options);

}, daysOption, levelsOption, servicesOption);

return await rootCommand.InvokeAsync(args);

// Handles both "--levels warn error" and "--levels warn,error" styles
static string[] ParseCommaSeparated(string[] values)
{
    return values
        .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .ToArray();
}

using LogDigest.Options;
using LogDigest.Services;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var options = new DigestOptions
{
    Days = config.GetValue("Datadog:Days", 1),
    Levels = config.GetSection("Datadog:Levels").Get<string[]>() ?? ["warn", "error"],
    Services = config.GetSection("Datadog:Services").Get<string[]>() ?? []
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

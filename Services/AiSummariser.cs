using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using LogDigest.Models;
using LogDigest.Options;
using Microsoft.Extensions.Configuration;

namespace LogDigest.Services;

public class AiSummariser
{
    private readonly HttpClient _http;
    private const string Model = "claude-sonnet-4-20250514";

    public AiSummariser(IConfiguration config)
    {
        var apiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Claude:ApiKey is required");

        _http = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com") };
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<DigestResult> SummariseAsync(
        List<LogGroup> groups, DigestOptions options, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(groups, options);

        var request = new
        {
            model = Model,
            max_tokens = 2048,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var response = await _http.PostAsJsonAsync("/v1/messages", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from Claude");

        var summary = result.Content?.FirstOrDefault()?.Text ?? "No summary generated.";

        return new DigestResult
        {
            Summary = summary,
            GeneratedAt = DateTimeOffset.UtcNow,
            TotalLogs = groups.Sum(g => g.Count),
            UniqueErrors = groups.Count
        };
    }

    private static string BuildPrompt(List<LogGroup> groups, DigestOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"I've uploaded Datadog error and warning logs from the last {options.Days} day(s).");
        sb.AppendLine($"Services: {options.ServicesDisplay} | Levels: {options.LevelsDisplay}");
        sb.AppendLine();
        sb.AppendLine("Please analyse these logs and produce a Slack-ready summary in Slack mrkdwn format. Structure it as:");
        sb.AppendLine("1. *Overview* — 2-3 sentence health summary. Mention total log count, number of services affected, and overall severity (healthy / concerning / critical).");
        sb.AppendLine("2. *Critical issues* — Errors needing immediate attention. For each: service name, error description, occurrence count, and a brief likely root cause based on the log messages.");
        sb.AppendLine("3. *Warnings to watch* — Warnings that could escalate. Same format, lower priority.");
        sb.AppendLine("4. *Recurring patterns* — Errors appearing across multiple services or pointing to systemic issues (shared dependencies, timeout patterns, etc).");
        sb.AppendLine("5. *Recommended actions* — Concrete next steps, prioritised by impact.");
        sb.AppendLine();
        sb.AppendLine("Formatting rules:");
        sb.AppendLine("- Use Slack mrkdwn: *bold*, _italic_, `code`");
        sb.AppendLine("- Use :rotating_light: for critical, :warning: for warnings, :white_check_mark: for healthy");
        sb.AppendLine("- Group by service");
        sb.AppendLine("- Include counts, e.g. \"TimeoutException (342 occurrences)\"");
        sb.AppendLine("- If no critical issues, say so — good news is worth reporting");
        sb.AppendLine("- Keep it scannable — this will be skimmed in 30 seconds");
        sb.AppendLine("- Output ONLY the Slack message, no extra commentary");
        sb.AppendLine();
        sb.AppendLine("Here are the grouped log entries:");
        sb.AppendLine();

        foreach (var group in groups)
        {
            sb.AppendLine($"[{group.Level.ToUpperInvariant()}] {group.Service} — {group.Count}x: {group.Message}");
            foreach (var sample in group.Samples)
            {
                sb.AppendLine($"  sample: {sample}");
            }
            foreach (var error in group.Errors)
            {
                sb.AppendLine($"  exception: {error}");
            }
            if (group.Attributes.Count > 0)
            {
                sb.AppendLine($"  attributes: {string.Join(", ", group.Attributes.Select(a => $"{a.Key}={a.Value}"))}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private class ClaudeResponse
    {
        [JsonPropertyName("content")] public List<ContentBlock>? Content { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }
}

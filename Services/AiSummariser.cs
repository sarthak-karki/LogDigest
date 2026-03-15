using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
        sb.AppendLine("You are a concise DevOps engineer writing a Slack digest of log errors/warnings.");
        sb.AppendLine($"Time window: last {options.Days} day(s) | Levels: {options.LevelsDisplay} | Services: {options.ServicesDisplay}");
        sb.AppendLine();
        sb.AppendLine("Format the output as a Slack message using mrkdwn:");
        sb.AppendLine("- Bold section headers per service");
        sb.AppendLine("- Bullet points for each error group with count");
        sb.AppendLine("- A brief 1-2 sentence overall assessment at the top");
        sb.AppendLine("- Flag anything that looks like it needs immediate attention with :rotating_light:");
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

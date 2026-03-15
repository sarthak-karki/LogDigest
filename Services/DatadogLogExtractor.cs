using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LogDigest.Models;
using LogDigest.Options;
using Microsoft.Extensions.Configuration;

namespace LogDigest.Services;

public class DatadogLogExtractor
{
    private readonly HttpClient _http;
    private readonly string _site;
    private const int MaxSamplesPerGroup = 3;
    private const int PageLimit = 1000;

    public DatadogLogExtractor(IConfiguration config)
    {
        var apiKey = config["Datadog:ApiKey"] ?? throw new InvalidOperationException("Datadog:ApiKey is required");
        var appKey = config["Datadog:AppKey"] ?? throw new InvalidOperationException("Datadog:AppKey is required");
        _site = config["Datadog:Site"] ?? "datadoghq.com";

        _http = new HttpClient { BaseAddress = new Uri($"https://api.{_site}") };
        _http.DefaultRequestHeaders.Add("DD-API-KEY", apiKey);
        _http.DefaultRequestHeaders.Add("DD-APPLICATION-KEY", appKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<LogGroup>> ExtractAsync(DigestOptions options, CancellationToken ct = default)
    {
        var query = BuildQuery(options);
        var allLogs = new List<DatadogLog>();
        string? cursor = null;

        do
        {
            var (logs, nextCursor) = await FetchPageAsync(query, options.From, options.To, cursor, ct);
            allLogs.AddRange(logs);
            cursor = nextCursor;
        } while (cursor is not null);

        return GroupLogs(allLogs);
    }

    private static string BuildQuery(DigestOptions options)
    {
        var parts = new List<string>();

        if (options.Levels.Length > 0)
        {
            var levels = string.Join(" OR ", options.Levels.Select(l => $"status:{l}"));
            parts.Add($"({levels})");
        }

        if (options.Services.Length > 0)
        {
            var services = string.Join(" OR ", options.Services.Select(s => $"service:{s}"));
            parts.Add($"({services})");
        }

        return parts.Count > 0 ? string.Join(" ", parts) : "status:warn OR status:error";
    }

    private async Task<(List<DatadogLog> Logs, string? Cursor)> FetchPageAsync(
        string query, DateTimeOffset from, DateTimeOffset to, string? cursor, CancellationToken ct)
    {
        var body = new SearchRequest
        {
            Filter = new SearchFilter
            {
                Query = query,
                From = from.ToString("o"),
                To = to.ToString("o")
            },
            Page = new SearchPage { Limit = PageLimit, Cursor = cursor },
            Sort = "timestamp"
        };

        var response = await _http.PostAsJsonAsync("/api/v2/logs/events/search", body, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from Datadog");

        var logs = result.Data?.Select(d => new DatadogLog
        {
            Service = d.Attributes?.Service ?? "unknown",
            Message = d.Attributes?.Message ?? "",
            Level = d.Attributes?.Status ?? "unknown",
            Timestamp = d.Attributes?.Timestamp ?? DateTimeOffset.MinValue
        }).ToList() ?? [];

        return (logs, result.Meta?.Page?.After);
    }

    private static List<LogGroup> GroupLogs(List<DatadogLog> logs)
    {
        return logs
            .GroupBy(l => new { l.Service, Key = NormaliseMessage(l.Message), l.Level })
            .Select(g =>
            {
                var group = new LogGroup
                {
                    Service = g.Key.Service,
                    Message = g.Key.Key,
                    Level = g.Key.Level,
                    Count = g.Count()
                };
                group.Samples.AddRange(
                    g.Take(MaxSamplesPerGroup).Select(l => l.Message));
                return group;
            })
            .OrderByDescending(g => g.Count)
            .ToList();
    }

    private static string NormaliseMessage(string message)
    {
        if (message.Length > 200)
            message = message[..200];

        // Strip UUIDs, timestamps, and numeric IDs to group similar messages
        message = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "<id>");
        message = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}[^\s]*",
            "<ts>");
        message = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"\b\d{6,}\b",
            "<num>");

        return message.Trim();
    }

    // --- Datadog API DTOs ---

    private class SearchRequest
    {
        [JsonPropertyName("filter")] public required SearchFilter Filter { get; set; }
        [JsonPropertyName("page")] public required SearchPage Page { get; set; }
        [JsonPropertyName("sort")] public string Sort { get; set; } = "timestamp";
    }

    private class SearchFilter
    {
        [JsonPropertyName("query")] public string Query { get; set; } = "";
        [JsonPropertyName("from")] public string From { get; set; } = "";
        [JsonPropertyName("to")] public string To { get; set; } = "";
    }

    private class SearchPage
    {
        [JsonPropertyName("limit")] public int Limit { get; set; }
        [JsonPropertyName("cursor")] public string? Cursor { get; set; }
    }

    private class SearchResponse
    {
        [JsonPropertyName("data")] public List<LogEvent>? Data { get; set; }
        [JsonPropertyName("meta")] public SearchMeta? Meta { get; set; }
    }

    private class LogEvent
    {
        [JsonPropertyName("attributes")] public LogAttributes? Attributes { get; set; }
    }

    private class LogAttributes
    {
        [JsonPropertyName("service")] public string? Service { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("timestamp")] public DateTimeOffset? Timestamp { get; set; }
    }

    private class SearchMeta
    {
        [JsonPropertyName("page")] public PageMeta? Page { get; set; }
    }

    private class PageMeta
    {
        [JsonPropertyName("after")] public string? After { get; set; }
    }

    private class DatadogLog
    {
        public string Service { get; init; } = "";
        public string Message { get; init; } = "";
        public string Level { get; init; } = "";
        public DateTimeOffset Timestamp { get; init; }
    }
}

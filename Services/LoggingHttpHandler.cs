using System.Diagnostics;

namespace LogDigest.Services;

/// <summary>
/// Delegating handler that logs HTTP request and response details to the console.
/// </summary>
public class LoggingHttpHandler : DelegatingHandler
{
    private readonly string _label;

    public LoggingHttpHandler(string label)
        : base(new HttpClientHandler())
    {
        _label = label;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method;
        var uri = request.RequestUri;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[{_label}] --> {method} {uri}");
        Console.ResetColor();

        var sw = Stopwatch.StartNew();

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            sw.Stop();

            var color = response.IsSuccessStatusCode ? ConsoleColor.DarkGreen : ConsoleColor.Red;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{_label}] <-- {(int)response.StatusCode} {response.StatusCode} ({sw.ElapsedMilliseconds}ms)");
            Console.ResetColor();

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{_label}] <-- FAILED after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name}: {ex.Message}");
            Console.ResetColor();

            throw;
        }
    }
}

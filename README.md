# LogDigest

A .NET CLI tool that fetches error and warning logs from Datadog, groups them by service and message pattern, and uses Claude AI to generate a Slack-ready summary of production issues.

## Features

- Fetches logs from Datadog filtered by service, level, and time range
- Normalises log messages (strips UUIDs, timestamps, numeric IDs) to group similar errors
- Extracts structured exception details from log entries
- Summarises grouped logs using Claude AI into actionable Slack-formatted digests
- Outputs to console, clipboard, and a timestamped file in `digests/`

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- A [Datadog](https://www.datadoghq.com/) account with API and App keys
- An [Anthropic](https://www.anthropic.com/) API key

## Setup

1. Clone the repository:

   ```bash
   git clone https://github.com/sarthak-karki/LogDigest.git
   cd LogDigest
   ```

2. Copy the example config and fill in your keys:

   ```bash
   cp appsettings.example.json appsettings.json
   ```

3. Edit `appsettings.json`:

   ```json
   {
     "Datadog": {
       "ApiKey": "YOUR_DD_API_KEY",
       "AppKey": "YOUR_DD_APP_KEY",
       "Site": "datadoghq.com",
       "Services": ["my-service-1", "my-service-2"],
       "Days": 1,
       "Levels": ["warn", "error"]
     },
     "Claude": {
       "ApiKey": "YOUR_ANTHROPIC_API_KEY"
     }
   }
   ```

   Configuration can also be set via environment variables (e.g. `Datadog__ApiKey`, `Claude__ApiKey`).

## Configuration Options

| Key | Description | Default |
|-----|-------------|---------|
| `Datadog:ApiKey` | Datadog API key | — |
| `Datadog:AppKey` | Datadog Application key | — |
| `Datadog:Site` | Datadog site | `datadoghq.com` |
| `Datadog:Services` | List of service names to query | `[]` (all) |
| `Datadog:Days` | Number of days to look back | `1` |
| `Datadog:Levels` | Log levels to include | `["warn", "error"]` |
| `Claude:ApiKey` | Anthropic API key | — |

## Usage

```bash
dotnet run
```

The tool will:

1. Query Datadog for matching logs
2. Group and normalise the results
3. Send grouped logs to Claude for summarisation
4. Print the digest to the console
5. Copy it to your clipboard
6. Save it to `digests/`

## Project Structure

```
├── Program.cs                 # Entry point and orchestration
├── Models/
│   ├── LogGroup.cs            # Grouped log representation
│   ├── DigestResult.cs        # Summary result model
│   └── ErrorInfo.cs           # Parsed exception details
├── Options/
│   └── DigestOptions.cs       # Configuration binding
├── Services/
│   ├── DatadogLogExtractor.cs # Datadog API client
│   ├── AiSummariser.cs        # Claude summarisation
│   └── OutputWriter.cs        # Console/file/clipboard output
├── appsettings.example.json   # Configuration template
└── LogDigest.csproj           # Project file
```

## License

See [LICENSE](LICENSE) for details.

namespace Project_auto_push_to_gitlab.SqlOptimizer.Mcp;

public sealed class SqlOptimizerMcpOptions
{
    public const int DefaultPort = 57321;
    public const string DefaultRoute = "/mcp";

    public int Port { get; init; } = DefaultPort;

    public string Route { get; init; } = DefaultRoute;

    public Uri Endpoint => new($"http://127.0.0.1:{Port}{Route}");

    public void Validate()
    {
        if (Port is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Port),
                "MCPポートは1024～65535で指定してください。");
        }

        if (string.IsNullOrWhiteSpace(Route) ||
            !Route.StartsWith("/", StringComparison.Ordinal) ||
            Route.Length > 128 ||
            Route.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "MCPルートは / から始まる空白なしのパスで指定してください。",
                nameof(Route));
        }
    }
}

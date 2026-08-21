using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class SqlOptimizerErrorSanitizerTests
{
    [Fact]
    public void UnknownException_DoesNotExposeItsMessage()
    {
        const string secret = "DATABASE-ERROR-SECRET-456";
        InvalidOperationException exception = new(secret);

        string persisted =
            SqlOptimizerErrorSanitizer.ForOperation(exception, "Candidate EXPLAIN");
        string mcp =
            SqlOptimizerErrorSanitizer.ForMcpBoundary(exception);

        Assert.DoesNotContain(secret, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, mcp, StringComparison.Ordinal);
        Assert.Contains("details were omitted", persisted);
        Assert.Contains("details were omitted", mcp);
    }
}

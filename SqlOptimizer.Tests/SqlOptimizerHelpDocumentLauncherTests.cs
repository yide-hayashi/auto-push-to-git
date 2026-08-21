using System.Diagnostics;
using Project_auto_push_to_gitlab.SqlOptimizer.Application;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class SqlOptimizerHelpDocumentLauncherTests
{
    [Fact]
    public void TryOpen_WhenGuideIsMissing_DoesNotStartAProcess()
    {
        using TestDirectory directory = new("help-guide-missing");
        bool processStarted = false;

        SqlOptimizerHelpDocumentOpenResult result =
            SqlOptimizerHelpDocumentLauncher.TryOpen(
                directory.Path,
                _ =>
                {
                    processStarted = true;
                    return null;
                });

        Assert.Equal(SqlOptimizerHelpDocumentOpenStatus.Missing, result.Status);
        Assert.False(processStarted);
        Assert.Equal(
            Path.Combine(
                directory.Path,
                "docs",
                SqlOptimizerHelpDocumentLauncher.GuideFileName),
            result.GuidePath);
    }

    [Fact]
    public void TryOpen_UsesTheFixedGuidePathAndShellAssociation()
    {
        using TestDirectory directory = new("help-guide-start-info");
        string docsDirectory = Path.Combine(directory.Path, "docs");
        Directory.CreateDirectory(docsDirectory);
        string guidePath = Path.Combine(
            docsDirectory,
            SqlOptimizerHelpDocumentLauncher.GuideFileName);
        File.WriteAllBytes(guidePath, [0x50, 0x4B]);
        ProcessStartInfo? capturedStartInfo = null;

        SqlOptimizerHelpDocumentOpenResult result =
            SqlOptimizerHelpDocumentLauncher.TryOpen(
                directory.Path,
                startInfo =>
                {
                    capturedStartInfo = startInfo;
                    return null;
                });

        Assert.Equal(SqlOptimizerHelpDocumentOpenStatus.Opened, result.Status);
        Assert.Null(result.Exception);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal(guidePath, capturedStartInfo.FileName);
        Assert.True(capturedStartInfo.UseShellExecute);
        Assert.Empty(capturedStartInfo.ArgumentList);
    }

    [Fact]
    public void TryOpen_WhenAssociationLaunchFails_ReturnsFailure()
    {
        using TestDirectory directory = new("help-guide-launch-failure");
        string docsDirectory = Path.Combine(directory.Path, "docs");
        Directory.CreateDirectory(docsDirectory);
        File.WriteAllBytes(
            Path.Combine(
                docsDirectory,
                SqlOptimizerHelpDocumentLauncher.GuideFileName),
            [0x50, 0x4B]);
        InvalidOperationException expected = new("simulated association failure");

        SqlOptimizerHelpDocumentOpenResult result =
            SqlOptimizerHelpDocumentLauncher.TryOpen(
                directory.Path,
                _ => throw expected);

        Assert.Equal(SqlOptimizerHelpDocumentOpenStatus.Failed, result.Status);
        Assert.Same(expected, result.Exception);
    }
}

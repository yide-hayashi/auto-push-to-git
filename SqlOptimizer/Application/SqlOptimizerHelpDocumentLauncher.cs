using System.Diagnostics;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Application;

internal static class SqlOptimizerHelpDocumentLauncher
{
    internal const string GuideFileName =
        "AI_SQL最適化_操作説明書_日台版.docx";

    internal static string GetGuidePath(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        return Path.GetFullPath(Path.Combine(
            baseDirectory,
            "docs",
            GuideFileName));
    }

    internal static SqlOptimizerHelpDocumentOpenResult TryOpen(
        string baseDirectory,
        Func<ProcessStartInfo, Process?> startProcess)
    {
        ArgumentNullException.ThrowIfNull(startProcess);

        string guidePath = GetGuidePath(baseDirectory);
        if (!File.Exists(guidePath))
        {
            return new SqlOptimizerHelpDocumentOpenResult(
                SqlOptimizerHelpDocumentOpenStatus.Missing,
                guidePath,
                null);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = guidePath,
            UseShellExecute = true
        };

        try
        {
            _ = startProcess(startInfo);
            return new SqlOptimizerHelpDocumentOpenResult(
                SqlOptimizerHelpDocumentOpenStatus.Opened,
                guidePath,
                null);
        }
        catch (Exception exception)
        {
            return new SqlOptimizerHelpDocumentOpenResult(
                SqlOptimizerHelpDocumentOpenStatus.Failed,
                guidePath,
                exception);
        }
    }
}

internal enum SqlOptimizerHelpDocumentOpenStatus
{
    Opened,
    Missing,
    Failed
}

internal sealed record SqlOptimizerHelpDocumentOpenResult(
    SqlOptimizerHelpDocumentOpenStatus Status,
    string GuidePath,
    Exception? Exception);

namespace Project_auto_push_to_gitlab.SqlOptimizer.Codex;

public sealed record CodexCliDiscovery(
    bool IsAvailable,
    string? ExecutablePath,
    string? Source,
    string? Error)
{
    public static CodexCliDiscovery NotFound(string error) =>
        new(false, null, null, error);
}

/// <summary>
/// Locates a native Codex CLI executable without invoking a command shell.
/// </summary>
public sealed class CodexCliLocator
{
    private static readonly string[] ExecutableNames =
        OperatingSystem.IsWindows()
            ? ["codex.exe"]
            : ["codex"];

    public CodexCliDiscovery Locate(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            string normalized = NormalizePath(explicitPath);
            return IsUsableExecutable(normalized)
                ? new CodexCliDiscovery(true, normalized, "explicit", null)
                : CodexCliDiscovery.NotFound(
                    $"指定されたCodex CLIが見つかりません: {normalized}");
        }

        string? environmentPath =
            Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            string normalized = NormalizePath(environmentPath);
            if (IsUsableExecutable(normalized))
            {
                return new CodexCliDiscovery(
                    true,
                    normalized,
                    "CODEX_CLI_PATH",
                    null);
            }
        }

        foreach (string candidate in EnumeratePathCandidates())
        {
            if (IsUsableExecutable(candidate))
            {
                return new CodexCliDiscovery(true, candidate, "PATH", null);
            }
        }

        foreach (string candidate in EnumerateVsCodeExtensionCandidates())
        {
            if (IsUsableExecutable(candidate))
            {
                return new CodexCliDiscovery(
                    true,
                    candidate,
                    "VS Code Codex extension",
                    null);
            }
        }

        return CodexCliDiscovery.NotFound(
            "Codex CLIが見つかりません。VS CodeのCodex拡張機能またはCodex CLIをインストールしてください。");
    }

    private static IEnumerable<string> EnumeratePathCandidates()
    {
        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            yield break;
        }

        foreach (string entry in pathValue.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            string directory = entry.Trim('"');
            if (directory.Length == 0)
            {
                continue;
            }

            foreach (string executableName in ExecutableNames)
            {
                string candidate;
                try
                {
                    candidate = Path.GetFullPath(
                        Path.Combine(directory, executableName));
                }
                catch (Exception exception)
                    when (exception is ArgumentException or
                          NotSupportedException or
                          PathTooLongException)
                {
                    continue;
                }

                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> EnumerateVsCodeExtensionCandidates()
    {
        string userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            yield break;
        }

        string[] extensionRoots =
        [
            Path.Combine(userProfile, ".vscode", "extensions"),
            Path.Combine(userProfile, ".vscode-insiders", "extensions")
        ];

        List<DirectoryInfo> extensionDirectories = [];
        foreach (string extensionRoot in extensionRoots)
        {
            if (!Directory.Exists(extensionRoot))
            {
                continue;
            }

            try
            {
                extensionDirectories.AddRange(
                    new DirectoryInfo(extensionRoot)
                        .EnumerateDirectories("openai.chatgpt-*"));
            }
            catch (UnauthorizedAccessException)
            {
                // Continue with the other known extension roots.
            }
            catch (IOException)
            {
                // A concurrent VS Code extension update can briefly move folders.
            }
        }

        string architectureFolder =
            System.Runtime.InteropServices.RuntimeInformation
                .ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.Arm64 =>
                    "windows-arm64",
                _ => "windows-x86_64"
            };

        foreach (DirectoryInfo extensionDirectory in extensionDirectories
                     .OrderByDescending(directory => directory.LastWriteTimeUtc))
        {
            yield return Path.Combine(
                extensionDirectory.FullName,
                "bin",
                architectureFolder,
                OperatingSystem.IsWindows() ? "codex.exe" : "codex");
        }
    }

    private static bool IsUsableExecutable(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path) && File.Exists(path);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException or
                  UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
    }
}

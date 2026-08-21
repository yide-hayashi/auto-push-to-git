using System.Security.Cryptography;
using System.Text;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Codex;

/// <summary>
/// Stores the MCP bearer token encrypted for the current Windows user.
/// The unencrypted token is never written to disk.
/// </summary>
public sealed class SqlOptimizerTokenStore
{
    public const string BearerTokenEnvironmentVariable =
        "AUTOCALLGIT_SQL_OPTIMIZER_MCP_TOKEN";

    private const int TokenByteLength = 32;
    private static readonly byte[] OptionalEntropy =
        Encoding.UTF8.GetBytes("AutoCallGit.SqlOptimizer.McpToken.v1");

    private readonly object _syncRoot = new();
    private readonly string _tokenFilePath;
    private string? _cachedToken;

    public SqlOptimizerTokenStore(string? storageDirectory = null)
    {
        string directory = storageDirectory ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoCallGit",
                "SqlOptimizer");

        _tokenFilePath = Path.Combine(directory, "mcp-token.bin");
    }

    public string TokenFilePath => _tokenFilePath;

    /// <summary>
    /// Gets the installation token, creating it when this is the first run.
    /// </summary>
    public string GetOrCreateToken()
    {
        lock (_syncRoot)
        {
            if (_cachedToken is not null)
            {
                return _cachedToken;
            }

            if (File.Exists(_tokenFilePath))
            {
                _cachedToken = ReadToken();
                return _cachedToken;
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(_tokenFilePath)
                ?? throw new InvalidOperationException("トークン保存先が不正です。"));

            byte[] tokenBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
            byte[] protectedBytes = ProtectForCurrentUser(tokenBytes);
            string temporaryPath = _tokenFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                using (FileStream stream = new(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                {
                    stream.Write(protectedBytes);
                    stream.Flush(flushToDisk: true);
                }

                try
                {
                    File.Move(temporaryPath, _tokenFilePath);
                }
                catch (IOException) when (File.Exists(_tokenFilePath))
                {
                    // Another application instance won the first-run race.
                    _cachedToken = ReadToken();
                    return _cachedToken;
                }

                _cachedToken = ToBase64Url(tokenBytes);
                return _cachedToken;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(tokenBytes);
                CryptographicOperations.ZeroMemory(protectedBytes);

                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    /// <summary>
    /// Compares an Authorization header without a timing-dependent string comparison.
    /// </summary>
    public bool IsAuthorized(string? authorizationHeader)
    {
        const string prefix = "Bearer ";

        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suppliedToken = authorizationHeader[prefix.Length..].Trim();
        string expectedToken = GetOrCreateToken();
        byte[] suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedToken);

        try
        {
            return suppliedBytes.Length == expectedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(suppliedBytes);
            CryptographicOperations.ZeroMemory(expectedBytes);
        }
    }

    /// <summary>
    /// Rotates the token. Existing MCP clients must be restarted with the new token.
    /// </summary>
    public string RotateToken()
    {
        lock (_syncRoot)
        {
            _cachedToken = null;
            if (File.Exists(_tokenFilePath))
            {
                File.Delete(_tokenFilePath);
            }

            return GetOrCreateToken();
        }
    }

    private string ReadToken()
    {
        byte[] protectedBytes = File.ReadAllBytes(_tokenFilePath);
        byte[] tokenBytes = UnprotectForCurrentUser(protectedBytes);

        try
        {
            if (tokenBytes.Length != TokenByteLength)
            {
                throw new InvalidDataException(
                    "MCPトークンファイルが破損しているか、形式が一致しません。");
            }

            return ToBase64Url(tokenBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] ProtectForCurrentUser(byte[] value)
    {
        EnsureWindows();
        return ProtectedData.Protect(
            value,
            OptionalEntropy,
            DataProtectionScope.CurrentUser);
    }

    private static byte[] UnprotectForCurrentUser(byte[] value)
    {
        EnsureWindows();
        return ProtectedData.Unprotect(
            value,
            OptionalEntropy,
            DataProtectionScope.CurrentUser);
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "MCPトークンのDPAPI保護はWindowsでのみ利用できます。");
        }
    }

}

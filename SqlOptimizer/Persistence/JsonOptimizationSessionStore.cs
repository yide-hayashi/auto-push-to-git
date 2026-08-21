using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Persistence
{
    /// <summary>
    /// Stores sanitized session history. Runtime bind values, preview rows and
    /// authentication tokens are intentionally absent from the persistence DTO.
    /// </summary>
    public sealed class JsonOptimizationSessionStore : IOptimizationSessionStore
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> DirectoryGates =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private readonly string _storageDirectory;
        private readonly SemaphoreSlim _directoryGate;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonOptimizationSessionStore(string? storageDirectory = null)
        {
            _storageDirectory = Path.GetFullPath(
                storageDirectory ??
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AutoCallGit",
                    "SqlOptimizer"));
            _directoryGate = DirectoryGates.GetOrAdd(
                _storageDirectory,
                _ => new SemaphoreSlim(1, 1));
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public string StorageDirectory => _storageDirectory;

        public async Task<SqlOptimizationSession?> LoadAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("Session id cannot be empty.", nameof(sessionId));
            }

            if (!Directory.Exists(_storageDirectory))
            {
                return null;
            }

            await _directoryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using FileStream processLock =
                    await AcquireProcessLockAsync(cancellationToken).ConfigureAwait(false);
                string path = GetSessionPath(sessionId);
                if (!File.Exists(path))
                {
                    return null;
                }

                PersistedOptimizationSession persisted =
                    await ReadPersistedSessionAsync(path, cancellationToken).ConfigureAwait(false);
                return persisted.ToDomain();
            }
            finally
            {
                _directoryGate.Release();
            }
        }

        public async Task<IReadOnlyList<SqlOptimizationSession>> LoadAllAsync(
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(_storageDirectory))
            {
                return Array.Empty<SqlOptimizationSession>();
            }

            await _directoryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using FileStream processLock =
                    await AcquireProcessLockAsync(cancellationToken).ConfigureAwait(false);
                List<SqlOptimizationSession> sessions = new List<SqlOptimizationSession>();

                foreach (string path in Directory
                    .EnumerateFiles(_storageDirectory, "session-*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PersistedOptimizationSession persisted =
                        await ReadPersistedSessionAsync(path, cancellationToken).ConfigureAwait(false);
                    sessions.Add(persisted.ToDomain());
                }

                return sessions;
            }
            finally
            {
                _directoryGate.Release();
            }
        }

        public async Task SaveAsync(
            SqlOptimizationSession session,
            CancellationToken cancellationToken = default)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Directory.CreateDirectory(_storageDirectory);
            await _directoryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            string? temporaryPath = null;

            try
            {
                using FileStream processLock =
                    await AcquireProcessLockAsync(cancellationToken).ConfigureAwait(false);
                string destinationPath = GetSessionPath(session.SessionId);
                temporaryPath = Path.Combine(
                    _storageDirectory,
                    $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

                PersistedOptimizationSession persisted =
                    PersistedOptimizationSession.FromDomain(session);

                await using (FileStream stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(
                            stream,
                            persisted,
                            _jsonOptions,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(true);
                }

                if (File.Exists(destinationPath))
                {
                    File.Replace(temporaryPath, destinationPath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, destinationPath);
                }

                temporaryPath = null;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is JsonException)
            {
                throw new SqlOptimizerPersistenceException(
                    $"Unable to save optimization session '{session.SessionId}'.",
                    exception);
            }
            finally
            {
                if (temporaryPath is not null && File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                _directoryGate.Release();
            }
        }

        private async Task<PersistedOptimizationSession> ReadPersistedSessionAsync(
            string path,
            CancellationToken cancellationToken)
        {
            try
            {
                await using FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                PersistedOptimizationSession? persisted =
                    await JsonSerializer.DeserializeAsync<PersistedOptimizationSession>(
                            stream,
                            _jsonOptions,
                            cancellationToken)
                        .ConfigureAwait(false);

                return persisted ??
                    throw new JsonException("The session document is empty.");
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is JsonException ||
                exception is NotSupportedException)
            {
                throw new SqlOptimizerPersistenceException(
                    $"Unable to read optimization session file '{Path.GetFileName(path)}'.",
                    exception);
            }
        }

        private async Task<FileStream> AcquireProcessLockAsync(
            CancellationToken cancellationToken)
        {
            string lockPath = Path.Combine(_storageDirectory, ".session-store.lock");
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        1,
                        FileOptions.None);
                }
                catch (IOException)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private string GetSessionPath(Guid sessionId)
        {
            return Path.Combine(_storageDirectory, $"session-{sessionId:N}.json");
        }
    }
}

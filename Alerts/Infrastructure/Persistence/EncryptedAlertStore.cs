using System.Security.Cryptography;
using System.Text.Json;
using Project_auto_push_to_gitlab.Alerts.Core;

namespace Project_auto_push_to_gitlab.Alerts.Infrastructure.Persistence;

/// <summary>以CurrentUser DPAPI原子保存告警設定、摘要及分頁明細，不建立明文暫存檔。</summary>
public sealed class EncryptedAlertStore : IAlertStore
{
    private readonly string _root;
    private static readonly byte[] Entropy = "AutoCallGit.Alerts.v1"u8.ToArray();

    /// <summary>建立專用保存路徑；建構時不讀取資料或建立檔案。</summary>
    /// <param name="root">測試可指定隔離目錄；null使用目前使用者的LocalAppData。</param>
    /// <remarks>UI：FrmAlerts及FrmAlertRule經AlertService間接使用；不操作UI。</remarks>
    public EncryptedAlertStore(string? root = null)
    {
        // 1151005 codex GPT-6_しずくちゃん：ユーザー専用の暗号化保存先を固定します。
        _root = Path.GetFullPath(root ?? Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "AutoCallGit", "Alerts"));
    }

    /// <summary>讀取加密規則檔，首次啟動回傳空List，解密或解析失敗保留例外。</summary>
    /// <param name="token">取消讀取。</param><returns>完整規則List，不回傳null。</returns>
    /// <remarks>UI：FrmAlerts的dgvRules經AlertService間接使用。</remarks>
    public Task<List<AlertRule>> LoadRulesAsync(CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：設定ファイルがない初回だけ空一覧を返します。
        return ReadOrNewAsync<List<AlertRule>>(Path.Combine(_root, "rules.bin"), token);
    }

    /// <summary>加密並原子保存所有規則。</summary>
    /// <param name="rules">不可為null，不變更內容。</param><param name="token">取消保存。</param>
    /// <returns>保存完成的Task。</returns><remarks>UI：FrmAlertRule的btnSave經AlertService間接使用。</remarks>
    public Task SaveRulesAsync(List<AlertRule> rules, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：固定値も含め設定全体を暗号化します。
        return WriteAsync(Path.Combine(_root, "rules.bin"), rules, token);
    }

    /// <summary>讀取執行摘要，任何損壞檔案均拋出例外，不視為正常零筆。</summary>
    /// <param name="token">取消讀取。</param><returns>依開始時間由新到舊排序的摘要List。</returns>
    /// <remarks>UI：FrmAlerts的dgvResults與dgvHistory經AlertService間接使用。</remarks>
    public async Task<List<AlertRun>> LoadRunsAsync(CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：専用履歴だけを読み破損を隠さず報告します。
        string history = Path.Combine(_root, "History");
        if (!Directory.Exists(history)) return [];
        RejectLinks(history);
        List<AlertRun> runs = [];
        foreach (string file in Directory.EnumerateFiles(history, "*.bin"))
            runs.Add(await ReadAsync<AlertRun>(file, token).ConfigureAwait(false));
        return runs.OrderByDescending(run => run.StartedAt).ThenByDescending(run => run.Sequence).ToList();
    }

    /// <summary>原子保存單次執行摘要；ID決定檔名，使用者輸入不形成路徑。</summary>
    /// <param name="run">不可為null，不變更內容。</param><param name="token">取消保存。</param>
    /// <returns>保存完成的Task。</returns><remarks>UI：FrmAlerts的dgvHistory經AlertService間接使用。</remarks>
    public Task SaveRunAsync(AlertRun run, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：取得時点の履歴を一つの確定ファイルに保存します。
        return WriteAsync(Path.Combine(_root, "History", $"{run.Id:N}.bin"), run, token);
    }

    /// <summary>讀取永久保留的去重資訊；缺檔時建立空內容。</summary>
    /// <param name="token">取消讀取。</param><returns>去重資訊，不回傳null。</returns>
    /// <remarks>UI：無；由AlertService排程呼叫。</remarks>
    public Task<AlertScheduleLedger> LoadLedgerAsync(CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：履歴の期限切れ後も予定の実行記録を保持します。
        return ReadOrNewAsync<AlertScheduleLedger>(Path.Combine(_root, "ledger.bin"), token);
    }

    /// <summary>原子保存預定去重資訊，不隨90日清理刪除。</summary>
    /// <param name="ledger">不可為null，不變更內容。</param><param name="token">取消保存。</param>
    /// <returns>保存完成的Task。</returns><remarks>UI：無；由AlertService呼叫。</remarks>
    public Task SaveLedgerAsync(AlertScheduleLedger ledger, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：完了した予定の水位を暗号化保存します。
        return WriteAsync(Path.Combine(_root, "ledger.bin"), ledger, token);
    }

    /// <summary>保存最多500筆的明細片段，Null與欄位位置原樣保留。</summary>
    /// <param name="runId">執行識別碼。</param><param name="campus">有效院區別名。</param>
    /// <param name="chunk">從零開始的片段序號。</param><param name="rows">不可為null，不修改內容。</param>
    /// <param name="token">取消保存。</param><returns>加密保存完成的Task。</returns>
    /// <exception cref="ArgumentException">院區或片段大小無效時拋出。</exception>
    /// <remarks>UI：FrmAlerts的dgvDetails經AlertService間接使用。</remarks>
    public Task WriteChunkAsync(Guid runId, string campus, int chunk, List<string?[]> rows, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：大量結果を小さな暗号化片に分割します。
        if (rows.Count is < 1 or > 500) throw new ArgumentException("明細片段大小無效。", nameof(rows));
        return WriteAsync(ChunkPath(runId, campus, chunk), rows, token);
    }

    /// <summary>按200筆頁面讀取明細，必要時跨越500筆儲存片段。</summary>
    /// <param name="runId">執行識別碼。</param><param name="campus">已確定的院區摘要，不修改內容。</param>
    /// <param name="page">從零開始頁碼。</param><param name="token">取消讀取。</param>
    /// <returns>最多200筆的List；超出結果為空List，缺檔或損壞時拋出。</returns>
    /// <exception cref="ArgumentOutOfRangeException">頁碼小於零時拋出。</exception>
    /// <exception cref="InvalidOperationException">院區未完成或取得失敗時拋出。</exception>
    /// <exception cref="InvalidDataException">片段筆數或欄位結構不符合摘要時拋出。</exception>
    /// <remarks>UI：FrmAlerts的btnPrevious與btnNext經Controller間接顯示dgvDetails。</remarks>
    public async Task<List<string?[]>> ReadPageAsync(Guid runId, AlertCampusResult campus, int page, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：必要な片だけを復号してページを組み立てます。
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        if (campus.Status is not (AlertCampusStatus.Normal or AlertCampusStatus.Anomaly))
            throw new InvalidOperationException("此院區沒有完整明細。");
        long first = (long)page * 200;
        long end = Math.Min(first + 200, campus.RowCount);
        List<string?[]> rows = [];
        for (long offset = first; offset < end;)
        {
            int chunk = checked((int)(offset / 500));
            List<string?[]> stored = await ReadAsync<List<string?[]>>(ChunkPath(runId, campus.Campus, chunk), token).ConfigureAwait(false);
            int index = (int)(offset % 500);
            int count = (int)Math.Min(end - offset, stored.Count - index);
            if (count <= 0 || stored.Any(row => row.Length != campus.Columns.Count))
                throw new InvalidDataException("明細檔案結構損壞。");
            rows.AddRange(stored.Skip(index).Take(count));
            offset += count;
        }
        return rows;
    }

    /// <summary>只清理專用目錄內超過90日的履歷與明細，拒絕連結路徑。</summary>
    /// <param name="now">到期判斷時間。</param><param name="token">取消清理。</param>
    /// <returns>清理完成的Task；遇到損壞或刪除失敗拋出。</returns>
    /// <remarks>UI：無；由AlertService啟動及日次維護呼叫。</remarks>
    public async Task CleanupAsync(DateTimeOffset now, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：期限切れの専用履歴だけを削除します。
        foreach (AlertRun run in await LoadRunsAsync(token).ConfigureAwait(false))
        {
            if (run.StartedAt >= now.AddDays(-90)) continue;
            token.ThrowIfCancellationRequested();
            string directory = Path.Combine(_root, "Details", run.Id.ToString("N"));
            if (Directory.Exists(directory))
            {
                RejectLinks(directory);
                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    RejectLinks(file);
                    File.Delete(file);
                }
                Directory.Delete(directory);
            }
            File.Delete(Path.Combine(_root, "History", $"{run.Id:N}.bin"));
        }
    }

    /// <summary>以院區白名單及數字建立明細路徑。</summary>
    /// <param name="id">執行ID。</param><param name="campus">院區別名，不可為空。</param>
    /// <param name="chunk">非負片段序號。</param><returns>位於專用目錄的完整路徑。</returns>
    /// <exception cref="ArgumentException">院區不受支援時拋出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">片段序號小於零時拋出。</exception>
    /// <remarks>UI：FrmAlerts的dgvDetails經明細儲存方法間接使用。</remarks>
    private string ChunkPath(Guid id, string campus, int chunk)
    {
        // 1151005 codex GPT-6_しずくちゃん：入力値による保存先の逸脱を防ぎます。
        if (!AlertSchedule.CampusOrder.Contains(campus)) throw new ArgumentException("院區無效。", nameof(campus));
        ArgumentOutOfRangeException.ThrowIfNegative(chunk);
        return Path.Combine(_root, "Details", id.ToString("N"), $"{campus}-{chunk:D8}.bin");
    }

    /// <summary>拒絕保存根目錄以外的路徑及已存在的ReparsePoint。</summary>
    /// <param name="path">待檢查的檔案或目錄，不更動內容。</param>
    /// <exception cref="IOException">路徑越界或含連結時拋出。</exception>
    /// <remarks>UI：無；由檔案讀寫及清理邊界呼叫。</remarks>
    private void RejectLinks(string path)
    {
        // 1151005 codex GPT-6_しずくちゃん：清掃と書込みの対象を専用保存先へ限定します。
        string absolute = Path.GetFullPath(path);
        if (absolute != _root && !absolute.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("保存路徑超出專用目錄。");
        for (string? current = absolute; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("保存路徑不可使用連結。");
            if (string.Equals(current, _root, StringComparison.OrdinalIgnoreCase)) break;
        }
    }

    /// <summary>讀取可不存在的加密檔，僅缺檔時建立空資料。</summary>
    /// <typeparam name="T">可建立空內容的模型。</typeparam><param name="path">專用目錄路徑。</param>
    /// <param name="token">取消讀取。</param><returns>已解密資料或新模型，不回傳null。</returns>
    /// <remarks>UI：無；由設定與去重資訊讀取方法呼叫。</remarks>
    private async Task<T> ReadOrNewAsync<T>(string path, CancellationToken token) where T : new()
    {
        // 1151005 codex GPT-6_しずくちゃん：ファイル破損と初回起動を区別します。
        RejectLinks(path);
        return File.Exists(path) ? await ReadAsync<T>(path, token).ConfigureAwait(false) : new T();
    }

    /// <summary>讀取並解密單一檔案，不記錄資料內容或完整例外文字。</summary>
    /// <typeparam name="T">預期模型型別。</typeparam><param name="path">已知專用目錄檔案。</param>
    /// <param name="token">取消讀取。</param><returns>已解析模型，不回傳null。</returns>
    /// <exception cref="InvalidDataException">JSON內容為null時拋出。</exception>
    /// <remarks>UI：無；由加密儲存公開方法呼叫。</remarks>
    private async Task<T> ReadAsync<T>(string path, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：CurrentUserの保護を解除して必要なモデルへ読みます。
        RejectLinks(path);
        byte[] ciphertext = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
        byte[] plaintext = ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<T>(plaintext) ?? throw new InvalidDataException("加密檔案內容無效。"); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    /// <summary>先加密，再以同目錄暫存檔原子取代正式檔；清除記憶體中的明文。</summary>
    /// <typeparam name="T">保存模型型別。</typeparam><param name="path">專用目錄目的檔。</param>
    /// <param name="value">不可為null，不更動物件。</param><param name="token">取消保存。</param>
    /// <returns>完整保存的Task；失敗保留原檔並拋出例外。</returns>
    /// <remarks>UI：無；由全部加密儲存寫入方法呼叫。</remarks>
    private async Task WriteAsync<T>(string path, T value, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：暗号化済みの一時ファイルだけを原子的に置換します。
        RejectLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            byte[] encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(encrypted, token).ConfigureAwait(false);
                stream.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporary, path, null, true);
            else File.Move(temporary, path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

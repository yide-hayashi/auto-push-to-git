using System.Text.Json;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.Alerts.Application;

/// <summary>協調規則、單一執行佇列、台北排程、明細保存與通知，不持有UI物件。</summary>
public sealed class AlertService : IAsyncDisposable
{
    private readonly IAlertStore _store;
    private readonly IAlertQueryExecutor _executor;
    private readonly IAlertNotifier _notifier;
    private readonly TimeProvider _clock;
    private readonly IAlertDiagnostics? _diagnostics;
    private readonly SemaphoreSlim _queue = new(1, 1);
    private readonly SemaphoreSlim _settings = new(1, 1);
    private readonly object _state = new();
    private readonly CancellationTokenSource _stop = new();
    private List<AlertRule> _rules = [];
    private List<AlertRun> _runs = [];
    private AlertScheduleLedger _ledger = new();
    private Task? _worker;
    private Task? _shutdown;
    private long _sequence;
    private DateOnly _cleanedDate;
    public string? SystemError { get; private set; }
    public event EventHandler? Changed;

    /// <summary>建立可測試的告警流程；建構時不讀檔、不啟動排程、不連線DB。</summary>
    /// <param name="store">加密保存邊界，不可為null。</param><param name="executor">讀取及Rollback邊界，不可為null。</param>
    /// <param name="notifier">通知邊界，不可為null。</param><param name="clock">測試時間來源；null使用系統UTC時間。</param>
    /// <param name="diagnostics">可替換的去識別化紀錄；null供不保存Log的Fake測試。</param>
    /// <exception cref="ArgumentNullException">保存、執行或通知邊界為null時拋出。</exception>
    /// <remarks>UI：FrmAlerts的btnRun及FrmAlertRule的btnTest、btnSave經Controller間接使用；不操作控制項。</remarks>
    public AlertService(IAlertStore store, IAlertQueryExecutor executor, IAlertNotifier notifier, TimeProvider? clock = null,
        IAlertDiagnostics? diagnostics = null)
    {
        // 1151005 codex GPT-6_しずくちゃん：画面から切り離した保存・実行・通知の境界を受け取ります。
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _clock = clock ?? TimeProvider.System;
        _diagnostics = diagnostics;
    }

    /// <summary>讀取完整設定與履歷，修復中斷標記及去重水位後開始排程。</summary>
    /// <param name="startWorker">True啟動背景排程；False供Fake測試手動觸發。</param>
    /// <param name="token">取消啟動讀取。</param><returns>初始化完成的Task。</returns>
    /// <remarks>UI：Program經Runtime間接使用；失敗不覆寫損壞設定，由StartupError顯示到FrmAlerts的lblStatus。</remarks>
    public async Task InitializeAsync(bool startWorker, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：中断した履歴を復旧し完了済みの予定を重複させません。
        _rules = await _store.LoadRulesAsync(token).ConfigureAwait(false);
        foreach (AlertRule rule in _rules) AlertSchedule.Validate(rule);
        _runs = await _store.LoadRunsAsync(token).ConfigureAwait(false);
        _sequence = _runs.Select(run => run.Sequence).DefaultIfEmpty().Max();
        _ledger = await _store.LoadLedgerAsync(token).ConfigureAwait(false);
        foreach (AlertRun run in _runs)
        {
            if (run.Status == AlertRunStatus.Running)
            {
                run.Status = AlertRunStatus.Interrupted;
                foreach (AlertCampusResult result in run.Campuses.Where(result => result.Status == AlertCampusStatus.Pending))
                    result.Status = AlertCampusStatus.Interrupted;
                await _store.SaveRunAsync(run, token).ConfigureAwait(false);
            }
            if (!run.Manual && run.Status == AlertRunStatus.Completed)
            {
                string key = $"{run.RuleId:N}:{run.RuleVersion}";
                if (!_ledger.Completed.TryGetValue(key, out DateTimeOffset saved) || saved < run.ScheduledAt)
                    _ledger.Completed[key] = run.ScheduledAt;
            }
        }
        await _store.SaveLedgerAsync(_ledger, token).ConfigureAwait(false);
        await _store.CleanupAsync(_clock.GetUtcNow(), token).ConfigureAwait(false);
        _runs = await _store.LoadRunsAsync(token).ConfigureAwait(false);
        _cleanedDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), AlertSchedule.Taipei).DateTime);
        if (startWorker) _worker = Task.Run(WorkerAsync);
    }

    /// <summary>取得獨立規則快照，避免畫面修改執行中的規則。</summary>
    /// <returns>完整規則副本，無資料為空List。</returns><remarks>UI：FrmAlerts的dgvRules經Controller直接使用。</remarks>
    public List<AlertRule> Rules()
    {
        // 1151005 codex GPT-6_しずくちゃん：UIへ可変の内部設定を渡さないよう複製します。
        lock (_state) return Clone(_rules);
    }

    /// <summary>取得獨立執行摘要快照。</summary>
    /// <returns>依開始時間由新到舊的List。</returns><remarks>UI：FrmAlerts的dgvResults與dgvHistory經Controller直接使用。</remarks>
    public List<AlertRun> Runs()
    {
        // 1151005 codex GPT-6_しずくちゃん：実行中の更新から表示用の履歴を分離します。
        lock (_state) return Clone(_runs.OrderByDescending(run => run.StartedAt).ThenByDescending(run => run.Sequence).ToList());
    }

    /// <summary>驗證並保存規則；新規無效，版本變更的啟用規則由下次預定開始。</summary>
    /// <param name="input">使用者輸入，不可為null，不變更原物件。</param><param name="token">取消保存。</param>
    /// <returns>已保存的規則副本。</returns><remarks>UI：FrmAlertRule的btnSave直接使用；輸入來自全部編輯欄位。</remarks>
    public async Task<AlertRule> SaveRuleAsync(AlertRule input, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：原子的な設定保存後に新しい版を有効にします。
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
        token = linked.Token;
        await _settings.WaitAsync(token).ConfigureAwait(false);
        try
        {
            List<AlertRule> rules = Rules();
            AlertRule rule = Clone(input);
            AlertRule? existing = rules.Find(item => item.Id == rule.Id);
            rule.Version = existing is null ? 1 : existing.Version + 1;
            if (existing is null) rule.Enabled = false;
            rule.EligibleAfter = _clock.GetUtcNow();
            AlertSchedule.Validate(rule);
            rules.RemoveAll(item => item.Id == rule.Id);
            rules.Add(rule);
            await _store.SaveRulesAsync(rules, token).ConfigureAwait(false);
            lock (_state) _rules = rules;
            Changed?.Invoke(this, EventArgs.Empty);
            return Clone(rule);
        }
        finally { _settings.Release(); }
    }

    /// <summary>手動執行指定規則，與定期執行共用直列佇列，不推進排程去重水位。</summary>
    /// <param name="ruleId">已保存的固定ID。</param><param name="token">取消等待或執行。</param>
    /// <returns>已保存的執行摘要；取消時拋出。</returns>
    /// <remarks>UI：FrmAlerts的btnRun經Controller.Service使用；等待佇列期間仍以操作時刻計算日期。</remarks>
    public async Task<AlertRun> RunManualAsync(Guid ruleId, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：手動実行も定期処理と同じ一列のキューに入れます。
        DateTimeOffset requested = _clock.GetUtcNow();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
        await _queue.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            AlertRule rule = Rules().Single(item => item.Id == ruleId);
            return await ExecuteAsync(rule, requested, true, linked.Token).ConfigureAwait(false);
        }
        finally { _queue.Release(); }
    }

    /// <summary>測試尚未保存的獨立規則，不改寫設定或排程，仍保存取得當時履歷。</summary>
    /// <param name="input">畫面輸入快照，不修改原物件。</param><param name="token">取消等待或查詢。</param>
    /// <returns>保存完成的手動執行摘要。</returns>
    /// <remarks>UI：FrmAlertRule的btnTest直接使用，輸入來自全部編輯控制項，結果顯示lblMessage。</remarks>
    public async Task<AlertRun> RunTestAsync(AlertRule input, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：未保存の編集内容も設定を変更せず同じキューで検証します。
        DateTimeOffset requested = _clock.GetUtcNow();
        AlertRule rule = Clone(input);
        AlertSchedule.Validate(rule);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
        await _queue.WaitAsync(linked.Token).ConfigureAwait(false);
        try { return await ExecuteAsync(rule, requested, true, linked.Token).ConfigureAwait(false); }
        finally { _queue.Release(); }
    }

    /// <summary>執行各啟用規則的最近未完成預定，停止期間只補最近一次。</summary>
    /// <param name="token">取消等待與執行。</param><returns>本次到期檢查完成的Task。</returns>
    /// <remarks>UI：無；背景Worker呼叫，結果經Changed通知FrmAlerts。</remarks>
    public async Task RunDueAsync(CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：初回有効化以前の予定を除外して直近一回を補います。
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
        token = linked.Token;
        await _queue.WaitAsync(token).ConfigureAwait(false);
        try
        {
            foreach (AlertRule rule in Rules().Where(rule => rule.Enabled))
            {
                DateTimeOffset due = AlertSchedule.Latest(rule, _clock.GetUtcNow());
                string key = AlertSchedule.Key(rule);
                AlertRun? interrupted = Runs().FirstOrDefault(run => !run.Manual && run.RuleId == rule.Id
                    && run.RuleVersion == rule.Version && run.Status == AlertRunStatus.Interrupted
                    && (!_ledger.Completed.TryGetValue(key, out DateTimeOffset stamp) || stamp < run.ScheduledAt));
                if (interrupted is not null) due = interrupted.ScheduledAt;
                if (due <= rule.EligibleAfter || (_ledger.Completed.TryGetValue(key, out DateTimeOffset completed) && completed >= due)) continue;
                await ExecuteAsync(rule, due, false, token).ConfigureAwait(false);
            }
        }
        finally { _queue.Release(); }
    }

    /// <summary>讀取200筆明細頁，錯誤由Controller顯示，不轉換為正常零筆。</summary>
    /// <param name="run">已保存摘要，不修改。</param><param name="campus">院區別名。</param>
    /// <param name="page">從零開始頁碼。</param><param name="token">取消讀取。</param>
    /// <returns>最多200筆的列值List。</returns><remarks>UI：FrmAlerts的dgvDetails經Controller間接使用。</remarks>
    public Task<List<string?[]>> ReadPageAsync(AlertRun run, string campus, int page, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：履歴取得時点の院区明細をページ単位で読みます。
        return _store.ReadPageAsync(run.Id, run.Campuses.Single(result => result.Campus == campus), page, token);
    }

    /// <summary>執行固定規則快照，逐院區保存明細後確定結果並傳送摘要通知。</summary>
    /// <param name="rule">已驗證規則，不修改。</param><param name="scheduled">預定時間或手動操作時間。</param>
    /// <param name="manual">True為手動，不更新排程水位。</param><param name="token">取消查詢與保存。</param>
    /// <returns>完整保存的摘要。</returns><remarks>UI：FrmAlerts的btnRun及背景排程間接更新dgvResults與dgvHistory。</remarks>
    private async Task<AlertRun> ExecuteAsync(AlertRule rule, DateTimeOffset scheduled, bool manual, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：一院区の失敗を残りから分離して取得時点の履歴を残します。
        AlertSchedule.Validate(rule);
        List<SqlBindParameter> binds = AlertSchedule.Bind(rule, scheduled);
        AlertRun run = new() { RuleId = rule.Id, RuleVersion = rule.Version, Sequence = ++_sequence, Name = rule.Name, Category = rule.Category,
            Purpose = rule.Purpose, ScheduledAt = scheduled, StartedAt = _clock.GetUtcNow(),
            Manual = manual, Status = AlertRunStatus.Running,
            Campuses = AlertSchedule.CampusOrder.Where(rule.Campuses.Contains)
                .Select(campus => new AlertCampusResult { Campus = campus }).ToList() };
        await _store.SaveRunAsync(run, token).ConfigureAwait(false);
        Publish(run);
        try
        {
            foreach (AlertCampusResult result in run.Campuses)
            {
                token.ThrowIfCancellationRequested();
                List<string?[]> buffer = [];
                long count = 0;
                int chunk = 0;
                try
                {
                    await _executor.ExecuteAsync(rule, result.Campus, binds,
                        columns => { result.Columns = columns; return Task.CompletedTask; },
                        async row =>
                        {
                            buffer.Add(row);
                            count++;
                            if (buffer.Count == 500)
                            {
                                await _store.WriteChunkAsync(run.Id, result.Campus, chunk++, buffer, token).ConfigureAwait(false);
                                buffer.Clear();
                            }
                        }, token).ConfigureAwait(false);
                    if (buffer.Count > 0) await _store.WriteChunkAsync(run.Id, result.Campus, chunk++, buffer, token).ConfigureAwait(false);
                    result.RowCount = count;
                    result.ChunkCount = chunk;
                    result.Status = count == 0 ? AlertCampusStatus.Normal : AlertCampusStatus.Anomaly;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    result.Status = AlertCampusStatus.Error;
                    result.RowCount = 0;
                    result.ChunkCount = 0;
                    result.Error = Report(exception, "告警資料取得");
                }
                await _store.SaveRunAsync(run, token).ConfigureAwait(false);
                Publish(run);
            }
            run.Status = AlertRunStatus.Completed;
            run.FinishedAt = _clock.GetUtcNow();
            await _store.SaveRunAsync(run, token).ConfigureAwait(false);
            if (!manual)
            {
                AlertScheduleLedger nextLedger = Clone(_ledger);
                nextLedger.Completed[AlertSchedule.Key(rule)] = scheduled;
                await _store.SaveLedgerAsync(nextLedger, token).ConfigureAwait(false);
                _ledger = nextLedger;
            }
        }
        catch (Exception exception)
        {
            run.Status = AlertRunStatus.Interrupted;
            foreach (AlertCampusResult result in run.Campuses.Where(result => result.Status == AlertCampusStatus.Pending))
                result.Status = AlertCampusStatus.Interrupted;
            SystemError = Report(exception, "告警執行或保存");
            await _store.SaveRunAsync(run, CancellationToken.None).ConfigureAwait(false);
            Publish(run);
            throw;
        }
        if (run.Campuses.Any(result => result.Status is AlertCampusStatus.Anomaly or AlertCampusStatus.Error))
        {
            try { await _notifier.NotifyAsync(Clone(run)).ConfigureAwait(false); }
            catch (Exception exception)
            {
                run.NotificationError = Report(exception, "Windows通知");
                await _store.SaveRunAsync(run, CancellationToken.None).ConfigureAwait(false);
            }
        }
        Publish(run);
        return Clone(run);
    }

    /// <summary>更新內部摘要副本並通知畫面，不從背景執行緒直接操作Control。</summary>
    /// <param name="run">本次摘要，不更動輸入。</param><remarks>UI：FrmAlerts經Controller接收Changed並更新所有Grid。</remarks>
    private void Publish(AlertRun run)
    {
        // 1151005 codex GPT-6_しずくちゃん：表示側へ状態更新だけを知らせます。
        lock (_state)
        {
            _runs.RemoveAll(item => item.Id == run.Id);
            _runs.Add(Clone(run));
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>執行可取消的分鐘排程與台北日次清理；失敗保留可見錯誤並停止自動重試。</summary>
    /// <returns>直到停止要求或不可恢復錯誤的Task。</returns>
    /// <remarks>UI：無；Runtime啟動，Changed通知FrmAlerts的lblStatus。</remarks>
    private async Task WorkerAsync()
    {
        // 1151005 codex GPT-6_しずくちゃん：常駐中の予定確認と専用履歴の期限管理を行います。
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await RunDueAsync(_stop.Token).ConfigureAwait(false);
                DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), AlertSchedule.Taipei).DateTime);
                if (today != _cleanedDate)
                {
                    await _store.CleanupAsync(_clock.GetUtcNow(), _stop.Token).ConfigureAwait(false);
                    lock (_state) _runs.RemoveAll(run => run.StartedAt < _clock.GetUtcNow().AddDays(-90));
                    _cleanedDate = today;
                    Changed?.Invoke(this, EventArgs.Empty);
                }
                await Task.Delay(TimeSpan.FromSeconds(30), _stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        { SystemError ??= "告警排程已停止。"; }
        catch (Exception exception)
        {
            SystemError = Report(exception, "告警排程") + " 定期執行已停止，請重新啟動應用程式。";
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>建立模型副本，僅在記憶體內序列化，不產生明文檔案。</summary>
    /// <typeparam name="T">告警模型型別。</typeparam><param name="value">不可為null，不變更輸入。</param>
    /// <returns>獨立副本。</returns><remarks>UI：無；服務的規則快照與結果發布使用。</remarks>
    private static T Clone<T>(T value)
    {
        // 1151005 codex GPT-6_しずくちゃん：キュー内の設定と編集入力を分離します。
        return JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))!;
    }

    /// <summary>記錄去識別化錯誤並回傳安全訊息，Writer失敗也提供可見狀態。</summary>
    /// <param name="exception">原始例外，不保存完整內容。</param><param name="operation">固定操作名稱。</param>
    /// <returns>安全繁體中文操作名稱與錯誤碼，附加Log保存失敗訊息。</returns>
    /// <remarks>UI：FrmAlerts的lblStatus及dgvHistory經錯誤流程間接使用。</remarks>
    private string Report(Exception exception, string operation)
    {
        // 1151005 codex GPT-6_しずくちゃん：保存可能な技術ログと画面の失敗状態を同じ境界で扱います。
        _diagnostics?.Record(exception, operation);
        string message = AlertErrors.ForOperation(exception, operation);
        return _diagnostics?.Error is string error ? message + " " + error : message;
    }

    /// <summary>取消排程及執行，等待Rollback與保存完成後釋放服務資源。</summary>
    /// <returns>全部告警背景作業結束的ValueTask。</returns>
    /// <remarks>UI：由Program finally在正式結束時呼叫；隱藏FrmAlerts不停止服務。</remarks>
    public ValueTask DisposeAsync()
    {
        // 1151005 codex GPT-6_しずくちゃん：終了時に実行中の接続が解放されるまで待ちます。
        lock (_state) return new ValueTask(_shutdown ??= StopAsync());
    }

    /// <summary>取消回呼失敗時回報SystemError，仍等待執行與保存離開，避免服務已停止卻無法關閉程式。</summary>
    /// <returns>背景執行、Rollback及設定保存均已離開的Task；取消回呼錯誤已記錄，不略過清理。</returns>
    /// <remarks>UI：frm_SessionGrid的closeToolStripMenuItem及Program finally經DisposeAsync間接使用；lblStatus與技術Log可追蹤取消錯誤。</remarks>
    private async Task StopAsync()
    {
        // 1151005 codex GPT-6_しずくちゃん：取消回呼に失敗しても接続解放を待つ終了経路を保証します。
        try { await _stop.CancelAsync().ConfigureAwait(false); }
        catch (Exception exception) { SystemError = Report(exception, "告警停止取消"); }
        finally
        {
            try
            {
                if (_worker is not null) await _worker.ConfigureAwait(false);
                await _queue.WaitAsync().ConfigureAwait(false);
                _queue.Release();
                await _settings.WaitAsync().ConfigureAwait(false);
                _settings.Release();
            }
            finally { _stop.Dispose(); }
        }
    }
}

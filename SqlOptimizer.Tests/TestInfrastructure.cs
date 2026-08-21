using System.Collections.Concurrent;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

internal sealed class InMemoryOptimizationSessionStore : IOptimizationSessionStore
{
    private readonly ConcurrentDictionary<Guid, SqlOptimizationSession> _sessions = new();
    private int _saveCount;

    public int SaveCount => Volatile.Read(ref _saveCount);

    public Task<SqlOptimizationSession?> LoadAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions.TryGetValue(sessionId, out SqlOptimizationSession? session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<SqlOptimizationSession>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SqlOptimizationSession> sessions = _sessions.Values.ToArray();
        return Task.FromResult(sessions);
    }

    public Task SaveAsync(
        SqlOptimizationSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions[session.SessionId] = session;
        Interlocked.Increment(ref _saveCount);
        return Task.CompletedTask;
    }
}

internal sealed class FakeOptimizerClock : IOptimizerClock
{
    public FakeOptimizerClock(DateTimeOffset? initial = null)
    {
        UtcNow = initial ?? new DateTimeOffset(2026, 7, 24, 8, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan duration)
    {
        UtcNow = UtcNow.Add(duration);
    }
}

/// <summary>
/// 提供不連線Oracle的計畫評估替身，記錄EXPLAIN與最終執行要求並回傳可控制的測試資料。
/// </summary>
/// <remarks>
/// UI：無；由SqlOptimizer單元測試透過SqlOptimizationService使用。
/// 不執行Commit、DDL、DML或任何外部資料庫操作。
/// </remarks>
internal sealed class FakeSqlPlanEvaluator : ISqlPlanEvaluator
{
    private readonly object _syncRoot = new();
    private readonly List<SqlEvaluationRequest> _explainRequests = new();
    private readonly List<SqlEvaluationRequest> _executeRequests = new();

    public string PreviewSecret { get; set; } = "ROW-PREVIEW-SECRET";

    public IReadOnlyList<string> ActualPlanWarnings { get; set; } =
        Array.Empty<string>();

    public Exception? FinalExecutionException { get; set; }

    public IReadOnlyList<SqlOutputColumn> OutputColumns { get; set; } =
        CreateOutputColumns();

    // 1150820 codex しずくちゃん_GPT-5：允許測試為每個SQL指定不同預估Cost，以驗證最低值保護。
    public Func<SqlEvaluationRequest, decimal?>? EstimatedCostSelector { get; set; }

    public IReadOnlyList<SqlEvaluationRequest> ExplainRequests
    {
        get
        {
            lock (_syncRoot)
            {
                return _explainRequests.ToArray();
            }
        }
    }

    public IReadOnlyList<SqlEvaluationRequest> ExecuteRequests
    {
        get
        {
            lock (_syncRoot)
            {
                return _executeRequests.ToArray();
            }
        }
    }

    /// <summary>
    /// 記錄EXPLAIN要求並回傳不含資料庫副作用的預估計畫。
    /// </summary>
    /// <param name="request">準備評估的SQL與工作階段條件，不可為null；方法不會修改此物件。</param>
    /// <param name="cancellationToken">取消測試評估的權杖；已取消時立即拋出取消例外。</param>
    /// <returns>包含可由EstimatedCostSelector控制Cost的預估計畫及測試輸出欄位。</returns>
    /// <remarks>
    /// UI：無；由SqlOptimizationServiceTests間接呼叫，不連線Oracle。
    /// </remarks>
    public Task<PlanEvaluationResult> ExplainAsync(
        SqlEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            _explainRequests.Add(request);
        }

        // 1150820 codex しずくちゃん_GPT-5：依SQL產生不同Cost，讓服務層測試能辨識錯誤的高Cost選取。
        decimal estimatedCost = EstimatedCostSelector?.Invoke(request) ?? 1m;
        return Task.FromResult(
            new PlanEvaluationResult(
                CreatePlan(
                    SqlPlanKind.Estimated,
                    request.Sql,
                    request.StatementId,
                    cost: estimatedCost),
                OutputColumns));
    }

    public Task<FinalExecutionResult> ExecuteFinalAsync(
        SqlEvaluationRequest request,
        int previewRowLimit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            _executeRequests.Add(request);
        }

        if (FinalExecutionException is not null)
        {
            return Task.FromException<FinalExecutionResult>(
                FinalExecutionException);
        }

        IReadOnlyList<SqlResultRow> previewRows = previewRowLimit == 0
            ? Array.Empty<SqlResultRow>()
            :
            [
                new SqlResultRow(
                    new Dictionary<string, object?>
                    {
                        ["VALUE"] = PreviewSecret
                    })
            ];

        return Task.FromResult(
            new FinalExecutionResult(
                CreatePlan(
                    SqlPlanKind.Actual,
                    request.Sql,
                    request.StatementId,
                    ActualPlanWarnings),
                new SqlExecutionMetrics(
                    TimeSpan.FromMilliseconds(12),
                    rowsRead: 1,
                    bufferGets: 3,
                    cpuTime: TimeSpan.FromMilliseconds(2)),
                OutputColumns,
                previewRows));
    }

    /// <summary>
    /// 建立固定格式且可指定根節點Cost的測試執行計畫。
    /// </summary>
    /// <param name="kind">計畫種類，必須為Estimated或Actual。</param>
    /// <param name="sql">顯示於測試計畫的SQL文字，不可為null。</param>
    /// <param name="statementId">EXPLAIN使用的Statement ID；最終實測時可為null。</param>
    /// <param name="warnings">附加至計畫的測試警告；沒有警告時可為null。</param>
    /// <param name="cost">根節點的預估Cost；需要模擬缺少Cost時可為null。</param>
    /// <returns>不含真實資料與Bind值的PlanSnapshot。</returns>
    /// <remarks>
    /// UI：無；由FakeSqlPlanEvaluator內部建立單元測試結果。
    /// </remarks>
    private static PlanSnapshot CreatePlan(
        SqlPlanKind kind,
        string sql,
        string? statementId,
        IReadOnlyList<string>? warnings = null,
        decimal? cost = 1m)
    {
        // 1150820 codex しずくちゃん_GPT-5：將測試指定值放入根節點，對齊正式最低Cost比較規則。
        return new PlanSnapshot(
            kind,
            $"{kind} PLAN FOR {sql}",
            [
                new PlanNode(
                    0,
                    null,
                    "SELECT STATEMENT",
                    cost: cost,
                    cardinality: 1)
            ],
            new DateTimeOffset(2026, 7, 24, 8, 0, 0, TimeSpan.Zero),
            statementId,
            planHashValue: "123456",
            warnings: warnings);
    }

    private static IReadOnlyList<SqlOutputColumn> CreateOutputColumns()
    {
        return
        [
            new SqlOutputColumn(0, "VALUE", "NUMBER", false)
        ];
    }
}

internal sealed class BlockingSqlPlanEvaluator : ISqlPlanEvaluator
{
    private readonly TaskCompletionSource _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public async Task<PlanEvaluationResult> ExplainAsync(
        SqlEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        _started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("The blocking evaluator should be cancelled.");
    }

    public Task<FinalExecutionResult> ExecuteFinalAsync(
        SqlEvaluationRequest request,
        int previewRowLimit,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}

internal sealed class OptimizerTestHarness
{
    public OptimizerTestHarness(
        IOptimizationSessionStore? store = null,
        FakeSqlPlanEvaluator? evaluator = null,
        FakeOptimizerClock? clock = null)
    {
        Store = store ?? new InMemoryOptimizationSessionStore();
        Evaluator = evaluator ?? new FakeSqlPlanEvaluator();
        Clock = clock ?? new FakeOptimizerClock();
        Service = new SqlOptimizationService(
            Store,
            Evaluator,
            new OracleSelectStatementValidator(),
            Clock);
    }

    public IOptimizationSessionStore Store { get; }

    public FakeSqlPlanEvaluator Evaluator { get; }

    public FakeOptimizerClock Clock { get; }

    public SqlOptimizationService Service { get; }

    public async Task<SqlOptimizationSession> CreateApprovedSessionAsync(
        string sql = "select :id as VALUE from dual",
        string bindValue = "TEST-BIND-VALUE",
        SqlOptimizationLimits? limits = null)
    {
        SqlOptimizationSession draft = await Service.CreateDraftAsync(
            new CreateOptimizationSessionRequest(
                "KTGH00",
                sql,
                SqlOptimizationObjective.Balanced,
                limits ?? new SqlOptimizationLimits()));

        return await Service.ApproveAsync(
            draft.SessionId,
            [
                new SqlBindParameter(
                    "id",
                    OracleBindDataType.Varchar2,
                    bindValue,
                    size: 128)
            ]);
    }
}

internal sealed class TestDirectory : IDisposable
{
    public TestDirectory(string purpose)
    {
        string safePurpose = new string(
            purpose.Where(character => char.IsAsciiLetterOrDigit(character)).ToArray());
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "AutoCallGit.SqlOptimizer.Tests",
            $"{safePurpose}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

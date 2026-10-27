using System.Data;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Oracle;

namespace Project_auto_push_to_gitlab.Alerts.Infrastructure.KTCC;

/// <summary>使用既有KTCC連線Factory逐筆讀取SELECT，Transaction始終Rollback，不指定Timeout。</summary>
public sealed class OracleAlertQueryExecutor : IAlertQueryExecutor
{
    private readonly IOracleConnectionFactory _connections;
    private readonly ISqlTextValidator _validator;

    /// <summary>注入現有連線與SELECT檢查，保留KTCC.Core的Provider及生命週期。</summary>
    /// <param name="connections">不可為null；持有KTCC控制器的連線Factory。</param>
    /// <param name="validator">不可為null；單一SELECT及具名Bind驗證。</param>
    /// <exception cref="ArgumentNullException">Factory或驗證器為null時拋出。</exception>
    /// <remarks>UI：FrmAlerts的btnRun經AlertService間接使用；建構時不連線DB。</remarks>
    public OracleAlertQueryExecutor(IOracleConnectionFactory connections, ISqlTextValidator validator)
    {
        // 1151005 codex GPT-6_しずくちゃん：既存のKTCC接続とSELECT検証を再利用します。
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>在唯讀Transaction逐筆傳送欄位與值，所有成功、失敗、取消路徑均Rollback。</summary>
    /// <param name="rule">有效快照，不可為null，不修改SQL。</param><param name="campus">受支援院區別名。</param>
    /// <param name="parameters">已轉換的具名參數，不修改內容。</param>
    /// <param name="columns">接收欄位結構的回呼，不可為null。</param><param name="row">接收列值的回呼，不可為null。</param>
    /// <param name="token">取消查詢及讀取。</param><returns>查詢、逐筆保存及Rollback全部完成的Task。</returns>
    /// <exception cref="AggregateException">取消命令或Rollback失敗時，保留原操作及清理的全部例外。</exception>
    /// <remarks>UI：FrmAlerts的btnRun經AlertService間接使用，輸出為dgvDetails；不操作UI。無Commit。</remarks>
    public async Task ExecuteAsync(AlertRule rule, string campus, IReadOnlyList<SqlBindParameter> parameters,
        Func<List<AlertColumn>, Task> columns, Func<string?[], Task> row, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：全件を逐次読み取り、Rollback失敗も取得失敗として返します。
        SqlValidationResult validation = _validator.Validate(rule.Sql);
        _validator.EnsureBindingsMatch(validation, parameters);
        token.ThrowIfCancellationRequested();
        using OracleConnectionLease lease = _connections.Open(campus);
        using OracleTransaction transaction = lease.Connection.BeginTransaction();
        Exception? failure = null;
        Exception? cancelFailure = null;
        try
        {
            using (OracleCommand readOnly = lease.Connection.CreateCommand())
            {
                readOnly.Transaction = transaction;
                readOnly.CommandText = "SET TRANSACTION READ ONLY";
                await readOnly.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            using OracleCommand command = lease.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = validation.ExecutableSql;
            OracleBindParameterFactory.AddParameters(command, parameters);
            using CancellationTokenRegistration cancel = token.Register(() =>
            {
                try { command.Cancel(); }
                catch (Exception exception)
                {
                    // 取消回呼不得阻斷畫面關閉；原例外交回讀取流程，Rollback後一起回報。
                    Interlocked.CompareExchange(ref cancelFailure, exception, null);
                }
            });
            using OracleDataReader reader = (OracleDataReader)await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess, token).ConfigureAwait(false);
            List<AlertColumn> metadata = Enumerable.Range(0, reader.FieldCount)
                .Select(ordinal => new AlertColumn { Name = reader.GetName(ordinal), DataType = reader.GetDataTypeName(ordinal) }).ToList();
            await columns(metadata).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                string?[] values = new string?[reader.FieldCount];
                for (int ordinal = 0; ordinal < values.Length; ordinal++)
                    values[ordinal] = reader.IsDBNull(ordinal) ? null : DisplayValue(reader.GetOracleValue(ordinal));
                await row(values).ConfigureAwait(false);
            }
        }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            try { transaction.Rollback(); }
            catch (Exception rollback)
            {
                if (failure is not null || cancelFailure is not null)
                    throw new AggregateException("資料取得、取消或Rollback失敗。",
                        new[] { failure, cancelFailure, rollback }.OfType<Exception>());
                throw;
            }
            if (cancelFailure is not null)
                throw new AggregateException("資料取得或取消失敗。", new[] { failure, cancelFailure }.OfType<Exception>());
        }
    }

    /// <summary>將Provider值轉為可完整保存的字串，不截斷CLOB及高精度NUMBER。</summary>
    /// <param name="value">非DB NULL的Provider值，不變更內容。</param>
    /// <returns>使用不變文化的文字；二進位使用Base64，不回傳null。</returns>
    /// <remarks>UI：FrmAlerts的dgvDetails經ExecuteAsync間接顯示，原型別保留於AlertColumn。</remarks>
    private static string DisplayValue(object value)
    {
        // 1151005 codex GPT-6_しずくちゃん：列順と型情報を保ちLOBを切り捨てず保存します。
        if (value is OracleClob clob)
        {
            using (clob) return clob.Value;
        }
        if (value is OracleBlob blob)
        {
            using (blob) return Convert.ToBase64String(blob.Value);
        }
        return value switch
        {
            OracleBinary binary => Convert.ToBase64String(binary.Value),
            byte[] bytes => Convert.ToBase64String(bytes),
            OracleDate date => date.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            OracleTimeStamp stamp => stamp.Value.ToString("O", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
    }
}

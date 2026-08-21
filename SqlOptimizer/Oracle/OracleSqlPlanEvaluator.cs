using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Oracle
{
    /// <summary>
    /// Oracle boundary for estimated-plan evaluation and the single final execution.
    /// Every operation gets a dedicated KTCC connection and all Oracle work is globally serialized.
    /// </summary>
    public sealed class OracleSqlPlanEvaluator : ISqlPlanEvaluator
    {
        private const string ExplainFormat = "ALL +PREDICATE +ALIAS +OUTLINE";
        private const string ActualFormat =
            "ALLSTATS LAST +PEEKED_BINDS +PREDICATE +ALIAS +OUTLINE";

        private static readonly SemaphoreSlim EvaluationGate = new SemaphoreSlim(1, 1);
        private static readonly Regex PlanHashRegex = new Regex(
            @"Plan hash value:\s*(?<hash>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly IOracleConnectionFactory _connectionFactory;
        private readonly ISqlTextValidator _validator;

        public OracleSqlPlanEvaluator(
            IOracleConnectionFactory connectionFactory,
            ISqlTextValidator? validator = null)
        {
            _connectionFactory = connectionFactory
                ?? throw new ArgumentNullException(nameof(connectionFactory));
            _validator = validator ?? new OracleSelectStatementValidator();
        }

        public async Task<PlanEvaluationResult> ExplainAsync(
            SqlEvaluationRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            SqlValidationResult validation = ValidateRequest(request);
            string statementId = CreateStatementId(request.StatementId);

            await EvaluationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using OracleConnectionLease lease =
                    _connectionFactory.Open(request.ConnectionAlias);
                OracleConnection connection = lease.Connection;
                using OracleTransaction transaction = connection.BeginTransaction();

                try
                {
                    await ExplainStatementAsync(
                            connection,
                            transaction,
                            validation.ExecutableSql,
                            request.BindParameters,
                            request.TimeoutSeconds,
                            statementId,
                            cancellationToken)
                        .ConfigureAwait(false);

                    IReadOnlyList<PlanNode> nodes = await ReadPlanNodesAsync(
                            connection,
                            transaction,
                            statementId,
                            request.TimeoutSeconds,
                            cancellationToken)
                        .ConfigureAwait(false);
                    string formattedPlan = await ReadPlanTextAsync(
                            connection,
                            transaction,
                            statementId,
                            request.TimeoutSeconds,
                            cancellationToken)
                        .ConfigureAwait(false);
                    IReadOnlyList<SqlOutputColumn> outputColumns = await DescribeOutputAsync(
                            connection,
                            transaction,
                            validation.ExecutableSql,
                            request.BindParameters,
                            request.TimeoutSeconds,
                            cancellationToken)
                        .ConfigureAwait(false);

                    PlanSnapshot snapshot = new PlanSnapshot(
                        SqlPlanKind.Estimated,
                        formattedPlan,
                        nodes,
                        DateTimeOffset.UtcNow,
                        statementId,
                        ExtractPlanHash(formattedPlan),
                        new[]
                        {
                            "EXPLAIN PLAN may not reproduce bind peeking. Use the completed actual plan for the final decision."
                        });

                    return new PlanEvaluationResult(snapshot, outputColumns);
                }
                finally
                {
                    TryRollback(transaction);
                }
            }
            finally
            {
                EvaluationGate.Release();
            }
        }

        public async Task<FinalExecutionResult> ExecuteFinalAsync(
            SqlEvaluationRequest request,
            int previewRowLimit,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (previewRowLimit < 0 || previewRowLimit > 1000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(previewRowLimit),
                    "The preview row limit must be between 0 and 1000.");
            }

            SqlValidationResult validation = ValidateRequest(request);
            await EvaluationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using OracleConnectionLease lease =
                    _connectionFactory.Open(request.ConnectionAlias);
                OracleConnection connection = lease.Connection;

                using OracleTransaction transaction = connection.BeginTransaction();
                try
                {
                    await SetReadOnlyTransactionAsync(
                            connection,
                            transaction,
                            request.TimeoutSeconds,
                            cancellationToken)
                        .ConfigureAwait(false);

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    (long rowsRead,
                        IReadOnlyList<SqlOutputColumn> outputColumns,
                        IReadOnlyList<SqlResultRow> previewRows) =
                        await ExecuteAndDrainAsync(
                                connection,
                                transaction,
                                validation.ExecutableSql,
                                request.BindParameters,
                                request.TimeoutSeconds,
                                previewRowLimit,
                                cancellationToken)
                            .ConfigureAwait(false);
                    stopwatch.Stop();

                    string actualPlan;
                    List<string> warnings = new List<string>();
                    try
                    {
                        actualPlan = await ReadActualPlanTextAsync(
                                connection,
                                transaction,
                                request.TimeoutSeconds,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (actualPlan.Contains(
                                "basic plan statistics not available",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.Add(
                                "Oracle did not collect row-source statistics for this cursor. No session or database setting was changed.");
                        }
                    }
                    catch (OracleException ex)
                    {
                        actualPlan =
                            "The SELECT completed, but DBMS_XPLAN.DISPLAY_CURSOR could not be read.";
                        warnings.Add(
                            $"Actual plan unavailable. Verify catalog/V$ privileges. Oracle error {ex.Number}.");
                    }

                    PlanSnapshot snapshot = new PlanSnapshot(
                        SqlPlanKind.Actual,
                        actualPlan,
                        Array.Empty<PlanNode>(),
                        DateTimeOffset.UtcNow,
                        planHashValue: ExtractPlanHash(actualPlan),
                        warnings: warnings);
                    SqlExecutionMetrics metrics = new SqlExecutionMetrics(
                        stopwatch.Elapsed,
                        rowsRead);

                    return new FinalExecutionResult(
                        snapshot,
                        metrics,
                        outputColumns,
                        previewRows);
                }
                finally
                {
                    TryRollback(transaction);
                }
            }
            finally
            {
                EvaluationGate.Release();
            }
        }

        private SqlValidationResult ValidateRequest(SqlEvaluationRequest request)
        {
            if (request.TimeoutSeconds < 1 || request.TimeoutSeconds > 3600)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    "The command timeout must be between 1 and 3600 seconds.");
            }

            SqlValidationResult validation = _validator.Validate(request.Sql);
            _validator.EnsureBindingsMatch(validation, request.BindParameters);
            return validation;
        }

        private static async Task ExplainStatementAsync(
            OracleConnection connection,
            OracleTransaction transaction,
            string sql,
            IReadOnlyList<SqlBindParameter> binds,
            int timeoutSeconds,
            string statementId,
            CancellationToken cancellationToken)
        {
            using OracleCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = timeoutSeconds;
            command.CommandText =
                $"EXPLAIN PLAN SET STATEMENT_ID = '{statementId}' FOR {sql}";
            OracleBindParameterFactory.AddParameters(command, binds);
            await ExecuteNonQueryAsync(command, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<IReadOnlyList<PlanNode>> ReadPlanNodesAsync(
            OracleConnection connection,
            OracleTransaction transaction,
            string statementId,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT id,
                       parent_id,
                       operation,
                       options,
                       object_owner,
                       object_name,
                       cost,
                       cardinality,
                       bytes,
                       depth,
                       position,
                       object_alias,
                       temp_space,
                       access_predicates,
                       filter_predicates,
                       projection,
                       time
                  FROM plan_table
                 WHERE statement_id = :statement_id
                 ORDER BY id
                """;

            using OracleCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.BindByName = true;
            command.CommandTimeout = timeoutSeconds;
            command.CommandText = sql;
            command.Parameters.Add(
                new OracleParameter("statement_id", OracleDbType.Varchar2, statementId, ParameterDirection.Input));

            List<PlanNode> nodes = new List<PlanNode>();
            using OracleDataReader reader =
                await ExecuteReaderAsync(command, CommandBehavior.SequentialAccess, cancellationToken)
                    .ConfigureAwait(false);
            using CancellationTokenRegistration readCancellation =
                cancellationToken.Register(
                    static state => TryCancel((OracleCommand)state!),
                    command);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                nodes.Add(new PlanNode(
                    GetInt32(reader, 0) ?? 0,
                    GetInt32(reader, 1),
                    GetString(reader, 2) ?? string.Empty,
                    GetString(reader, 3),
                    GetString(reader, 4),
                    GetString(reader, 5),
                    GetDecimal(reader, 6),
                    GetInt64(reader, 7),
                    GetInt64(reader, 8),
                    GetString(reader, 13),
                    GetString(reader, 14),
                    GetInt32(reader, 9),
                    GetInt32(reader, 10),
                    GetString(reader, 11),
                    GetInt64(reader, 12),
                    GetString(reader, 15),
                    GetInt64(reader, 16)));
            }

            return nodes;
        }

        private static Task<string> ReadPlanTextAsync(
            OracleConnection connection,
            OracleTransaction transaction,
            string statementId,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT plan_table_output
                  FROM TABLE(
                       DBMS_XPLAN.DISPLAY(
                           'PLAN_TABLE',
                           :statement_id,
                           'ALL +PREDICATE +ALIAS +OUTLINE'))
                """;
            return ReadTextLinesAsync(
                connection,
                transaction,
                sql,
                statementId,
                timeoutSeconds,
                cancellationToken);
        }

        private static Task<string> ReadActualPlanTextAsync(
            OracleConnection connection,
            OracleTransaction transaction,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT plan_table_output
                  FROM TABLE(
                       DBMS_XPLAN.DISPLAY_CURSOR(
                           NULL,
                           NULL,
                           'ALLSTATS LAST +PEEKED_BINDS +PREDICATE +ALIAS +OUTLINE'))
                """;
            return ReadTextLinesAsync(
                connection,
                transaction,
                sql,
                null,
                timeoutSeconds,
                cancellationToken);
        }

        private static async Task<string> ReadTextLinesAsync(
            OracleConnection connection,
            OracleTransaction transaction,
            string sql,
            string? statementId,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using OracleCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.BindByName = true;
            command.CommandTimeout = timeoutSeconds;
            command.CommandText = sql;
            if (statementId is not null)
            {
                command.Parameters.Add(
                    new OracleParameter(
                        "statement_id",
                        OracleDbType.Varchar2,
                        statementId,
                        ParameterDirection.Input));
            }

            StringBuilder output = new StringBuilder();
            using OracleDataReader reader =
                await ExecuteReaderAsync(command, CommandBehavior.SequentialAccess, cancellationToken)
                    .ConfigureAwait(false);
            using CancellationTokenRegistration readCancellation =
                cancellationToken.Register(
                    static state => TryCancel((OracleCommand)state!),
                    command);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (output.Length > 0)
                {
                    output.AppendLine();
                }

                output.Append(reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
            }

            return output.ToString();
        }

        private static async Task<IReadOnlyList<SqlOutputColumn>> DescribeOutputAsync(
            OracleConnection connection,
            OracleTransaction transaction,
            string sql,
            IReadOnlyList<SqlBindParameter> binds,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using OracleCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = timeoutSeconds;
            command.CommandText = $"SELECT * FROM ({sql}) AISO_METADATA WHERE 1 = 0";
            OracleBindParameterFactory.AddParameters(command, binds);

            using OracleDataReader reader = await ExecuteReaderAsync(
                    command,
                    CommandBehavior.SchemaOnly,
                    cancellationToken)
                .ConfigureAwait(false);
            return ReadOutputColumns(reader);
        }

        private static async Task SetReadOnlyTransactionAsync(
            OracleConnection connection,
            OracleTransaction transaction,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using OracleCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = timeoutSeconds;
            command.CommandText = "SET TRANSACTION READ ONLY";
            await ExecuteNonQueryAsync(command, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<(
            long RowsRead,
            IReadOnlyList<SqlOutputColumn> OutputColumns,
            IReadOnlyList<SqlResultRow> PreviewRows)> ExecuteAndDrainAsync(
            OracleConnection connection,
            OracleTransaction transaction,
            string sql,
            IReadOnlyList<SqlBindParameter> binds,
            int timeoutSeconds,
            int previewRowLimit,
            CancellationToken cancellationToken)
        {
            using OracleCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = timeoutSeconds;
            command.CommandText = sql;
            OracleBindParameterFactory.AddParameters(command, binds);

            using OracleDataReader reader = await ExecuteReaderAsync(
                    command,
                    CommandBehavior.SequentialAccess,
                    cancellationToken)
                .ConfigureAwait(false);
            using CancellationTokenRegistration readCancellation =
                cancellationToken.Register(
                    static state => TryCancel((OracleCommand)state!),
                    command);
            IReadOnlyList<SqlOutputColumn> columns = ReadOutputColumns(reader);
            List<SqlResultRow> preview = new List<SqlResultRow>();
            long rowsRead = 0;

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rowsRead++;
                if (preview.Count >= previewRowLimit)
                {
                    continue;
                }

                Dictionary<string, object?> values =
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                {
                    string name = CreateUniqueColumnName(values, reader.GetName(ordinal), ordinal);
                    object? value = reader.IsDBNull(ordinal)
                        ? null
                        : NormalizePreviewValue(reader.GetValue(ordinal));
                    values.Add(name, value);
                }

                preview.Add(new SqlResultRow(values));
            }

            return (rowsRead, columns, preview);
        }

        private static IReadOnlyList<SqlOutputColumn> ReadOutputColumns(OracleDataReader reader)
        {
            DataTable? schema = reader.GetSchemaTable();
            List<SqlOutputColumn> output = new List<SqlOutputColumn>(reader.FieldCount);
            for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                bool? nullable = null;
                if (schema is not null &&
                    ordinal < schema.Rows.Count &&
                    schema.Columns.Contains("AllowDBNull") &&
                    schema.Rows[ordinal]["AllowDBNull"] is bool allowDBNull)
                {
                    nullable = allowDBNull;
                }

                output.Add(new SqlOutputColumn(
                    ordinal,
                    reader.GetName(ordinal),
                    reader.GetDataTypeName(ordinal),
                    nullable,
                    ReadSchemaInt(schema, ordinal, "ColumnSize"),
                    ReadSchemaInt(schema, ordinal, "NumericPrecision"),
                    ReadSchemaInt(schema, ordinal, "NumericScale")));
            }

            return output;
        }

        private static int? ReadSchemaInt(
            DataTable? schema,
            int ordinal,
            string columnName)
        {
            if (schema is null ||
                ordinal >= schema.Rows.Count ||
                !schema.Columns.Contains(columnName))
            {
                return null;
            }

            object value = schema.Rows[ordinal][columnName];
            return value is DBNull
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static async Task<int> ExecuteNonQueryAsync(
            OracleCommand command,
            CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration registration =
                cancellationToken.Register(static state => TryCancel((OracleCommand)state!), command);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<OracleDataReader> ExecuteReaderAsync(
            OracleCommand command,
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration registration =
                cancellationToken.Register(static state => TryCancel((OracleCommand)state!), command);
            return (OracleDataReader)await command
                .ExecuteReaderAsync(behavior, cancellationToken)
                .ConfigureAwait(false);
        }

        private static void TryCancel(OracleCommand command)
        {
            try
            {
                command.Cancel();
            }
            catch
            {
                // Cancellation is best-effort; the original operation reports the terminal error.
            }
        }

        private static void TryRollback(OracleTransaction transaction)
        {
            try
            {
                transaction.Rollback();
            }
            catch
            {
                // Connection disposal is the final rollback boundary.
            }
        }

        private static string CreateStatementId(string? requested)
        {
            string source = string.IsNullOrWhiteSpace(requested)
                ? "AISO_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
                : requested.Trim().ToUpperInvariant();
            string sanitized = new string(
                source
                    .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
                    .ToArray());

            if (sanitized.Length == 0)
            {
                sanitized = "AISO_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            }

            return sanitized.Substring(0, Math.Min(30, sanitized.Length));
        }

        private static string? ExtractPlanHash(string plan)
        {
            Match match = PlanHashRegex.Match(plan ?? string.Empty);
            return match.Success ? match.Groups["hash"].Value : null;
        }

        private static string CreateUniqueColumnName(
            IReadOnlyDictionary<string, object?> values,
            string? requestedName,
            int ordinal)
        {
            string baseName = string.IsNullOrWhiteSpace(requestedName)
                ? $"Column{ordinal + 1}"
                : requestedName;
            string candidate = baseName;
            int suffix = 2;
            while (values.ContainsKey(candidate))
            {
                candidate = $"{baseName}#{suffix++}";
            }

            return candidate;
        }

        private static object? NormalizePreviewValue(object value)
        {
            return value switch
            {
                DBNull => null,
                OracleClob clob => Truncate(clob.Value, 4000),
                OracleBlob blob => $"[BLOB {blob.Length:N0} bytes]",
                byte[] bytes => $"[BINARY {bytes.LongLength:N0} bytes]",
                string text => Truncate(text, 4000),
                _ => value
            };
        }

        private static string Truncate(string value, int maximumLength)
        {
            return value.Length <= maximumLength
                ? value
                : value.Substring(0, maximumLength) + "…";
        }

        private static string? GetString(OracleDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? null
                : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static int? GetInt32(OracleDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? null
                : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static long? GetInt64(OracleDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? null
                : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static decimal? GetDecimal(OracleDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? null
                : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
    }
}

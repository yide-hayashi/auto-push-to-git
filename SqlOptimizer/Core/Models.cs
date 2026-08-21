using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Core
{
    public enum SqlOptimizationSessionStatus
    {
        Draft,
        Approved,
        Running,
        AwaitingUser,
        Accepted,
        Stopped,
        Error
    }

    public enum SqlOptimizationObjective
    {
        Balanced,
        ResponseTime,
        InputOutput,
        PlanStability
    }

    public enum SqlPlanKind
    {
        Estimated,
        Actual
    }

    public enum SqlIterationStatus
    {
        Pending,
        Explained,
        Executing,
        Completed,
        Failed
    }

    public enum OracleBindDataType
    {
        Varchar2,
        NVarchar2,
        Char,
        NChar,
        Int16,
        Int32,
        Int64,
        Decimal,
        Single,
        Double,
        Date,
        TimeStamp,
        TimeStampWithTimeZone,
        TimeStampWithLocalTimeZone,
        Raw,
        Blob,
        Clob,
        NClob,
        Guid,
        Boolean
    }

    public sealed class SqlOptimizationLimits
    {
        public const int HardMaximumCandidatesPerRound = 10;

        public SqlOptimizationLimits(
            int commandTimeoutSeconds = 60,
            int previewRowLimit = 200,
            int maxCandidatesPerRound = HardMaximumCandidatesPerRound)
        {
            if (commandTimeoutSeconds < 1 || commandTimeoutSeconds > 3600)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(commandTimeoutSeconds),
                    "The command timeout must be between 1 and 3600 seconds.");
            }

            if (previewRowLimit < 0 || previewRowLimit > 1000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(previewRowLimit),
                    "The preview row limit must be between 0 and 1000.");
            }

            if (maxCandidatesPerRound < 1 ||
                maxCandidatesPerRound > HardMaximumCandidatesPerRound)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxCandidatesPerRound),
                    $"The candidate limit must be between 1 and {HardMaximumCandidatesPerRound}.");
            }

            CommandTimeoutSeconds = commandTimeoutSeconds;
            PreviewRowLimit = previewRowLimit;
            MaxCandidatesPerRound = maxCandidatesPerRound;
        }

        public int CommandTimeoutSeconds { get; }

        public int PreviewRowLimit { get; }

        public int MaxCandidatesPerRound { get; }
    }

    public sealed class SqlBindDefinition
    {
        public SqlBindDefinition(string name, OracleBindDataType dataType, int? size = null)
        {
            Name = SqlBindName.Normalize(name);
            DataType = dataType;

            if (size.HasValue && size.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Bind size must be positive.");
            }

            Size = size;
        }

        public string Name { get; }

        public OracleBindDataType DataType { get; }

        public int? Size { get; }
    }

    /// <summary>
    /// A runtime bind value. Value and null state must never be serialized.
    /// </summary>
    public sealed class SqlBindParameter
    {
        public SqlBindParameter(
            string name,
            OracleBindDataType dataType,
            object? value,
            bool isNull = false,
            int? size = null)
        {
            Definition = new SqlBindDefinition(name, dataType, size);
            Value = value;
            IsNull = isNull || value is null;
        }

        public SqlBindDefinition Definition { get; }

        public string Name => Definition.Name;

        public OracleBindDataType DataType => Definition.DataType;

        public int? Size => Definition.Size;

        [JsonIgnore]
        public bool IsNull { get; }

        [JsonIgnore]
        public object? Value { get; }
    }

    public sealed class SqlApprovalSnapshot
    {
        public SqlApprovalSnapshot(
            string connectionAlias,
            string sqlHash,
            string bindSchemaHash,
            IReadOnlyList<SqlBindDefinition> bindDefinitions,
            SqlOptimizationLimits limits,
            DateTimeOffset approvedAt)
        {
            ConnectionAlias = connectionAlias;
            SqlHash = sqlHash;
            BindSchemaHash = bindSchemaHash;
            BindDefinitions = new ReadOnlyCollection<SqlBindDefinition>(
                bindDefinitions.ToArray());
            Limits = limits;
            ApprovedAt = approvedAt;
        }

        public string ConnectionAlias { get; }

        public string SqlHash { get; }

        public string BindSchemaHash { get; }

        public IReadOnlyList<SqlBindDefinition> BindDefinitions { get; }

        public SqlOptimizationLimits Limits { get; }

        public DateTimeOffset ApprovedAt { get; }
    }

    public sealed class PlanNode
    {
        public PlanNode(
            int id,
            int? parentId,
            string operation,
            string? options = null,
            string? objectOwner = null,
            string? objectName = null,
            decimal? cost = null,
            long? cardinality = null,
            long? bytes = null,
            string? accessPredicate = null,
            string? filterPredicate = null,
            int? depth = null,
            int? position = null,
            string? objectAlias = null,
            long? tempSpace = null,
            string? projection = null,
            long? timeSeconds = null)
        {
            Id = id;
            ParentId = parentId;
            Operation = operation ?? string.Empty;
            Options = options;
            ObjectOwner = objectOwner;
            ObjectName = objectName;
            Cost = cost;
            Cardinality = cardinality;
            Bytes = bytes;
            AccessPredicate = accessPredicate;
            FilterPredicate = filterPredicate;
            Depth = depth;
            Position = position;
            ObjectAlias = objectAlias;
            TempSpace = tempSpace;
            Projection = projection;
            TimeSeconds = timeSeconds;
        }

        public int Id { get; }

        public int? ParentId { get; }

        public string Operation { get; }

        public string? Options { get; }

        public string? ObjectOwner { get; }

        public string? ObjectName { get; }

        public decimal? Cost { get; }

        public long? Cardinality { get; }

        public long? Bytes { get; }

        public string? AccessPredicate { get; }

        public string? FilterPredicate { get; }

        public int? Depth { get; }

        public int? Position { get; }

        public string? ObjectAlias { get; }

        public long? TempSpace { get; }

        public string? Projection { get; }

        public long? TimeSeconds { get; }
    }

    public sealed class PlanSnapshot
    {
        public PlanSnapshot(
            SqlPlanKind kind,
            string formattedPlan,
            IReadOnlyList<PlanNode>? nodes,
            DateTimeOffset capturedAt,
            string? statementId = null,
            string? planHashValue = null,
            IReadOnlyList<string>? warnings = null)
        {
            Kind = kind;
            FormattedPlan = SqlPlanTextRedactor.RedactPeekedBindValues(formattedPlan);
            Nodes = new ReadOnlyCollection<PlanNode>((nodes ?? Array.Empty<PlanNode>()).ToArray());
            CapturedAt = capturedAt;
            StatementId = statementId;
            PlanHashValue = planHashValue;
            Warnings = new ReadOnlyCollection<string>((warnings ?? Array.Empty<string>()).ToArray());
        }

        public SqlPlanKind Kind { get; }

        public string FormattedPlan { get; }

        public IReadOnlyList<PlanNode> Nodes { get; }

        public DateTimeOffset CapturedAt { get; }

        public string? StatementId { get; }

        public string? PlanHashValue { get; }

        public IReadOnlyList<string> Warnings { get; }
    }

    public sealed class SqlOutputColumn
    {
        public SqlOutputColumn(
            int ordinal,
            string name,
            string providerTypeName,
            bool? isNullable = null,
            int? columnSize = null,
            int? numericPrecision = null,
            int? numericScale = null)
        {
            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            Ordinal = ordinal;
            Name = name ?? string.Empty;
            ProviderTypeName = providerTypeName ?? string.Empty;
            IsNullable = isNullable;
            ColumnSize = columnSize;
            NumericPrecision = numericPrecision;
            NumericScale = numericScale;
        }

        public int Ordinal { get; }

        public string Name { get; }

        public string ProviderTypeName { get; }

        public bool? IsNullable { get; }

        public int? ColumnSize { get; }

        public int? NumericPrecision { get; }

        public int? NumericScale { get; }
    }

    public sealed class SqlExecutionMetrics
    {
        public SqlExecutionMetrics(
            TimeSpan elapsed,
            long rowsRead,
            long? bufferGets = null,
            TimeSpan? cpuTime = null)
        {
            if (rowsRead < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rowsRead));
            }

            Elapsed = elapsed;
            RowsRead = rowsRead;
            BufferGets = bufferGets;
            CpuTime = cpuTime;
        }

        public TimeSpan Elapsed { get; }

        public long RowsRead { get; }

        public long? BufferGets { get; }

        public TimeSpan? CpuTime { get; }
    }

    /// <summary>
    /// Runtime-only result data for the WinForms preview.
    /// </summary>
    public sealed class SqlResultRow
    {
        public SqlResultRow(IReadOnlyDictionary<string, object?> values)
        {
            Values = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase));
        }

        [JsonIgnore]
        public IReadOnlyDictionary<string, object?> Values { get; }
    }

    public sealed class PlanEvaluationResult
    {
        public PlanEvaluationResult(
            PlanSnapshot plan,
            IReadOnlyList<SqlOutputColumn>? outputColumns = null)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            OutputColumns = new ReadOnlyCollection<SqlOutputColumn>(
                (outputColumns ?? Array.Empty<SqlOutputColumn>()).ToArray());
        }

        public PlanSnapshot Plan { get; }

        public IReadOnlyList<SqlOutputColumn> OutputColumns { get; }
    }

    public sealed class FinalExecutionResult
    {
        public FinalExecutionResult(
            PlanSnapshot actualPlan,
            SqlExecutionMetrics metrics,
            IReadOnlyList<SqlOutputColumn>? outputColumns = null,
            IReadOnlyList<SqlResultRow>? previewRows = null)
        {
            ActualPlan = actualPlan ?? throw new ArgumentNullException(nameof(actualPlan));
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            OutputColumns = new ReadOnlyCollection<SqlOutputColumn>(
                (outputColumns ?? Array.Empty<SqlOutputColumn>()).ToArray());
            PreviewRows = new ReadOnlyCollection<SqlResultRow>(
                (previewRows ?? Array.Empty<SqlResultRow>()).ToArray());
        }

        public PlanSnapshot ActualPlan { get; }

        public SqlExecutionMetrics Metrics { get; }

        public IReadOnlyList<SqlOutputColumn> OutputColumns { get; }

        [JsonIgnore]
        public IReadOnlyList<SqlResultRow> PreviewRows { get; }
    }

    internal static class SqlBindName
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A bind name is required.", nameof(name));
            }

            string normalized = name.Trim();
            if (normalized.StartsWith(":", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            if (normalized.Length == 0)
            {
                throw new ArgumentException("A bind name is required.", nameof(name));
            }

            return normalized.ToUpperInvariant();
        }
    }
}

using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Oracle.ManagedDataAccess.Client;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Oracle
{
    internal static class OracleBindParameterFactory
    {
        public static void AddParameters(
            OracleCommand command,
            IReadOnlyList<SqlBindParameter> bindParameters)
        {
            command.BindByName = true;
            foreach (SqlBindParameter bind in bindParameters)
            {
                OracleParameter parameter = new OracleParameter
                {
                    ParameterName = bind.Name,
                    OracleDbType = ToOracleDbType(bind.DataType),
                    Direction = System.Data.ParameterDirection.Input,
                    Value = bind.IsNull
                        ? DBNull.Value
                        : OracleBindValueConverter.Convert(
                            bind.DataType,
                            bind.Value)
                };

                if (bind.Size.HasValue)
                {
                    parameter.Size = bind.Size.Value;
                }

                command.Parameters.Add(parameter);
            }
        }

        private static OracleDbType ToOracleDbType(OracleBindDataType type)
        {
            return type switch
            {
                OracleBindDataType.Varchar2 => OracleDbType.Varchar2,
                OracleBindDataType.NVarchar2 => OracleDbType.NVarchar2,
                OracleBindDataType.Char => OracleDbType.Char,
                OracleBindDataType.NChar => OracleDbType.NChar,
                OracleBindDataType.Int16 => OracleDbType.Int16,
                OracleBindDataType.Int32 => OracleDbType.Int32,
                OracleBindDataType.Int64 => OracleDbType.Int64,
                OracleBindDataType.Decimal => OracleDbType.Decimal,
                OracleBindDataType.Single => OracleDbType.BinaryFloat,
                OracleBindDataType.Double => OracleDbType.BinaryDouble,
                OracleBindDataType.Date => OracleDbType.Date,
                OracleBindDataType.TimeStamp => OracleDbType.TimeStamp,
                OracleBindDataType.TimeStampWithTimeZone => OracleDbType.TimeStampTZ,
                OracleBindDataType.TimeStampWithLocalTimeZone => OracleDbType.TimeStampLTZ,
                OracleBindDataType.Raw => OracleDbType.Raw,
                OracleBindDataType.Blob => OracleDbType.Blob,
                OracleBindDataType.Clob => OracleDbType.Clob,
                OracleBindDataType.NClob => OracleDbType.NClob,
                OracleBindDataType.Guid => OracleDbType.Raw,
                OracleBindDataType.Boolean => OracleDbType.Int16,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported Oracle bind type.")
            };
        }

    }
}

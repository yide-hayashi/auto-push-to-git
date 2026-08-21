using KTCC.Core;
using Oracle.ManagedDataAccess.Client;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Oracle
{
    public interface IOracleConnectionFactory
    {
        OracleConnectionLease Open(string connectionAlias);
    }

    /// <summary>
    /// Keeps the KTCC controller alive for the whole Oracle operation.
    /// </summary>
    public sealed class OracleConnectionLease : IDisposable
    {
        private readonly KTConnectionController _controller;
        private bool _disposed;

        internal OracleConnectionLease(
            KTConnectionController controller,
            OracleConnection connection)
        {
            _controller = controller;
            Connection = connection;
        }

        public OracleConnection Connection { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _controller.Dispose();
        }
    }

    /// <summary>
    /// Reuses the connection information already supplied by KTCC.Core.
    /// Credentials and connection strings are never exposed to the optimizer.
    /// </summary>
    public sealed class KtccOracleConnectionFactory : IOracleConnectionFactory
    {
        public OracleConnectionLease Open(string connectionAlias)
        {
            ConnectHost host = NormalizeAlias(connectionAlias);
            KTConnectionController controller = new KTConnectionController();

            try
            {
                controller.Connect(host);
                if (controller.Connection is not OracleConnection connection)
                {
                    throw new InvalidOperationException(
                        "KTCC.Core did not return an Oracle connection.");
                }

                if (connection.State != System.Data.ConnectionState.Open)
                {
                    connection.Open();
                }

                return new OracleConnectionLease(controller, connection);
            }
            catch
            {
                controller.Dispose();
                throw;
            }
        }

        private static ConnectHost NormalizeAlias(string connectionAlias)
        {
            return connectionAlias?.Trim().ToUpperInvariant() switch
            {
                "KTGH00" => ConnectHost.KTGH00,
                "HPK210" => ConnectHost.HPK210,
                "TSHIS" => ConnectHost.TSHIS,
                _ => throw new ArgumentException(
                    "Connection alias must be KTGH00, HPK210, or TSHIS.",
                    nameof(connectionAlias))
            };
        }
    }
}

using Oracle.ManagedDataAccess.Client;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Core
{
    /// <summary>
    /// Converts exceptions into messages that are safe to persist or return to
    /// an Agent. Oracle error text is untrusted because database code can put
    /// bind or row values into RAISE_APPLICATION_ERROR messages.
    /// </summary>
    public static class SqlOptimizerErrorSanitizer
    {
        private const int MaximumSafeMessageLength = 512;

        public static string ForOperation(
            Exception exception,
            string operation)
        {
            ArgumentNullException.ThrowIfNull(exception);
            string safeOperation = string.IsNullOrWhiteSpace(operation)
                ? "SQL optimizer operation"
                : SanitizeText(operation);

            if (TryGetOracleErrorNumber(exception, out int oracleErrorNumber))
            {
                return $"{safeOperation} failed with ORA-{Math.Abs((long)oracleErrorNumber):00000}. Database error details were omitted.";
            }

            if (exception is OperationCanceledException)
            {
                return $"{safeOperation} was canceled.";
            }

            if (exception is TimeoutException)
            {
                return $"{safeOperation} timed out.";
            }

            if (exception is SqlOptimizerException optimizerException)
            {
                return Truncate(
                    $"{safeOperation} failed: {SanitizeText(optimizerException.Message)}");
            }

            return $"{safeOperation} failed. Error details were omitted.";
        }

        public static string ForMcpBoundary(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (TryGetOracleErrorNumber(exception, out int oracleErrorNumber))
            {
                return $"Oracle operation failed with ORA-{Math.Abs((long)oracleErrorNumber):00000}. Database error details were omitted.";
            }

            if (exception is SqlOptimizerException or ArgumentException)
            {
                return Truncate(SanitizeText(exception.Message));
            }

            if (exception is TimeoutException)
            {
                return "SQL optimizer operation timed out.";
            }

            return "SQL optimizer operation failed. Error details were omitted.";
        }

        private static bool TryGetOracleErrorNumber(
            Exception exception,
            out int errorNumber)
        {
            Exception? current = exception;
            while (current is not null)
            {
                if (current is OracleException oracleException)
                {
                    errorNumber = oracleException.Number;
                    return true;
                }

                current = current.InnerException;
            }

            errorNumber = 0;
            return false;
        }

        private static string SanitizeText(string value)
        {
            char[] characters = value
                .Select(character =>
                    character is '\r' or '\n' or '\t'
                        ? ' '
                        : char.IsControl(character)
                            ? '?'
                            : character)
                .ToArray();
            return Truncate(new string(characters).Trim());
        }

        private static string Truncate(string value)
        {
            return value.Length <= MaximumSafeMessageLength
                ? value
                : value[..MaximumSafeMessageLength] + "…";
        }
    }
}

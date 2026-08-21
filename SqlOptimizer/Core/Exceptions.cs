namespace Project_auto_push_to_gitlab.SqlOptimizer.Core
{
    public class SqlOptimizerException : Exception
    {
        public SqlOptimizerException(string message)
            : base(message)
        {
        }

        public SqlOptimizerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class SqlValidationException : SqlOptimizerException
    {
        public SqlValidationException(string message)
            : base(message)
        {
        }
    }

    public sealed class InvalidSessionStateException : SqlOptimizerException
    {
        public InvalidSessionStateException(string message)
            : base(message)
        {
        }
    }

    public sealed class SessionLeaseException : SqlOptimizerException
    {
        public SessionLeaseException(string message)
            : base(message)
        {
        }
    }

    public sealed class CandidateLimitExceededException : SqlOptimizerException
    {
        public CandidateLimitExceededException(int maximum)
            : base($"The current round already contains the maximum of {maximum} candidates.")
        {
        }
    }

    public sealed class SqlOutputShapeMismatchException : SqlOptimizerException
    {
        public SqlOutputShapeMismatchException(string message)
            : base(message)
        {
        }
    }

    public sealed class OptimizationSessionNotFoundException : SqlOptimizerException
    {
        public OptimizationSessionNotFoundException(Guid sessionId)
            : base($"Optimization session '{sessionId}' was not found.")
        {
        }
    }

    public sealed class SqlOptimizerPersistenceException : SqlOptimizerException
    {
        public SqlOptimizerPersistenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

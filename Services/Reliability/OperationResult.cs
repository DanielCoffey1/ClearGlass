using System;

namespace ClearGlass.Services.Reliability
{
    /// <summary>
    /// Represents the result of a single operation with retry support
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// Gets whether the operation completed successfully
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets the name of the operation
        /// </summary>
        public string OperationName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the number of attempts used to complete the operation
        /// </summary>
        public int AttemptsUsed { get; init; }

        /// <summary>
        /// Gets the last exception that occurred, if any
        /// </summary>
        public Exception? LastException { get; init; }

        /// <summary>
        /// Gets additional verification details about the operation
        /// </summary>
        public string? VerificationDetails { get; init; }

        /// <summary>
        /// Creates a successful operation result
        /// </summary>
        public static OperationResult Succeeded(string operationName, int attempts = 1, string? details = null)
        {
            return new OperationResult
            {
                Success = true,
                OperationName = operationName,
                AttemptsUsed = attempts,
                VerificationDetails = details
            };
        }

        /// <summary>
        /// Creates a failed operation result
        /// </summary>
        public static OperationResult Failed(string operationName, int attempts, Exception? exception = null, string? details = null)
        {
            return new OperationResult
            {
                Success = false,
                OperationName = operationName,
                AttemptsUsed = attempts,
                LastException = exception,
                VerificationDetails = details
            };
        }
    }
}

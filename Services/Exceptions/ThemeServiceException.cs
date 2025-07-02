using System;

namespace ClearGlass.Services.Exceptions
{
    /// <summary>
    /// Represents errors that occur during theme service operations
    /// </summary>
    public class ThemeServiceException : Exception
    {
        /// <summary>
        /// Gets the operation that caused the exception
        /// </summary>
        public ThemeServiceOperation Operation { get; }

        /// <summary>
        /// Initializes a new instance of the ThemeServiceException class
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="operation">The operation that caused the exception</param>
        public ThemeServiceException(string message, ThemeServiceOperation operation) 
            : base(message)
        {
            Operation = operation;
        }

        /// <summary>
        /// Initializes a new instance of the ThemeServiceException class
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="operation">The operation that caused the exception</param>
        /// <param name="innerException">The inner exception</param>
        public ThemeServiceException(string message, ThemeServiceOperation operation, Exception innerException) 
            : base(message, innerException)
        {
            Operation = operation;
        }
    }

    /// <summary>
    /// Represents the type of theme service operation
    /// </summary>
    public enum ThemeServiceOperation
    {
        /// <summary>
        /// Registry access operation
        /// </summary>
        RegistryAccess,

        /// <summary>
        /// Windows API operation
        /// </summary>
        WindowsApi,

        /// <summary>
        /// Process management operation
        /// </summary>
        ProcessManagement,

        /// <summary>
        /// File system operation
        /// </summary>
        FileSystem
    }
} 
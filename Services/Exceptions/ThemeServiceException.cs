using System;

namespace ClearGlass.Services.Exceptions
{
    /// <summary>
    /// Defines the type of operation that caused an error
    /// </summary>
    public enum ThemeServiceOperation
    {
        RegistryAccess,
        WindowsApi,
        ProcessManagement,
        FileSystem,
        AdminPrivileges
    }

    /// <summary>
    /// Represents errors that occur during theme service operations
    /// </summary>
    public class ThemeServiceException : Exception
    {
        /// <summary>
        /// Gets the type of operation that caused the error
        /// </summary>
        public ThemeServiceOperation Operation { get; }

        /// <summary>
        /// Initializes a new instance of the ThemeServiceException class
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="operation">The type of operation that caused the error</param>
        /// <param name="innerException">The inner exception that caused this error</param>
        public ThemeServiceException(string message, ThemeServiceOperation operation, Exception innerException = null)
            : base(message, innerException)
        {
            Operation = operation;
        }
    }
} 
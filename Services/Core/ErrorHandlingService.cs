using System;
using System.Windows;

namespace ClearGlass.Services.Core
{
    public class ErrorHandlingService
    {
        public void HandleError(string operation, Exception ex, bool showMessageBox = true)
        {
            // Log the error
            LogError(operation, ex);

            // Show message box if requested
            if (showMessageBox)
            {
                ShowErrorMessage(operation, ex);
            }
        }

        public void HandleWarning(string message, string title = "Warning")
        {
            // Log the warning
            LogWarning(message);

            // Show warning message box
            CustomMessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        public bool HandleErrorWithRetry(string operation, Exception ex)
        {
            LogError(operation, ex);

            var result = CustomMessageBox.Show(
                $"{operation} failed: {ex.Message}\n\nWould you like to retry?",
                "Operation Failed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            return result == MessageBoxResult.Yes;
        }

        private void ShowErrorMessage(string operation, Exception ex)
        {
            CustomMessageBox.Show(
                $"{operation} failed: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void LogError(string operation, Exception ex)
        {
            // TODO: Replace with proper logging system
            System.Diagnostics.Debug.WriteLine($"Error in {operation}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        private void LogWarning(string message)
        {
            // TODO: Replace with proper logging system
            System.Diagnostics.Debug.WriteLine($"Warning: {message}");
        }
    }
} 
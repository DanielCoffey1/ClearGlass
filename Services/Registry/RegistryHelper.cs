using Microsoft.Win32;
using System;
using System.Diagnostics;
using ClearGlass.Services.Exceptions;

namespace ClearGlass.Services.Registry
{
    /// <summary>
    /// Provides centralized access to registry operations with proper error handling
    /// </summary>
    internal static class RegistryHelper
    {
        #region Registry Paths
        public const string TaskbarSettingsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        public const string SearchSettingsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search";
        public const string WidgetsPolicyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        public const string WidgetsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Widgets";
        public const string FeedsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Feeds";
        public const string WebWidgetsPath = @"SOFTWARE\Policies\Microsoft\Dsh";
        public const string WidgetsGPOPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds";
        public const string DesktopIconsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        public const string PersonalizePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        public const string AccentColorPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\History";
        public const string AccentColorSettingsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        public const string ThemePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes";
        public const string CurrentThemePath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
        #endregion

        /// <summary>
        /// Gets a value from the registry with type conversion and error handling
        /// </summary>
        public static T GetValue<T>(string keyPath, string valueName, T defaultValue = default)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
                if (key == null) return defaultValue;
                
                var value = key.GetValue(valueName);
                return value != null ? (T)Convert.ChangeType(value, typeof(T)) : defaultValue;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading registry value {valueName} from {keyPath}: {ex.Message}");
                throw new ThemeServiceException(
                    $"Failed to read registry value: {valueName}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        /// <summary>
        /// Sets a value in the registry with error handling
        /// </summary>
        public static void SetValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true) 
                               ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath);
                if (key == null)
                {
                    throw new ThemeServiceException(
                        $"Failed to create or open registry key: {keyPath}",
                        ThemeServiceOperation.RegistryAccess);
                }
                key.SetValue(valueName, value, valueKind);
            }
            catch (Exception ex) when (ex is not ThemeServiceException)
            {
                Debug.WriteLine($"Error writing registry value {valueName} to {keyPath}: {ex.Message}");
                throw new ThemeServiceException(
                    $"Failed to write registry value: {valueName}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        /// <summary>
        /// Sets a value in the registry, suppressing non-critical errors
        /// </summary>
        public static void SetValueWithFallback(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                SetValue(keyPath, valueName, value, valueKind);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Non-critical error writing registry value {valueName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Flushes registry changes to disk
        /// </summary>
        public static void FlushChanges(string keyPath)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true);
                key?.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error flushing registry changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Flushes registry changes for multiple paths at once
        /// </summary>
        public static void FlushAll(params string[] keyPaths)
        {
            foreach (var keyPath in keyPaths)
            {
                FlushChanges(keyPath);
            }
        }

        /// <summary>
        /// Sets a value in the registry with verification (write, flush, read-back)
        /// </summary>
        /// <returns>True if the value was written and verified successfully</returns>
        public static bool SetValueVerified(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                SetValue(keyPath, valueName, value, valueKind);
                FlushChanges(keyPath);

                // Read back and verify
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
                if (key == null) return false;

                var readBack = key.GetValue(valueName);
                if (readBack == null) return false;

                // Compare values based on type
                if (valueKind == RegistryValueKind.DWord)
                {
                    return Convert.ToInt32(readBack) == Convert.ToInt32(value);
                }
                else if (valueKind == RegistryValueKind.String)
                {
                    return string.Equals(readBack.ToString(), value.ToString(), StringComparison.Ordinal);
                }
                else
                {
                    return readBack.Equals(value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in verified registry write for {valueName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets a value in the registry with verification and retry logic
        /// </summary>
        /// <returns>True if the value was written and verified successfully</returns>
        public static bool SetValueWithRetry(string keyPath, string valueName, object value, RegistryValueKind valueKind, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (SetValueVerified(keyPath, valueName, value, valueKind))
                {
                    if (attempt > 1)
                    {
                        Debug.WriteLine($"Registry write for {valueName} succeeded on attempt {attempt}");
                    }
                    return true;
                }

                if (attempt < maxRetries)
                {
                    // Exponential backoff: 100ms, 200ms, 400ms
                    System.Threading.Thread.Sleep(100 * (int)Math.Pow(2, attempt - 1));
                }
            }

            Debug.WriteLine($"Registry write for {valueName} failed after {maxRetries} attempts");
            return false;
        }

        /// <summary>
        /// Sets a value in HKEY_LOCAL_MACHINE registry with error handling
        /// </summary>
        public static void SetMachineValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath, true) 
                               ?? Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath);
                if (key == null)
                {
                    throw new ThemeServiceException(
                        $"Failed to create or open machine registry key: {keyPath}",
                        ThemeServiceOperation.RegistryAccess);
                }
                key.SetValue(valueName, value, valueKind);
            }
            catch (Exception ex) when (ex is not ThemeServiceException)
            {
                Debug.WriteLine($"Error writing machine registry value {valueName} to {keyPath}: {ex.Message}");
                throw new ThemeServiceException(
                    $"Failed to write machine registry value: {valueName}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        /// <summary>
        /// Gets a value from HKEY_LOCAL_MACHINE registry with type conversion and error handling
        /// </summary>
        public static T GetMachineValue<T>(string keyPath, string valueName, T defaultValue = default)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                if (key == null) return defaultValue;
                
                var value = key.GetValue(valueName);
                return value != null ? (T)Convert.ChangeType(value, typeof(T)) : defaultValue;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading machine registry value {valueName} from {keyPath}: {ex.Message}");
                throw new ThemeServiceException(
                    $"Failed to read machine registry value: {valueName}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }
    }
} 
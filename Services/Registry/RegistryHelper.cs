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
                var value = key?.GetValue(valueName);
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
                key.SetValue(valueName, value, valueKind);
            }
            catch (Exception ex)
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
        /// Sets a value in HKEY_LOCAL_MACHINE registry with error handling
        /// </summary>
        public static void SetMachineValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath, true) 
                    ?? Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath);
                key.SetValue(valueName, value, valueKind);
            }
            catch (Exception ex)
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
                var value = key?.GetValue(valueName);
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
using Microsoft.Win32;
using System;
using System.Threading.Tasks;

namespace ClearGlass.Services.Core
{
    public class RegistryService
    {
        public bool GetDWordValue(string keyPath, string valueName, bool defaultValue = false)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                var value = key?.GetValue(valueName);
                return value == null ? defaultValue : (int)value == 1;
            }
            catch (Exception ex)
            {
                LogError($"Error reading registry value {valueName} from {keyPath}", ex);
                return defaultValue;
            }
        }

        public void SetDWordValue(string keyPath, string valueName, bool value)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
                key?.SetValue(valueName, value ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                LogError($"Error setting registry value {valueName} in {keyPath}", ex);
                throw;
            }
        }

        public string? GetStringValue(string keyPath, string valueName)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                return key?.GetValue(valueName) as string;
            }
            catch (Exception ex)
            {
                LogError($"Error reading registry string value {valueName} from {keyPath}", ex);
                return null;
            }
        }

        public void SetStringValue(string keyPath, string valueName, string value)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
                key?.SetValue(valueName, value, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                LogError($"Error setting registry string value {valueName} in {keyPath}", ex);
                throw;
            }
        }

        public bool DeleteValue(string keyPath, string valueName)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
                if (key == null) return false;
                
                key.DeleteValue(valueName, false);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error deleting registry value {valueName} from {keyPath}", ex);
                return false;
            }
        }

        public bool DeleteKey(string keyPath)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error deleting registry key {keyPath}", ex);
                return false;
            }
        }

        private void LogError(string message, Exception ex)
        {
            // TODO: Replace with proper logging system
            System.Diagnostics.Debug.WriteLine($"{message}: {ex.Message}");
        }
    }
} 
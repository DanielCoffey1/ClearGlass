using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using ClearGlass.Services.Exceptions;
using ClearGlass.Services.Registry;
using Microsoft.Win32;

namespace ClearGlass.Services.Features
{
    /// <summary>
    /// Handles Windows 11 widget-related functionality
    /// </summary>
    internal class WidgetService
    {
        private bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private void KillWidgetsProcess()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c taskkill /f /im Widgets.exe /im WidgetService.exe 2>nul",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c sc config \"Windows Widgets Service\" start=disabled 2>nul && net stop \"Windows Widgets Service\" 2>nul",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error killing widgets processes: {ex.Message}");
            }
        }

        private void EnableWidgetsService()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c sc config \"Windows Widgets Service\" start=auto 2>nul && net start \"Windows Widgets Service\" 2>nul",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enabling widgets service: {ex.Message}");
            }
        }

        private bool GetWidgetsPolicyState()
        {
            try
            {
                return RegistryHelper.GetMachineValue<int>(RegistryHelper.WidgetsPolicyPath, "AllowWidgets", 1) != 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading widgets policy: {ex.Message}");
                return true; // Default to enabled if can't read policy
            }
        }

        private bool CheckWidgetsRegistryState()
        {
            return RegistryHelper.GetValue<int>(RegistryHelper.WidgetsPath, "WidgetsDisabled", 0) != 1;
        }

        private bool CheckFeedsState()
        {
            return RegistryHelper.GetValue<int>(RegistryHelper.FeedsPath, "ShellFeedsTaskbarViewMode", 1) != 0;
        }

        private bool CheckGroupPolicyState()
        {
            return RegistryHelper.GetMachineValue(RegistryHelper.WidgetsGPOPath, "EnableFeeds", 1) != 0;
        }

        private bool CheckWebWidgetsState()
        {
            return RegistryHelper.GetMachineValue(RegistryHelper.WebWidgetsPath, "AllowWebContentOnLockScreen", 1) != 0;
        }

        private void SetWidgetsPolicyState(bool enable)
        {
            try
            {
                RegistryHelper.SetMachineValue(RegistryHelper.WidgetsPolicyPath, "AllowWidgets", enable ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                throw new ThemeServiceException(
                    "Failed to set widgets policy",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        private void SetWidgetsRegistryState(bool enable)
        {
            RegistryHelper.SetValue(RegistryHelper.WidgetsPath, "WidgetsDisabled", enable ? 0 : 1, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegistryHelper.WidgetsPath, "ConfiguredByPolicy", enable ? 0 : 1, RegistryValueKind.DWord);
        }

        private void SetFeedsState(bool enable)
        {
            RegistryHelper.SetValue(RegistryHelper.FeedsPath, "ShellFeedsTaskbarViewMode", enable ? 1 : 0, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegistryHelper.FeedsPath, "IsFeedsAvailable", enable ? 1 : 0, RegistryValueKind.DWord);
        }

        private void SetGroupPolicyState(bool enable)
        {
            RegistryHelper.SetMachineValue(RegistryHelper.WidgetsGPOPath, "EnableFeeds", enable ? 1 : 0, RegistryValueKind.DWord);
        }

        private void SetWebWidgetsState(bool enable)
        {
            RegistryHelper.SetValue(RegistryHelper.WebWidgetsPath, "AllowWebContentOnLockScreen", enable ? 1 : 0, RegistryValueKind.DWord);
        }

        private void ShowWidgetsDisabledMessage()
        {
            MessageBox.Show(
                "Widgets have been disabled. If they still appear, you may need to:\n\n" +
                "1. Sign out and sign back in\n" +
                "2. Or restart your computer\n\n" +
                "This is sometimes necessary due to Windows 11's widget system.",
                "Action Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Gets or sets whether widgets are enabled
        /// </summary>
        public bool AreWidgetsEnabled
        {
            get
            {
                try
                {
                    return GetWidgetsPolicyState() &&
                           CheckWidgetsRegistryState() &&
                           CheckFeedsState() &&
                           CheckGroupPolicyState() &&
                           CheckWebWidgetsState();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading widgets state: {ex.Message}");
                    return true;
                }
            }
            set
            {
                if (!IsAdministrator())
                {
                    MessageBox.Show(
                        "This application requires administrator privileges to modify widgets settings.\n\nPlease right-click the application and select 'Run as administrator'.",
                        "Administrator Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    SetWidgetsPolicyState(value);
                    SetWidgetsRegistryState(value);
                    SetFeedsState(value);
                    SetGroupPolicyState(value);
                    SetWebWidgetsState(value);

                    if (!value)
                    {
                        KillWidgetsProcess();
                    }
                    else
                    {
                        EnableWidgetsService();
                    }
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error modifying widgets settings: {ex.Message}",
                        ThemeServiceOperation.RegistryAccess,
                        ex);
                }
            }
        }
    }
} 
using System;
using System.Diagnostics;
using ClearGlass.Services.Exceptions;
using ClearGlass.Services.Registry;
using Microsoft.Win32;

namespace ClearGlass.Services.Features
{
    /// <summary>
    /// Handles Windows taskbar-related functionality
    /// </summary>
    internal class TaskbarService
    {
        /// <summary>
        /// Restarts the Windows Explorer process
        /// </summary>
        public void RestartExplorer()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c taskkill /f /im explorer.exe && start explorer.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                throw new ThemeServiceException(
                    "Failed to restart Explorer",
                    ThemeServiceOperation.ProcessManagement,
                    ex);
            }
        }

        /// <summary>
        /// Gets or sets whether the taskbar is centered
        /// </summary>
        public bool IsTaskbarCentered
        {
            get
            {
                try
                {
                    return RegistryHelper.GetValue<int>(RegistryHelper.TaskbarSettingsPath, "TaskbarAl", 1) == 1;
                }
                catch (ThemeServiceException)
                {
                    return true; // Default value if registry access fails
                }
            }
            set
            {
                try
                {
                    RegistryHelper.SetValue(RegistryHelper.TaskbarSettingsPath, "TaskbarAl", value ? 1 : 0, RegistryValueKind.DWord);
                    RestartExplorer();
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error setting taskbar alignment: {ex.Message}",
                        ThemeServiceOperation.RegistryAccess,
                        ex);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the task view button is visible
        /// </summary>
        public bool IsTaskViewEnabled
        {
            get
            {
                try
                {
                    return RegistryHelper.GetValue<int>(RegistryHelper.TaskbarSettingsPath, "ShowTaskViewButton", 1) == 1;
                }
                catch (ThemeServiceException)
                {
                    return true;
                }
            }
            set
            {
                try
                {
                    RegistryHelper.SetValue(RegistryHelper.TaskbarSettingsPath, "ShowTaskViewButton", value ? 1 : 0, RegistryValueKind.DWord);
                    RestartExplorer();
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error setting task view: {ex.Message}",
                        ThemeServiceOperation.RegistryAccess,
                        ex);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the search box is visible
        /// </summary>
        public bool IsSearchVisible
        {
            get
            {
                try
                {
                    return RegistryHelper.GetValue<int>(RegistryHelper.SearchSettingsPath, "SearchboxTaskbarMode", 1) != 0;
                }
                catch (ThemeServiceException)
                {
                    return true;
                }
            }
            set
            {
                try
                {
                    RegistryHelper.SetValue(RegistryHelper.SearchSettingsPath, "SearchboxTaskbarMode", value ? 1 : 0, RegistryValueKind.DWord);
                    RestartExplorer();
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error setting search visibility: {ex.Message}",
                        ThemeServiceOperation.RegistryAccess,
                        ex);
                }
            }
        }
    }
} 
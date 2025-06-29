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
        private bool _restartPending = false;
        private bool _taskbarCentered;
        private bool _taskViewEnabled;
        private bool _searchVisible;

        public TaskbarService()
        {
            // Initialize current values
            _taskbarCentered = GetTaskbarCentered();
            _taskViewEnabled = GetTaskViewEnabled();
            _searchVisible = GetSearchVisible();
        }

        /// <summary>
        /// Restarts the Windows Explorer process
        /// </summary>
        private void RestartExplorer()
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
                _restartPending = false;
            }
            catch (Exception ex)
            {
                throw new ThemeServiceException(
                    "Failed to restart Explorer",
                    ThemeServiceOperation.ProcessManagement,
                    ex);
            }
        }

        private bool GetTaskbarCentered()
        {
            try
            {
                return RegistryHelper.GetValue<int>(RegistryHelper.TaskbarSettingsPath, "TaskbarAl", 1) == 1;
            }
            catch (ThemeServiceException)
            {
                return true;
            }
        }

        private bool GetTaskViewEnabled()
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

        private bool GetSearchVisible()
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

        /// <summary>
        /// Gets whether the taskbar is centered
        /// </summary>
        public bool IsTaskbarCentered => _taskbarCentered;

        /// <summary>
        /// Gets whether the task view button is visible
        /// </summary>
        public bool IsTaskViewEnabled => _taskViewEnabled;

        /// <summary>
        /// Gets whether the search box is visible
        /// </summary>
        public bool IsSearchVisible => _searchVisible;

        /// <summary>
        /// Applies multiple taskbar settings at once
        /// </summary>
        public void ApplySettings(bool? isTaskbarCentered = null, bool? isTaskViewEnabled = null, bool? isSearchVisible = null, bool applyImmediately = false)
        {
            bool changed = false;

            try
            {
                if (isTaskbarCentered.HasValue && isTaskbarCentered.Value != _taskbarCentered)
                {
                    RegistryHelper.SetValue(RegistryHelper.TaskbarSettingsPath, "TaskbarAl", isTaskbarCentered.Value ? 1 : 0, RegistryValueKind.DWord);
                    _taskbarCentered = isTaskbarCentered.Value;
                    changed = true;
                }
                
                if (isTaskViewEnabled.HasValue && isTaskViewEnabled.Value != _taskViewEnabled)
                {
                    RegistryHelper.SetValue(RegistryHelper.TaskbarSettingsPath, "ShowTaskViewButton", isTaskViewEnabled.Value ? 1 : 0, RegistryValueKind.DWord);
                    _taskViewEnabled = isTaskViewEnabled.Value;
                    changed = true;
                }
                
                if (isSearchVisible.HasValue && isSearchVisible.Value != _searchVisible)
                {
                    RegistryHelper.SetValue(RegistryHelper.SearchSettingsPath, "SearchboxTaskbarMode", isSearchVisible.Value ? 1 : 0, RegistryValueKind.DWord);
                    _searchVisible = isSearchVisible.Value;
                    changed = true;
                }

                if (changed)
                {
                    _restartPending = true;
                    if (applyImmediately)
                    {
                        ApplyPendingChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ThemeServiceException(
                    $"Error applying taskbar settings: {ex.Message}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        /// <summary>
        /// Checks if changes are pending that require an Explorer restart
        /// </summary>
        public bool HasPendingChanges => _restartPending;

        /// <summary>
        /// Applies any pending changes by restarting Explorer if necessary
        /// </summary>
        public void ApplyPendingChanges()
        {
            if (_restartPending)
            {
                RestartExplorer();
            }
        }
    }
} 
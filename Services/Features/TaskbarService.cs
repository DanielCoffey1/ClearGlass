using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClearGlass.Services.Exceptions;
using ClearGlass.Services.Native;
using ClearGlass.Services.Registry;
using ClearGlass.Services.Reliability;
using Microsoft.Win32;

namespace ClearGlass.Services.Features
{
    /// <summary>
    /// Handles Windows taskbar-related functionality with reliability features
    /// </summary>
    internal class TaskbarService
    {
        private const string TaskbarWindowClass = "Shell_TrayWnd";
        private const string DesktopWindowClass = "Progman";
        private const int DefaultTimeoutMs = 30000;
        private const int MaxRetries = 3;

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
        /// Restarts the Windows Explorer process and waits for stabilization
        /// </summary>
        private async Task RestartExplorerAsync(CancellationToken cancellationToken = default)
        {
            bool explorerKilled = false;

            try
            {
                Debug.WriteLine("Restarting Explorer...");

                // Kill all explorer.exe processes
                var explorerProcesses = Process.GetProcessesByName("explorer");
                if (explorerProcesses.Length > 0)
                {
                    foreach (var process in explorerProcesses)
                    {
                        try
                        {
                            process.Kill();
                            explorerKilled = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to kill explorer process {process.Id}: {ex.Message}");
                        }
                    }

                    // Wait for explorer processes to exit
                    var waitStart = DateTime.UtcNow;
                    while (Process.GetProcessesByName("explorer").Length > 0)
                    {
                        if (DateTime.UtcNow - waitStart > TimeSpan.FromSeconds(10))
                        {
                            Debug.WriteLine("Timeout waiting for explorer to exit, continuing anyway");
                            break;
                        }
                        await Task.Delay(100, cancellationToken);
                    }
                }

                // Start a new explorer process
                await StartExplorerAsync();

                // Wait for Explorer to stabilize
                await WaitForExplorerStabilizationAsync(cancellationToken);

                _restartPending = false;
                Debug.WriteLine("Explorer restarted successfully");
            }
            catch (OperationCanceledException)
            {
                // Even on cancellation, ensure Explorer is running
                await EnsureExplorerRunningAsync();
                throw;
            }
            catch (Exception ex)
            {
                // Ensure Explorer is running even if something failed
                await EnsureExplorerRunningAsync();
                throw new ThemeServiceException(
                    "Failed to restart Explorer",
                    ThemeServiceOperation.ProcessManagement,
                    ex);
            }
        }

        /// <summary>
        /// Starts Explorer process
        /// </summary>
        private async Task StartExplorerAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                });
                await Task.Delay(500); // Give it time to start
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start explorer: {ex.Message}");
                // Try alternative method
                try
                {
                    Process.Start("explorer.exe");
                    await Task.Delay(500);
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"Alternative explorer start also failed: {ex2.Message}");
                }
            }
        }

        /// <summary>
        /// Ensures Explorer is running, starts it if not
        /// </summary>
        private async Task EnsureExplorerRunningAsync()
        {
            try
            {
                // Check if Explorer is running
                if (Process.GetProcessesByName("explorer").Length == 0)
                {
                    Debug.WriteLine("Explorer not running, starting it...");
                    await StartExplorerAsync();

                    // Wait a bit for it to initialize
                    await Task.Delay(1000);

                    // Verify it started
                    if (Process.GetProcessesByName("explorer").Length == 0)
                    {
                        Debug.WriteLine("Explorer still not running after start attempt");
                        // Last resort - try via cmd
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = "/c start explorer.exe",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"CMD start explorer failed: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnsureExplorerRunning failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Waits for Explorer to fully stabilize (taskbar and desktop are responsive)
        /// </summary>
        private async Task WaitForExplorerStabilizationAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            bool taskbarFound = false;
            bool desktopFound = false;

            while (stopwatch.ElapsedMilliseconds < DefaultTimeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check for responsive taskbar
                if (!taskbarFound)
                {
                    var taskbarHwnd = WindowsApi.FindResponsiveWindow(TaskbarWindowClass, 500);
                    if (taskbarHwnd != IntPtr.Zero)
                    {
                        taskbarFound = true;
                        Debug.WriteLine($"Taskbar found after {stopwatch.ElapsedMilliseconds}ms");
                    }
                }

                // Check for responsive desktop
                if (!desktopFound)
                {
                    var desktopHwnd = WindowsApi.FindResponsiveWindow(DesktopWindowClass, 500);
                    if (desktopHwnd != IntPtr.Zero)
                    {
                        desktopFound = true;
                        Debug.WriteLine($"Desktop found after {stopwatch.ElapsedMilliseconds}ms");
                    }
                }

                // Both found and responsive
                if (taskbarFound && desktopFound)
                {
                    // Additional stabilization delay
                    await Task.Delay(500, cancellationToken);
                    Debug.WriteLine($"Explorer stabilized after {stopwatch.ElapsedMilliseconds}ms total");
                    return;
                }

                await Task.Delay(200, cancellationToken);
            }

            // Timeout - log but don't throw
            Debug.WriteLine($"Explorer stabilization timed out after {DefaultTimeoutMs}ms. Taskbar: {taskbarFound}, Desktop: {desktopFound}");
        }

        /// <summary>
        /// Legacy synchronous restart method for backward compatibility
        /// </summary>
        private void RestartExplorer()
        {
            try
            {
                RestartExplorerAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RestartExplorer failed: {ex.Message}");
                // Ensure Explorer is running even on failure
                EnsureExplorerRunningAsync().GetAwaiter().GetResult();
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
        /// Verifies that the taskbar alignment matches expected state
        /// </summary>
        public bool VerifyTaskbarCentered(bool expected)
        {
            var actual = GetTaskbarCentered();
            return actual == expected;
        }

        /// <summary>
        /// Verifies that the task view button state matches expected state
        /// </summary>
        public bool VerifyTaskViewEnabled(bool expected)
        {
            var actual = GetTaskViewEnabled();
            return actual == expected;
        }

        /// <summary>
        /// Verifies that the search visibility matches expected state
        /// </summary>
        public bool VerifySearchVisible(bool expected)
        {
            var actual = GetSearchVisible();
            return actual == expected;
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
        /// Applies multiple taskbar settings at once with optional verification
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
        /// Applies taskbar settings with retry and verification
        /// </summary>
        public async Task<OperationResult> ApplySettingsWithRetryAsync(
            bool? isTaskbarCentered = null,
            bool? isTaskViewEnabled = null,
            bool? isSearchVisible = null,
            CancellationToken cancellationToken = default)
        {
            var failedSettings = new List<string>();

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                failedSettings.Clear();

                try
                {
                    // Re-read current state from registry at start of each attempt
                    // This ensures we don't rely on stale cached values
                    var currentTaskbarCentered = GetTaskbarCentered();
                    var currentTaskViewEnabled = GetTaskViewEnabled();
                    var currentSearchVisible = GetSearchVisible();

                    bool anyChanged = false;

                    // Apply taskbar centered setting with verified write
                    if (isTaskbarCentered.HasValue)
                    {
                        if (currentTaskbarCentered != isTaskbarCentered.Value)
                        {
                            if (RegistryHelper.SetValueVerified(RegistryHelper.TaskbarSettingsPath, "TaskbarAl",
                                isTaskbarCentered.Value ? 1 : 0, RegistryValueKind.DWord))
                            {
                                _taskbarCentered = isTaskbarCentered.Value;
                                anyChanged = true;
                            }
                            else
                            {
                                failedSettings.Add("TaskbarAl");
                            }
                        }
                        else
                        {
                            _taskbarCentered = isTaskbarCentered.Value;
                        }
                    }

                    // Apply task view setting with verified write
                    if (isTaskViewEnabled.HasValue)
                    {
                        if (currentTaskViewEnabled != isTaskViewEnabled.Value)
                        {
                            if (RegistryHelper.SetValueVerified(RegistryHelper.TaskbarSettingsPath, "ShowTaskViewButton",
                                isTaskViewEnabled.Value ? 1 : 0, RegistryValueKind.DWord))
                            {
                                _taskViewEnabled = isTaskViewEnabled.Value;
                                anyChanged = true;
                            }
                            else
                            {
                                failedSettings.Add("ShowTaskViewButton");
                            }
                        }
                        else
                        {
                            _taskViewEnabled = isTaskViewEnabled.Value;
                        }
                    }

                    // Apply search visibility setting with verified write
                    if (isSearchVisible.HasValue)
                    {
                        if (currentSearchVisible != isSearchVisible.Value)
                        {
                            if (RegistryHelper.SetValueVerified(RegistryHelper.SearchSettingsPath, "SearchboxTaskbarMode",
                                isSearchVisible.Value ? 1 : 0, RegistryValueKind.DWord))
                            {
                                _searchVisible = isSearchVisible.Value;
                                anyChanged = true;
                            }
                            else
                            {
                                failedSettings.Add("SearchboxTaskbarMode");
                            }
                        }
                        else
                        {
                            _searchVisible = isSearchVisible.Value;
                        }
                    }

                    if (anyChanged)
                    {
                        _restartPending = true;
                    }

                    // Small delay to let registry settle
                    await Task.Delay(100, cancellationToken);

                    // Verify all settings match expected values
                    bool verified = true;
                    if (isTaskbarCentered.HasValue && !VerifyTaskbarCentered(isTaskbarCentered.Value))
                    {
                        verified = false;
                        if (!failedSettings.Contains("TaskbarAl"))
                            failedSettings.Add("TaskbarAl (verify)");
                    }
                    if (isTaskViewEnabled.HasValue && !VerifyTaskViewEnabled(isTaskViewEnabled.Value))
                    {
                        verified = false;
                        if (!failedSettings.Contains("ShowTaskViewButton"))
                            failedSettings.Add("ShowTaskViewButton (verify)");
                    }
                    if (isSearchVisible.HasValue && !VerifySearchVisible(isSearchVisible.Value))
                    {
                        verified = false;
                        if (!failedSettings.Contains("SearchboxTaskbarMode"))
                            failedSettings.Add("SearchboxTaskbarMode (verify)");
                    }

                    if (verified)
                    {
                        return OperationResult.Succeeded("TaskbarSettings", attempt, "All settings verified");
                    }

                    Debug.WriteLine($"Taskbar verification failed on attempt {attempt}: {string.Join(", ", failedSettings)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Taskbar settings attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        return OperationResult.Failed("TaskbarSettings", attempt, ex);
                    }
                }

                // Exponential backoff
                await Task.Delay(500 * (int)Math.Pow(2, attempt - 1), cancellationToken);
            }

            return OperationResult.Failed("TaskbarSettings", MaxRetries, null,
                $"Verification failed: {string.Join(", ", failedSettings)}");
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

        /// <summary>
        /// Applies pending changes asynchronously with Explorer restart and stabilization wait
        /// </summary>
        public async Task<OperationResult> ApplyPendingChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_restartPending)
            {
                return OperationResult.Succeeded("ExplorerRestart", 1, "No restart needed");
            }

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await RestartExplorerAsync(cancellationToken);

                    // Verify taskbar and desktop are accessible
                    var taskbarHwnd = WindowsApi.FindResponsiveWindow(TaskbarWindowClass, 2000);
                    var desktopHwnd = WindowsApi.FindResponsiveWindow(DesktopWindowClass, 2000);

                    if (taskbarHwnd != IntPtr.Zero && desktopHwnd != IntPtr.Zero)
                    {
                        return OperationResult.Succeeded("ExplorerRestart", attempt, "Explorer restarted and responsive");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Explorer restart attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        return OperationResult.Failed("ExplorerRestart", attempt, ex);
                    }
                }

                // Exponential backoff
                await Task.Delay(1000 * (int)Math.Pow(2, attempt - 1), cancellationToken);
            }

            return OperationResult.Failed("ExplorerRestart", MaxRetries, null, "Explorer not responsive after retries");
        }
    }
}

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
        private readonly LoggingService _log;

        public TaskbarService()
        {
            _log = LoggingService.Instance;
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
            var stopwatch = Stopwatch.StartNew();
            _log.LogSubsection("Restarting Windows Explorer");

            try
            {
                // Kill all explorer.exe processes
                var explorerProcesses = Process.GetProcessesByName("explorer");
                _log.LogProcess("FOUND", "explorer.exe", $"{explorerProcesses.Length} process(es) running");

                if (explorerProcesses.Length > 0)
                {
                    _log.LogDetail("Terminating Explorer processes...");
                    foreach (var process in explorerProcesses)
                    {
                        try
                        {
                            _log.LogProcess("KILL", "explorer.exe", $"PID {process.Id}");
                            process.Kill();
                        }
                        catch (Exception ex)
                        {
                            _log.LogFailure($"Failed to kill explorer process {process.Id}: {ex.Message}");
                        }
                    }

                    // Wait for explorer processes to exit
                    _log.LogWaiting("Waiting for Explorer processes to exit...");
                    var waitStart = DateTime.UtcNow;
                    while (Process.GetProcessesByName("explorer").Length > 0)
                    {
                        if (DateTime.UtcNow - waitStart > TimeSpan.FromSeconds(10))
                        {
                            _log.LogWarning("Timeout waiting for Explorer to exit - continuing anyway");
                            break;
                        }
                        await Task.Delay(100, cancellationToken);
                    }
                    _log.LogSuccess("Explorer processes terminated");
                }

                // Start a new explorer process
                _log.LogDetail("Starting new Explorer process...");
                await StartExplorerAsync();

                // Wait for Explorer to stabilize
                await WaitForExplorerStabilizationAsync(cancellationToken);

                _restartPending = false;
                stopwatch.Stop();
                _log.LogSuccess($"Explorer restarted successfully in {stopwatch.Elapsed.TotalSeconds:F2}s");
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
            _log.LogWaiting("Waiting for Explorer to stabilize...");
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
                        _log.LogWinApi("FindWindow", $"Taskbar (Shell_TrayWnd) found and responsive after {stopwatch.ElapsedMilliseconds}ms");
                    }
                }

                // Check for responsive desktop
                if (!desktopFound)
                {
                    var desktopHwnd = WindowsApi.FindResponsiveWindow(DesktopWindowClass, 500);
                    if (desktopHwnd != IntPtr.Zero)
                    {
                        desktopFound = true;
                        _log.LogWinApi("FindWindow", $"Desktop (Progman) found and responsive after {stopwatch.ElapsedMilliseconds}ms");
                    }
                }

                // Both found and responsive
                if (taskbarFound && desktopFound)
                {
                    // Additional stabilization delay
                    _log.LogDetail("Both taskbar and desktop responsive - allowing additional stabilization time...");
                    await Task.Delay(500, cancellationToken);
                    _log.LogSuccess($"Explorer fully stabilized in {stopwatch.ElapsedMilliseconds}ms");
                    return;
                }

                await Task.Delay(200, cancellationToken);
            }

            // Timeout - log but don't throw
            _log.LogWarning($"Explorer stabilization timed out after {DefaultTimeoutMs / 1000}s (Taskbar: {taskbarFound}, Desktop: {desktopFound})");
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
            _log.LogSubsection("Configuring Taskbar Settings");
            var failedSettings = new List<string>();

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                failedSettings.Clear();

                if (attempt > 1)
                {
                    _log.LogRetry(attempt, MaxRetries, "Applying taskbar settings");
                }

                try
                {
                    // Re-read current state from registry at start of each attempt
                    _log.LogDetail("Reading current taskbar state from registry...");
                    var currentTaskbarCentered = GetTaskbarCentered();
                    var currentTaskViewEnabled = GetTaskViewEnabled();
                    var currentSearchVisible = GetSearchVisible();

                    _log.LogDebugDetail($"Current state - Centered: {currentTaskbarCentered}, TaskView: {currentTaskViewEnabled}, Search: {currentSearchVisible}");

                    bool anyChanged = false;

                    // Apply taskbar centered setting with verified write
                    if (isTaskbarCentered.HasValue)
                    {
                        if (currentTaskbarCentered != isTaskbarCentered.Value)
                        {
                            _log.LogDetail($"Setting taskbar alignment to: {(isTaskbarCentered.Value ? "Center" : "Left")}");
                            if (RegistryHelper.SetValueVerified(RegistryHelper.TaskbarSettingsPath, "TaskbarAl",
                                isTaskbarCentered.Value ? 1 : 0, RegistryValueKind.DWord))
                            {
                                _log.LogRegistry("SET", "TaskbarSettings", "TaskbarAl", isTaskbarCentered.Value ? 1 : 0);
                                _taskbarCentered = isTaskbarCentered.Value;
                                anyChanged = true;
                            }
                            else
                            {
                                _log.LogFailure("Failed to set taskbar alignment");
                                failedSettings.Add("TaskbarAl");
                            }
                        }
                        else
                        {
                            _log.LogSuccess("Taskbar alignment already set correctly - skipping");
                            _taskbarCentered = isTaskbarCentered.Value;
                        }
                    }

                    // Apply task view setting with verified write
                    if (isTaskViewEnabled.HasValue)
                    {
                        if (currentTaskViewEnabled != isTaskViewEnabled.Value)
                        {
                            _log.LogDetail($"Setting Task View button to: {(isTaskViewEnabled.Value ? "Visible" : "Hidden")}");
                            if (RegistryHelper.SetValueVerified(RegistryHelper.TaskbarSettingsPath, "ShowTaskViewButton",
                                isTaskViewEnabled.Value ? 1 : 0, RegistryValueKind.DWord))
                            {
                                _log.LogRegistry("SET", "TaskbarSettings", "ShowTaskViewButton", isTaskViewEnabled.Value ? 1 : 0);
                                _taskViewEnabled = isTaskViewEnabled.Value;
                                anyChanged = true;
                            }
                            else
                            {
                                _log.LogFailure("Failed to set Task View button visibility");
                                failedSettings.Add("ShowTaskViewButton");
                            }
                        }
                        else
                        {
                            _log.LogSuccess("Task View button already set correctly - skipping");
                            _taskViewEnabled = isTaskViewEnabled.Value;
                        }
                    }

                    // Apply search visibility setting with verified write
                    if (isSearchVisible.HasValue)
                    {
                        if (currentSearchVisible != isSearchVisible.Value)
                        {
                            _log.LogDetail($"Setting Search box to: {(isSearchVisible.Value ? "Visible" : "Hidden")}");
                            if (RegistryHelper.SetValueVerified(RegistryHelper.SearchSettingsPath, "SearchboxTaskbarMode",
                                isSearchVisible.Value ? 1 : 0, RegistryValueKind.DWord))
                            {
                                _log.LogRegistry("SET", "SearchSettings", "SearchboxTaskbarMode", isSearchVisible.Value ? 1 : 0);
                                _searchVisible = isSearchVisible.Value;
                                anyChanged = true;
                            }
                            else
                            {
                                _log.LogFailure("Failed to set Search box visibility");
                                failedSettings.Add("SearchboxTaskbarMode");
                            }
                        }
                        else
                        {
                            _log.LogSuccess("Search box already set correctly - skipping");
                            _searchVisible = isSearchVisible.Value;
                        }
                    }

                    if (anyChanged)
                    {
                        _restartPending = true;
                        _log.LogDetail("Changes detected - Explorer restart will be required");
                    }

                    // Small delay to let registry settle
                    await Task.Delay(100, cancellationToken);

                    // Verify all settings match expected values
                    _log.LogDetail("Verifying registry values were written correctly...");
                    bool verified = true;
                    if (isTaskbarCentered.HasValue && !VerifyTaskbarCentered(isTaskbarCentered.Value))
                    {
                        verified = false;
                        _log.LogFailure("Taskbar alignment verification failed");
                        if (!failedSettings.Contains("TaskbarAl"))
                            failedSettings.Add("TaskbarAl (verify)");
                    }
                    if (isTaskViewEnabled.HasValue && !VerifyTaskViewEnabled(isTaskViewEnabled.Value))
                    {
                        verified = false;
                        _log.LogFailure("Task View button verification failed");
                        if (!failedSettings.Contains("ShowTaskViewButton"))
                            failedSettings.Add("ShowTaskViewButton (verify)");
                    }
                    if (isSearchVisible.HasValue && !VerifySearchVisible(isSearchVisible.Value))
                    {
                        verified = false;
                        _log.LogFailure("Search box verification failed");
                        if (!failedSettings.Contains("SearchboxTaskbarMode"))
                            failedSettings.Add("SearchboxTaskbarMode (verify)");
                    }

                    if (verified)
                    {
                        _log.LogSuccess($"All taskbar settings verified successfully (attempt {attempt})");
                        return OperationResult.Succeeded("TaskbarSettings", attempt, "All settings verified");
                    }

                    _log.LogFailure($"Verification failed for: {string.Join(", ", failedSettings)}");
                }
                catch (Exception ex)
                {
                    _log.LogFailure($"Attempt {attempt} failed with exception: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        _log.LogError($"Taskbar settings failed after {MaxRetries} attempts", ex);
                        return OperationResult.Failed("TaskbarSettings", attempt, ex);
                    }
                }

                // Exponential backoff
                var backoffMs = 500 * (int)Math.Pow(2, attempt - 1);
                _log.LogWaiting($"Waiting {backoffMs}ms before retry...");
                await Task.Delay(backoffMs, cancellationToken);
            }

            _log.LogFailure($"Taskbar settings verification failed after {MaxRetries} retries");
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
                _log.LogDetail("No pending changes - Explorer restart not required");
                return OperationResult.Succeeded("ExplorerRestart", 1, "No restart needed");
            }

            _log.LogDetail("Pending changes detected - initiating Explorer restart sequence");

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 1)
                {
                    _log.LogRetry(attempt, MaxRetries, "Explorer restart");
                }

                try
                {
                    await RestartExplorerAsync(cancellationToken);

                    // Verify taskbar and desktop are accessible
                    _log.LogDetail("Verifying Explorer components are responsive...");
                    var taskbarHwnd = WindowsApi.FindResponsiveWindow(TaskbarWindowClass, 2000);
                    var desktopHwnd = WindowsApi.FindResponsiveWindow(DesktopWindowClass, 2000);

                    if (taskbarHwnd != IntPtr.Zero && desktopHwnd != IntPtr.Zero)
                    {
                        _log.LogSuccess($"Explorer restart completed successfully (attempt {attempt})");
                        return OperationResult.Succeeded("ExplorerRestart", attempt, "Explorer restarted and responsive");
                    }

                    _log.LogFailure($"Explorer components not fully responsive (Taskbar: {taskbarHwnd != IntPtr.Zero}, Desktop: {desktopHwnd != IntPtr.Zero})");
                }
                catch (OperationCanceledException)
                {
                    _log.LogWarning("Explorer restart was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogFailure($"Explorer restart attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        _log.LogError($"Explorer restart failed after {MaxRetries} attempts", ex);
                        return OperationResult.Failed("ExplorerRestart", attempt, ex);
                    }
                }

                // Exponential backoff
                var backoffMs = 1000 * (int)Math.Pow(2, attempt - 1);
                _log.LogWaiting($"Waiting {backoffMs}ms before retry...");
                await Task.Delay(backoffMs, cancellationToken);
            }

            _log.LogFailure($"Explorer not responsive after {MaxRetries} restart attempts");
            return OperationResult.Failed("ExplorerRestart", MaxRetries, null, "Explorer not responsive after retries");
        }
    }
}

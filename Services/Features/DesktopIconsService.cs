using System;
using System.Diagnostics;
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
    /// Handles Windows desktop icons functionality with reliability features
    /// </summary>
    internal class DesktopIconsService
    {
        private const int MaxRetries = 3;
        private const int HandleRetryDelayMs = 200;
        private readonly LoggingService _log;

        public DesktopIconsService()
        {
            _log = LoggingService.Instance;
        }

        /// <summary>
        /// Gets the desktop list view handle using the standard Progman approach
        /// </summary>
        private IntPtr GetDesktopListViewHandle()
        {
            return WindowsApi.FindWindowEx(
                WindowsApi.FindWindowEx(
                    WindowsApi.FindWindow("Progman", null),
                    IntPtr.Zero,
                    "SHELLDLL_DefView",
                    null),
                IntPtr.Zero,
                "SysListView32",
                "FolderView");
        }

        /// <summary>
        /// Gets the desktop handle via WorkerW (for Windows 10+ with animated wallpapers)
        /// </summary>
        private IntPtr GetDesktopHandleViaWorkerW()
        {
            IntPtr defViewHandle = IntPtr.Zero;

            // Enumerate all top-level windows to find WorkerW with SHELLDLL_DefView
            WindowsApi.EnumWindows((hwnd, lParam) =>
            {
                var className = WindowsApi.GetWindowClassName(hwnd);
                if (className == "WorkerW")
                {
                    var shelldll = WindowsApi.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shelldll != IntPtr.Zero)
                    {
                        defViewHandle = shelldll;
                        return false; // Stop enumeration
                    }
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            if (defViewHandle != IntPtr.Zero)
            {
                return WindowsApi.FindWindowEx(defViewHandle, IntPtr.Zero, "SysListView32", "FolderView");
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the desktop list view handle with retry logic, trying multiple approaches
        /// </summary>
        private IntPtr GetDesktopListViewHandleWithRetry(int maxAttempts = 5)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // First try the standard Progman approach
                var handle = GetDesktopListViewHandle();
                if (handle != IntPtr.Zero && WindowsApi.IsWindow(handle))
                {
                    Debug.WriteLine($"Desktop handle found via Progman on attempt {attempt}");
                    return handle;
                }

                // Try the WorkerW approach (for animated wallpapers)
                handle = GetDesktopHandleViaWorkerW();
                if (handle != IntPtr.Zero && WindowsApi.IsWindow(handle))
                {
                    Debug.WriteLine($"Desktop handle found via WorkerW on attempt {attempt}");
                    return handle;
                }

                if (attempt < maxAttempts)
                {
                    Thread.Sleep(HandleRetryDelayMs * attempt);
                }
            }

            Debug.WriteLine($"Failed to get desktop handle after {maxAttempts} attempts");
            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the desktop list view handle asynchronously with retry logic
        /// </summary>
        private async Task<IntPtr> GetDesktopListViewHandleWithRetryAsync(int maxAttempts = 5, CancellationToken cancellationToken = default)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // First try the standard Progman approach
                var handle = GetDesktopListViewHandle();
                if (handle != IntPtr.Zero && WindowsApi.IsWindow(handle))
                {
                    Debug.WriteLine($"Desktop handle found via Progman on attempt {attempt}");
                    return handle;
                }

                // Try the WorkerW approach (for animated wallpapers)
                handle = GetDesktopHandleViaWorkerW();
                if (handle != IntPtr.Zero && WindowsApi.IsWindow(handle))
                {
                    Debug.WriteLine($"Desktop handle found via WorkerW on attempt {attempt}");
                    return handle;
                }

                if (attempt < maxAttempts)
                {
                    await Task.Delay(HandleRetryDelayMs * attempt, cancellationToken);
                }
            }

            Debug.WriteLine($"Failed to get desktop handle after {maxAttempts} attempts");
            return IntPtr.Zero;
        }

        private bool GetDesktopIconsRegistryState()
        {
            return RegistryHelper.GetValue<int>(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideIcons", 0) == 0;
        }

        private void SetDesktopIconsRegistryState(bool show)
        {
            RegistryHelper.SetValue(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "HideIcons",
                show ? 0 : 1,
                RegistryValueKind.DWord);
        }

        private bool SetDesktopIconsRegistryStateVerified(bool show)
        {
            return RegistryHelper.SetValueVerified(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "HideIcons",
                show ? 0 : 1,
                RegistryValueKind.DWord);
        }

        private void SetDesktopIconsVisibility(IntPtr handle, bool show)
        {
            if (handle != IntPtr.Zero)
            {
                if (show && !WindowsApi.IsWindowVisible(handle))
                {
                    WindowsApi.ShowWindow(handle, WindowsApi.SW_SHOW);
                }
                else if (!show && WindowsApi.IsWindowVisible(handle))
                {
                    WindowsApi.ShowWindow(handle, WindowsApi.SW_HIDE);
                }
            }
        }

        /// <summary>
        /// Verifies that the desktop icons state matches the expected value
        /// </summary>
        public bool VerifyDesktopIconsState(bool expectedVisible)
        {
            // Check registry state
            bool registryState = GetDesktopIconsRegistryState();
            if (registryState != expectedVisible)
            {
                return false;
            }

            // Check actual window visibility
            var handle = GetDesktopListViewHandleWithRetry(2);
            if (handle != IntPtr.Zero)
            {
                bool windowVisible = WindowsApi.IsWindowVisible(handle);
                return windowVisible == expectedVisible;
            }

            // If we can't get the handle, trust the registry state
            return registryState == expectedVisible;
        }

        /// <summary>
        /// Gets or sets whether desktop icons are visible
        /// </summary>
        public bool AreDesktopIconsVisible
        {
            get
            {
                try
                {
                    // First check registry for persisted state
                    bool registryState = GetDesktopIconsRegistryState();

                    // Fallback to checking window state
                    var toggleHandle = GetDesktopListViewHandleWithRetry(2);
                    return toggleHandle != IntPtr.Zero ? WindowsApi.IsWindowVisible(toggleHandle) : registryState;
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error getting desktop icons state: {ex.Message}",
                        ThemeServiceOperation.WindowsApi,
                        ex);
                }
            }
            set
            {
                try
                {
                    SetDesktopIconsRegistryState(value);
                    var toggleHandle = GetDesktopListViewHandleWithRetry();
                    SetDesktopIconsVisibility(toggleHandle, value);
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error toggling desktop icons: {ex.Message}",
                        ThemeServiceOperation.WindowsApi,
                        ex);
                }
            }
        }

        /// <summary>
        /// Sets desktop icons visibility with retry and verification
        /// </summary>
        public async Task<OperationResult> SetDesktopIconsVisibleAsync(bool visible, CancellationToken cancellationToken = default)
        {
            var targetState = visible ? "visible" : "hidden";
            _log.LogSubsection($"Setting Desktop Icons to {targetState}");

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 1)
                {
                    _log.LogRetry(attempt, MaxRetries, $"Setting desktop icons {targetState}");
                }

                try
                {
                    // Set registry state with verification
                    _log.LogDetail($"Writing registry value (HideIcons = {(visible ? 0 : 1)})...");
                    if (!SetDesktopIconsRegistryStateVerified(visible))
                    {
                        _log.LogFailure($"Registry write verification failed on attempt {attempt}");
                        if (attempt == MaxRetries)
                        {
                            _log.LogError("Failed to write desktop icons registry value after all attempts");
                            return OperationResult.Failed("DesktopIcons", attempt, null, "Registry write verification failed");
                        }
                        await Task.Delay(500 * (int)Math.Pow(2, attempt - 1), cancellationToken);
                        continue;
                    }
                    _log.LogRegistry("SET", "Explorer\\Advanced", "HideIcons", visible ? 0 : 1);

                    // Get handle with retry
                    _log.LogDetail("Locating desktop ListView window handle...");
                    var handle = await GetDesktopListViewHandleWithRetryAsync(3, cancellationToken);
                    if (handle != IntPtr.Zero)
                    {
                        _log.LogWinApi("FindWindow", $"Desktop ListView handle found: 0x{handle.ToInt64():X}");

                        _log.LogDetail($"Calling ShowWindow to {(visible ? "show" : "hide")} icons...");
                        SetDesktopIconsVisibility(handle, visible);

                        // Small delay before verification
                        await Task.Delay(100, cancellationToken);

                        // Verify the change
                        _log.LogDetail("Verifying desktop icons state...");
                        if (VerifyDesktopIconsState(visible))
                        {
                            _log.LogSuccess($"Desktop icons {targetState} and verified (attempt {attempt})");
                            return OperationResult.Succeeded("DesktopIcons", attempt, $"Icons {(visible ? "shown" : "hidden")} and verified");
                        }
                        _log.LogFailure("Desktop icons state verification failed");
                    }
                    else
                    {
                        _log.LogWarning("Could not find desktop ListView handle");
                        // Handle not found but registry is set - partial success
                        if (GetDesktopIconsRegistryState() == visible)
                        {
                            _log.LogSuccess("Registry state set correctly - icons will update after Explorer restart");
                            return OperationResult.Succeeded("DesktopIcons", attempt, "Registry set, handle not available (may need Explorer restart)");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _log.LogWarning("Desktop icons operation was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogFailure($"Attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        _log.LogError($"Desktop icons operation failed after {MaxRetries} attempts", ex);
                        return OperationResult.Failed("DesktopIcons", attempt, ex);
                    }
                }

                // Exponential backoff
                var backoffMs = 500 * (int)Math.Pow(2, attempt - 1);
                _log.LogWaiting($"Waiting {backoffMs}ms before retry...");
                await Task.Delay(backoffMs, cancellationToken);
            }

            _log.LogFailure($"Desktop icons verification failed after {MaxRetries} retries");
            return OperationResult.Failed("DesktopIcons", MaxRetries, null, "Verification failed after retries");
        }
    }
}

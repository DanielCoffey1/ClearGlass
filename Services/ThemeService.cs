using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Windows;
using ClearGlass.Services.Exceptions;
using ClearGlass.Services.Features;
using ClearGlass.Services.Models;
using ClearGlass.Services.Native;
using ClearGlass.Services.Registry;
using ClearGlass.Services.Reliability;

namespace ClearGlass.Services
{
    /// <summary>
    /// Main service for managing Windows theme and related settings
    /// </summary>
    public class ThemeService : IThemeService
    {
        private const int MaxRetries = 3;
        private readonly TaskbarService _taskbarService;
        private readonly WidgetService _widgetService;
        private readonly WallpaperService _wallpaperService;
        private readonly DesktopIconsService _desktopIconsService;
        private readonly LoggingService _log;

        public ThemeService()
        {
            _taskbarService = new TaskbarService();
            _widgetService = new WidgetService();
            _wallpaperService = new WallpaperService();
            _desktopIconsService = new DesktopIconsService();
            _log = LoggingService.Instance;
        }

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

        private void BroadcastThemeChange()
        {
            try
            {
                WindowsApi.BroadcastMessage(WindowsApi.WM_SYSCOLORCHANGE);
                WindowsApi.BroadcastMessage(WindowsApi.WM_THEMECHANGE);
                WindowsApi.BroadcastMessage(WindowsApi.WM_SETTINGCHANGE, "ImmersiveColorSet");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error broadcasting theme change: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies that dark mode state matches expected value
        /// </summary>
        private bool VerifyDarkMode(bool expected)
        {
            try
            {
                var actual = RegistryHelper.GetValue<int>(RegistryHelper.PersonalizePath, "SystemUsesLightTheme", 1) == 0;
                return actual == expected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets or sets whether dark mode is enabled
        /// </summary>
        public bool IsDarkMode
        {
            get
            {
                try
                {
                    return RegistryHelper.GetValue<int>(RegistryHelper.PersonalizePath, "SystemUsesLightTheme", 1) == 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting theme state: {ex.Message}");
                    return false;
                }
            }
            set
            {
                try
                {
                    Debug.WriteLine($"Setting theme to: {(value ? "Dark" : "Light")}");

                    // Set accent color settings
                    RegistryHelper.SetValueWithFallback(RegistryHelper.AccentColorSettingsPath, "EnableTransparency", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    RegistryHelper.SetValueWithFallback(RegistryHelper.AccentColorSettingsPath, "ColorPrevalence", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    RegistryHelper.SetValueWithFallback(RegistryHelper.AccentColorSettingsPath, "AccentColor", -1, Microsoft.Win32.RegistryValueKind.DWord);
                    RegistryHelper.SetValueWithFallback(RegistryHelper.AccentColorSettingsPath, "AccentColorInactive", -1, Microsoft.Win32.RegistryValueKind.DWord);

                    // Set system theme
                    RegistryHelper.SetValue(RegistryHelper.PersonalizePath, "SystemUsesLightTheme", value ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
                    RegistryHelper.SetValue(RegistryHelper.PersonalizePath, "AppsUseLightTheme", value ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);

                    // Update wallpaper if available
                    var wallpaperPath = _wallpaperService.GetWallpaperPath(value);
                    if (!string.IsNullOrEmpty(wallpaperPath))
                    {
                        _wallpaperService.SetWallpaper(wallpaperPath);
                    }

                    BroadcastThemeChange();
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error setting theme: {ex.Message}",
                        ThemeServiceOperation.RegistryAccess,
                        ex);
                }
            }
        }

        /// <summary>
        /// Sets dark mode with retry and verification
        /// </summary>
        public async Task<OperationResult> ApplyDarkModeAsync(bool isDarkMode, CancellationToken cancellationToken = default)
        {
            var themeName = isDarkMode ? "Dark" : "Light";
            _log.LogSubsection($"Applying {themeName} Theme");

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 1)
                {
                    _log.LogRetry(attempt, MaxRetries, $"Setting {themeName} theme");
                }

                try
                {
                    _log.LogDetail("Configuring accent color settings...");
                    // Set accent color settings with verification
                    RegistryHelper.SetValueWithRetry(RegistryHelper.AccentColorSettingsPath, "EnableTransparency", 1, Microsoft.Win32.RegistryValueKind.DWord, 2);
                    _log.LogRegistry("SET", "AccentColorSettings", "EnableTransparency", 1);

                    RegistryHelper.SetValueWithRetry(RegistryHelper.AccentColorSettingsPath, "ColorPrevalence", 0, Microsoft.Win32.RegistryValueKind.DWord, 2);
                    _log.LogRegistry("SET", "AccentColorSettings", "ColorPrevalence", 0);

                    _log.LogDetail("Setting system theme preference...");
                    // Set system theme with verification
                    bool systemThemeSet = RegistryHelper.SetValueVerified(
                        RegistryHelper.PersonalizePath,
                        "SystemUsesLightTheme",
                        isDarkMode ? 0 : 1,
                        Microsoft.Win32.RegistryValueKind.DWord);
                    _log.LogRegistry("SET", "Personalize", "SystemUsesLightTheme", isDarkMode ? 0 : 1);

                    bool appsThemeSet = RegistryHelper.SetValueVerified(
                        RegistryHelper.PersonalizePath,
                        "AppsUseLightTheme",
                        isDarkMode ? 0 : 1,
                        Microsoft.Win32.RegistryValueKind.DWord);
                    _log.LogRegistry("SET", "Personalize", "AppsUseLightTheme", isDarkMode ? 0 : 1);

                    // Flush registry changes
                    _log.LogDetail("Flushing registry changes...");
                    RegistryHelper.FlushAll(
                        RegistryHelper.PersonalizePath,
                        RegistryHelper.AccentColorSettingsPath);

                    // Broadcast the theme change
                    _log.LogDetail("Broadcasting theme change to Windows...");
                    BroadcastThemeChange();

                    // Small delay for system to process
                    _log.LogWaiting("Waiting for system to apply theme...");
                    await Task.Delay(300, cancellationToken);

                    // Verify the change
                    _log.LogDetail("Verifying theme was applied correctly...");
                    if (systemThemeSet && appsThemeSet && VerifyDarkMode(isDarkMode))
                    {
                        _log.LogSuccess($"{themeName} theme applied successfully (attempt {attempt})");
                        return OperationResult.Succeeded("DarkMode", attempt, $"Theme set to {themeName}");
                    }

                    _log.LogFailure($"Theme verification failed on attempt {attempt}");
                }
                catch (OperationCanceledException)
                {
                    _log.LogWarning("Theme application was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogFailure($"Attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        _log.LogError($"Failed to apply {themeName} theme after {MaxRetries} attempts", ex);
                        return OperationResult.Failed("DarkMode", attempt, ex);
                    }
                }

                // Exponential backoff
                var backoffMs = 500 * (int)Math.Pow(2, attempt - 1);
                _log.LogWaiting($"Waiting {backoffMs}ms before retry...");
                await Task.Delay(backoffMs, cancellationToken);
            }

            _log.LogFailure($"Failed to apply {themeName} theme after {MaxRetries} retries");
            return OperationResult.Failed("DarkMode", MaxRetries, null, "Dark mode verification failed after retries");
        }

        // Delegate taskbar properties to TaskbarService
        public bool IsTaskbarCentered
        {
            get => _taskbarService.IsTaskbarCentered;
            set => _taskbarService.ApplySettings(isTaskbarCentered: value, applyImmediately: false);
        }

        public bool IsTaskViewEnabled
        {
            get => _taskbarService.IsTaskViewEnabled;
            set => _taskbarService.ApplySettings(isTaskViewEnabled: value, applyImmediately: false);
        }

        public bool IsSearchVisible
        {
            get => _taskbarService.IsSearchVisible;
            set => _taskbarService.ApplySettings(isSearchVisible: value, applyImmediately: false);
        }

        /// <summary>
        /// Applies taskbar settings asynchronously with retry, verification, and Explorer restart safeguards
        /// </summary>
        public async Task<OperationResult> ApplyTaskbarSettingAsync(
            bool? isTaskbarCentered = null,
            bool? isTaskViewEnabled = null,
            bool? isSearchVisible = null,
            CancellationToken cancellationToken = default)
        {
            // Apply settings with retry
            var settingsResult = await _taskbarService.ApplySettingsWithRetryAsync(
                isTaskbarCentered,
                isTaskViewEnabled,
                isSearchVisible,
                cancellationToken);

            // Apply pending changes (restart Explorer) if needed
            if (_taskbarService.HasPendingChanges)
            {
                var restartResult = await _taskbarService.ApplyPendingChangesAsync(cancellationToken);
                if (!restartResult.Success)
                {
                    return restartResult;
                }
            }

            return settingsResult;
        }

        /// <summary>
        /// Applies desktop icons visibility asynchronously with retry and verification
        /// </summary>
        public async Task<OperationResult> ApplyDesktopIconsAsync(bool visible, CancellationToken cancellationToken = default)
        {
            return await _desktopIconsService.SetDesktopIconsVisibleAsync(visible, cancellationToken);
        }

        // Delegate widgets property to WidgetService
        public bool AreWidgetsEnabled
        {
            get => _widgetService.AreWidgetsEnabled;
            set => _widgetService.AreWidgetsEnabled = value;
        }

        // Delegate desktop icons property to DesktopIconsService
        public bool AreDesktopIconsVisible
        {
            get => _desktopIconsService.AreDesktopIconsVisible;
            set => _desktopIconsService.AreDesktopIconsVisible = value;
        }

        /// <summary>
        /// Sets the desktop wallpaper
        /// </summary>
        public void SetWallpaper(string path)
        {
            _wallpaperService.SetWallpaper(path);
        }

        /// <summary>
        /// Sets the desktop wallpaper with retry and verification
        /// </summary>
        public async Task<OperationResult> SetWallpaperAsync(string path, CancellationToken cancellationToken = default)
        {
            return await _wallpaperService.SetWallpaperWithRetryAsync(path, cancellationToken);
        }

        /// <summary>
        /// Gets the current theme settings
        /// </summary>
        public ThemeSettings GetCurrentSettings()
        {
            return new ThemeSettings
            {
                IsDarkMode = IsDarkMode,
                IsTaskbarCentered = IsTaskbarCentered,
                IsTaskViewEnabled = IsTaskViewEnabled,
                AreWidgetsEnabled = AreWidgetsEnabled,
                IsSearchVisible = IsSearchVisible,
                AreDesktopIconsVisible = AreDesktopIconsVisible
            };
        }

        /// <summary>
        /// Applies the specified theme settings
        /// </summary>
        public void ApplySettings(ThemeSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            try
            {
                // Apply all registry changes first
                if (settings.IsDarkMode != IsDarkMode)
                {
                    // Set accent color settings
                    RegistryHelper.SetValueWithFallback(RegistryHelper.AccentColorSettingsPath, "EnableTransparency", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    RegistryHelper.SetValueWithFallback(RegistryHelper.AccentColorSettingsPath, "ColorPrevalence", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    RegistryHelper.SetValueWithFallback(RegistryHelper.AccentColorSettingsPath, "AccentColor", -1, Microsoft.Win32.RegistryValueKind.DWord);
                    RegistryHelper.SetValueWithFallback(RegistryHelper.AccentColorSettingsPath, "AccentColorInactive", -1, Microsoft.Win32.RegistryValueKind.DWord);

                    // Set system theme
                    RegistryHelper.SetValue(RegistryHelper.PersonalizePath, "SystemUsesLightTheme", settings.IsDarkMode ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
                    RegistryHelper.SetValue(RegistryHelper.PersonalizePath, "AppsUseLightTheme", settings.IsDarkMode ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
                }

                // Batch taskbar changes
                _taskbarService.ApplySettings(
                    isTaskbarCentered: settings.IsTaskbarCentered,
                    isTaskViewEnabled: settings.IsTaskViewEnabled,
                    isSearchVisible: settings.IsSearchVisible,
                    applyImmediately: false
                );

                // Apply other settings
                if (settings.AreWidgetsEnabled != AreWidgetsEnabled)
                {
                    _widgetService.AreWidgetsEnabled = settings.AreWidgetsEnabled;
                }

                if (settings.AreDesktopIconsVisible != AreDesktopIconsVisible)
                {
                    _desktopIconsService.AreDesktopIconsVisible = settings.AreDesktopIconsVisible;
                }

                // Set wallpaper if provided
                if (!string.IsNullOrEmpty(settings.WallpaperPath))
                {
                    _wallpaperService.SetWallpaper(settings.WallpaperPath);
                }

                // Apply all changes at once
                if (_taskbarService.HasPendingChanges)
                {
                    _taskbarService.ApplyPendingChanges();
                }

                // Broadcast changes
                BroadcastThemeChange();
            }
            catch (Exception ex)
            {
                throw new ThemeServiceException(
                    $"Error applying settings: {ex.Message}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        /// <summary>
        /// Applies theme settings reliably with retry, verification, and progress reporting
        /// </summary>
        public async Task<ThemeApplicationResult> ApplySettingsReliableAsync(
            ThemeSettings settings,
            IProgress<ThemeApplicationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var stopwatch = Stopwatch.StartNew();
            _log.ResetStepCounter();
            _log.LogSectionHeader("APPLYING CLEAR GLASS THEME");
            _log.LogInformation("Starting theme application with the following settings:");
            _log.LogDetail($"Dark Mode: {settings.IsDarkMode}");
            _log.LogDetail($"Taskbar Centered: {settings.IsTaskbarCentered}");
            _log.LogDetail($"Task View Enabled: {settings.IsTaskViewEnabled}");
            _log.LogDetail($"Search Visible: {settings.IsSearchVisible}");
            _log.LogDetail($"Desktop Icons Visible: {settings.AreDesktopIconsVisible}");
            _log.LogDetail($"Wallpaper: {settings.WallpaperPath ?? "(none)"}");

            var results = new List<OperationResult>();
            int totalSteps = 5;
            int currentStep = 0;

            try
            {
                // Step 1: Apply dark mode
                _log.LogStep(1, totalSteps, "Applying Windows Theme");
                progress?.Report(new ThemeApplicationProgress("Applying theme...", (++currentStep * 100) / totalSteps));

                if (settings.IsDarkMode != IsDarkMode)
                {
                    var darkModeResult = await ApplyDarkModeAsync(settings.IsDarkMode, cancellationToken);
                    results.Add(darkModeResult);

                    if (darkModeResult.Success)
                    {
                        _log.LogWaiting("Allowing system to process theme change...");
                        await Task.Delay(500, cancellationToken);
                    }
                }
                else
                {
                    _log.LogSuccess("Theme already set to desired mode - skipping");
                    results.Add(OperationResult.Succeeded("DarkMode", 1, "No change needed"));
                }

                // Step 2: Apply taskbar settings
                _log.LogStep(2, totalSteps, "Configuring Taskbar Settings");
                progress?.Report(new ThemeApplicationProgress("Configuring taskbar...", (++currentStep * 100) / totalSteps));

                _log.LogDetail($"Taskbar alignment: {(settings.IsTaskbarCentered ? "Center" : "Left")}");
                _log.LogDetail($"Task View button: {(settings.IsTaskViewEnabled ? "Visible" : "Hidden")}");
                _log.LogDetail($"Search box: {(settings.IsSearchVisible ? "Visible" : "Hidden")}");

                var taskbarResult = await _taskbarService.ApplySettingsWithRetryAsync(
                    isTaskbarCentered: settings.IsTaskbarCentered,
                    isTaskViewEnabled: settings.IsTaskViewEnabled,
                    isSearchVisible: settings.IsSearchVisible,
                    cancellationToken);
                results.Add(taskbarResult);

                if (taskbarResult.Success)
                {
                    _log.LogSuccess("Taskbar settings configured successfully");
                }
                else
                {
                    _log.LogFailure($"Taskbar settings issue: {taskbarResult.VerificationDetails}");
                }

                // Step 3: Apply desktop icons
                _log.LogStep(3, totalSteps, "Configuring Desktop Icons");
                progress?.Report(new ThemeApplicationProgress("Configuring desktop icons...", (++currentStep * 100) / totalSteps));

                if (settings.AreDesktopIconsVisible != AreDesktopIconsVisible)
                {
                    _log.LogDetail($"Setting desktop icons to: {(settings.AreDesktopIconsVisible ? "Visible" : "Hidden")}");
                    var iconsResult = await _desktopIconsService.SetDesktopIconsVisibleAsync(
                        settings.AreDesktopIconsVisible,
                        cancellationToken);
                    results.Add(iconsResult);

                    if (iconsResult.Success)
                    {
                        _log.LogSuccess($"Desktop icons {(settings.AreDesktopIconsVisible ? "shown" : "hidden")} successfully");
                    }
                    else
                    {
                        _log.LogFailure($"Desktop icons issue: {iconsResult.VerificationDetails}");
                    }
                }
                else
                {
                    _log.LogSuccess("Desktop icons already in desired state - skipping");
                    results.Add(OperationResult.Succeeded("DesktopIcons", 1, "No change needed"));
                }

                // Step 4: Restart Explorer if needed
                _log.LogStep(4, totalSteps, "Applying System Changes");
                progress?.Report(new ThemeApplicationProgress("Applying changes...", (++currentStep * 100) / totalSteps));

                if (_taskbarService.HasPendingChanges)
                {
                    _log.LogDetail("Pending changes detected - Explorer restart required");
                    var explorerResult = await _taskbarService.ApplyPendingChangesAsync(cancellationToken);
                    results.Add(explorerResult);

                    if (explorerResult.Success)
                    {
                        _log.LogSuccess("System changes applied successfully");
                        _log.LogWaiting("Allowing Explorer to stabilize...");
                        await Task.Delay(500, cancellationToken);
                    }
                    else
                    {
                        _log.LogFailure($"Explorer restart issue: {explorerResult.VerificationDetails}");
                    }
                }
                else
                {
                    _log.LogSuccess("No Explorer restart needed");
                    results.Add(OperationResult.Succeeded("ExplorerRestart", 1, "No restart needed"));
                }

                // Step 5: Apply wallpaper
                _log.LogStep(5, totalSteps, "Setting Desktop Wallpaper");
                progress?.Report(new ThemeApplicationProgress("Setting wallpaper...", (++currentStep * 100) / totalSteps));

                if (!string.IsNullOrEmpty(settings.WallpaperPath))
                {
                    _log.LogDetail($"Wallpaper path: {settings.WallpaperPath}");
                    var wallpaperResult = await _wallpaperService.SetWallpaperWithRetryAsync(
                        settings.WallpaperPath,
                        cancellationToken);
                    results.Add(wallpaperResult);

                    if (wallpaperResult.Success)
                    {
                        _log.LogSuccess("Wallpaper applied successfully");
                    }
                    else
                    {
                        _log.LogFailure($"Wallpaper issue: {wallpaperResult.VerificationDetails}");
                    }
                }
                else
                {
                    _log.LogSuccess("No wallpaper specified - skipping");
                    results.Add(OperationResult.Succeeded("Wallpaper", 1, "No wallpaper specified"));
                }

                // Final broadcast
                _log.LogDetail("Broadcasting final theme change notification...");
                BroadcastThemeChange();

                progress?.Report(new ThemeApplicationProgress("Complete", 100));
            }
            catch (OperationCanceledException)
            {
                _log.LogWarning("Theme application was cancelled by user");
                results.Add(OperationResult.Failed("Operation", 0, null, "Operation was cancelled"));
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError($"Unexpected error during theme application: {ex.Message}", ex);
                results.Add(OperationResult.Failed("Operation", 0, ex, ex.Message));
            }

            stopwatch.Stop();
            var finalResult = ThemeApplicationResult.FromResults(results);

            // Log summary
            _log.LogSummary("THEME APPLICATION COMPLETE",
                finalResult.SuccessCount,
                finalResult.FailureCount,
                stopwatch.Elapsed);

            if (finalResult.Success)
            {
                _log.LogSuccess("All theme settings applied successfully!");
            }
            else
            {
                _log.LogWarning($"Theme application completed with issues: {finalResult.Summary}");
            }

            return finalResult;
        }

        /// <summary>
        /// Refreshes all Windows UI elements
        /// </summary>
        public void RefreshWindows()
        {
            try
            {
                if (_taskbarService.HasPendingChanges)
                {
                    _taskbarService.ApplyPendingChanges();
                }
                BroadcastThemeChange();
                RegistryHelper.FlushChanges(RegistryHelper.TaskbarSettingsPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing Windows: {ex.Message}");
            }
        }

        private void CheckRegistryAccess()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software", false);
                if (key == null)
                {
                    throw new ThemeServiceException(
                        "Cannot access registry",
                        ThemeServiceOperation.RegistryAccess);
                }
            }
            catch (Exception ex)
            {
                throw new ThemeServiceException(
                    "Registry access error",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }
    }
}

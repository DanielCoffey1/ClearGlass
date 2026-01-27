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

        public ThemeService()
        {
            _taskbarService = new TaskbarService();
            _widgetService = new WidgetService();
            _wallpaperService = new WallpaperService();
            _desktopIconsService = new DesktopIconsService();
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
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Set accent color settings with verification
                    RegistryHelper.SetValueWithRetry(RegistryHelper.AccentColorSettingsPath, "EnableTransparency", 1, Microsoft.Win32.RegistryValueKind.DWord, 2);
                    RegistryHelper.SetValueWithRetry(RegistryHelper.AccentColorSettingsPath, "ColorPrevalence", 0, Microsoft.Win32.RegistryValueKind.DWord, 2);

                    // Set system theme with verification
                    bool systemThemeSet = RegistryHelper.SetValueVerified(
                        RegistryHelper.PersonalizePath,
                        "SystemUsesLightTheme",
                        isDarkMode ? 0 : 1,
                        Microsoft.Win32.RegistryValueKind.DWord);

                    bool appsThemeSet = RegistryHelper.SetValueVerified(
                        RegistryHelper.PersonalizePath,
                        "AppsUseLightTheme",
                        isDarkMode ? 0 : 1,
                        Microsoft.Win32.RegistryValueKind.DWord);

                    // Flush registry changes
                    RegistryHelper.FlushAll(
                        RegistryHelper.PersonalizePath,
                        RegistryHelper.AccentColorSettingsPath);

                    // Broadcast the theme change
                    BroadcastThemeChange();

                    // Small delay for system to process
                    await Task.Delay(300, cancellationToken);

                    // Verify the change
                    if (systemThemeSet && appsThemeSet && VerifyDarkMode(isDarkMode))
                    {
                        return OperationResult.Succeeded("DarkMode", attempt, $"Theme set to {(isDarkMode ? "Dark" : "Light")}");
                    }

                    Debug.WriteLine($"Dark mode verification failed on attempt {attempt}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Dark mode attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        return OperationResult.Failed("DarkMode", attempt, ex);
                    }
                }

                // Exponential backoff
                await Task.Delay(500 * (int)Math.Pow(2, attempt - 1), cancellationToken);
            }

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

            var results = new List<OperationResult>();
            int totalSteps = 5; // Dark mode, taskbar, search, desktop icons, wallpaper
            int currentStep = 0;

            try
            {
                // Step 1: Apply dark mode
                progress?.Report(new ThemeApplicationProgress("Applying theme...", (++currentStep * 100) / totalSteps));

                if (settings.IsDarkMode != IsDarkMode)
                {
                    var darkModeResult = await ApplyDarkModeAsync(settings.IsDarkMode, cancellationToken);
                    results.Add(darkModeResult);

                    if (darkModeResult.Success)
                    {
                        // Give the system time to process theme change
                        await Task.Delay(500, cancellationToken);
                    }
                }
                else
                {
                    results.Add(OperationResult.Succeeded("DarkMode", 1, "No change needed"));
                }

                // Step 2: Apply taskbar settings
                progress?.Report(new ThemeApplicationProgress("Configuring taskbar...", (++currentStep * 100) / totalSteps));

                var taskbarResult = await _taskbarService.ApplySettingsWithRetryAsync(
                    isTaskbarCentered: settings.IsTaskbarCentered,
                    isTaskViewEnabled: settings.IsTaskViewEnabled,
                    isSearchVisible: settings.IsSearchVisible,
                    cancellationToken);
                results.Add(taskbarResult);

                // Step 3: Apply desktop icons
                progress?.Report(new ThemeApplicationProgress("Configuring desktop icons...", (++currentStep * 100) / totalSteps));

                if (settings.AreDesktopIconsVisible != AreDesktopIconsVisible)
                {
                    var iconsResult = await _desktopIconsService.SetDesktopIconsVisibleAsync(
                        settings.AreDesktopIconsVisible,
                        cancellationToken);
                    results.Add(iconsResult);
                }
                else
                {
                    results.Add(OperationResult.Succeeded("DesktopIcons", 1, "No change needed"));
                }

                // Step 4: Restart Explorer if needed
                progress?.Report(new ThemeApplicationProgress("Applying changes...", (++currentStep * 100) / totalSteps));

                if (_taskbarService.HasPendingChanges)
                {
                    var explorerResult = await _taskbarService.ApplyPendingChangesAsync(cancellationToken);
                    results.Add(explorerResult);

                    if (explorerResult.Success)
                    {
                        // Allow time for Explorer to fully stabilize
                        await Task.Delay(500, cancellationToken);
                    }
                }
                else
                {
                    results.Add(OperationResult.Succeeded("ExplorerRestart", 1, "No restart needed"));
                }

                // Step 5: Apply wallpaper
                progress?.Report(new ThemeApplicationProgress("Setting wallpaper...", (++currentStep * 100) / totalSteps));

                if (!string.IsNullOrEmpty(settings.WallpaperPath))
                {
                    var wallpaperResult = await _wallpaperService.SetWallpaperWithRetryAsync(
                        settings.WallpaperPath,
                        cancellationToken);
                    results.Add(wallpaperResult);
                }
                else
                {
                    results.Add(OperationResult.Succeeded("Wallpaper", 1, "No wallpaper specified"));
                }

                // Final broadcast
                BroadcastThemeChange();

                progress?.Report(new ThemeApplicationProgress("Complete", 100));
            }
            catch (OperationCanceledException)
            {
                results.Add(OperationResult.Failed("Operation", 0, null, "Operation was cancelled"));
                throw;
            }
            catch (Exception ex)
            {
                results.Add(OperationResult.Failed("Operation", 0, ex, ex.Message));
            }

            return ThemeApplicationResult.FromResults(results);
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

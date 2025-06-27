using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.ComponentModel;
using System.Windows;
using ClearGlass.Services.Exceptions;
using ClearGlass.Services.Features;
using ClearGlass.Services.Models;
using ClearGlass.Services.Native;
using ClearGlass.Services.Registry;
using System.Collections.Generic;

namespace ClearGlass.Services
{
    /// <summary>
    /// Main service for managing Windows theme and related settings
    /// </summary>
    public class ThemeService : IThemeService
    {
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

            var errorMessages = new List<string>();

            // Apply all registry changes first
            try
            {
                if (settings.IsDarkMode != IsDarkMode)
                {
                    try
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
                    catch (Exception ex)
                    {
                        errorMessages.Add($"Error applying dark mode: {ex.Message}");
                    }
                }

                // Batch taskbar changes
                try
                {
                    _taskbarService.ApplySettings(
                        isTaskbarCentered: settings.IsTaskbarCentered,
                        isTaskViewEnabled: settings.IsTaskViewEnabled,
                        isSearchVisible: settings.IsSearchVisible,
                        applyImmediately: false
                    );
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"Error applying taskbar settings: {ex.Message}");
                }

                // Apply widgets
                if (settings.AreWidgetsEnabled != AreWidgetsEnabled)
                {
                    try
                    {
                        _widgetService.AreWidgetsEnabled = settings.AreWidgetsEnabled;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"Error modifying widgets settings: {ex.Message}");
                    }
                }

                // Apply desktop icons
                if (settings.AreDesktopIconsVisible != AreDesktopIconsVisible)
                {
                    try
                    {
                        _desktopIconsService.AreDesktopIconsVisible = settings.AreDesktopIconsVisible;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"Error modifying desktop icons: {ex.Message}");
                    }
                }

                // Set wallpaper if provided
                if (!string.IsNullOrEmpty(settings.WallpaperPath))
                {
                    try
                    {
                        _wallpaperService.SetWallpaper(settings.WallpaperPath);
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"Error setting wallpaper: {ex.Message}");
                    }
                }

                // Apply all changes at once
                try
                {
                    if (_taskbarService.HasPendingChanges)
                    {
                        _taskbarService.ApplyPendingChanges();
                    }
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"Error restarting Explorer: {ex.Message}");
                }

                // Broadcast changes
                try
                {
                    BroadcastThemeChange();
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"Error broadcasting theme change: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                errorMessages.Add($"General error applying settings: {ex.Message}");
            }

            if (errorMessages.Count > 0)
            {
                throw new ThemeServiceException(
                    "One or more errors occurred while applying settings:\n" + string.Join("\n", errorMessages),
                    ThemeServiceOperation.RegistryAccess
                );
            }
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
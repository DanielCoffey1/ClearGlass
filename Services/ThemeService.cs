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
            set => _taskbarService.IsTaskbarCentered = value;
        }

        public bool IsTaskViewEnabled
        {
            get => _taskbarService.IsTaskViewEnabled;
            set => _taskbarService.IsTaskViewEnabled = value;
        }

        public bool IsSearchVisible
        {
            get => _taskbarService.IsSearchVisible;
            set => _taskbarService.IsSearchVisible = value;
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

            IsDarkMode = settings.IsDarkMode;
            IsTaskbarCentered = settings.IsTaskbarCentered;
            IsTaskViewEnabled = settings.IsTaskViewEnabled;
            AreWidgetsEnabled = settings.AreWidgetsEnabled;
            IsSearchVisible = settings.IsSearchVisible;
            AreDesktopIconsVisible = settings.AreDesktopIconsVisible;

            if (!string.IsNullOrEmpty(settings.WallpaperPath))
            {
                SetWallpaper(settings.WallpaperPath);
            }

            BroadcastThemeChange();
        }

        /// <summary>
        /// Refreshes all Windows UI elements
        /// </summary>
        public void RefreshWindows()
        {
            try
            {
                _taskbarService.RestartExplorer();
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
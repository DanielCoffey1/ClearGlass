using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.ComponentModel;
using System.Windows;

namespace ClearGlass.Services
{
    public class ThemeService
    {
        private const string TaskbarSettingsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string SearchSettingsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search";
        private const string WidgetsPolicyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string WidgetsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Widgets";
        private const string FeedsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Feeds";
        private const string WebWidgetsPath = @"SOFTWARE\Policies\Microsoft\Dsh";
        private const string WidgetsGPOPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds";
        private const string DesktopIconsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string PersonalizePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AccentColorPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\History";
        private const string AccentColorSettingsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string ThemePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes";
        private const string CurrentThemePath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";

        // Add wallpaper paths
        private readonly string _windowsWallpaperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Web\\Wallpaper\\Windows");

        private string LightWallpaperPath => Path.Combine(_windowsWallpaperPath, "img19.jpg"); // Light Bloom
        private string DarkWallpaperPath => Path.Combine(_windowsWallpaperPath, "img20.jpg");  // Dark Bloom

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
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error restarting Explorer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void KillWidgetsProcess()
        {
            try
            {
                // Kill both Widgets.exe and WidgetService.exe
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c taskkill /f /im Widgets.exe /im WidgetService.exe 2>nul",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                // Try to disable the service
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
                // Enable and start the service
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

        public bool IsTaskbarCentered
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(TaskbarSettingsPath);
                    var value = key?.GetValue("TaskbarAl");
                    return value == null || (int)value == 1;
                }
                catch { return true; }
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(TaskbarSettingsPath, true);
                    key?.SetValue("TaskbarAl", value ? 1 : 0, RegistryValueKind.DWord);
                    RestartExplorer();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error setting taskbar alignment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public bool IsTaskViewEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(TaskbarSettingsPath);
                    var value = key?.GetValue("ShowTaskViewButton");
                    return value == null || (int)value == 1;
                }
                catch { return true; }
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(TaskbarSettingsPath, true);
                    key?.SetValue("ShowTaskViewButton", value ? 1 : 0, RegistryValueKind.DWord);
                    RestartExplorer();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error setting task view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool CheckWidgetsPolicyState()
        {
            using var key = Registry.CurrentUser.OpenSubKey(WidgetsPolicyPath);
            var value = key?.GetValue("TaskbarDa");
            return value == null || (int)value != 0;
        }

        private bool CheckWidgetsRegistryState()
        {
            using var key = Registry.CurrentUser.OpenSubKey(WidgetsPath);
            var value = key?.GetValue("WidgetsDisabled");
            return value == null || (int)value != 1;
        }

        private bool CheckFeedsState()
        {
            using var key = Registry.CurrentUser.OpenSubKey(FeedsPath);
            var value = key?.GetValue("ShellFeedsTaskbarViewMode");
            return value == null || (int)value != 0;
        }

        private bool CheckGroupPolicyState()
        {
            using var key = Registry.LocalMachine.OpenSubKey(WidgetsGPOPath);
            var value = key?.GetValue("EnableFeeds");
            return value == null || (int)value != 0;
        }

        private bool CheckWebWidgetsState()
        {
            using var key = Registry.LocalMachine.OpenSubKey(WebWidgetsPath);
            var value = key?.GetValue("AllowWebContentOnLockScreen");
            return value == null || (int)value != 0;
        }

        private void SetWidgetsPolicyState(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(WidgetsPolicyPath, true) 
                ?? Registry.CurrentUser.CreateSubKey(WidgetsPolicyPath);
            key.SetValue("TaskbarDa", enable ? 1 : 0, RegistryValueKind.DWord);
        }

        private void SetWidgetsRegistryState(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(WidgetsPath, true) 
                ?? Registry.CurrentUser.CreateSubKey(WidgetsPath);
            key.SetValue("WidgetsDisabled", enable ? 0 : 1, RegistryValueKind.DWord);
            key.SetValue("ConfiguredByPolicy", enable ? 0 : 1, RegistryValueKind.DWord);
        }

        private void SetFeedsState(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(FeedsPath, true) 
                ?? Registry.CurrentUser.CreateSubKey(FeedsPath);
            key.SetValue("ShellFeedsTaskbarViewMode", enable ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("IsFeedsAvailable", enable ? 1 : 0, RegistryValueKind.DWord);
        }

        private void SetGroupPolicyState(bool enable)
        {
            using var key = Registry.LocalMachine.OpenSubKey(WidgetsGPOPath, true) 
                ?? Registry.LocalMachine.CreateSubKey(WidgetsGPOPath);
            key.SetValue("EnableFeeds", enable ? 1 : 0, RegistryValueKind.DWord);
        }

        private void SetWebWidgetsState(bool enable)
        {
            using var key = Registry.LocalMachine.OpenSubKey(WebWidgetsPath, true) 
                ?? Registry.LocalMachine.CreateSubKey(WebWidgetsPath);
            key.SetValue("AllowWebContentOnLockScreen", enable ? 1 : 0, RegistryValueKind.DWord);
        }

        private void ShowWidgetsDisabledMessage()
        {
            CustomMessageBox.Show(
                "Widgets have been disabled. If they still appear, you may need to:\n\n" +
                "1. Sign out and sign back in\n" +
                "2. Or restart your computer\n\n" +
                "This is sometimes necessary due to Windows 11's widget system.",
                "Action Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public bool AreWidgetsEnabled
        {
            get
            {
                try
                {
                    return CheckWidgetsPolicyState() &&
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
                    CustomMessageBox.Show(
                        "This application requires administrator privileges to modify widgets settings.\n\nPlease right-click the application and select 'Run as administrator'.",
                        "Administrator Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    // Set all registry states
                    try { SetWidgetsPolicyState(value); }
                    catch (Exception ex) { Debug.WriteLine($"Error setting TaskbarDa: {ex.Message}"); }

                    try { SetWidgetsRegistryState(value); }
                    catch (Exception ex) { Debug.WriteLine($"Error setting WidgetsDisabled: {ex.Message}"); }

                    try { SetFeedsState(value); }
                    catch (Exception ex) { Debug.WriteLine($"Error setting Feeds keys: {ex.Message}"); }

                    try { SetGroupPolicyState(value); }
                    catch (Exception ex) { Debug.WriteLine($"Error setting GPO EnableFeeds: {ex.Message}"); }

                    try { SetWebWidgetsState(value); }
                    catch (Exception ex) { Debug.WriteLine($"Error setting WebWidgets policy: {ex.Message}"); }

                    // Handle processes and services
                    if (!value)
                    {
                        KillWidgetsProcess();
                    }
                    else
                    {
                        EnableWidgetsService();
                    }

                    RestartExplorer();

                    if (!value)
                    {
                        ShowWidgetsDisabledMessage();
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(
                        $"Error modifying widgets settings: {ex.Message}\n\nSome changes may require a system restart to take effect.",
                        "Widget Settings Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        public bool IsSearchVisible
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(SearchSettingsPath);
                    var value = key?.GetValue("SearchboxTaskbarMode");
                    return value == null || (int)value != 0;
                }
                catch { return true; }
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(SearchSettingsPath, true);
                    key?.SetValue("SearchboxTaskbarMode", value ? 1 : 0, RegistryValueKind.DWord);
                    RestartExplorer();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error setting search visibility: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SetWallpaperStyle()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            if (key != null)
            {
                key.SetValue("WallpaperStyle", "10"); // 10 = Fill
                key.SetValue("TileWallpaper", "0");
            }
        }

        private void ApplyWallpaperImage(string path)
        {
            const int SPI_SETDESKWALLPAPER = 0x0014;
            const int SPIF_UPDATEINIFILE = 0x01;
            const int SPIF_SENDCHANGE = 0x02;

            if (!NativeMethods.SystemParametersInfo(
                (uint)SPI_SETDESKWALLPAPER,
                0,
                path,
                (uint)(SPIF_UPDATEINIFILE | SPIF_SENDCHANGE)))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }
        }

        public void SetWallpaper(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.WriteLine("Cannot set wallpaper: path is null or empty");
                return;
            }

            try
            {
                Debug.WriteLine($"Attempting to set wallpaper: {path}");
                SetWallpaperStyle();
                ApplyWallpaperImage(path);
                Debug.WriteLine("Wallpaper set successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting wallpaper: {ex.Message}");
                throw;
            }
        }

        private IntPtr GetDesktopListViewHandle()
        {
            return NativeMethods.FindWindowEx(
                NativeMethods.FindWindowEx(
                    NativeMethods.FindWindow("Progman", null),
                    IntPtr.Zero,
                    "SHELLDLL_DefView",
                    null),
                IntPtr.Zero,
                "SysListView32",
                "FolderView");
        }

        private bool GetDesktopIconsRegistryState()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var value = key?.GetValue("HideIcons");
            return value == null || (int)value == 0;
        }

        private void SetDesktopIconsRegistryState(bool show)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true);
            key?.SetValue("HideIcons", show ? 0 : 1, RegistryValueKind.DWord);
        }

        private void SetDesktopIconsVisibility(IntPtr handle, bool show)
        {
            if (handle != IntPtr.Zero)
            {
                if (show && !NativeMethods.IsWindowVisible(handle))
                {
                    NativeMethods.ShowWindow(handle, SW_SHOW);
                }
                else if (!show && NativeMethods.IsWindowVisible(handle))
                {
                    NativeMethods.ShowWindow(handle, SW_HIDE);
                }
            }
        }

        public bool AreDesktopIconsVisible
        {
            get
            {
                try
                {
                    // First check registry for persisted state
                    bool registryState = GetDesktopIconsRegistryState();

                    // Fallback to checking window state
                    var toggleHandle = GetDesktopListViewHandle();
                    return toggleHandle != IntPtr.Zero ? NativeMethods.IsWindowVisible(toggleHandle) : registryState;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting desktop icons state: {ex.Message}");
                    return true;
                }
            }
            set
            {
                try
                {
                    SetDesktopIconsRegistryState(value);
                    var toggleHandle = GetDesktopListViewHandle();
                    SetDesktopIconsVisibility(toggleHandle, value);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error toggling desktop icons: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SendSystemColorChangeMessage()
        {
            const int HWND_BROADCAST = 0xFFFF;
            const int WM_SYSCOLORCHANGE = 0x0015;

            NativeMethods.SendMessageTimeout(
                new IntPtr(HWND_BROADCAST),
                WM_SYSCOLORCHANGE,
                IntPtr.Zero,
                null,
                NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_NORMAL,
                300,
                out _);
        }

        private void SendThemeChangeMessage()
        {
            const int HWND_BROADCAST = 0xFFFF;
            const int WM_THEMECHANGE = 0x031A;

            NativeMethods.SendMessageTimeout(
                new IntPtr(HWND_BROADCAST),
                WM_THEMECHANGE,
                IntPtr.Zero,
                null,
                NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_NORMAL,
                300,
                out _);
        }

        private void NotifyShellOfThemeChange()
        {
            const int WM_THEMECHANGE = 0x031A;

            var shell = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (shell != IntPtr.Zero)
            {
                NativeMethods.SendMessageTimeout(
                    shell,
                    WM_THEMECHANGE,
                    IntPtr.Zero,
                    null,
                    NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_NORMAL,
                    300,
                    out _);
            }
        }

        private void SendImmersiveColorSetMessage()
        {
            const int HWND_BROADCAST = 0xFFFF;
            const int WM_SETTINGCHANGE = 0x001A;

            NativeMethods.SendMessageTimeout(
                new IntPtr(HWND_BROADCAST),
                WM_SETTINGCHANGE,
                IntPtr.Zero,
                "ImmersiveColorSet",
                NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_NORMAL,
                300,
                out _);
        }

        private void BroadcastThemeChange()
        {
            try
            {
                SendSystemColorChangeMessage();
                SendThemeChangeMessage();
                NotifyShellOfThemeChange();
                SendImmersiveColorSetMessage();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error broadcasting theme change: {ex.Message}");
            }
        }

        private void SetAccentColorSettings()
        {
            using var key = Registry.CurrentUser.OpenSubKey(AccentColorSettingsPath, true);
            if (key != null)
            {
                key.SetValue("EnableTransparency", 1, RegistryValueKind.DWord);
                key.SetValue("ColorPrevalence", 0, RegistryValueKind.DWord);
                key.SetValue("AccentColor", -1, RegistryValueKind.DWord);
                key.SetValue("AccentColorInactive", -1, RegistryValueKind.DWord);
            }
        }

        private void SetSystemTheme(bool isDarkMode)
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizePath, true);
            if (key != null)
            {
                key.SetValue("SystemUsesLightTheme", isDarkMode ? 0 : 1, RegistryValueKind.DWord);
                key.SetValue("AppsUseLightTheme", isDarkMode ? 0 : 1, RegistryValueKind.DWord);
            }
        }

        private void ApplyWindowsTheme(bool isDarkMode)
        {
            string themesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Resources", "Themes");

            string themeFile = isDarkMode ? "dark.theme" : "aero.theme";
            string themePath = Path.Combine(themesPath, themeFile);

            if (File.Exists(themePath))
            {
                Debug.WriteLine($"Applying theme: {themePath}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = themePath,
                    UseShellExecute = true
                });
            }
        }

        public bool IsDarkMode
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(PersonalizePath);
                    var value = key?.GetValue("SystemUsesLightTheme");
                    return value != null && (int)value == 0;
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

                    SetAccentColorSettings();
                    SetSystemTheme(value);
                    ApplyWindowsTheme(value);

                    // Broadcast the theme change
                    BroadcastThemeChange();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting theme: {ex.Message}");
                    throw;
                }
            }
        }

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private static class NativeMethods
        {
            public const int SMTO_ABORTIFHUNG = 0x0002;
            public const int SMTO_NORMAL = 0x0000;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, string lParam,
                int fuFlags, int uTimeout, out IntPtr lpdwResult);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
        }

        private void FlushRegistryChanges()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(TaskbarSettingsPath, true);
                key?.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error flushing registry changes: {ex.Message}");
            }
        }

        public void RefreshWindows()
        {
            try
            {
                RestartExplorer();
                BroadcastThemeChange();
                FlushRegistryChanges();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing Windows: {ex.Message}");
            }
        }
    }
} 
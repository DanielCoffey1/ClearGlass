using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.ComponentModel;

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
                System.Windows.MessageBox.Show($"Error restarting Explorer: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                    System.Windows.MessageBox.Show($"Error setting taskbar alignment: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                    System.Windows.MessageBox.Show($"Error setting task view: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        public bool AreWidgetsEnabled
        {
            get
            {
                try
                {
                    // Check multiple registry locations
                    using (var key1 = Registry.CurrentUser.OpenSubKey(WidgetsPolicyPath))
                    {
                        var value1 = key1?.GetValue("TaskbarDa");
                        if (value1 != null && (int)value1 == 0) return false;
                    }

                    using (var key2 = Registry.CurrentUser.OpenSubKey(WidgetsPath))
                    {
                        var value2 = key2?.GetValue("WidgetsDisabled");
                        if (value2 != null && (int)value2 == 1) return false;
                    }

                    using (var key3 = Registry.CurrentUser.OpenSubKey(FeedsPath))
                    {
                        var value3 = key3?.GetValue("ShellFeedsTaskbarViewMode");
                        if (value3 != null && (int)value3 == 0) return false;
                    }

                    // Check Group Policy settings
                    using (var key4 = Registry.LocalMachine.OpenSubKey(WidgetsGPOPath))
                    {
                        var value4 = key4?.GetValue("EnableFeeds");
                        if (value4 != null && (int)value4 == 0) return false;
                    }

                    using (var key5 = Registry.LocalMachine.OpenSubKey(WebWidgetsPath))
                    {
                        var value5 = key5?.GetValue("AllowWebContentOnLockScreen");
                        if (value5 != null && (int)value5 == 0) return false;
                    }

                    return true;
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
                    System.Windows.MessageBox.Show(
                        "This application requires administrator privileges to modify widgets settings.\n\nPlease right-click the application and select 'Run as administrator'.",
                        "Administrator Required",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    // HKCU settings
                    try
                    {
                        using var key1 = Registry.CurrentUser.OpenSubKey(WidgetsPolicyPath, true) 
                            ?? Registry.CurrentUser.CreateSubKey(WidgetsPolicyPath);
                        key1.SetValue("TaskbarDa", value ? 1 : 0, RegistryValueKind.DWord);
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error setting TaskbarDa: {ex.Message}"); }

                    try
                    {
                        using var key2 = Registry.CurrentUser.OpenSubKey(WidgetsPath, true) 
                            ?? Registry.CurrentUser.CreateSubKey(WidgetsPath);
                        key2.SetValue("WidgetsDisabled", value ? 0 : 1, RegistryValueKind.DWord);
                        key2.SetValue("ConfiguredByPolicy", value ? 0 : 1, RegistryValueKind.DWord);
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error setting WidgetsDisabled: {ex.Message}"); }

                    try
                    {
                        using var key3 = Registry.CurrentUser.OpenSubKey(FeedsPath, true) 
                            ?? Registry.CurrentUser.CreateSubKey(FeedsPath);
                        key3.SetValue("ShellFeedsTaskbarViewMode", value ? 1 : 0, RegistryValueKind.DWord);
                        key3.SetValue("IsFeedsAvailable", value ? 1 : 0, RegistryValueKind.DWord);
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error setting Feeds keys: {ex.Message}"); }

                    // HKLM Group Policy settings
                    try
                    {
                        using var key4 = Registry.LocalMachine.OpenSubKey(WidgetsGPOPath, true) 
                            ?? Registry.LocalMachine.CreateSubKey(WidgetsGPOPath);
                        key4.SetValue("EnableFeeds", value ? 1 : 0, RegistryValueKind.DWord);
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error setting GPO EnableFeeds: {ex.Message}"); }

                    try
                    {
                        using var key5 = Registry.LocalMachine.OpenSubKey(WebWidgetsPath, true) 
                            ?? Registry.LocalMachine.CreateSubKey(WebWidgetsPath);
                        key5.SetValue("AllowWebContentOnLockScreen", value ? 1 : 0, RegistryValueKind.DWord);
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error setting WebWidgets policy: {ex.Message}"); }

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
                        System.Windows.MessageBox.Show(
                            "Widgets have been disabled. If they still appear, you may need to:\n\n" +
                            "1. Sign out and sign back in\n" +
                            "2. Or restart your computer\n\n" +
                            "This is sometimes necessary due to Windows 11's widget system.",
                            "Action Required",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Error modifying widgets settings: {ex.Message}\n\nSome changes may require a system restart to take effect.",
                        "Widget Settings Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
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
                    System.Windows.MessageBox.Show($"Error setting search visibility: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                    var value = key?.GetValue("HideIcons");
                    if (value != null)
                    {
                        return (int)value == 0;
                    }

                    // Fallback to checking window state
                    var toggleHandle = NativeMethods.FindWindowEx(
                        NativeMethods.FindWindowEx(
                            NativeMethods.FindWindow("Progman", null),
                            IntPtr.Zero,
                            "SHELLDLL_DefView",
                            null),
                        IntPtr.Zero,
                        "SysListView32",
                        "FolderView");

                    return toggleHandle != IntPtr.Zero && NativeMethods.IsWindowVisible(toggleHandle);
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
                    // Persist the state in registry
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true);
                    key?.SetValue("HideIcons", value ? 0 : 1, RegistryValueKind.DWord);

                    // Apply the change to the desktop window
                    var toggleHandle = NativeMethods.FindWindowEx(
                        NativeMethods.FindWindowEx(
                            NativeMethods.FindWindow("Progman", null),
                            IntPtr.Zero,
                            "SHELLDLL_DefView",
                            null),
                        IntPtr.Zero,
                        "SysListView32",
                        "FolderView");

                    if (toggleHandle != IntPtr.Zero)
                    {
                        if (value && !NativeMethods.IsWindowVisible(toggleHandle))
                        {
                            NativeMethods.ShowWindow(toggleHandle, SW_SHOW);
                        }
                        else if (!value && NativeMethods.IsWindowVisible(toggleHandle))
                        {
                            NativeMethods.ShowWindow(toggleHandle, SW_HIDE);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error toggling desktop icons: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private string GetWallpaperPath(bool isDarkMode)
        {
            try
            {
                string wallpaperBasePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Web", "Wallpaper", "Windows");

                // Use the correct filenames
                string fileName = isDarkMode ? "img19_1920x1200.jpg" : "img0_1920x1200.jpg";
                string wallpaperPath = Path.Combine(wallpaperBasePath, fileName);

                Debug.WriteLine($"Trying wallpaper path: {wallpaperPath}");

                if (File.Exists(wallpaperPath))
                {
                    return wallpaperPath;
                }

                Debug.WriteLine("Wallpaper file not found");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding wallpaper path: {ex.Message}");
                return null;
            }
        }

        public void SetWallpaper(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.WriteLine("Cannot set wallpaper: path is null or empty");
                return;
            }

            const int SPI_SETDESKWALLPAPER = 0x0014;
            const int SPIF_UPDATEINIFILE = 0x01;
            const int SPIF_SENDCHANGE = 0x02;

            try
            {
                Debug.WriteLine($"Attempting to set wallpaper: {path}");

                // First try to set the wallpaper style to Fill
                using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null)
                    {
                        key.SetValue("WallpaperStyle", "10"); // 10 = Fill
                        key.SetValue("TileWallpaper", "0");
                    }
                }

                // Set the wallpaper
                if (!NativeMethods.SystemParametersInfo(
                    (uint)SPI_SETDESKWALLPAPER,
                    0,
                    path,
                    (uint)(SPIF_UPDATEINIFILE | SPIF_SENDCHANGE)))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error);
                }

                Debug.WriteLine("Wallpaper set successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting wallpaper: {ex.Message}");
                throw;
            }
        }

        private void BroadcastThemeChange()
        {
            const int HWND_BROADCAST = 0xFFFF;
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_SYSCOLORCHANGE = 0x0015;
            const int WM_THEMECHANGE = 0x031A;

            try
            {
                // Send all messages in quick succession
                NativeMethods.SendMessageTimeout(
                    new IntPtr(HWND_BROADCAST),
                    WM_SYSCOLORCHANGE,
                    IntPtr.Zero,
                    null,
                    NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_NORMAL,
                    300,
                    out _);

                NativeMethods.SendMessageTimeout(
                    new IntPtr(HWND_BROADCAST),
                    WM_THEMECHANGE,
                    IntPtr.Zero,
                    null,
                    NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_NORMAL,
                    300,
                    out _);

                // Notify Windows Shell about the change
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

                // Send the final immersive color set change
                NativeMethods.SendMessageTimeout(
                    new IntPtr(HWND_BROADCAST),
                    WM_SETTINGCHANGE,
                    IntPtr.Zero,
                    "ImmersiveColorSet",
                    NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_NORMAL,
                    300,
                    out _);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error broadcasting theme change: {ex.Message}");
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

                    // Set accent color to automatic first
                    using (var key = Registry.CurrentUser.OpenSubKey(AccentColorSettingsPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("EnableTransparency", 1, RegistryValueKind.DWord);
                            key.SetValue("ColorPrevalence", 0, RegistryValueKind.DWord);
                            key.SetValue("AccentColor", -1, RegistryValueKind.DWord);
                            key.SetValue("AccentColorInactive", -1, RegistryValueKind.DWord);
                        }
                    }

                    // Set system theme
                    using (var key = Registry.CurrentUser.OpenSubKey(PersonalizePath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("SystemUsesLightTheme", value ? 0 : 1, RegistryValueKind.DWord);
                            key.SetValue("AppsUseLightTheme", value ? 0 : 1, RegistryValueKind.DWord);
                        }
                    }

                    // Apply the appropriate Windows theme
                    string themesPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "Resources", "Themes");

                    string themeFile = value ? "dark.theme" : "aero.theme";
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
                    else
                    {
                        Debug.WriteLine($"Theme file not found: {themePath}");
                    }

                    // Broadcast theme change
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

        public void RefreshWindows()
        {
            try
            {
                // Restart Explorer to ensure all UI changes take effect
                RestartExplorer();
                
                // Broadcast theme change to all windows
                BroadcastThemeChange();
                
                // Additional registry flush to ensure changes are committed
                using (var key = Registry.CurrentUser.OpenSubKey(TaskbarSettingsPath, true))
                {
                    if (key != null)
                    {
                        key.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing Windows: {ex.Message}");
            }
        }
    }
} 
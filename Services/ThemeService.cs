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
    /// <summary>
    /// Represents errors that occur during theme service operations
    /// </summary>
    public class ThemeServiceException : Exception
    {
        public ThemeServiceOperation Operation { get; }

        public ThemeServiceException(string message, ThemeServiceOperation operation, Exception innerException = null)
            : base(message, innerException)
        {
            Operation = operation;
        }
    }

    /// <summary>
    /// Defines the type of operation that caused an error
    /// </summary>
    public enum ThemeServiceOperation
    {
        RegistryAccess,
        WindowsApi,
        ProcessManagement,
        FileSystem,
        AdminPrivileges
    }

    /// <summary>
    /// Handles registry operations with proper error handling
    /// </summary>
    internal static class RegistryHelper
    {
        public static T GetValue<T>(string keyPath, string valueName, T defaultValue = default)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                var value = key?.GetValue(valueName);
                return value != null ? (T)Convert.ChangeType(value, typeof(T)) : defaultValue;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading registry value {valueName} from {keyPath}: {ex.Message}");
                throw new ThemeServiceException(
                    $"Failed to read registry value: {valueName}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        public static void SetValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true) 
                    ?? Registry.CurrentUser.CreateSubKey(keyPath);
                key.SetValue(valueName, value, valueKind);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error writing registry value {valueName} to {keyPath}: {ex.Message}");
                throw new ThemeServiceException(
                    $"Failed to write registry value: {valueName}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        public static void SetValueWithFallback(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                SetValue(keyPath, valueName, value, valueKind);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Non-critical error writing registry value {valueName}: {ex.Message}");
            }
        }
    }

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

        private void HandleError(Exception ex, string operation, bool isCritical = true)
        {
            string message = $"Error during {operation}: {ex.Message}";
            Debug.WriteLine(message);

            if (isCritical)
            {
                if (ex is ThemeServiceException)
                    throw ex;
                
                throw new ThemeServiceException(
                    message,
                    DetermineOperationType(ex),
                    ex);
            }
        }

        private ThemeServiceOperation DetermineOperationType(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException _ => ThemeServiceOperation.AdminPrivileges,
                Win32Exception _ => ThemeServiceOperation.WindowsApi,
                IOException _ => ThemeServiceOperation.FileSystem,
                _ => ThemeServiceOperation.RegistryAccess
            };
        }

        private void ShowError(string message, string title = "Error")
        {
            CustomMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool IsTaskbarCentered
        {
            get
            {
                try
                {
                    return RegistryHelper.GetValue<int>(TaskbarSettingsPath, "TaskbarAl", 1) == 1;
                }
                catch (ThemeServiceException)
                {
                    return true; // Default value if registry access fails
                }
            }
            set
            {
                try
                {
                    RegistryHelper.SetValue(TaskbarSettingsPath, "TaskbarAl", value ? 1 : 0, RegistryValueKind.DWord);
                    RestartExplorer();
                }
                catch (Exception ex)
                {
                    HandleError(ex, "setting taskbar alignment");
                }
            }
        }

        public bool IsTaskViewEnabled
        {
            get
            {
                try
                {
                    return RegistryHelper.GetValue<int>(TaskbarSettingsPath, "ShowTaskViewButton", 1) == 1;
                }
                catch (ThemeServiceException)
                {
                    return true;
                }
            }
            set
            {
                try
                {
                    RegistryHelper.SetValue(TaskbarSettingsPath, "ShowTaskViewButton", value ? 1 : 0, RegistryValueKind.DWord);
                    RestartExplorer();
                }
                catch (Exception ex)
                {
                    HandleError(ex, "setting task view");
                }
            }
        }

        private bool CheckWidgetsPolicyState()
        {
            return RegistryHelper.GetValue<int>(WidgetsPolicyPath, "TaskbarDa", 1) != 0;
        }

        private bool CheckWidgetsRegistryState()
        {
            return RegistryHelper.GetValue<int>(WidgetsPath, "WidgetsDisabled", 0) != 1;
        }

        private bool CheckFeedsState()
        {
            return RegistryHelper.GetValue<int>(FeedsPath, "ShellFeedsTaskbarViewMode", 1) != 0;
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
                    HandleError(ex, "modifying widgets settings");
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
                    HandleError(ex, "setting search visibility");
                }
            }
        }

        private void SetWallpaperStyle()
        {
            RegistryHelper.SetValue(@"Control Panel\Desktop", "WallpaperStyle", "10", RegistryValueKind.String);
            RegistryHelper.SetValue(@"Control Panel\Desktop", "TileWallpaper", "0", RegistryValueKind.String);
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
                HandleError(ex, "setting wallpaper");
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
                    HandleError(ex, "toggling desktop icons");
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
            RegistryHelper.SetValueWithFallback(AccentColorSettingsPath, "EnableTransparency", 1, RegistryValueKind.DWord);
            RegistryHelper.SetValueWithFallback(AccentColorSettingsPath, "ColorPrevalence", 0, RegistryValueKind.DWord);
            RegistryHelper.SetValueWithFallback(AccentColorSettingsPath, "AccentColor", -1, RegistryValueKind.DWord);
            RegistryHelper.SetValueWithFallback(AccentColorSettingsPath, "AccentColorInactive", -1, RegistryValueKind.DWord);
        }

        private void SetSystemTheme(bool isDarkMode)
        {
            RegistryHelper.SetValue(PersonalizePath, "SystemUsesLightTheme", isDarkMode ? 0 : 1, RegistryValueKind.DWord);
            RegistryHelper.SetValue(PersonalizePath, "AppsUseLightTheme", isDarkMode ? 0 : 1, RegistryValueKind.DWord);
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
                    return RegistryHelper.GetValue<int>(PersonalizePath, "SystemUsesLightTheme", 1) == 0;
                }
                catch (ThemeServiceException ex)
                {
                    HandleError(ex, "getting theme state", false);
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
                    BroadcastThemeChange();
                }
                catch (Exception ex)
                {
                    HandleError(ex, "setting theme");
                }
            }
        }

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        /// <summary>
        /// Contains native Win32 API method declarations and constants
        /// </summary>
        private static class NativeMethods
        {
            #region Window Message Constants
            private const int HWND_BROADCAST = 0xFFFF;
            public const int SMTO_ABORTIFHUNG = 0x0002;
            public const int SMTO_NORMAL = 0x0000;
            public const int WM_SETTINGCHANGE = 0x001A;
            public const int WM_SYSCOLORCHANGE = 0x0015;
            public const int WM_THEMECHANGE = 0x031A;
            #endregion

            #region System Parameters
            private const int SPI_SETDESKWALLPAPER = 0x0014;
            private const int SPIF_UPDATEINIFILE = 0x01;
            private const int SPIF_SENDCHANGE = 0x02;

            public static uint GetWallpaperFlags() => (uint)(SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            public static uint GetWallpaperAction() => (uint)SPI_SETDESKWALLPAPER;
            #endregion

            #region Window Management
            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
            #endregion

            #region System Parameters and Messaging
            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr SendMessageTimeout(
                IntPtr hWnd,
                int msg,
                IntPtr wParam,
                string lParam,
                int fuFlags,
                int uTimeout,
                out IntPtr lpdwResult);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SystemParametersInfo(
                uint uiAction,
                uint uiParam,
                string pvParam,
                uint fWinIni);
            #endregion

            /// <summary>
            /// Broadcasts a theme change message to all windows
            /// </summary>
            public static void BroadcastMessage(int msg, string lParam = null)
            {
                SendMessageTimeout(
                    new IntPtr(HWND_BROADCAST),
                    msg,
                    IntPtr.Zero,
                    lParam,
                    SMTO_ABORTIFHUNG | SMTO_NORMAL,
                    300,
                    out _);
            }
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
                HandleError(ex, "flushing registry changes", false);
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
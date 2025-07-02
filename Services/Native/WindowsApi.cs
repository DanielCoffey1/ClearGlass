using System;
using System.Runtime.InteropServices;

namespace ClearGlass.Services.Native
{
    /// <summary>
    /// Provides access to Windows API functions for theme operations
    /// </summary>
    internal static class WindowsApi
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

        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        public static uint GetWallpaperFlags() => (uint)(SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        public static uint GetWallpaperAction() => (uint)SPI_SETDESKWALLPAPER;
        #endregion

        #region Window Management
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        #endregion

        #region System Parameters and Messaging
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string? lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            string? lParam,
            int fuFlags,
            int uTimeout,
            out IntPtr lpdwResult);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(
            uint uiAction,
            uint uiParam,
            string? pvParam,
            uint fWinIni);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);
        #endregion

        /// <summary>
        /// Broadcasts a message to all top-level windows
        /// </summary>
        public static void BroadcastMessage(uint message, string? lParam = null)
        {
            if (lParam != null)
            {
                SendMessage(new IntPtr(HWND_BROADCAST), message, IntPtr.Zero, lParam);
            }
            else
            {
                PostMessage(new IntPtr(HWND_BROADCAST), message, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
} 
using System;
using System.Diagnostics;
using System.IO;
using ClearGlass.Services.Exceptions;
using ClearGlass.Services.Native;
using ClearGlass.Services.Registry;
using Microsoft.Win32;

namespace ClearGlass.Services.Features
{
    /// <summary>
    /// Handles Windows wallpaper-related functionality
    /// </summary>
    internal class WallpaperService
    {
        private readonly string _windowsWallpaperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Web\\Wallpaper\\Windows");

        private string LightWallpaperPath => Path.Combine(_windowsWallpaperPath, "img19.jpg"); // Light Bloom
        private string DarkWallpaperPath => Path.Combine(_windowsWallpaperPath, "img20.jpg");  // Dark Bloom

        private void SetWallpaperStyle()
        {
            try
            {
                RegistryHelper.SetValue(@"Control Panel\Desktop", "WallpaperStyle", "10", RegistryValueKind.String);
                RegistryHelper.SetValue(@"Control Panel\Desktop", "TileWallpaper", "0", RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                throw new ThemeServiceException(
                    $"Error setting wallpaper style: {ex.Message}",
                    ThemeServiceOperation.RegistryAccess,
                    ex);
            }
        }

        private void ApplyWallpaperImage(string path)
        {
            if (!WindowsApi.SystemParametersInfo(
                WindowsApi.GetWallpaperAction(),
                0,
                path,
                WindowsApi.GetWallpaperFlags()))
            {
                throw new ThemeServiceException(
                    "Failed to apply wallpaper image",
                    ThemeServiceOperation.WindowsApi);
            }
        }

        /// <summary>
        /// Sets the desktop wallpaper
        /// </summary>
        /// <param name="path">The path to the wallpaper image</param>
        public void SetWallpaper(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    throw new ThemeServiceException(
                        $"Wallpaper file not found: {path}",
                        ThemeServiceOperation.FileSystem);
                }

                SetWallpaperStyle();
                ApplyWallpaperImage(path);
            }
            catch (Exception ex) when (ex is not ThemeServiceException)
            {
                throw new ThemeServiceException(
                    $"Error setting wallpaper: {ex.Message}",
                    ThemeServiceOperation.WindowsApi,
                    ex);
            }
        }

        /// <summary>
        /// Gets the appropriate wallpaper path for the given theme
        /// </summary>
        /// <param name="isDarkMode">Whether dark mode is enabled</param>
        /// <returns>The path to the wallpaper image</returns>
        public string GetWallpaperPath(bool isDarkMode)
        {
            var path = isDarkMode ? DarkWallpaperPath : LightWallpaperPath;
            return File.Exists(path) ? path : string.Empty;
        }
    }
} 
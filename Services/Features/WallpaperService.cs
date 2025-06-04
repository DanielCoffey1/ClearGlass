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
        /// Gets the appropriate wallpaper path based on the theme mode
        /// </summary>
        public string? GetWallpaperPath(bool isDarkMode)
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
                throw new ThemeServiceException(
                    $"Error finding wallpaper path: {ex.Message}",
                    ThemeServiceOperation.FileSystem,
                    ex);
            }
        }

        /// <summary>
        /// Sets the desktop wallpaper
        /// </summary>
        public void SetWallpaper(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path), "Wallpaper path cannot be null or empty");
            }

            if (!File.Exists(path))
            {
                throw new ThemeServiceException(
                    $"Wallpaper file not found: {path}",
                    ThemeServiceOperation.FileSystem);
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
                throw new ThemeServiceException(
                    $"Error setting wallpaper: {ex.Message}",
                    ThemeServiceOperation.WindowsApi,
                    ex);
            }
        }
    }
} 
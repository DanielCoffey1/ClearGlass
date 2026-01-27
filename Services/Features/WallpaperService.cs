using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClearGlass.Services.Exceptions;
using ClearGlass.Services.Native;
using ClearGlass.Services.Registry;
using ClearGlass.Services.Reliability;
using Microsoft.Win32;

namespace ClearGlass.Services.Features
{
    /// <summary>
    /// Handles Windows wallpaper-related functionality with reliability features
    /// </summary>
    internal class WallpaperService
    {
        private const int MaxRetries = 3;
        private const string WallpaperRegistryPath = @"Control Panel\Desktop";

        private readonly string _windowsWallpaperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Web\\Wallpaper\\Windows");

        private string LightWallpaperPath => Path.Combine(_windowsWallpaperPath, "img19.jpg"); // Light Bloom
        private string DarkWallpaperPath => Path.Combine(_windowsWallpaperPath, "img20.jpg");  // Dark Bloom

        private void SetWallpaperStyle()
        {
            try
            {
                RegistryHelper.SetValue(WallpaperRegistryPath, "WallpaperStyle", "10", RegistryValueKind.String);
                RegistryHelper.SetValue(WallpaperRegistryPath, "TileWallpaper", "0", RegistryValueKind.String);
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
        /// Gets the current wallpaper path from the registry
        /// </summary>
        private string? GetCurrentWallpaperPath()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WallpaperRegistryPath);
                return key?.GetValue("WallPaper") as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Verifies that the wallpaper was set correctly
        /// </summary>
        private bool VerifyWallpaper(string expectedPath)
        {
            var currentPath = GetCurrentWallpaperPath();
            if (string.IsNullOrEmpty(currentPath))
                return false;

            return string.Equals(currentPath, expectedPath, StringComparison.OrdinalIgnoreCase);
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
        /// Sets the desktop wallpaper with retry and verification
        /// </summary>
        public async Task<OperationResult> SetWallpaperWithRetryAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
            {
                return OperationResult.Failed("Wallpaper", 0, null, $"Wallpaper file not found: {path}");
            }

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Set wallpaper style
                    SetWallpaperStyle();

                    // Apply wallpaper image
                    ApplyWallpaperImage(path);

                    // Small delay before verification
                    await Task.Delay(200, cancellationToken);

                    // Verify the wallpaper was set
                    if (VerifyWallpaper(path))
                    {
                        return OperationResult.Succeeded("Wallpaper", attempt, "Wallpaper applied and verified");
                    }

                    Debug.WriteLine($"Wallpaper verification failed on attempt {attempt}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Wallpaper attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        return OperationResult.Failed("Wallpaper", attempt, ex);
                    }
                }

                // Exponential backoff
                await Task.Delay(500 * (int)Math.Pow(2, attempt - 1), cancellationToken);
            }

            return OperationResult.Failed("Wallpaper", MaxRetries, null, "Wallpaper verification failed after retries");
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

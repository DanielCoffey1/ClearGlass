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
        private readonly LoggingService _log;

        private readonly string _windowsWallpaperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Web\\Wallpaper\\Windows");

        private string LightWallpaperPath => Path.Combine(_windowsWallpaperPath, "img19.jpg"); // Light Bloom
        private string DarkWallpaperPath => Path.Combine(_windowsWallpaperPath, "img20.jpg");  // Dark Bloom

        public WallpaperService()
        {
            _log = LoggingService.Instance;
        }

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
            _log.LogSubsection("Setting Desktop Wallpaper");
            _log.LogDetail($"Wallpaper path: {path}");

            if (!File.Exists(path))
            {
                _log.LogFailure($"Wallpaper file not found: {path}");
                return OperationResult.Failed("Wallpaper", 0, null, $"Wallpaper file not found: {path}");
            }

            var fileInfo = new FileInfo(path);
            _log.LogDetail($"File size: {fileInfo.Length / 1024} KB");

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 1)
                {
                    _log.LogRetry(attempt, MaxRetries, "Setting wallpaper");
                }

                try
                {
                    // Set wallpaper style
                    _log.LogDetail("Setting wallpaper style (Fill mode)...");
                    SetWallpaperStyle();
                    _log.LogRegistry("SET", "Control Panel\\Desktop", "WallpaperStyle", "10 (Fill)");

                    // Apply wallpaper image
                    _log.LogDetail("Applying wallpaper via SystemParametersInfo...");
                    ApplyWallpaperImage(path);
                    _log.LogWinApi("SystemParametersInfo", "SPI_SETDESKWALLPAPER called");

                    // Small delay before verification
                    _log.LogWaiting("Waiting for wallpaper to apply...");
                    await Task.Delay(200, cancellationToken);

                    // Verify the wallpaper was set
                    _log.LogDetail("Verifying wallpaper was applied correctly...");
                    if (VerifyWallpaper(path))
                    {
                        _log.LogSuccess($"Wallpaper applied and verified (attempt {attempt})");
                        return OperationResult.Succeeded("Wallpaper", attempt, "Wallpaper applied and verified");
                    }

                    var currentPath = GetCurrentWallpaperPath();
                    _log.LogFailure($"Wallpaper verification failed - expected: {path}, actual: {currentPath ?? "(null)"}");
                }
                catch (OperationCanceledException)
                {
                    _log.LogWarning("Wallpaper operation was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogFailure($"Attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetries)
                    {
                        _log.LogError($"Wallpaper setting failed after {MaxRetries} attempts", ex);
                        return OperationResult.Failed("Wallpaper", attempt, ex);
                    }
                }

                // Exponential backoff
                var backoffMs = 500 * (int)Math.Pow(2, attempt - 1);
                _log.LogWaiting($"Waiting {backoffMs}ms before retry...");
                await Task.Delay(backoffMs, cancellationToken);
            }

            _log.LogFailure($"Wallpaper verification failed after {MaxRetries} retries");
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

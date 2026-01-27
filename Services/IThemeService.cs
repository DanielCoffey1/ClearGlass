using System;
using System.Threading;
using System.Threading.Tasks;
using ClearGlass.Services.Models;
using ClearGlass.Services.Reliability;

namespace ClearGlass.Services
{
    /// <summary>
    /// Defines the contract for Windows theme management
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Gets or sets whether dark mode is enabled
        /// </summary>
        bool IsDarkMode { get; set; }

        /// <summary>
        /// Gets or sets whether the taskbar is centered
        /// </summary>
        bool IsTaskbarCentered { get; set; }

        /// <summary>
        /// Gets or sets whether the task view button is visible
        /// </summary>
        bool IsTaskViewEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether the search box is visible
        /// </summary>
        bool IsSearchVisible { get; set; }

        /// <summary>
        /// Gets or sets whether widgets are enabled
        /// </summary>
        bool AreWidgetsEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether desktop icons are visible
        /// </summary>
        bool AreDesktopIconsVisible { get; set; }

        /// <summary>
        /// Sets the desktop wallpaper
        /// </summary>
        /// <param name="path">The path to the wallpaper image</param>
        void SetWallpaper(string path);

        /// <summary>
        /// Sets the desktop wallpaper with retry and verification
        /// </summary>
        /// <param name="path">The path to the wallpaper image</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with success status and details</returns>
        Task<OperationResult> SetWallpaperAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current theme settings
        /// </summary>
        /// <returns>The current theme settings</returns>
        ThemeSettings GetCurrentSettings();

        /// <summary>
        /// Applies the specified theme settings
        /// </summary>
        /// <param name="settings">The theme settings to apply</param>
        void ApplySettings(ThemeSettings settings);

        /// <summary>
        /// Applies theme settings reliably with retry, verification, and progress reporting
        /// </summary>
        /// <param name="settings">The theme settings to apply</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result containing success status and details for each operation</returns>
        Task<ThemeApplicationResult> ApplySettingsReliableAsync(
            ThemeSettings settings,
            IProgress<ThemeApplicationProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies dark mode with retry and verification
        /// </summary>
        /// <param name="isDarkMode">Whether to enable dark mode</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with success status and details</returns>
        Task<OperationResult> ApplyDarkModeAsync(bool isDarkMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies taskbar settings asynchronously with retry, verification, and Explorer restart safeguards
        /// </summary>
        Task<OperationResult> ApplyTaskbarSettingAsync(
            bool? isTaskbarCentered = null,
            bool? isTaskViewEnabled = null,
            bool? isSearchVisible = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies desktop icons visibility asynchronously with retry and verification
        /// </summary>
        Task<OperationResult> ApplyDesktopIconsAsync(bool visible, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes all Windows UI elements
        /// </summary>
        void RefreshWindows();
    }
}

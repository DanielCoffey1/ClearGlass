using ClearGlass.Services.Models;

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
        /// Refreshes all Windows UI elements
        /// </summary>
        void RefreshWindows();
    }
} 
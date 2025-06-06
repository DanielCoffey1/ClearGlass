namespace ClearGlass.Services.Models
{
    /// <summary>
    /// Represents the current theme settings for Windows
    /// </summary>
    public class ThemeSettings
    {
        /// <summary>
        /// Gets or sets whether dark mode is enabled
        /// </summary>
        public bool IsDarkMode { get; set; }

        /// <summary>
        /// Gets or sets whether the taskbar is centered
        /// </summary>
        public bool IsTaskbarCentered { get; set; }

        /// <summary>
        /// Gets or sets whether the task view button is visible
        /// </summary>
        public bool IsTaskViewEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether widgets are enabled
        /// </summary>
        public bool AreWidgetsEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether the search box is visible
        /// </summary>
        public bool IsSearchVisible { get; set; }

        /// <summary>
        /// Gets or sets whether desktop icons are visible
        /// </summary>
        public bool AreDesktopIconsVisible { get; set; }

        /// <summary>
        /// Gets or sets the current wallpaper path
        /// </summary>
        public string? WallpaperPath { get; set; }
    }

    /// <summary>
    /// Represents accent color settings for Windows theme
    /// </summary>
    public class AccentColorSettings
    {
        /// <summary>
        /// Gets or sets whether transparency effects are enabled
        /// </summary>
        public bool EnableTransparency { get; set; }

        /// <summary>
        /// Gets or sets whether color prevalence is enabled
        /// </summary>
        public bool ColorPrevalence { get; set; }

        /// <summary>
        /// Gets or sets the accent color value
        /// </summary>
        public int AccentColor { get; set; }

        /// <summary>
        /// Gets or sets the inactive window accent color value
        /// </summary>
        public int AccentColorInactive { get; set; }
    }
} 
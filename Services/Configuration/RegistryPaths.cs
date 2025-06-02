namespace ClearGlass.Services.Configuration
{
    public static class RegistryPaths
    {
        // Explorer and Taskbar settings
        public const string TaskbarSettings = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        public const string SearchSettings = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search";
        
        // Widgets settings
        public const string WidgetsPolicy = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        public const string Widgets = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Widgets";
        public const string Feeds = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Feeds";
        public const string WebWidgets = @"SOFTWARE\Policies\Microsoft\Dsh";
        public const string WidgetsGPO = @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds";
        
        // Desktop and Theme settings
        public const string DesktopIcons = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        public const string Personalize = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        public const string AccentColor = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\History";
        public const string AccentColorSettings = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        public const string Theme = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes";
        public const string CurrentTheme = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";

        // Registry value names
        public static class Values
        {
            // Taskbar
            public const string TaskbarAlignment = "TaskbarAl";
            public const string TaskViewButton = "ShowTaskViewButton";
            public const string SearchIcon = "SearchboxTaskbarMode";
            
            // Widgets
            public const string TaskbarWidgets = "TaskbarDa";
            public const string WidgetsDisabled = "WidgetsDisabled";
            public const string ShellFeedsMode = "ShellFeedsTaskbarViewMode";
            
            // Theme
            public const string SystemUsesLightTheme = "SystemUsesLightTheme";
            public const string AppsUseLightTheme = "AppsUseLightTheme";
            public const string ColorPrevalence = "ColorPrevalence";
            
            // Desktop
            public const string HideDesktopIcons = "HideIcons";
        }
    }
} 
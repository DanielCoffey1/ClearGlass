using System.Collections.Generic;

namespace ClearGlass.Services.Configuration
{
    public static class AppConfig
    {
        public static class EssentialApps
        {
            public static readonly string[] Default = new[]
            {
                "Microsoft.Windows.ShellExperienceHost",
                "Microsoft.Windows.StartMenuExperienceHost",
                "Microsoft.Windows.Cortana",
                "Microsoft.WindowsStore",
                "Microsoft.AAD.BrokerPlugin",
                "Microsoft.AccountsControl",
                "Microsoft.Windows.Photos",
                "Microsoft.WindowsNotepad",
                "Microsoft.ScreenSketch",
                "Microsoft.WindowsCalculator",
                "Microsoft.Windows.SecHealthUI",
                "Microsoft.MicrosoftEdge",
                "Microsoft.WindowsTerminal",
                "Microsoft.WindowsSoundRecorder",
                "Microsoft.WindowsCamera",
                "Microsoft.WindowsAlarms",
                "Microsoft.WindowsMaps",
                "Microsoft.WindowsFeedbackHub",
                "Microsoft.GetHelp",
                "Microsoft.Windows.CloudExperienceHost",
                "Microsoft.Win32WebViewHost",
                "Microsoft.UI.Xaml",
                "Microsoft.VCLibs",
                "Microsoft.Services.Store.Engagement",
                "Microsoft.NET"
            };
        }

        public static class Paths
        {
            public static string GetWallpaperPath(bool isDarkMode) =>
                System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows),
                    "Web", "Wallpaper", "Windows",
                    isDarkMode ? "img20.jpg" : "img19.jpg");

            public static string AutologonPath =>
                System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                    "ClearGlass", "Tools", "Autologon.exe");

            public static string CustomWallpaperPath =>
                System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "ClearGlass", "Wallpapers", "glassbackground.png");
        }

        public static class PowerShellScripts
        {
            public const string GetInstalledApps = @"
                Get-AppxPackage -AllUsers | Select-Object Name, PackageFullName | ConvertTo-Json
            ";

            public const string CreateRestorePoint = @"
                # Suppress warnings
                $ProgressPreference = 'SilentlyContinue'
                $WarningPreference = 'SilentlyContinue'

                # Create Restore Point
                Write-Host 'Creating system restore point...' -ForegroundColor Cyan
                try {
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                    Enable-ComputerRestore -Drive 'C:\' -ErrorAction SilentlyContinue
                    Checkpoint-Computer -Description 'Before ClearGlass Bloatware Removal' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction SilentlyContinue
                    Write-Host 'Restore point created successfully' -ForegroundColor Green
                } catch {
                    Write-Host 'Could not create restore point. Continuing...' -ForegroundColor Yellow
                }
            ";
        }
    }
} 
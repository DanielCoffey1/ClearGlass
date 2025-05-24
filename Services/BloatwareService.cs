using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.ObjectModel;
using ClearGlass.Models;
using System.Linq;
using System.Collections.Generic;

namespace ClearGlass.Services
{
    public class BloatwareService
    {
        private readonly string[] defaultEssentialApps = new[]
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
            "Microsoft.Windows.SecHealthUI", // Windows Security
            "Microsoft.MicrosoftEdge", // Edge is often required for Windows Updates
            "Microsoft.WindowsTerminal",
            "Microsoft.WindowsSoundRecorder",
            "Microsoft.WindowsCamera",
            "Microsoft.WindowsAlarms",
            "Microsoft.WindowsMaps",
            "Microsoft.WindowsFeedbackHub", // Useful for reporting Windows issues
            "Microsoft.GetHelp", // Windows Help app
            "Microsoft.Windows.CloudExperienceHost", // Required for Windows Hello and other features
            "Microsoft.Win32WebViewHost", // Required for various Windows components
            "Microsoft.UI.Xaml", // Required UI framework
            "Microsoft.VCLibs", // Visual C++ Runtime
            "Microsoft.Services.Store.Engagement", // Required for Store
            "Microsoft.NET" // .NET Runtime
        };

        private List<string> _sessionEssentialApps = new();

        public BloatwareService()
        {
            // Initialize session list with default values
            ResetToDefaultEssentialApps();
        }

        public void ResetToDefaultEssentialApps()
        {
            _sessionEssentialApps = new List<string>(defaultEssentialApps);
        }

        public void UpdateSessionEssentialApps(IEnumerable<WindowsApp> selectedApps)
        {
            // Start with the default essential apps
            _sessionEssentialApps = new List<string>(defaultEssentialApps);

            // Add any newly selected apps that aren't already in the list
            foreach (var app in selectedApps.Where(a => a.IsSelected))
            {
                if (!_sessionEssentialApps.Contains(app.Name))
                {
                    _sessionEssentialApps.Add(app.Name);
                }
            }
        }

        public IReadOnlyList<string> EssentialApps => _sessionEssentialApps;

        public async Task<ObservableCollection<WindowsApp>> GetInstalledApps()
        {
            var apps = new ObservableCollection<WindowsApp>();

            try
            {
                string script = @"
                    Get-AppxPackage -AllUsers | Select-Object Name, PackageFullName | ConvertTo-Json
                ";

                // Save the script to a temporary file
                string scriptPath = Path.Combine(Path.GetTempPath(), "GetInstalledApps.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                // Run PowerShell with elevated privileges
                var startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new InvalidOperationException("Failed to start PowerShell process");
                    }

                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        var appList = System.Text.Json.JsonSerializer.Deserialize<List<WindowsApp>>(output);
                        if (appList != null)
                        {
                            foreach (var app in appList.OrderBy(a => a.Name))
                            {
                                app.DisplayName = GetDisplayName(app.Name);
                                app.IsSelected = defaultEssentialApps.Any(e => app.Name.StartsWith(e, StringComparison.OrdinalIgnoreCase));
                                apps.Add(app);
                            }
                        }
                    }
                }

                // Clean up the temporary script file
                File.Delete(scriptPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error getting installed apps: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return apps;
        }

        private string GetDisplayName(string appName)
        {
            // Remove Microsoft. prefix if present
            if (appName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            {
                appName = appName.Substring("Microsoft.".Length);
            }

            // Split on dots and PascalCase
            var parts = System.Text.RegularExpressions.Regex.Split(appName, @"(?<!^)(?=[A-Z])|[.]")
                .Where(p => !string.IsNullOrWhiteSpace(p));

            // Join with spaces
            return string.Join(" ", parts);
        }

        public async Task RemoveWindowsBloatware(IEnumerable<WindowsApp> appsToKeep)
        {
            try
            {
                MessageBox.Show(
                    "Starting Windows bloatware removal. This will remove unnecessary Windows apps while keeping selected ones.\n\n" +
                    "McAfee will also be removed if it is installed.\n\n" +
                    "A system restore point will be created before making changes.",
                    "Starting Bloatware Removal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                var appsToKeepNames = appsToKeep.Where(a => a.IsSelected).Select(a => a.Name).ToList();
                string script = @"
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
                        Write-Host 'Could not create restore point. Continuing with bloatware removal...' -ForegroundColor Yellow
                    }

                    # Check for McAfee and remove it
                    Write-Host 'Checking for McAfee products...' -ForegroundColor Cyan
                    
                    # Method 1: Check using WMI
                    $mcafeeProducts = Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -like '*McAfee*' }
                    
                    # Method 2: Check Program Files directories
                    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
                    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
                    
                    $mcafeePaths = @(
                        ""$programFiles\McAfee"",
                        ""$programFilesX86\McAfee"",
                        ""$programFiles\Common Files\McAfee"",
                        ""$programFilesX86\Common Files\McAfee""
                    )
                    
                    # Method 3: Check registry uninstall keys
                    $uninstallKeys = @(
                        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
                    )
                    
                    $mcafeeFound = $false
                    
                    # Check WMI results
                    if ($mcafeeProducts) {
                        $mcafeeFound = $true
                        Write-Host 'McAfee products found via WMI. Attempting removal...' -ForegroundColor Yellow
                        foreach ($product in $mcafeeProducts) {
                            try {
                                Write-Host ""Removing $($product.Name)..."" -ForegroundColor Cyan
                                $product.Uninstall()
                                Write-Host ""Successfully removed $($product.Name)"" -ForegroundColor Green
                            } catch {
                                Write-Host ""Failed to remove $($product.Name): $($_.Exception.Message)"" -ForegroundColor Red
                            }
                        }
                    }
                    
                    # Check Program Files
                    foreach ($path in $mcafeePaths) {
                        if (Test-Path $path) {
                            $mcafeeFound = $true
                            Write-Host ""Found McAfee installation at: $path"" -ForegroundColor Yellow
                            
                            # Look for uninstaller executables
                            $uninstallers = Get-ChildItem -Path $path -Recurse -Include @('uninst.exe', 'uninstall.exe', 'mcuninstall.exe', 'FWUninstaller.exe') -ErrorAction SilentlyContinue
                            foreach ($uninstaller in $uninstallers) {
                                try {
                                    Write-Host ""Running uninstaller: $($uninstaller.FullName)"" -ForegroundColor Cyan
                                    Start-Process -FilePath $uninstaller.FullName -ArgumentList '/SILENT' -Wait
                                    Write-Host 'Uninstaller completed' -ForegroundColor Green
                                } catch {
                                    Write-Host ""Failed to run uninstaller: $($_.Exception.Message)"" -ForegroundColor Red
                                }
                            }
                        }
                    }
                    
                    # Check registry uninstall keys
                    foreach ($key in $uninstallKeys) {
                        if (Test-Path $key) {
                            Get-ChildItem $key | ForEach-Object {
                                $uninstallString = $null
                                try {
                                    $uninstallString = (Get-ItemProperty -Path $_.PSPath).UninstallString
                                    $displayName = (Get-ItemProperty -Path $_.PSPath).DisplayName
                                    
                                    if ($displayName -like '*McAfee*' -and $uninstallString) {
                                        $mcafeeFound = $true
                                        Write-Host ""Found McAfee product in registry: $displayName"" -ForegroundColor Yellow
                                        
                                        # Extract the executable path and any arguments
                                        if ($uninstallString -match '^""([^""]+)""(.*)') {
                                            $exePath = $matches[1]
                                            $args = $matches[2]
                                        } else {
                                            $exePath = $uninstallString
                                            $args = ''
                                        }
                                        
                                        if (Test-Path $exePath) {
                                            try {
                                                Write-Host ""Running uninstaller: $exePath"" -ForegroundColor Cyan
                                                if ($args) {
                                                    Start-Process -FilePath $exePath -ArgumentList ""$args /SILENT"" -Wait
                                                } else {
                                                    Start-Process -FilePath $exePath -ArgumentList '/SILENT' -Wait
                                                }
                                                Write-Host 'Uninstaller completed' -ForegroundColor Green
                                            } catch {
                                                Write-Host ""Failed to run uninstaller: $($_.Exception.Message)"" -ForegroundColor Red
                                            }
                                        }
                                    }
                                } catch {
                                    # Continue to next registry key if there's an error
                                    continue
                                }
                            }
                        }
                    }
                    
                    if ($mcafeeFound) {
                        # Additional cleanup for McAfee services
                        Write-Host 'Cleaning up McAfee services...' -ForegroundColor Cyan
                        $mcafeeServices = Get-Service | Where-Object { $_.Name -like '*McAfee*' -or $_.DisplayName -like '*McAfee*' }
                        foreach ($service in $mcafeeServices) {
                            try {
                                Stop-Service -Name $service.Name -Force -ErrorAction SilentlyContinue
                                Set-Service -Name $service.Name -StartupType Disabled -ErrorAction SilentlyContinue
                                Write-Host ""Disabled service: $($service.DisplayName)"" -ForegroundColor Green
                            } catch {
                                Write-Host ""Failed to disable service $($service.DisplayName)"" -ForegroundColor Red
                            }
                        }

                        # Remove McAfee registry entries
                        Write-Host 'Cleaning up McAfee registry entries...' -ForegroundColor Cyan
                        $registryPaths = @(
                            'HKLM:\SOFTWARE\McAfee',
                            'HKLM:\SOFTWARE\Wow6432Node\McAfee',
                            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\McAfee*',
                            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\McAfee*'
                        )

                        foreach ($path in $registryPaths) {
                            if (Test-Path $path) {
                                try {
                                    Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
                                    Write-Host ""Removed registry entries: $path"" -ForegroundColor Green
                                } catch {
                                    Write-Host ""Failed to remove registry entries: $path"" -ForegroundColor Red
                                }
                            }
                        }
                        
                        Write-Host 'McAfee removal process completed. A system restart may be required.' -ForegroundColor Green
                    } else {
                        Write-Host 'No McAfee products were detected on the system.' -ForegroundColor Green
                    }

                    Write-Host 'Starting bloatware removal...' -ForegroundColor Cyan
                    
                    # Get all installed UWP apps for all users
                    $apps = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue | Where-Object {
                        $app = $_.Name
                        $keep = @('" + string.Join("','", appsToKeepNames) + @"')
                        -not ($keep -contains $app)
                    }

                    $total = @($apps).Count
                    $current = 0
                    $removed = 0
                    $skipped = 0
                    $essential = 0

                    foreach ($app in $apps) {
                        $current++
                        $percentComplete = [math]::Round(($current / $total) * 100)
                        Write-Host ""[$percentComplete%] Processing $($app.Name)..."" -ForegroundColor Cyan -NoNewline

                        try {
                            Remove-AppxPackage -Package $app.PackageFullName -AllUsers -ErrorAction Stop | Out-Null
                            Write-Host "" Removed"" -ForegroundColor Green
                            $removed++
                        } catch {
                            # Check if this is a system app
                            if ($app.NonRemovable -or $app.Name.StartsWith('Windows') -or $app.Name.StartsWith('Microsoft.Windows') -or $app.Name.StartsWith('Microsoft.UI') -or $app.Name.StartsWith('Microsoft.NET')) {
                                Write-Host "" Skipped - System Required"" -ForegroundColor Green
                                $essential++
                            } else {
                                Write-Host "" Skipped - Protected Component"" -ForegroundColor Yellow
                                $skipped++
                            }
                        }
                    }

                    Write-Host ""`nBloatware removal completed!"" -ForegroundColor Green
                    Write-Host ""Total apps processed: $total"" -ForegroundColor Cyan
                    Write-Host ""Successfully removed: $removed"" -ForegroundColor Green
                    Write-Host ""System required: $essential"" -ForegroundColor Green
                    if ($skipped -gt 0) {
                        Write-Host ""Protected components: $skipped"" -ForegroundColor Yellow
                    }
                ";

                // Save the script to a temporary file
                string scriptPath = Path.Combine(Path.GetTempPath(), "ClearGlassBloatwareRemoval.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                // Run PowerShell with elevated privileges
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    RedirectStandardOutput = false
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new InvalidOperationException("Failed to start PowerShell process");
                    }

                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        MessageBox.Show(
                            "Windows bloatware has been successfully removed while keeping selected apps!\n\n" +
                            "Some apps may require a system restart to be fully removed.",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Some apps may not have been removed successfully. Please check the PowerShell window for details.",
                            "Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                // Clean up the temporary script file
                File.Delete(scriptPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error during bloatware removal: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task RemoveWindowsBloatware()
        {
            var apps = await GetInstalledApps();
            await RemoveWindowsBloatware(apps);
        }
    }
} 
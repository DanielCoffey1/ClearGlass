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
                CustomMessageBox.Show(
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
                CustomMessageBox.Show(
                    "Starting Windows bloatware removal. This will remove unnecessary Windows apps while keeping selected ones.\n\n" +
                    "McAfee and Norton products will also be removed if they are installed.\n\n" +
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

                    # Check for Norton and Norton 360 and remove them
                    Write-Host 'Checking for Norton products...' -ForegroundColor Cyan
                    
                    # Method 1: Check using WMI
                    $nortonProducts = Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -like '*Norton*' }
                    
                    # Method 2: Check Program Files directories
                    $nortonPaths = @(
                        ""$programFiles\Norton"",
                        ""$programFilesX86\Norton"",
                        ""$programFiles\NortonInstaller"",
                        ""$programFilesX86\NortonInstaller"",
                        ""$programFiles\Norton 360"",
                        ""$programFilesX86\Norton 360"",
                        ""$programFiles\Common Files\Symantec Shared"",
                        ""$programFilesX86\Common Files\Symantec Shared""
                    )
                    
                    $nortonFound = $false
                    
                    # Check WMI results
                    if ($nortonProducts) {
                        $nortonFound = $true
                        Write-Host 'Norton products found via WMI. Attempting removal...' -ForegroundColor Yellow
                        foreach ($product in $nortonProducts) {
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
                    foreach ($path in $nortonPaths) {
                        if (Test-Path $path) {
                            $nortonFound = $true
                            Write-Host ""Found Norton installation at: $path"" -ForegroundColor Yellow
                            
                            # Look for uninstaller executables
                            $uninstallers = Get-ChildItem -Path $path -Recurse -Include @('Uninstall.exe', 'InstallWizard.exe', 'Remove.exe', 'SymSetup.exe') -ErrorAction SilentlyContinue
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
                                    
                                    if (($displayName -like '*Norton*' -or $displayName -like '*Symantec*') -and $uninstallString) {
                                        $nortonFound = $true
                                        Write-Host ""Found Norton product in registry: $displayName"" -ForegroundColor Yellow
                                        
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
                    
                    if ($nortonFound) {
                        # Additional cleanup for Norton services
                        Write-Host 'Cleaning up Norton services...' -ForegroundColor Cyan
                        $nortonServices = Get-Service | Where-Object { $_.Name -like '*Norton*' -or $_.DisplayName -like '*Norton*' -or $_.Name -like '*Symantec*' -or $_.DisplayName -like '*Symantec*' }
                        foreach ($service in $nortonServices) {
                            try {
                                Stop-Service -Name $service.Name -Force -ErrorAction SilentlyContinue
                                Set-Service -Name $service.Name -StartupType Disabled -ErrorAction SilentlyContinue
                                Write-Host ""Disabled service: $($service.DisplayName)"" -ForegroundColor Green
                            } catch {
                                Write-Host ""Failed to disable service $($service.DisplayName)"" -ForegroundColor Red
                            }
                        }

                        # Remove Norton registry entries
                        Write-Host 'Cleaning up Norton registry entries...' -ForegroundColor Cyan
                        $registryPaths = @(
                            'HKLM:\SOFTWARE\Norton',
                            'HKLM:\SOFTWARE\Wow6432Node\Norton',
                            'HKLM:\SOFTWARE\Symantec',
                            'HKLM:\SOFTWARE\Wow6432Node\Symantec',
                            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Norton*',
                            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Symantec*',
                            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Norton*',
                            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Symantec*'
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
                        
                        Write-Host 'Norton removal process completed. A system restart may be required.' -ForegroundColor Green
                    } else {
                        Write-Host 'No Norton products were detected on the system.' -ForegroundColor Green
                    }

                    Write-Host 'Starting bloatware removal...' -ForegroundColor Cyan
                    
                    # Check for Microsoft 365/OneNote
                    Write-Host 'Checking for Microsoft 365/Office/OneNote...' -ForegroundColor Cyan
                    
                    # Check Program Files location
                    $officePath = ""C:\Program Files\Microsoft Office""
                    if (Test-Path $officePath) {
                        Write-Host ""Found Microsoft Office installation in Program Files. Attempting to remove..."" -ForegroundColor Yellow
                        
                        # Try using Office Setup for both Office and OneNote
                        $setupPath = Join-Path $officePath ""Office16\setup.exe""
                        if (Test-Path $setupPath) {
                            try {
                                Write-Host ""Running Office setup.exe uninstaller for Office..."" -ForegroundColor Cyan
                                Start-Process -FilePath $setupPath -ArgumentList ""/uninstall ProPlus /config C:\Windows\Temp\uninstall.xml"" -Wait -NoNewWindow
                                Write-Host ""Successfully initiated Office uninstallation"" -ForegroundColor Green
                                
                                Write-Host ""Running Office setup.exe uninstaller for OneNote..."" -ForegroundColor Cyan
                                Start-Process -FilePath $setupPath -ArgumentList ""/uninstall OneNote /config C:\Windows\Temp\uninstall.xml"" -Wait -NoNewWindow
                                Write-Host ""Successfully initiated OneNote uninstallation"" -ForegroundColor Green
                                $removed++
                            } catch {
                                Write-Host ""Failed to run Office/OneNote uninstaller: $($_.Exception.Message)"" -ForegroundColor Red
                                $skipped++
                            }
                        }

                        # Also try using the Office Deployment Tool
                        $odtPath = Join-Path $officePath ""deployment\setup.exe""
                        if (Test-Path $odtPath) {
                            try {
                                Write-Host ""Running Office Deployment Tool uninstaller..."" -ForegroundColor Cyan
                                Start-Process -FilePath $odtPath -ArgumentList ""/configure"", ""C:\Windows\Temp\uninstall.xml"" -Wait -NoNewWindow
                                Write-Host ""Successfully initiated ODT uninstallation"" -ForegroundColor Green
                                $removed++
                            } catch {
                                Write-Host ""Failed to run ODT: $($_.Exception.Message)"" -ForegroundColor Red
                                $skipped++
                            }
                        }
                    }

                    # Check for Store versions
                    $microsoft365 = Get-AppxPackage -Name ""*Microsoft.Office.Desktop*"" -AllUsers -ErrorAction SilentlyContinue
                    $oneNote = Get-AppxPackage -Name ""*Microsoft.Office.OneNote*"" -AllUsers -ErrorAction SilentlyContinue
                    
                    if ($microsoft365) {
                        try {
                            Write-Host ""Found Microsoft 365 Store version. Attempting to remove..."" -ForegroundColor Yellow
                            Remove-AppxPackage -Package $microsoft365.PackageFullName -AllUsers -ErrorAction Stop
                            Write-Host ""Successfully removed Microsoft 365 Store version"" -ForegroundColor Green
                            $removed++
                        } catch {
                            Write-Host ""Failed to remove Microsoft 365 Store version: $($_.Exception.Message)"" -ForegroundColor Red
                            $skipped++
                        }
                    }

                    if ($oneNote) {
                        try {
                            Write-Host ""Found OneNote Store version. Attempting to remove..."" -ForegroundColor Yellow
                            Remove-AppxPackage -Package $oneNote.PackageFullName -AllUsers -ErrorAction Stop
                            Write-Host ""Successfully removed OneNote Store version"" -ForegroundColor Green
                            $removed++
                        } catch {
                            Write-Host ""Failed to remove OneNote Store version: $($_.Exception.Message)"" -ForegroundColor Red
                            $skipped++
                        }
                    }

                    # Try registry uninstall strings
                    Write-Host ""Checking registry for Office/OneNote uninstall strings..."" -ForegroundColor Cyan
                    $uninstallKeys = @(
                        ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"",
                        ""HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall""
                    )

                    foreach ($key in $uninstallKeys) {
                        if (Test-Path $key) {
                            Get-ChildItem $key | ForEach-Object {
                                try {
                                    $displayName = (Get-ItemProperty -Path $_.PSPath).DisplayName
                                    if ($displayName -like ""*Microsoft 365*"" -or $displayName -like ""*Office*"" -or $displayName -like ""*OneNote*"") {
                                        $uninstallString = (Get-ItemProperty -Path $_.PSPath).UninstallString
                                        if ($uninstallString) {
                                            Write-Host ""Found product in registry: $displayName"" -ForegroundColor Yellow
                                            
                                            if ($uninstallString -match '^""([^""]+)""(.*)') {
                                                $exePath = $matches[1]
                                                $args = $matches[2]
                                            } else {
                                                $exePath = $uninstallString
                                                $args = """"
                                            }
                                            
                                            if (Test-Path $exePath) {
                                                try {
                                                    Write-Host ""Running uninstaller: $exePath"" -ForegroundColor Cyan
                                                    Start-Process -FilePath $exePath -ArgumentList $args -Wait -NoNewWindow
                                                    Write-Host ""Successfully ran uninstaller"" -ForegroundColor Green
                                                    $removed++
                                                } catch {
                                                    Write-Host ""Failed to run uninstaller: $($_.Exception.Message)"" -ForegroundColor Red
                                                    $skipped++
                                                }
                                            }
                                        }
                                    }
                                } catch {
                                    continue
                                }
                            }
                        }
                    }

                    # Also try to remove via winget as a last resort
                    Write-Host ""Checking for Microsoft 365/OneNote via winget..."" -ForegroundColor Cyan
                    
                    # Check for Office
                    $wingetCheck = winget list --id ""Microsoft.Office"" --exact --accept-source-agreements 2>&1
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host ""Found Microsoft 365 via winget. Attempting to remove..."" -ForegroundColor Yellow
                        winget uninstall --id ""Microsoft.Office"" --silent --accept-source-agreements 2>&1
                        if ($LASTEXITCODE -eq 0) {
                            Write-Host ""Successfully removed Microsoft 365 via winget"" -ForegroundColor Green
                            $removed++
                        } else {
                            Write-Host ""Failed to remove Microsoft 365 via winget"" -ForegroundColor Red
                            $skipped++
                        }
                    }

                    # Check for OneNote
                    $wingetCheck = winget list --id ""Microsoft.Office.OneNote"" --exact --accept-source-agreements 2>&1
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host ""Found OneNote via winget. Attempting to remove..."" -ForegroundColor Yellow
                        winget uninstall --id ""Microsoft.Office.OneNote"" --silent --accept-source-agreements 2>&1
                        if ($LASTEXITCODE -eq 0) {
                            Write-Host ""Successfully removed OneNote via winget"" -ForegroundColor Green
                            $removed++
                        } else {
                            Write-Host ""Failed to remove OneNote via winget"" -ForegroundColor Red
                            $skipped++
                        }
                    }

                    # Check for Windows Copilot and its preview feature
                    Write-Host 'Checking for Windows Copilot and Preview...' -ForegroundColor Cyan
                    
                    # Remove Copilot app package
                    $copilotApp = Get-AppxPackage -Name ""Microsoft.Windows.Copilot"" -AllUsers -ErrorAction SilentlyContinue
                    if ($copilotApp) {
                        try {
                            Write-Host ""Found Windows Copilot app. Attempting to remove..."" -ForegroundColor Yellow
                            Remove-AppxPackage -Package $copilotApp.PackageFullName -AllUsers -ErrorAction Stop
                            Write-Host ""Successfully removed Windows Copilot app"" -ForegroundColor Green
                            $removed++
                        } catch {
                            Write-Host ""Failed to remove Windows Copilot app: $($_.Exception.Message)"" -ForegroundColor Red
                            $skipped++
                        }
                    } else {
                        Write-Host ""Windows Copilot app not found on the system"" -ForegroundColor Green
                    }

                    # Disable Copilot Preview in taskbar
                    Write-Host ""Disabling Windows Copilot Preview in taskbar..."" -ForegroundColor Cyan
                    try {
                        # Create/modify registry keys to disable Copilot
                        $registryPaths = @(
                            'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced',
                            'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot'
                        )

                        foreach ($path in $registryPaths) {
                            if (-not (Test-Path $path)) {
                                New-Item -Path $path -Force | Out-Null
                            }
                        }

                        # Disable Copilot button in taskbar
                        Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowCopilotButton' -Value 0 -Type DWord -Force
                        
                        # Disable Copilot via policy
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot' -Name 'TurnOffWindowsCopilot' -Value 1 -Type DWord -Force

                        # Restart Explorer to apply changes
                        Get-Process -Name explorer | Stop-Process -Force
                        Write-Host ""Successfully disabled Windows Copilot Preview in taskbar"" -ForegroundColor Green
                    } catch {
                        Write-Host ""Failed to disable Windows Copilot Preview: $($_.Exception.Message)"" -ForegroundColor Red
                    }

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
                        CustomMessageBox.Show(
                            "Windows bloatware has been successfully removed while keeping selected apps!\n\n" +
                            "Some apps may require a system restart to be fully removed.",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        CustomMessageBox.Show(
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
                CustomMessageBox.Show(
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
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace ClearGlass.Services
{
    public class OptimizationService
    {
        public async Task TweakWindowsSettings()
        {
            try
            {
                // Create restore point
                await CreateRestorePoint();

                // Run PowerShell commands with elevated privileges
                string script = @"
                    Write-Host 'Creating system restore point...'
                    # Create Restore Point (with override for 24-hour limitation)
                    try {
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -Force
                        Enable-ComputerRestore -Drive 'C:\'
                        Checkpoint-Computer -Description 'Before ClearGlass Optimization' -RestorePointType 'MODIFY_SETTINGS'
                        Write-Host 'Restore point created successfully'
                    } catch {
                        Write-Warning 'Could not create restore point. Continuing with optimization...'
                    }

                    Write-Host 'Cleaning temporary files...'
                    # Delete temporary files (skip locked files)
                    Get-ChildItem -Path 'C:\Windows\Temp\*' -File -Force | ForEach-Object {
                        try {
                            Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
                        } catch {}
                    }
                    Get-ChildItem -Path $env:TEMP\* -File -Force | ForEach-Object {
                        try {
                            Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
                        } catch {}
                    }
                    Write-Host 'Temporary files cleaned'

                    Write-Host 'Configuring Windows settings...'
                    # Disable Consumer Features
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Name 'DisableWindowsConsumerFeatures' -Value 1 -Type DWord -Force
                    Write-Host 'Consumer features disabled'

                    Write-Host 'Disabling telemetry...'
                    # Disable Telemetry
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                    Write-Host 'Telemetry disabled'

                    Write-Host 'Disabling activity history...'
                    # Disable Activity History
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -Value 0 -Type DWord -Force
                    Write-Host 'Activity history disabled'

                    Write-Host 'Configuring Explorer settings...'
                    # Disable Explorer Automatic Folder Type Discovery
                    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'AutoCheckSelect' -Value 0 -Type DWord -Force
                    Write-Host 'Explorer settings configured'

                    Write-Host 'Disabling GameDVR...'
                    # Disable GameDVR
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Name 'AllowGameDVR' -Value 0 -Type DWord -Force
                    Write-Host 'GameDVR disabled'

                    Write-Host 'Disabling hibernation...'
                    # Disable Hibernation
                    powercfg /hibernate off
                    Write-Host 'Hibernation disabled'

                    Write-Host 'Disabling location tracking...'
                    # Disable Location Tracking
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}' -Name 'SensorPermissionState' -Value 0 -Type DWord -Force
                    New-Item -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration' -Name 'Status' -Value 0 -Type DWord -Force
                    Write-Host 'Location tracking disabled'

                    Write-Host 'Disabling Storage Sense...'
                    # Disable Storage Sense
                    Remove-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' -Recurse -ErrorAction SilentlyContinue
                    Write-Host 'Storage Sense disabled'

                    Write-Host 'Disabling Wi-Fi Sense...'
                    # Disable Wi-Fi Sense
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config' -Name 'AutoConnectAllowedOEM' -Value 0 -Type DWord -Force
                    Write-Host 'Wi-Fi Sense disabled'

                    Write-Host 'Configuring taskbar settings...'
                    # Enable End Task with Right Click
                    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarRightClickMenu' -Value 1 -Type DWord -Force
                    Write-Host 'Taskbar settings configured'

                    Write-Host 'Running disk cleanup...'
                    # Run Disk Cleanup silently
                    Start-Process -FilePath cleanmgr -ArgumentList '/sagerun:1' -NoNewWindow -Wait
                    Write-Host 'Disk cleanup completed'

                    Write-Host 'Disabling PowerShell telemetry...'
                    # Disable PowerShell 7 Telemetry
                    [Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', '1', 'Machine')
                    Write-Host 'PowerShell telemetry disabled'

                    Write-Host 'Configuring ReCall settings...'
                    # Check and Disable ReCall if installed
                    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\ReCall') {
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\ReCall' -Name 'Enabled' -Value 0 -Type DWord -Force
                        Write-Host 'ReCall disabled'
                    }

                    Write-Host 'Configuring services...'
                    # Set Services to Manual (only if they exist)
                    $servicesToManual = @(
                        'DiagTrack',                     # Connected User Experiences and Telemetry
                        'dmwappushservice',              # Device Management Wireless Application Protocol
                        'lfsvc',                         # Geolocation Service
                        'MapsBroker',                    # Downloaded Maps Manager
                        'NetTcpPortSharing',             # Net.Tcp Port Sharing Service
                        'RemoteAccess',                  # Routing and Remote Access
                        'RemoteRegistry',                # Remote Registry
                        'SharedAccess',                  # Internet Connection Sharing
                        'TrkWks',                        # Distributed Link Tracking Client
                        'WbioSrvc',                      # Windows Biometric Service
                        'WMPNetworkSvc',                 # Windows Media Player Network Sharing Service
                        'WSearch'                        # Windows Search
                    )

                    foreach ($service in $servicesToManual) {
                        if (Get-Service -Name $service -ErrorAction SilentlyContinue) {
                            try {
                                Write-Host ""Configuring service: $service...""
                                Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
                                Set-Service -Name $service -StartupType Manual -ErrorAction SilentlyContinue
                                Write-Host ""Service $service configured""
                            } catch {
                                Write-Warning ""Could not configure service: $service""
                            }
                        }
                    }
                    Write-Host 'Services configured'

                    Write-Host 'Resetting restore point settings...'
                    # Reset the restore point creation frequency back to default
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 1440 -Type DWord -Force
                    Write-Host 'Restore point settings reset'

                    Write-Host 'Optimization completed successfully!'
                ";

                // Save the script to a temporary file
                string scriptPath = Path.Combine(Path.GetTempPath(), "ClearGlassOptimization.ps1");
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

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    throw new InvalidOperationException("Failed to start PowerShell process");
                }

                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    CustomMessageBox.Show(
                        "Windows settings have been successfully optimized!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    CustomMessageBox.Show(
                        "Some optimizations may not have completed successfully. Please check the system logs for more information.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Clean up the temporary script file
                File.Delete(scriptPath);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error during optimization: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task TweakWindowsSettingsSilent()
        {
            try
            {
                await CreateRestorePoint();

                string script = @"
                    Enable-ComputerRestore -Drive 'C:\' -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -Force
                    Checkpoint-Computer -Description 'Before ClearGlass Optimization' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction SilentlyContinue
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Name 'DisableWindowsConsumerFeatures' -Value 1 -Type DWord -Force
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -Value 0 -Type DWord -Force
                    
                    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'AutoCheckSelect' -Value 0 -Type DWord -Force
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Name 'AllowGameDVR' -Value 0 -Type DWord -Force
                    
                    powercfg /hibernate off
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}' -Name 'SensorPermissionState' -Value 0 -Type DWord -Force
                    New-Item -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration' -Name 'Status' -Value 0 -Type DWord -Force
                    
                    Remove-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' -Recurse -ErrorAction SilentlyContinue
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config' -Name 'AutoConnectAllowedOEM' -Value 0 -Type DWord -Force
                    
                    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarRightClickMenu' -Value 1 -Type DWord -Force
                    
                    Start-Process -FilePath cleanmgr -ArgumentList '/sagerun:1' -NoNewWindow -Wait
                    
                    [Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', '1', 'Machine')
                    
                    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\ReCall') {
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\ReCall' -Name 'Enabled' -Value 0 -Type DWord -Force
                    }
                    
                    $servicesToManual = @(
                        'DiagTrack', 'dmwappushservice', 'lfsvc', 'MapsBroker', 'NetTcpPortSharing',
                        'RemoteAccess', 'RemoteRegistry', 'SharedAccess', 'TrkWks', 'WbioSrvc',
                        'WMPNetworkSvc', 'WSearch'
                    )

                    foreach ($service in $servicesToManual) {
                        if (Get-Service -Name $service -ErrorAction SilentlyContinue) {
                            try {
                                Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
                                Set-Service -Name $service -StartupType Manual -ErrorAction SilentlyContinue
                            } catch {}
                        }
                    }
                    
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 1440 -Type DWord -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "ClearGlassOptimizationSilent.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    throw new InvalidOperationException("Failed to start PowerShell process");
                }

                await process.WaitForExitAsync();
                File.Delete(scriptPath);
            }
            catch (Exception ex)
            {
                // Log error but continue with main process
                System.Diagnostics.Debug.WriteLine($"Error during silent optimization: {ex.Message}");
            }
        }

        public async Task RemoveWindowsAIOnly()
        {
            try
            {
                await RemoveWindowsAIComponents();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error during AI component removal: {ex.Message}\n\n" +
                    "The script has attempted to restore Windows Update services automatically.\n" +
                    "If you experience issues with Windows Update, you may need to restart your computer.",
                    "AI Removal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task RemoveWindowsAIOnlySilent()
        {
            try
            {
                await RemoveWindowsAIComponents();
            }
            catch (Exception ex)
            {
                // Log error but continue with main process
                System.Diagnostics.Debug.WriteLine($"Error during silent AI component removal: {ex.Message}");
            }
        }

        private async Task RemoveWindowsAIComponents()
        {
            try
            {
                // Get the path to the RemoveWindowsAi.ps1 script
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "RemoveWindowsAi.ps1");
                
                // Check if the script exists
                if (!File.Exists(scriptPath))
                {
                    CustomMessageBox.Show(
                        "Windows AI removal script not found. Skipping AI component removal.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Run the AI removal script with elevated privileges
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Force",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    throw new InvalidOperationException("Failed to start AI removal process");
                }

                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    CustomMessageBox.Show(
                        "Windows AI removal completed with some warnings. This is normal for systems without AI components.",
                        "AI Removal Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error during AI component removal: {ex.Message}\n\nContinuing with other optimizations...",
                    "AI Removal Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task CreateRestorePoint()
        {
            try
            {
                // Enable System Restore if it's disabled
                using var enableProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Enable-ComputerRestore -Drive 'C:\'\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    }
                };
                enableProcess.Start();
                await enableProcess.WaitForExitAsync();

                // Create the restore point
                using var restoreProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Checkpoint-Computer -Description 'Before ClearGlass Optimization' -RestorePointType 'MODIFY_SETTINGS'\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    }
                };
                restoreProcess.Start();
                await restoreProcess.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                // Log the error but continue with optimization
                System.Diagnostics.Debug.WriteLine($"Error creating restore point: {ex.Message}");
            }
        }
    }
} 
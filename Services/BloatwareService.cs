using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace ClearGlass.Services
{
    public class BloatwareService
    {
        private readonly string[] essentialApps = new[]
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

        public async Task RemoveWindowsBloatware()
        {
            try
            {
                MessageBox.Show(
                    "Starting Windows bloatware removal. This will remove unnecessary Windows apps while keeping essential ones.\n\n" +
                    "A system restore point will be created before making changes.",
                    "Starting Bloatware Removal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                string script = @"
                    # Create Restore Point
                    Write-Host 'Creating system restore point...'
                    try {
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -Force
                        Enable-ComputerRestore -Drive 'C:\'
                        Checkpoint-Computer -Description 'Before ClearGlass Bloatware Removal' -RestorePointType 'MODIFY_SETTINGS'
                        Write-Host 'Restore point created successfully'
                    } catch {
                        Write-Warning 'Could not create restore point. Continuing with bloatware removal...'
                    }

                    Write-Host 'Starting bloatware removal...'
                    
                    # Get all installed UWP apps for all users
                    Get-AppxPackage -AllUsers | Where-Object {
                        $app = $_.Name
                        $essential = @('" + string.Join("','", essentialApps) + @"')
                        $keep = $false
                        
                        foreach ($e in $essential) {
                            if ($app -like ""$e*"") {
                                $keep = $true
                                break
                            }
                        }
                        
                        -not $keep
                    } | ForEach-Object {
                        try {
                            Write-Host ""Removing $($_.Name)...""
                            Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue
                            Write-Host ""Successfully removed $($_.Name)""
                        } catch {
                            Write-Warning ""Failed to remove $($_.Name): $_""
                        }
                    }

                    Write-Host 'Bloatware removal completed!'
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
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        MessageBox.Show(
                            "Windows bloatware has been successfully removed while keeping essential apps!\n\n" +
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
    }
} 
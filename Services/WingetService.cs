using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using ClearGlass.Models;
using System.Text;
using System.IO;
using Microsoft.Win32;
using System.Linq;

namespace ClearGlass.Services
{
    public class WingetService
    {
        public async Task<bool> IsWingetInstalled()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string version = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Found winget version: {version.Trim()}");
                    return true;
                }
                return false;
            }
            catch
            {
                Console.WriteLine("Winget is not installed.");
                return false;
            }
        }

        public async Task InstallWinget()
        {
            Console.WriteLine("Opening Microsoft Store to install winget (App Installer)...");
            // Open the Microsoft Store page for App Installer (which includes winget)
            await Task.Run(() => Process.Start(new ProcessStartInfo
            {
                FileName = "ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1",
                UseShellExecute = true
            }));

            throw new Exception(
                "Please install the App Installer from the Microsoft Store that just opened.\n" +
                "After installation is complete, restart Clear Glass to continue.");
        }

        public async Task<bool> IsAppInstalled(string packageId)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"list --id {packageId} --exact --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        public async Task UpdateApp(string packageId, string appName)
        {
            Console.WriteLine($"Checking for {appName} updates...");
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"upgrade --id {packageId} --exact --accept-source-agreements --accept-package-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var output = "";
            process.OutputDataReceived += (sender, e) => 
            { 
                if (e.Data != null)
                {
                    output += e.Data + "\n";
                    Console.WriteLine(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) => 
            { 
                if (e.Data != null)
                {
                    output += e.Data + "\n";
                    Console.WriteLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            // Exit code -1978335189 means no updates available
            if (process.ExitCode != 0 && process.ExitCode != -1978335189)
            {
                throw new Exception($"Failed to update {appName}. Error:\n{output}");
            }
        }

        public async Task InstallApp(string packageId, string appName)
        {
            if (!await IsWingetInstalled())
            {
                await InstallWinget();
                return;
            }

            // Check if app is already installed
            if (await IsAppInstalled(packageId))
            {
                Console.WriteLine($"{appName} is already installed. Checking for updates...");
                await UpdateApp(packageId, appName);
                return;
            }

            Console.WriteLine($"Installing {appName}...");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install --id {packageId} --exact --accept-source-agreements --accept-package-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var output = "";
            process.OutputDataReceived += (sender, e) => 
            { 
                if (e.Data != null)
                {
                    output += e.Data + "\n";
                    Console.WriteLine(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) => 
            { 
                if (e.Data != null)
                {
                    output += e.Data + "\n";
                    Console.WriteLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Failed to install {appName}. Error:\n{output}");
            }
            
            Console.WriteLine($"{appName} has been successfully installed!");
        }

        public async Task<List<InstalledApp>> GetInstalledApps()
        {
            var installedApps = new List<InstalledApp>();

            // Only get apps from winget
            if (await IsWingetInstalled())
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = "list --accept-source-agreements",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };

                var output = new StringBuilder();
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    // Parse the output
                    var lines = output.ToString().Split('\n');
                    bool headerPassed = false;
                    foreach (var line in lines)
                    {
                        if (!headerPassed)
                        {
                            if (line.Contains("Name") && line.Contains("Id") && line.Contains("Version"))
                            {
                                headerPassed = true;
                            }
                            continue;
                        }

                        // Skip separator line and empty lines
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("--"))
                        {
                            continue;
                        }

                        // Split the line by multiple spaces
                        var parts = Regex.Split(line.Trim(), @"\s{2,}");
                        if (parts.Length >= 3)
                        {
                            installedApps.Add(new InstalledApp(
                                name: parts[0].Trim(),
                                id: parts[1].Trim(),
                                version: parts[2].Trim()
                            ));
                        }
                    }
                }
            }

            return installedApps.OrderBy(a => a.Name).ToList();
        }

        public async Task<InstalledApp?> GetAppInfo(string packageId)
        {
            if (!await IsWingetInstalled())
            {
                return null;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"list --id {packageId} --exact --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            var output = new StringBuilder();
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var lines = output.ToString().Split('\n');
                bool headerPassed = false;
                foreach (var line in lines)
                {
                    if (!headerPassed)
                    {
                        if (line.Contains("Name") && line.Contains("Id") && line.Contains("Version"))
                        {
                            headerPassed = true;
                        }
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("--"))
                    {
                        continue;
                    }

                    var parts = Regex.Split(line.Trim(), @"\s{2,}");
                    if (parts.Length >= 3)
                    {
                        return new InstalledApp(
                            name: parts[0].Trim(),
                            id: parts[1].Trim(),
                            version: parts[2].Trim()
                        );
                    }
                }
            }

            return null;
        }

        public async Task UninstallApp(string packageId)
        {
            if (!await IsWingetInstalled())
            {
                throw new Exception("Winget is not installed on this system.");
            }

            var appInfo = await GetAppInfo(packageId);
            string appName = appInfo?.Name ?? packageId;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"uninstall --id {packageId} --silent --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Winget uninstall failed with exit code: {process.ExitCode}");
                }

                // After successful winget uninstall, perform thorough cleanup
                var uninstallService = new UninstallService(this);
                var progress = new Progress<string>(message => Debug.WriteLine($"Cleanup: {message}"));
                await uninstallService.CleanupAfterUninstall(packageId, appName, progress);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error uninstalling app: {ex.Message}", ex);
            }
            finally
            {
                process.Dispose();
            }
        }
    }
} 
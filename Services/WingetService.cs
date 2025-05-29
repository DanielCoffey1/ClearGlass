using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using ClearGlass.Models;
using System.Text;

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
            if (!await IsWingetInstalled())
            {
                await InstallWinget();
                return new List<InstalledApp>();
            }

            var installedApps = new List<InstalledApp>();
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

            if (process.ExitCode != 0)
            {
                throw new Exception($"Failed to list installed applications. Error:\n{output}");
            }

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

            return installedApps;
        }

        public async Task UninstallApp(string packageId)
        {
            if (!await IsWingetInstalled())
            {
                await InstallWinget();
                return;
            }

            // Check if this is a Steam game (Steam App IDs are typically numeric)
            if (packageId.StartsWith("Steam") && long.TryParse(packageId.Replace("Steam App ", ""), out long steamAppId))
            {
                Console.WriteLine($"Uninstalling Steam game (AppID: {steamAppId})...");
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "steam://uninstall/" + steamAppId,
                        UseShellExecute = true
                    }
                };

                process.Start();
                // We can't wait for exit here as it opens Steam client
                return;
            }

            Console.WriteLine($"Uninstalling package {packageId}...");
            var uninstallProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"uninstall --id {packageId} --exact --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var output = new StringBuilder();
            uninstallProcess.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                    Console.WriteLine(e.Data);
                }
            };
            uninstallProcess.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                    Console.WriteLine(e.Data);
                }
            };

            uninstallProcess.Start();
            uninstallProcess.BeginOutputReadLine();
            uninstallProcess.BeginErrorReadLine();
            await uninstallProcess.WaitForExitAsync();

            if (uninstallProcess.ExitCode != 0)
            {
                throw new Exception($"Failed to uninstall {packageId}. Error:\n{output}");
            }
        }
    }
} 
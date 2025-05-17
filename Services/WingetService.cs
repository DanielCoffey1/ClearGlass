using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task InstallWinget()
        {
            // Open the Microsoft Store page for App Installer (which includes winget)
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1",
                UseShellExecute = true
            });

            throw new Exception(
                "Please install the App Installer from the Microsoft Store that just opened.\n" +
                "After installation is complete, restart Clear Glass to continue.");
        }

        public async Task InstallApp(string packageId, string appName)
        {
            if (!await IsWingetInstalled())
            {
                await InstallWinget();
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install -e --id {packageId} --accept-source-agreements --accept-package-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var output = "";
            process.OutputDataReceived += (sender, e) => { if (e.Data != null) output += e.Data + "\n"; };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) output += e.Data + "\n"; };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Failed to install {appName}. Error:\n{output}");
            }
        }
    }
} 
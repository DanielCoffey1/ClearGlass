using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

namespace ClearGlass.Services.Core
{
    public class ProcessHelper
    {
        [DllImport("shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private const int SHCNE_ASSOCCHANGED = 0x8000000;
        private const int SHCNF_FLUSH = 0x1000;

        private const int DefaultRetryAttempts = 3;
        private const int RetryDelayMs = 1000;

        public static async Task<(bool Success, string Output, string Error)> RunProcessAsync(
            ProcessStartInfo startInfo,
            bool requireAdmin = false,
            int retryAttempts = DefaultRetryAttempts)
        {
            if (requireAdmin)
            {
                startInfo.Verb = "runas";
            }

            // Ensure we can capture output
            if (!startInfo.UseShellExecute)
            {
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
            }

            for (int attempt = 1; attempt <= retryAttempts; attempt++)
            {
                try
                {
                    using var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        throw new InvalidOperationException($"Failed to start process: {startInfo.FileName}");
                    }

                    string output = string.Empty;
                    string error = string.Empty;

                    if (!startInfo.UseShellExecute)
                    {
                        output = await process.StandardOutput.ReadToEndAsync();
                        error = await process.StandardError.ReadToEndAsync();
                    }

                    await process.WaitForExitAsync();
                    return (process.ExitCode == 0, output, error);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Process execution attempt {attempt} failed: {ex.Message}");
                    
                    if (attempt == retryAttempts)
                    {
                        return (false, string.Empty, ex.Message);
                    }

                    await Task.Delay(RetryDelayMs);
                }
            }

            return (false, string.Empty, "Maximum retry attempts reached");
        }

        public static async Task<(bool Success, string Output)> RunPowerShellScriptAsync(
            string script,
            bool requireAdmin = false,
            bool showWindow = false,
            int retryAttempts = DefaultRetryAttempts)
        {
            try
            {
                // Save the script to a temporary file with a unique name
                string scriptPath = Path.Combine(
                    Path.GetTempPath(),
                    $"ClearGlass_Script_{Guid.NewGuid()}.ps1");

                await File.WriteAllTextAsync(scriptPath, script);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = showWindow,
                    CreateNoWindow = !showWindow,
                    RedirectStandardOutput = !showWindow,
                    RedirectStandardError = !showWindow
                };

                var (success, output, error) = await RunProcessAsync(startInfo, requireAdmin, retryAttempts);

                // Clean up the temporary script file
                try
                {
                    if (File.Exists(scriptPath))
                    {
                        File.Delete(scriptPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete temporary script file: {ex.Message}");
                }

                return (success, success ? output : error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to run PowerShell script: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public static async Task<bool> KillProcessAsync(
            string processName,
            bool waitForExit = true,
            int timeoutMs = 5000)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    return true;
                }

                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            if (waitForExit)
                            {
                                await process.WaitForExitAsync(
                                    new CancellationTokenSource(timeoutMs).Token);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to kill process {process.ProcessName}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in KillProcessAsync: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> RestartExplorerAsync()
        {
            try
            {
                string script = @"
                    # Stop Explorer process
                    Get-Process explorer | Stop-Process -Force
                    Start-Sleep -Seconds 1

                    # Start a single instance of Explorer
                    $shell = New-Object -ComObject Shell.Application
                    $shell.WindowSwitcher()
                    Start-Sleep -Seconds 2
                ";

                var (success, _) = await RunPowerShellScriptAsync(script, requireAdmin: false);
                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to restart Explorer: {ex.Message}");
                return true; // Return true anyway since Explorer auto-restarts
            }
        }
    }
} 
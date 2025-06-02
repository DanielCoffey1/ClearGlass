using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;

namespace ClearGlass.Services.Core
{
    public class ProcessService
    {
        public async Task<(bool Success, string Output, string Error)> RunProcessAsync(
            string fileName,
            string arguments,
            bool useShellExecute = false,
            bool runAsAdmin = false,
            bool createNoWindow = true)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = useShellExecute,
                    RedirectStandardOutput = !useShellExecute,
                    RedirectStandardError = !useShellExecute,
                    CreateNoWindow = createNoWindow
                };

                if (runAsAdmin && !useShellExecute)
                {
                    startInfo.Verb = "runas";
                }

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return (false, string.Empty, "Failed to start process");
                }

                string output = string.Empty;
                string error = string.Empty;

                if (!useShellExecute)
                {
                    output = await process.StandardOutput.ReadToEndAsync();
                    error = await process.StandardError.ReadToEndAsync();
                }

                await process.WaitForExitAsync();
                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                LogError($"Error running process {fileName}", ex);
                return (false, string.Empty, ex.Message);
            }
        }

        public async Task<(bool Success, string Output)> RunPowerShellScriptAsync(
            string script,
            bool runAsAdmin = false)
        {
            try
            {
                // Save the script to a temporary file
                string scriptPath = Path.Combine(Path.GetTempPath(), $"ClearGlass_{Guid.NewGuid()}.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var (success, output, error) = await RunProcessAsync(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    runAsAdmin: runAsAdmin);

                // Clean up the temporary script file
                try
                {
                    File.Delete(scriptPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }

                return (success, success ? output : error);
            }
            catch (Exception ex)
            {
                LogError("Error running PowerShell script", ex);
                return (false, ex.Message);
            }
        }

        public async Task<bool> KillProcessAsync(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error killing process {processName}", ex);
                return false;
            }
        }

        private void LogError(string message, Exception ex)
        {
            // TODO: Replace with proper logging system
            System.Diagnostics.Debug.WriteLine($"{message}: {ex.Message}");
        }
    }
} 
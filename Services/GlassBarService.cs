using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ClearGlass.Services
{
    public class GlassBarService
    {
        private readonly LoggingService _loggingService;
        private const string ResourceName = "ClearGlass.Resources.ClearGlassAddons.GlassBar_Setup.exe";

        public GlassBarService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        /// <summary>
        /// Extracts the embedded GlassBar installer to a temporary location and runs it silently
        /// </summary>
        /// <returns>True if installation was successful, false otherwise</returns>
        public async Task<bool> InstallGlassBarAsync()
        {
            string? tempFilePath = null;
            try
            {
                _loggingService.LogInformation("Starting GlassBar installation via PowerShell...");

                // Extract the embedded exe to a temporary file
                tempFilePath = await ExtractGlassBarInstallerAsync();
                if (string.IsNullOrEmpty(tempFilePath))
                {
                    _loggingService.LogError("Failed to extract GlassBar installer");
                    return false;
                }

                // Use PowerShell script to handle both installation and launching
                var success = await RunGlassBarInstallationScript(tempFilePath);
                
                if (success)
                {
                    _loggingService.LogInformation("GlassBar installation and startup completed successfully");
                }
                else
                {
                    _loggingService.LogError("GlassBar installation or startup failed");
                }

                return success;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during GlassBar installation: {ex.Message}");
                return false;
            }
            finally
            {
                // Clean up the temporary installer file
                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                        _loggingService.LogInformation("Cleaned up temporary GlassBar installer file");
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Failed to delete temporary file: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Runs the PowerShell script to install and start GlassBar
        /// </summary>
        private async Task<bool> RunGlassBarInstallationScript(string installerPath)
        {
            try
            {
                // Get the path to our PowerShell script with error suppression
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "InstallGlassBarWithErrorSuppression.ps1");
                
                if (!File.Exists(scriptPath))
                {
                    _loggingService.LogError($"PowerShell installation script not found at: {scriptPath}");
                    return false;
                }

                var logPath = Path.Combine(Path.GetTempPath(), "ClearGlass_GlassBar_Complete.log");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -InstallerPath \"{installerPath}\" -LogPath \"{logPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _loggingService.LogInformation($"Executing PowerShell installation script with error suppression: {scriptPath}");

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    _loggingService.LogError("Failed to start PowerShell process for installation");
                    return false;
                }

                // Read output for logging
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var exitCode = process.ExitCode;
                _loggingService.LogInformation($"PowerShell installation script completed with exit code: {exitCode}");

                if (!string.IsNullOrEmpty(output))
                {
                    _loggingService.LogInformation($"PowerShell output: {output}");
                }

                if (!string.IsNullOrEmpty(error))
                {
                    _loggingService.LogWarning($"PowerShell error: {error}");
                }

                // Also read the log file if it exists
                try
                {
                    if (File.Exists(logPath))
                    {
                        var logContent = await File.ReadAllTextAsync(logPath);
                        _loggingService.LogInformation($"PowerShell installation log:\n{logContent}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogWarning($"Could not read PowerShell installation log file: {ex.Message}");
                }

                return exitCode == 0;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error running PowerShell installation script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts a background task that continuously kills GlassBar processes during installation
        /// </summary>
        private System.Threading.CancellationTokenSource StartContinuousProcessKiller()
        {
            var cts = new System.Threading.CancellationTokenSource();
            
            Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        await KillGlassBarProcessesAsync();
                        await Task.Delay(500, cts.Token); // Check every 500ms
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    _loggingService.LogInformation("Continuous process killer stopped");
                }
                catch (Exception ex)
                {
                    _loggingService.LogWarning($"Error in continuous process killer: {ex.Message}");
                }
            }, cts.Token);

            return cts;
        }

        /// <summary>
        /// Extracts the embedded GlassBar_Setup.exe to a temporary file
        /// </summary>
        /// <returns>Path to the extracted temporary file, or null if extraction failed</returns>
        private async Task<string?> ExtractGlassBarInstallerAsync()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                
                // Check if the resource exists
                using var resourceStream = assembly.GetManifestResourceStream(ResourceName);
                if (resourceStream == null)
                {
                    _loggingService.LogError($"Could not find embedded resource: {ResourceName}");
                    
                    // List available resources for debugging
                    var availableResources = assembly.GetManifestResourceNames();
                    _loggingService.LogInformation($"Available resources: {string.Join(", ", availableResources)}");
                    
                    return null;
                }

                // Create a temporary file
                var tempPath = Path.GetTempPath();
                var tempFileName = $"GlassBar_Setup_{Guid.NewGuid():N}.exe";
                var tempFilePath = Path.Combine(tempPath, tempFileName);

                // Extract the resource to the temporary file
                using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
                await resourceStream.CopyToAsync(fileStream);

                _loggingService.LogInformation($"GlassBar installer extracted to: {tempFilePath}");
                return tempFilePath;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to extract GlassBar installer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Runs the GlassBar installer silently
        /// </summary>
        /// <param name="installerPath">Path to the installer executable</param>
        /// <returns>True if the installer ran successfully, false otherwise</returns>
        private async Task<bool> RunGlassBarInstallerAsync(string installerPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/VERYSILENT /NORESTART", // Very silent installation with no restart
                    UseShellExecute = true, // Required for UAC elevation
                    Verb = "runas", // Request admin privileges
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _loggingService.LogInformation($"Starting GlassBar installer: {installerPath}");

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    _loggingService.LogError("Failed to start GlassBar installer process");
                    return false;
                }

                // Wait for the installer to complete (with timeout)
                var completed = await Task.Run(() => process.WaitForExit(300000)); // 5 minute timeout
                
                if (!completed)
                {
                    _loggingService.LogError("GlassBar installer timed out after 5 minutes");
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Failed to kill timed-out installer process: {ex.Message}");
                    }
                    return false;
                }

                var exitCode = process.ExitCode;
                _loggingService.LogInformation($"GlassBar installer completed with exit code: {exitCode}");

                // Exit code 0 typically means success
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error running GlassBar installer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Kills any running GlassBar processes to prevent the XAML diagnostics error
        /// </summary>
        private async Task KillGlassBarProcessesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Try multiple process names that GlassBar might use
                    var processNames = new[] { "GlassBar", "glassbar", "GlassBar.exe" };
                    
                    foreach (var processName in processNames)
                    {
                        var processes = Process.GetProcessesByName(processName.Replace(".exe", ""));
                        foreach (var process in processes)
                        {
                            try
                            {
                                if (!process.HasExited)
                                {
                                    _loggingService.LogInformation($"Terminating {processName} process (PID: {process.Id})");
                                    process.Kill();
                                    
                                    // Force immediate termination
                                    if (!process.WaitForExit(2000)) // Wait max 2 seconds
                                    {
                                        _loggingService.LogWarning($"Process {process.Id} did not exit gracefully, forcing termination");
                                    }
                                    
                                    _loggingService.LogInformation($"{processName} process terminated successfully");
                                }
                            }
                            catch (Exception ex)
                            {
                                _loggingService.LogWarning($"Failed to terminate {processName} process: {ex.Message}");
                            }
                            finally
                            {
                                process.Dispose();
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error while killing GlassBar processes: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts GlassBar using PowerShell script to avoid execution context issues
        /// </summary>
        public async Task<bool> StartGlassBarAsync()
        {
            try
            {
                // Wait a bit to ensure system is stable after installation and cleanup
                await Task.Delay(3000);

                _loggingService.LogInformation("Starting GlassBar using PowerShell script...");

                // Use PowerShell script to launch GlassBar in proper user context
                return await RunGlassBarPowerShellScript();
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error starting GlassBar via PowerShell: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs the PowerShell script to start GlassBar
        /// </summary>
        private async Task<bool> RunGlassBarPowerShellScript()
        {
            try
            {
                // Get the path to our PowerShell script
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "StartGlassBar.ps1");
                
                if (!File.Exists(scriptPath))
                {
                    _loggingService.LogError($"PowerShell script not found at: {scriptPath}");
                    return false;
                }

                var logPath = Path.Combine(Path.GetTempPath(), "ClearGlass_GlassBar.log");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -LogPath \"{logPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _loggingService.LogInformation($"Executing PowerShell script: {scriptPath}");

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    _loggingService.LogError("Failed to start PowerShell process");
                    return false;
                }

                // Read output for logging
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var exitCode = process.ExitCode;
                _loggingService.LogInformation($"PowerShell script completed with exit code: {exitCode}");

                if (!string.IsNullOrEmpty(output))
                {
                    _loggingService.LogInformation($"PowerShell output: {output}");
                }

                if (!string.IsNullOrEmpty(error))
                {
                    _loggingService.LogWarning($"PowerShell error: {error}");
                }

                // Also read the log file if it exists
                try
                {
                    if (File.Exists(logPath))
                    {
                        var logContent = await File.ReadAllTextAsync(logPath);
                        _loggingService.LogInformation($"PowerShell script log:\n{logContent}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogWarning($"Could not read PowerShell log file: {ex.Message}");
                }

                return exitCode == 0;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error running PowerShell script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds the GlassBar executable with comprehensive search
        /// </summary>
        private string? FindGlassBarExecutable()
        {
            // Common installation paths for GlassBar executable
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GlassBar", "GlassBar.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GlassBar", "GlassBar.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlassBar", "GlassBar.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassBar", "GlassBar.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "GlassBar", "GlassBar.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", "GlassBar", "GlassBar.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _loggingService.LogInformation($"Found GlassBar executable at: {path}");
                    return path;
                }
                else
                {
                    _loggingService.LogInformation($"Checked path (not found): {path}");
                }
            }

            // Try to find it in the registry or start menu
            return FindGlassBarInRegistry();
        }

        /// <summary>
        /// Tries to find GlassBar installation path from registry
        /// </summary>
        private string? FindGlassBarInRegistry()
        {
            try
            {
                // Check common registry locations for installed programs
                var registryPaths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var regPath in registryPaths)
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath);
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey != null)
                            {
                                var displayName = subKey.GetValue("DisplayName")?.ToString();
                                if (!string.IsNullOrEmpty(displayName) && displayName.Contains("GlassBar"))
                                {
                                    var installLocation = subKey.GetValue("InstallLocation")?.ToString();
                                    if (!string.IsNullOrEmpty(installLocation))
                                    {
                                        var exePath = Path.Combine(installLocation, "GlassBar.exe");
                                        if (File.Exists(exePath))
                                        {
                                            _loggingService.LogInformation($"Found GlassBar via registry at: {exePath}");
                                            return exePath;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error searching registry for GlassBar: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Tries multiple approaches to start GlassBar
        /// </summary>
        private async Task<bool> TryStartGlassBarMultipleWays(string glassBarPath)
        {
            var approaches = new[]
            {
                new { Name = "Shell Execute", UseShell = true, CreateWindow = false, WindowStyle = ProcessWindowStyle.Hidden },
                new { Name = "Direct Execute", UseShell = false, CreateWindow = true, WindowStyle = ProcessWindowStyle.Normal },
                new { Name = "Background Execute", UseShell = false, CreateWindow = false, WindowStyle = ProcessWindowStyle.Hidden }
            };

            foreach (var approach in approaches)
            {
                try
                {
                    _loggingService.LogInformation($"Trying to start GlassBar using: {approach.Name}");

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = glassBarPath,
                        UseShellExecute = approach.UseShell,
                        CreateNoWindow = !approach.CreateWindow,
                        WindowStyle = approach.WindowStyle,
                        WorkingDirectory = Path.GetDirectoryName(glassBarPath)
                    };

                    var process = Process.Start(startInfo);
                    
                    if (process != null)
                    {
                        _loggingService.LogInformation($"GlassBar process started (PID: {process.Id}) using {approach.Name}");
                        
                        // Wait a moment to see if the process stays alive
                        await Task.Delay(3000);
                        
                        if (!process.HasExited)
                        {
                            _loggingService.LogInformation($"GlassBar is running successfully using {approach.Name}");
                            return true;
                        }
                        else
                        {
                            _loggingService.LogWarning($"GlassBar process exited immediately with code: {process.ExitCode} using {approach.Name}");
                        }
                    }
                    else
                    {
                        _loggingService.LogWarning($"Failed to start GlassBar process using {approach.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogWarning($"Error starting GlassBar using {approach.Name}: {ex.Message}");
                }

                // Wait between attempts
                await Task.Delay(1000);
            }

            _loggingService.LogError("All attempts to start GlassBar failed");
            return false;
        }

        /// <summary>
        /// Checks if GlassBar is already installed by looking for common installation paths
        /// </summary>
        /// <returns>True if GlassBar appears to be installed, false otherwise</returns>
        public bool IsGlassBarInstalled()
        {
            try
            {
                // Common installation paths for GlassBar
                var possiblePaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GlassBar"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GlassBar"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlassBar")
                };

                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        _loggingService.LogInformation($"Found GlassBar installation at: {path}");
                        return true;
                    }
                }

                _loggingService.LogInformation("GlassBar installation not detected");
                return false;
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error checking GlassBar installation: {ex.Message}");
                return false;
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Security.Principal;
using System.Windows;

namespace ClearGlass.Services
{
    public class SelfDestructService
    {
        private readonly string _appPath;
        private readonly string _tempDir;

        public SelfDestructService()
        {
            _appPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            _tempDir = Path.GetTempPath();
        }

        /// <summary>
        /// Schedules the RemoveWindowsAi.ps1 script to run at startup and then restarts the PC
        /// </summary>
        public async Task ScheduleStartupScriptAndRestart()
        {
            try
            {
                // Extract the RemoveWindowsAi.ps1 script to temp directory
                string scriptPath = await ExtractStartupScript();
                
                // Create a batch file that will run the script and then self-destruct
                string batchPath = CreateSelfDestructBatch(scriptPath);
                
                // Schedule the batch file to run at startup
                ScheduleStartupTask(batchPath);
                
                // Show final message and restart
                await ShowFinalMessageAndRestart();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to schedule startup script: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extracts the RemoveWindowsAi.ps1 script from embedded resources
        /// </summary>
        private async Task<string> ExtractStartupScript()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = "ClearGlass.Scripts.RemoveWindowsAi.ps1";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    throw new InvalidOperationException("RemoveWindowsAi.ps1 script not found in embedded resources");
                }

                string scriptPath = Path.Combine(_tempDir, "ClearGlass_RemoveWindowsAi.ps1");
                using var fileStream = new FileStream(scriptPath, FileMode.Create);
                await stream.CopyToAsync(fileStream);
                
                return scriptPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract startup script: {ex.Message}", ex);
            }
        }



        /// <summary>
        /// Creates a batch file that will run the script and then self-destruct
        /// </summary>
        private string CreateSelfDestructBatch(string scriptPath)
        {
            string batchContent = $@"@echo off
echo ClearGlass: Running Windows AI removal script...
echo.

REM Wait a moment for system to fully boot
timeout /t 30 /nobreak >nul

REM Run the AI removal script with elevated privileges
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{scriptPath}""

REM Wait for script completion
timeout /t 5 /nobreak >nul

REM Show final restart message
start /min ""ClearGlass Restart"" powershell.exe -WindowStyle Hidden -Command ""Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show('Windows AI removal complete! Your PC will restart in 10 seconds to finalize cleanup.', 'ClearGlass Complete', 'OK', 'Information')""

REM Restart the computer
echo ClearGlass: Restarting computer to complete cleanup...
shutdown /r /t 10 /c ""ClearGlass: Restarting to complete Windows AI removal""

REM Self-destruct this batch file
del ""%~f0""
";

            string batchPath = Path.Combine(_tempDir, "ClearGlass_StartupTask.bat");
            File.WriteAllText(batchPath, batchContent);
            
            return batchPath;
        }

        /// <summary>
        /// Schedules the batch file to run at startup using Windows Task Scheduler
        /// </summary>
        private void ScheduleStartupTask(string batchPath)
        {
            try
            {
                // Create a scheduled task that runs at startup
                string taskName = "ClearGlass_WindowsAIRemoval";
                
                // Delete existing task if it exists
                Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/delete /tn \"{taskName}\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                })?.WaitForExit();

                // Create new task
                string createTaskCommand = $"/create /tn \"{taskName}\" /tr \"{batchPath}\" /sc onstart /ru \"SYSTEM\" /f";
                
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = createTaskCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start task scheduler process");
                }

                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new InvalidOperationException($"Failed to create scheduled task: {error}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to schedule startup task: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Shows final message and initiates restart
        /// </summary>
        private async Task ShowFinalMessageAndRestart()
        {
            // Show completion message
            var result = CustomMessageBox.Show(
                "🎉 **ClearGlass Optimization Complete!**\n\n" +
                "✅ All optimizations have been applied successfully\n" +
                "✅ Windows AI removal script scheduled for next startup\n" +
                "✅ System will restart to complete the process\n\n" +
                "**What happens next:**\n" +
                "1. Your PC will restart in 30 seconds\n" +
                "2. After restart, the AI removal script will run automatically\n" +
                "3. The script will remove Windows AI components and restart again\n" +
                "4. You'll have a clean, optimized system with no trace of ClearGlass\n\n" +
                "**Note:** The ClearGlass application will be automatically removed after the restart.\n\n" +
                "Click 'OK' to restart now, or 'Cancel' to restart manually later.",
                "ClearGlass Complete! 🚀",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information,
                System.Windows.TextAlignment.Left);

            if (result == MessageBoxResult.OK)
            {
                // Restart the computer
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/r /t 5 /c \"ClearGlass: Restarting to complete Windows AI removal\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                // Wait a moment for shutdown to initiate
                await Task.Delay(2000);
                
                // Force exit the application
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Creates a self-destruct batch file that will remove the application after restart
        /// </summary>
        public void CreateSelfDestructBatch()
        {
            try
            {
                string batchContent = $@"@echo off
echo ClearGlass: Self-destructing application...
echo.

REM Wait for the application to fully close
timeout /t 5 /nobreak >nul

REM Delete the application executable
if exist ""{_appPath}"" (
    del ""{_appPath}"" /f /q
    echo Deleted: {_appPath}
)

REM Delete the application directory if it's empty
set ""appDir={Path.GetDirectoryName(_appPath)}""
if exist ""%appDir%"" (
    rmdir ""%appDir%"" /s /q 2>nul
    echo Cleaned up application directory
)

REM Delete this batch file
del ""%~f0""

echo ClearGlass: Self-destruction complete.
";

                string batchPath = Path.Combine(_tempDir, "ClearGlass_SelfDestruct.bat");
                File.WriteAllText(batchPath, batchContent);
                
                // Schedule the self-destruct batch to run at startup
                string taskName = "ClearGlass_SelfDestruct";
                
                // Delete existing task if it exists
                Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/delete /tn \"{taskName}\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                })?.WaitForExit();

                // Create new task
                string createTaskCommand = $"/create /tn \"{taskName}\" /tr \"{batchPath}\" /sc onstart /ru \"SYSTEM\" /f";
                
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = createTaskCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                // Log error but don't throw - self-destruct is not critical
                Console.WriteLine($"Warning: Failed to create self-destruct batch: {ex.Message}");
            }
        }
    }
} 
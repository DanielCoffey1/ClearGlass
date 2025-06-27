using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.ObjectModel;
using ClearGlass.Models;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace ClearGlass.Services
{
    public class BloatwareService
    {
        private const string POWERSHELL_PATH = "powershell.exe";
        private const string TEMP_SCRIPT_NAME = "ClearGlassBloatwareRemoval.ps1";
        private const string TEMP_GET_APPS_SCRIPT_NAME = "GetInstalledApps.ps1";
        private const string TEMP_START_MENU_SCRIPT_NAME = "ClearStartMenu.ps1";
        private const string SCRIPTS_NAMESPACE = "ClearGlass.Scripts";

        private readonly LoggingService _logger;
        private readonly OptimizationService _optimizationService;
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
            "Microsoft.Windows.SecHealthUI", // Windows Security
            "Microsoft.WindowsTerminal",
            "Microsoft.Windows.CloudExperienceHost", // Required for Windows Hello and other features
            "Microsoft.Win32WebViewHost", // Required for various Windows components
            "Microsoft.UI.Xaml", // Required UI framework
            "Microsoft.VCLibs", // Visual C++ Runtime
            "Microsoft.Services.Store.Engagement", // Required for Store
            "Microsoft.NET", // .NET Runtime
            "Microsoft.Paint", // Classic Paint app
            "Microsoft.MSPaint" // Paint 3D
        };

        private List<string> _sessionEssentialApps = new();

        public BloatwareService(LoggingService logger, OptimizationService optimizationService)
        {
            _logger = logger;
            _optimizationService = optimizationService;
            ResetToDefaultEssentialApps();
            _logger.LogInformation("BloatwareService initialized");
        }

        public void ResetToDefaultEssentialApps()
        {
            _sessionEssentialApps = new List<string>(defaultEssentialApps);
            _logger.LogInformation("Reset to default essential apps list");
        }

        public void UpdateSessionEssentialApps(IEnumerable<WindowsApp> selectedApps)
        {
            _sessionEssentialApps = new List<string>(defaultEssentialApps);
            var newApps = selectedApps
                .Where(a => a.IsSelected)
                .Select(a => a.Name)
                .Where(name => !_sessionEssentialApps.Contains(name))
                .ToList();

            _sessionEssentialApps.AddRange(newApps);
            
            _logger.LogInformation(
                "Updated essential apps list. Added {Count} new apps: {Apps}", 
                newApps.Count, 
                string.Join(", ", newApps)
            );
        }

        public IReadOnlyList<string> EssentialApps => _sessionEssentialApps;

        public async Task<ObservableCollection<WindowsApp>> GetInstalledApps()
        {
            _logger.LogOperationStart("Getting installed apps");
            var apps = new ObservableCollection<WindowsApp>();
            string scriptPath = await CreateGetAppsScript();

            try
            {
                var output = await RunPowerShellScript(scriptPath, false);
                if (!string.IsNullOrEmpty(output))
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
                        _logger.LogInformation("Found {Count} installed apps", appList.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get installed apps", ex);
                ShowError("Error getting installed apps", ex);
            }
            finally
            {
                CleanupScript(scriptPath);
            }

            _logger.LogOperationComplete("Getting installed apps");
            return apps;
        }

        private async Task<string> LoadRemovalScriptContent()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"{SCRIPTS_NAMESPACE}.RemoveBloatware.ps1";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    var error = $"Could not find embedded script: {resourceName}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                _logger.LogInformation("Successfully loaded removal script");
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load removal script", ex);
                throw;
            }
        }

        private async Task<string> LoadGetAppsScriptContent()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"{SCRIPTS_NAMESPACE}.GetInstalledApps.ps1";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    var error = $"Could not find embedded script: {resourceName}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                _logger.LogInformation("Successfully loaded get apps script");
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load get apps script", ex);
                throw;
            }
        }

        private async Task<string> LoadStartMenuScriptContent()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"{SCRIPTS_NAMESPACE}.ClearStartMenu.ps1";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    var error = $"Could not find embedded script: {resourceName}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                _logger.LogInformation("Successfully loaded start menu script");
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load start menu script", ex);
                throw;
            }
        }

        private async Task<string> CreateGetAppsScript()
        {
            try
            {
                string scriptPath = Path.Combine(Path.GetTempPath(), TEMP_GET_APPS_SCRIPT_NAME);
                string scriptContent = await LoadGetAppsScriptContent();
                await File.WriteAllTextAsync(scriptPath, scriptContent);
                _logger.LogInformation("Created get apps script at: {Path}", scriptPath);
                return scriptPath;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create get apps script", ex);
                throw;
            }
        }

        public async Task RemoveWindowsBloatware(IEnumerable<WindowsApp> appsToKeep, bool clearStartMenu = true, bool createRestorePoint = true)
        {
            _logger.LogOperationStart("Removing Windows bloatware");
            ShowStartupMessage();

            // Optionally create restore point
            if (createRestorePoint)
            {
                await _optimizationService.CreateRestorePoint();
            }
            
            // Ask user about Edge removal
            bool removeEdge = await AskUserAboutEdgeRemoval();
            
            string scriptPath = await CreateRemovalScript(appsToKeep);

            try
            {
                await ExecuteRemovalScript(scriptPath);
                _logger.LogOperationComplete("Removing Windows bloatware");
                
                // Remove Edge if user requested it
                if (removeEdge)
                {
                    await RemoveMicrosoftEdge();
                }
                
                // Clear start menu after bloatware removal if requested
                if (clearStartMenu)
                {
                    await ClearStartMenu();
                }
                
                ShowSuccessMessage(clearStartMenu);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during bloatware removal", ex);
                ShowError("Error during bloatware removal", ex);
            }
            finally
            {
                CleanupScript(scriptPath);
            }
        }

        public async Task RemoveWindowsBloatware(bool clearStartMenu = true, bool createRestorePoint = true)
        {
            var apps = await GetInstalledApps();
            await RemoveWindowsBloatware(apps, clearStartMenu, createRestorePoint);
        }

        public async Task RemoveWindowsBloatwareWithStartMenuChoice(IEnumerable<WindowsApp> appsToKeep, bool createRestorePoint = true)
        {
            bool clearStartMenu = await AskUserAboutStartMenuClearing();
            await RemoveWindowsBloatware(appsToKeep, clearStartMenu, createRestorePoint);
        }

        public async Task RemoveWindowsBloatwareWithStartMenuChoice(bool createRestorePoint = true)
        {
            var apps = await GetInstalledApps();
            await RemoveWindowsBloatwareWithStartMenuChoice(apps, createRestorePoint);
        }

        public async Task ClearStartMenuWithRecommendationsDisabled()
        {
            await ClearStartMenu();
        }

        private async Task<bool> AskUserAboutStartMenuClearing()
        {
            var result = CustomMessageBox.Show(
                "Would you like to clear the Windows Start Menu and disable recommendations?\n\n" +
                "This will:\n" +
                "• Remove all pinned applications from the Start Menu\n" +
                "• Disable personalized recommendations\n" +
                "• Create a clean, minimal Start Menu experience\n\n" +
                "You can always restore your previous Start Menu from backup files.",
                "Clear Start Menu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            return result == MessageBoxResult.Yes;
        }

        private async Task<bool> AskUserAboutEdgeRemoval()
        {
            var result = CustomMessageBox.Show(
                "Would you like to remove Microsoft Edge during this cleanup?\n\n" +
                "⚠️  WARNING: Removing Microsoft Edge may cause issues:\n" +
                "• Some Windows features may not work properly\n" +
                "• Windows Update may fail to download updates\n" +
                "• Some applications may have compatibility issues\n" +
                "• You may need to reinstall Edge later for system stability\n\n" +
                "Microsoft Edge is often required for Windows Updates and system functionality.\n\n" +
                "Do you want to proceed with Edge removal?",
                "Remove Microsoft Edge",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            return result == MessageBoxResult.Yes;
        }

        private async Task RemoveMicrosoftEdge()
        {
            try
            {
                _logger.LogInformation("Starting Microsoft Edge removal");
                
                // Get the directory where the executable is located
                string exePath = Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);
                
                _logger.LogInformation("Executable path: {Path}", exePath);
                _logger.LogInformation("Executable directory: {Path}", exeDir);
                _logger.LogInformation("Current working directory: {Path}", Directory.GetCurrentDirectory());
                
                // Look for Scripts folder in the same directory as the executable
                string scriptsPath = Path.Combine(exeDir, "Scripts");
                _logger.LogInformation("Trying scripts path 1: {Path}", scriptsPath);
                
                if (!Directory.Exists(scriptsPath))
                {
                    // Try the current working directory
                    scriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts");
                    _logger.LogInformation("Trying scripts path 2: {Path}", scriptsPath);
                    
                    if (!Directory.Exists(scriptsPath))
                    {
                        _logger.LogWarning("Scripts folder not found. Tried: {Path1} and {Path2}", 
                            Path.Combine(exeDir, "Scripts"), 
                            Path.Combine(Directory.GetCurrentDirectory(), "Scripts"));
                        CustomMessageBox.Show(
                            "Scripts folder not found. Please ensure the 'Scripts' folder is present in the application directory.",
                            "Scripts Folder Missing",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                _logger.LogInformation("Found Scripts folder at: {Path}", scriptsPath);

                // Find the Edge removal PowerShell script
                string scriptPath = Path.Combine(scriptsPath, "RemoveEdge.ps1");
                if (!File.Exists(scriptPath))
                {
                    _logger.LogWarning("RemoveEdge.ps1 not found at: {Path}", scriptPath);
                    CustomMessageBox.Show(
                        "Edge removal script not found. Please ensure RemoveEdge.ps1 is present in the 'Scripts' folder.",
                        "Edge Removal Script Missing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _logger.LogInformation("Found RemoveEdge.ps1 at: {Path}", scriptPath);
                _logger.LogInformation("Executing Edge removal script: {Path}", scriptPath);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = POWERSHELL_PATH,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    var error = "Failed to start Edge removal PowerShell process";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("Edge removal script completed with non-zero exit code: {ExitCode}", process.ExitCode);
                    CustomMessageBox.Show(
                        "Edge removal completed with warnings. Some components may not have been removed successfully.",
                        "Edge Removal Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    _logger.LogInformation("Edge removal script completed successfully");
                    CustomMessageBox.Show(
                        "Microsoft Edge has been successfully removed from your system.",
                        "Edge Removal Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to remove Microsoft Edge", ex);
                CustomMessageBox.Show(
                    $"Error removing Microsoft Edge: {ex.Message}",
                    "Edge Removal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task<string> CreateRemovalScript(IEnumerable<WindowsApp> appsToKeep)
        {
            try
            {
                string scriptPath = Path.Combine(Path.GetTempPath(), TEMP_SCRIPT_NAME);
                string scriptContent = await LoadRemovalScriptContent();

                // Use the full session essential apps list for apps to keep
                var keepAppNames = EssentialApps;
                _logger.LogInformation("Creating removal script with {Count} apps to keep: {Apps}",
                    keepAppNames.Count,
                    string.Join(", ", keepAppNames));

                scriptContent = scriptContent.Replace(
                    "__APP_NAMES_PLACEHOLDER__",
                    string.Join("','", keepAppNames)
                );

                await File.WriteAllTextAsync(scriptPath, scriptContent);
                _logger.LogInformation("Created removal script at: {Path}", scriptPath);
                return scriptPath;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create removal script", ex);
                throw;
            }
        }

        private async Task ExecuteRemovalScript(string scriptPath)
        {
            try
            {
                _logger.LogInformation("Executing removal script: {Path}", scriptPath);
                var startInfo = new ProcessStartInfo()
                {
                    FileName = POWERSHELL_PATH,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    var error = "Failed to start PowerShell process";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("Removal script completed with non-zero exit code: {ExitCode}", process.ExitCode);
                    ShowWarning();
                }
                else
                {
                    _logger.LogInformation("Removal script completed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to execute removal script", ex);
                throw;
            }
        }

        private async Task<string> RunPowerShellScript(string scriptPath, bool elevated = true)
        {
            try
            {
                _logger.LogInformation("Running PowerShell script: {Path} (Elevated: {Elevated})", scriptPath, elevated);
                var startInfo = new ProcessStartInfo()
                {
                    FileName = POWERSHELL_PATH,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                if (elevated)
                {
                    startInfo.Verb = "runas";
                }

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    var error = "Failed to start PowerShell process";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("Script completed with non-zero exit code: {ExitCode}", process.ExitCode);
                }
                else
                {
                    _logger.LogInformation("Script completed successfully");
                }

                return output;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to run PowerShell script", ex);
                throw;
            }
        }

        private void CleanupScript(string scriptPath)
        {
            try
            {
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                    _logger.LogInformation("Cleaned up script: {Path}", scriptPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to cleanup script {Path}: {Error}", scriptPath, ex.Message);
            }
        }

        private string GetDisplayName(string appName)
        {
            if (appName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            {
                appName = appName.Substring("Microsoft.".Length);
            }

            var parts = System.Text.RegularExpressions.Regex.Split(appName, @"(?<!^)(?=[A-Z])|[.]")
                .Where(p => !string.IsNullOrWhiteSpace(p));

            return string.Join(" ", parts);
        }

        private void ShowStartupMessage()
        {
            var message = "Starting Windows bloatware removal. This will remove unnecessary Windows apps while keeping selected ones.\n\n" +
                         "McAfee and Norton products will also be removed if they are installed.\n\n" +
                         "A system restore point will be created before making changes.";
            
            _logger.LogInformation("Showing startup message to user");
            CustomMessageBox.Show(
                message,
                "Starting Bloatware Removal",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowSuccessMessage(bool clearStartMenu)
        {
            var message = "Windows bloatware has been successfully removed while keeping selected apps!";
            
            if (clearStartMenu)
            {
                message += "\n\nThe start menu has also been cleared of all pinned applications.";
            }
            
            message += "\n\nSome apps may require a system restart to be fully removed.";
            
            _logger.LogInformation("Showing success message to user");
            CustomMessageBox.Show(
                message,
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowWarning()
        {
            var message = "Some apps may not have been removed successfully. Please check the PowerShell window for details.";
            
            _logger.LogWarning("Showing warning message to user");
            CustomMessageBox.Show(
                message,
                "Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void ShowError(string message, Exception ex)
        {
            _logger.LogError("Showing error message to user: {Message}", message);
            CustomMessageBox.Show(
                $"{message}: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private async Task ClearStartMenu()
        {
            _logger.LogOperationStart("Clearing start menu");
            string scriptPath = await CreateStartMenuScript();

            try
            {
                await ExecuteStartMenuScript(scriptPath);
                _logger.LogOperationComplete("Clearing start menu");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during start menu clearing", ex);
                // Don't show error to user as this is a secondary operation
            }
            finally
            {
                CleanupScript(scriptPath);
            }
        }

        private async Task<string> CreateStartMenuScript()
        {
            try
            {
                string scriptPath = Path.Combine(Path.GetTempPath(), TEMP_START_MENU_SCRIPT_NAME);
                string scriptContent = await LoadStartMenuScriptContent();
                
                // Extract the start2.bin file to the same directory as the script
                string assetsPath = Path.Combine(Path.GetDirectoryName(scriptPath)!, "Assets", "Start");
                Directory.CreateDirectory(assetsPath);
                await ExtractStartMenuAssets(assetsPath);
                
                await File.WriteAllTextAsync(scriptPath, scriptContent);
                _logger.LogInformation("Created start menu script at: {Path}", scriptPath);
                return scriptPath;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create start menu script", ex);
                throw;
            }
        }

        private async Task ExtractStartMenuAssets(string assetsPath)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "ClearGlass.Resources.Assets.Start.start2.bin";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    var error = $"Could not find embedded asset: {resourceName}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                string binFilePath = Path.Combine(assetsPath, "start2.bin");
                using var fileStream = File.Create(binFilePath);
                await stream.CopyToAsync(fileStream);
                
                _logger.LogInformation("Extracted start2.bin to: {Path}", binFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to extract start menu assets", ex);
                throw;
            }
        }

        private async Task ExecuteStartMenuScript(string scriptPath)
        {
            try
            {
                _logger.LogInformation("Executing start menu script: {Path}", scriptPath);
                var arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Silent -AllUsers";
                
                var startInfo = new ProcessStartInfo()
                {
                    FileName = POWERSHELL_PATH,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    var error = "Failed to start PowerShell process for start menu clearing";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("Start menu script completed with non-zero exit code: {ExitCode}", process.ExitCode);
                }
                else
                {
                    _logger.LogInformation("Start menu script completed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to execute start menu script", ex);
                throw;
            }
        }
    }
} 
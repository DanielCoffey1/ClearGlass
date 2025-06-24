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

        private List<string> _sessionEssentialApps = new();

        public BloatwareService(LoggingService logger)
        {
            _logger = logger;
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

        public async Task RemoveWindowsBloatware(IEnumerable<WindowsApp> appsToKeep)
        {
            _logger.LogOperationStart("Removing Windows bloatware");
            ShowStartupMessage();
            
            string scriptPath = await CreateRemovalScript(appsToKeep);

            try
            {
                await ExecuteRemovalScript(scriptPath);
                _logger.LogOperationComplete("Removing Windows bloatware");
                
                // Clear start menu after bloatware removal
                await ClearStartMenu();
                
                ShowSuccessMessage();
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

        public async Task RemoveWindowsBloatware()
        {
            var apps = await GetInstalledApps();
            await RemoveWindowsBloatware(apps);
        }

        private async Task<string> CreateRemovalScript(IEnumerable<WindowsApp> appsToKeep)
        {
            try
            {
                string scriptPath = Path.Combine(Path.GetTempPath(), TEMP_SCRIPT_NAME);
                string scriptContent = await LoadRemovalScriptContent();
                
                var selectedApps = appsToKeep.Where(a => a.IsSelected).Select(a => a.Name).ToList();
                _logger.LogInformation("Creating removal script with {Count} apps to keep: {Apps}", 
                    selectedApps.Count, 
                    string.Join(", ", selectedApps));

                scriptContent = scriptContent.Replace(
                    "__APP_NAMES_PLACEHOLDER__", 
                    string.Join("','", selectedApps)
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

        private void ShowSuccessMessage()
        {
            var message = "Windows bloatware has been successfully removed while keeping selected apps!\n\n" +
                         "The start menu has also been cleared of all pinned applications.\n\n" +
                         "Some apps may require a system restart to be fully removed.";
            
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
                var startInfo = new ProcessStartInfo()
                {
                    FileName = POWERSHELL_PATH,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Silent -AllUsers",
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
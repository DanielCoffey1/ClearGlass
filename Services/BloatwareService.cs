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
        private const string SCRIPTS_NAMESPACE = "ClearGlass.Scripts";

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

        public BloatwareService()
        {
            ResetToDefaultEssentialApps();
        }

        public void ResetToDefaultEssentialApps()
        {
            _sessionEssentialApps = new List<string>(defaultEssentialApps);
        }

        public void UpdateSessionEssentialApps(IEnumerable<WindowsApp> selectedApps)
        {
            _sessionEssentialApps = new List<string>(defaultEssentialApps);
            _sessionEssentialApps.AddRange(
                selectedApps
                    .Where(a => a.IsSelected)
                    .Select(a => a.Name)
                    .Where(name => !_sessionEssentialApps.Contains(name))
            );
        }

        public IReadOnlyList<string> EssentialApps => _sessionEssentialApps;

        public async Task<ObservableCollection<WindowsApp>> GetInstalledApps()
        {
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
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Error getting installed apps", ex);
            }
            finally
            {
                CleanupScript(scriptPath);
            }

            return apps;
        }

        private async Task<string> LoadRemovalScriptContent()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{SCRIPTS_NAMESPACE}.RemoveBloatware.ps1";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Could not find embedded script: {resourceName}");
            }

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        private async Task<string> LoadGetAppsScriptContent()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{SCRIPTS_NAMESPACE}.GetInstalledApps.ps1";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Could not find embedded script: {resourceName}");
            }

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        private async Task<string> CreateGetAppsScript()
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), TEMP_GET_APPS_SCRIPT_NAME);
            string scriptContent = await LoadGetAppsScriptContent();
            await File.WriteAllTextAsync(scriptPath, scriptContent);
            return scriptPath;
        }

        public async Task RemoveWindowsBloatware(IEnumerable<WindowsApp> appsToKeep)
        {
            ShowStartupMessage();
            string scriptPath = await CreateRemovalScript(appsToKeep);

            try
            {
                await ExecuteRemovalScript(scriptPath);
                ShowSuccessMessage();
            }
            catch (Exception ex)
            {
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
            string scriptPath = Path.Combine(Path.GetTempPath(), TEMP_SCRIPT_NAME);
            string scriptContent = await LoadRemovalScriptContent();
            
            // Replace the apps to keep placeholder
            scriptContent = scriptContent.Replace(
                "__APP_NAMES_PLACEHOLDER__", 
                string.Join("','", appsToKeep.Where(a => a.IsSelected).Select(a => a.Name))
            );

            await File.WriteAllTextAsync(scriptPath, scriptContent);
            return scriptPath;
        }

        private async Task ExecuteRemovalScript(string scriptPath)
        {
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
                throw new InvalidOperationException("Failed to start PowerShell process");
            }

            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                ShowWarning();
            }
        }

        private async Task<string> RunPowerShellScript(string scriptPath, bool elevated = true)
        {
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
                throw new InvalidOperationException("Failed to start PowerShell process");
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        private void CleanupScript(string scriptPath)
        {
            try
            {
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch
            {
                // Best effort cleanup, ignore errors
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
            CustomMessageBox.Show(
                "Starting Windows bloatware removal. This will remove unnecessary Windows apps while keeping selected ones.\n\n" +
                "McAfee and Norton products will also be removed if they are installed.\n\n" +
                "A system restore point will be created before making changes.",
                "Starting Bloatware Removal",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowSuccessMessage()
        {
            CustomMessageBox.Show(
                "Windows bloatware has been successfully removed while keeping selected apps!\n\n" +
                "Some apps may require a system restart to be fully removed.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowWarning()
        {
            CustomMessageBox.Show(
                "Some apps may not have been removed successfully. Please check the PowerShell window for details.",
                "Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void ShowError(string message, Exception ex)
        {
            CustomMessageBox.Show(
                $"{message}: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
} 
using System.Windows;
using System.Threading.Tasks;
using ClearGlass.Services;
using System;
using System.Windows.Threading;
using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using ClearGlass.Models;
using System.Windows.Controls;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClearGlass
{
    public partial class MainWindow : Window
    {
        private readonly ThemeService _themeService;
        private readonly OptimizationService _optimizationService;
        private readonly BloatwareService _bloatwareService;
        private readonly WingetService _wingetService;
        private readonly UninstallService _uninstallService;
        private readonly UpdateService _updateService;
        private bool _isThemeChanging = false;
        private readonly string _wallpaperPath;
        private readonly string _autologonPath;
        private Storyboard _showAddonsOverlay = null!;
        private Storyboard _hideAddonsOverlay = null!;
        private Storyboard _showOptimizationOverlay = null!;
        private Storyboard _hideOptimizationOverlay = null!;
        private Storyboard _showKeepAppsOverlay = null!;
        private Storyboard _hideKeepAppsOverlay = null!;
        private Storyboard _showTweaksOverlay = null!;
        private Storyboard _hideTweaksOverlay = null!;
        private Storyboard _showRemoveAppsOverlay = null!;
        private Storyboard _hideRemoveAppsOverlay = null!;
        private ObservableCollection<WindowsApp>? _installedApps;
        private readonly ObservableCollection<InstalledApp> _installedAppsCollection = new();
        private List<InstalledApp> _originalAppsList = new();
        private List<WindowsApp> _originalKeepAppsList = new();

        public MainWindow()
        {
            InitializeComponent();
            _themeService = new ThemeService();
            _optimizationService = new OptimizationService();
            var loggingService = new LoggingService();
            _bloatwareService = new BloatwareService(loggingService);
            _wingetService = new WingetService();
            _uninstallService = new UninstallService(_wingetService);
            _updateService = new UpdateService();
            
            // Store in Windows' tools directory
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _autologonPath = Path.Combine(commonAppData, "ClearGlass", "Tools", "Autologon.exe");
                
            // Store wallpaper in application's local data
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _wallpaperPath = Path.Combine(localAppData, "ClearGlass", "Wallpapers", "glassbackground.png");
            
            // Ensure wallpaper directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_wallpaperPath));
            
            // Extract wallpaper from resources if it doesn't exist
            if (!File.Exists(_wallpaperPath))
            {
                ExtractWallpaperFromResources();
            }

            // Enable window dragging
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };

            // Initialize storyboards
            _showAddonsOverlay = (Storyboard)FindResource("ShowAddonsOverlay");
            _hideAddonsOverlay = (Storyboard)FindResource("HideAddonsOverlay");
            _showOptimizationOverlay = (Storyboard)FindResource("ShowOptimizationOverlay");
            _hideOptimizationOverlay = (Storyboard)FindResource("HideOptimizationOverlay");
            _showKeepAppsOverlay = (Storyboard)FindResource("ShowKeepAppsOverlay");
            _hideKeepAppsOverlay = (Storyboard)FindResource("HideKeepAppsOverlay");
            _showTweaksOverlay = (Storyboard)FindResource("ShowTweaksOverlay");
            _hideTweaksOverlay = (Storyboard)FindResource("HideTweaksOverlay");
            _showRemoveAppsOverlay = (Storyboard)FindResource("ShowRemoveAppsOverlay");
            _hideRemoveAppsOverlay = (Storyboard)FindResource("HideRemoveAppsOverlay");
            
            // Ensure overlays are hidden initially
            AddonsOverlay.Opacity = 0;
            AddonsOverlay.Margin = new Thickness(0, 600, 0, -600);
            OptimizationOverlay.Opacity = 0;
            OptimizationOverlay.Margin = new Thickness(0, 600, 0, -600);
            KeepAppsOverlay.Opacity = 0;
            KeepAppsOverlay.Margin = new Thickness(0, 600, 0, -600);
            TweaksOverlay.Opacity = 0;
            TweaksOverlay.Margin = new Thickness(0, 600, 0, -600);
            RemoveAppsOverlay.Opacity = 0;
            RemoveAppsOverlay.Margin = new Thickness(0, 600, 0, -600);

            // Set the ItemsSource for the InstalledAppsList
            InstalledAppsList.ItemsSource = _installedAppsCollection;

            // Load current system settings into toggle states
            LoadCurrentSettings();

            CheckForUpdatesAsync();
        }

        private void ExtractWallpaperFromResources()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("ClearGlass.Resources.Wallpapers.glassbackground.png"))
                {
                    if (stream == null)
                    {
                        throw new InvalidOperationException("Wallpaper resource not found in assembly");
                    }
                    
                    using (var fileStream = new FileStream(_wallpaperPath, FileMode.Create))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error extracting wallpaper: {ex.Message}",
                    "Wallpaper Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void LoadCurrentSettings()
        {
            TaskbarAlignmentToggle.IsChecked = _themeService.IsTaskbarCentered;
            TaskViewToggle.IsChecked = _themeService.IsTaskViewEnabled;
            SearchToggle.IsChecked = _themeService.IsSearchVisible;
            DesktopIconsToggle.IsChecked = _themeService.AreDesktopIconsVisible;
            ThemeToggle.IsChecked = _themeService.IsDarkMode;
        }

        private void OnTaskbarAlignmentToggle(object sender, RoutedEventArgs e)
        {
            _themeService.IsTaskbarCentered = TaskbarAlignmentToggle.IsChecked ?? false;
        }

        private void OnTaskViewToggle(object sender, RoutedEventArgs e)
        {
            _themeService.IsTaskViewEnabled = TaskViewToggle.IsChecked ?? false;
        }

        private void OnSearchToggle(object sender, RoutedEventArgs e)
        {
            _themeService.IsSearchVisible = SearchToggle.IsChecked ?? false;
        }

        private void OnDesktopIconsToggle(object sender, RoutedEventArgs e)
        {
            _themeService.AreDesktopIconsVisible = DesktopIconsToggle.IsChecked ?? false;
        }

        private async void OnThemeToggle(object sender, RoutedEventArgs e)
        {
            if (_isThemeChanging)
            {
                e.Handled = true;
                return;
            }

            _isThemeChanging = true;
            bool isDarkMode = ThemeToggle.IsChecked ?? false;

            try
            {
                // Disable only theme toggle during change
                ThemeToggle.IsEnabled = false;

                await Task.Run(() =>
                {
                    try
                    {
                        _themeService.IsDarkMode = isDarkMode;
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                        {
                            CustomMessageBox.Show(
                                $"Error changing theme: {ex.Message}",
                                "Theme Change Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);

                            // Revert the toggle state
                            ThemeToggle.IsChecked = !isDarkMode;
                        }));
                    }
                });

                // Shorter delay to let the theme change settle
                await Task.Delay(400);
            }
            finally
            {
                ThemeToggle.IsEnabled = true;
                _isThemeChanging = false;
            }
        }

        private async Task EnsureWallpaperAsync()
        {
            const int maxRetries = 3;
            const int retryDelayMs = 1000;
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Ensure the wallpaper file exists
                    if (!File.Exists(_wallpaperPath))
                    {
                        ExtractWallpaperFromResources();
                        await Task.Delay(200); // Short delay after extraction
                    }

                    // Double-check the file exists after potential extraction
                    if (!File.Exists(_wallpaperPath))
                    {
                        throw new FileNotFoundException("Wallpaper file not found even after extraction attempt");
                    }

                    // Set the wallpaper
                    _themeService.SetWallpaper(_wallpaperPath);
                    
                    // Verify the wallpaper was set by checking the registry
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                    {
                        string? currentWallpaper = key?.GetValue("WallPaper") as string;
                        if (string.IsNullOrEmpty(currentWallpaper) || !currentWallpaper.Equals(_wallpaperPath, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception("Wallpaper verification failed - registry value not updated");
                        }
                    }

                    // If we get here, wallpaper was successfully set
                    Debug.WriteLine($"Wallpaper successfully set on attempt {attempt}");
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.WriteLine($"Attempt {attempt} failed: {ex.Message}");
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs * attempt); // Exponential backoff
                        Debug.WriteLine($"Retrying wallpaper set... (Attempt {attempt + 1}/{maxRetries})");
                    }
                }
            }

            // If we get here, all attempts failed
            CustomMessageBox.Show(
                $"Failed to set wallpaper after {maxRetries} attempts: {lastException?.Message}\n\n" +
                "You can try manually setting the wallpaper from:\n" +
                _wallpaperPath,
                "Wallpaper Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private async void OnClearGlassThemeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ClearGlassThemeButton.IsEnabled = false;

                // Show desktop icons first
                DesktopIconsToggle.IsChecked = true;
                _themeService.AreDesktopIconsVisible = true;
                await Task.Delay(200);

                // Apply dark theme first as it's a major change
                ThemeToggle.IsChecked = true;
                await Task.Run(() => _themeService.IsDarkMode = true);
                await Task.Delay(500);

                // First shell refresh after theme change
                _themeService.RefreshWindows();
                await Task.Delay(1000); // Increased delay after major theme change

                // Apply taskbar settings
                TaskbarAlignmentToggle.IsChecked = false;
                _themeService.IsTaskbarCentered = false;
                await Task.Delay(200);

                // Apply task view settings
                TaskViewToggle.IsChecked = false;
                _themeService.IsTaskViewEnabled = false;
                await Task.Delay(100);

                // Show search first to ensure proper state, then hide
                SearchToggle.IsChecked = true;
                _themeService.IsSearchVisible = true;
                await Task.Delay(200);

                SearchToggle.IsChecked = false;
                _themeService.IsSearchVisible = false;
                await Task.Delay(200);

                // Second shell refresh after UI changes
                _themeService.RefreshWindows();
                await Task.Delay(1000); // Increased delay before wallpaper

                // Hide desktop icons
                DesktopIconsToggle.IsChecked = false;
                _themeService.AreDesktopIconsVisible = false;
                await Task.Delay(500); // Increased delay after hiding icons

                // Final step: Apply Clear Glass wallpaper after all UI changes are complete
                await EnsureWallpaperAsync();

                CustomMessageBox.Show(
                    "Clear Glass Theme applied successfully!\n\n" +
                    "Some changes may take a few seconds to fully apply.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error applying Clear Glass Theme: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ClearGlassThemeButton.IsEnabled = true;
            }
        }

        private async void OnClearGlassClick(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.Show(
                "This will apply the complete Clear Glass experience:\n\n" +
                "1. Optimize Windows settings (privacy, performance, services)\n" +
                "2. Remove Windows AI components (Copilot, Recall, etc.)\n" +
                "3. Remove unnecessary Windows bloatware\n" +
                "4. Apply the Clear Glass theme (dark mode, left-aligned taskbar, etc.)\n\n" +
                "A system restore point will be created before making changes.\n\n" +
                "Do you want to continue?",
                "Apply Complete Clear Glass Experience",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Show Copilot+ PC warning
                var copilotWarning = CustomMessageBox.Show(
                    "Windows Copilot+ PC Warning\n\n" +
                    "If you have a Windows Copilot+ PC with Recall enabled, you may need to manually terminate a service during AI removal:\n\n" +
                    "1. Right-click the taskbar → Task Manager\n" +
                    "2. Search for 'wsaifabricsvc' \n" +
                    "3. Right-click it → End Task\n" +
                    "4. Click 'Shut down' on the Windows warning popup\n" +
                    "5. Return here and click OK\n\n" +
                    "If you don't have a Copilot+ PC or Recall enabled, you can safely click OK to continue.",
                    "Windows Copilot+ PC Warning",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (copilotWarning == MessageBoxResult.Cancel)
                    return;

                // Ask user if they want to apply additional tweaks
                var tweaksResult = CustomMessageBox.Show(
                    "Would you like to apply additional system tweaks?\n\n" +
                    "This will apply:\n" +
                    "• Remove OneDrive\n" +
                    "• Disable Search Suggestions\n" +
                    "• Disable Privacy Permissions\n" +
                    "• Enable End Task in Taskbar\n" +
                    "• Set This PC as Default\n" +
                    "• Restore Classic Context Menu\n\n" +
                    "These tweaks will be applied before the main Clear Glass functions.",
                    "Apply Additional Tweaks",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                try
                {
                    ClearGlassButton.IsEnabled = false;

                    // Show unified progress dialog
                    ProgressDialog progressDialog = null!;
                    progressDialog = new ProgressDialog(
                        (step) => progressDialog?.UpdateProgress(step, 0), // This will be updated in the try block
                        () => { } // Empty completion action
                    );
                    progressDialog.Show();

                    try
                    {
                        // Apply tweaks first if user chose to
                        if (tweaksResult == MessageBoxResult.Yes)
                        {
                            progressDialog.UpdateProgress("Applying system tweaks...", 10);
                            await ApplyAllTweaksSilent();
                            progressDialog.UpdateProgress("System tweaks applied", 20);
                        }

                        // Run Windows settings optimization
                        progressDialog.UpdateProgress("Optimizing Windows settings...", 30);
                        await _optimizationService.TweakWindowsSettingsSilent();
                        progressDialog.UpdateProgress("Windows settings optimized", 40);

                        // Run Windows AI component removal
                        progressDialog.UpdateProgress("Removing Windows AI components...", 50);
                        await _optimizationService.RemoveWindowsAIOnlySilent();
                        progressDialog.UpdateProgress("AI components removed", 60);

                        // Run bloatware removal
                        progressDialog.UpdateProgress("Removing Windows bloatware...", 70);
                        await _bloatwareService.RemoveWindowsBloatwareSilent();
                        progressDialog.UpdateProgress("Bloatware removed", 80);

                        // Apply Clear Glass theme
                        progressDialog.UpdateProgress("Applying Clear Glass theme...", 90);
                        await ApplyClearGlassThemeSilent();
                        progressDialog.UpdateProgress("Theme applied", 100);

                        // Close progress dialog
                        progressDialog.Close();

                        // Single final success message
                        CustomMessageBox.Show(
                            "🎉 Clear Glass experience has been fully applied!\n\n" +
                            "✅ Windows optimized and privacy enhanced\n" +
                            "✅ AI components removed\n" +
                            "✅ Bloatware cleaned up\n" +
                            "✅ Modern glass theme applied\n\n" +
                            "Some changes may require a system restart to take full effect.",
                            "Clear Glass Complete!",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        progressDialog.Close();
                        throw; // Re-throw to be caught by outer try-catch
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(
                        $"Error applying Clear Glass experience: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    ClearGlassButton.IsEnabled = true;
                }
            }
        }

        private async Task ApplyAllTweaks()
        {
            try
            {
                // Show progress message
                CustomMessageBox.Show(
                    "Applying system tweaks...\n\n" +
                    "This may take a few moments. Please wait.",
                    "Applying Tweaks",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Apply all tweaks in sequence
                await RemoveOneDrive();
                await DisableSearchSuggestions();
                await DisablePrivacyPermissions();
                await EnableEndTask();
                await SetThisPCDefault();
                await RestoreClassicMenu();

                CustomMessageBox.Show(
                    "All system tweaks have been applied successfully!",
                    "Tweaks Applied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error applying tweaks: {ex.Message}\n\n" +
                    "The main Clear Glass functions will continue.",
                    "Tweak Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task ApplyAllTweaksSilent()
        {
            try
            {
                // Apply all tweaks in sequence without user dialogs
                await RemoveOneDriveSilent();
                await DisableSearchSuggestionsSilent();
                await DisablePrivacyPermissionsSilent();
                await EnableEndTaskSilent();
                await SetThisPCDefaultSilent();
                await RestoreClassicMenuSilent();
            }
            catch (Exception ex)
            {
                // Log error but continue with main process
                Debug.WriteLine($"Error applying tweaks silently: {ex.Message}");
            }
        }

        private async Task RemoveOneDrive()
        {
            try
            {
                // Step 1: Kill OneDrive processes
                foreach (var process in Process.GetProcessesByName("OneDrive"))
                {
                    process.Kill();
                }

                // Step 2: Try uninstalling via winget first
                try
                {
                    if (await _wingetService.IsWingetInstalled())
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "winget",
                            Arguments = "uninstall \"Microsoft OneDrive\" --silent",
                            UseShellExecute = true,
                            Verb = "runas"
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                await process.WaitForExitAsync();
                            }
                        }
                    }
                }
                catch (Exception) { } // Continue if winget fails

                // Step 3: Try uninstalling via built-in uninstaller
                try
                {
                    var oneDrivePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive", "OneDrive.exe");
                    if (File.Exists(oneDrivePath))
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = oneDrivePath,
                            Arguments = "/uninstall",
                            UseShellExecute = true,
                            Verb = "runas"
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                await process.WaitForExitAsync();
                            }
                        }
                    }
                }
                catch (Exception) { } // Continue if uninstaller fails

                // Step 4: Remove OneDrive from Windows Store if present
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "Get-AppxPackage *Microsoft.OneDrive* | Remove-AppxPackage",
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                        }
                    }
                }
                catch (Exception) { } // Continue if Store removal fails

                // Step 5: Clean up registry entries
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = "delete \"HKEY_CLASSES_ROOT\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}\" /f",
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                        }
                    }

                    startInfo.Arguments = "delete \"HKEY_CLASSES_ROOT\\Wow6432Node\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}\" /f";
                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                        }
                    }
                }
                catch (Exception) { } // Continue if registry cleanup fails
            }
            catch (Exception) { } // Continue if OneDrive removal fails
        }

        private async Task DisableSearchSuggestions()
        {
            try
            {
                string script = @"
                    Write-Host 'Disabling search suggestions...'
                    
                    # Disable web search in Windows Search
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'BingSearchEnabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'CortanaConsent' -Value 0 -Type DWord -Force
                    
                    # Disable search suggestions
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -Value 1 -Type DWord -Force
                    
                    # Disable web results in search
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'AllowSearchToUseLocation' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'AllowCortana' -Value 0 -Type DWord -Force
                    
                    Write-Host 'Search suggestions have been disabled successfully!'
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "ClearGlassDisableSearch.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    RedirectStandardOutput = false
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }

                // Clean up
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { } // Continue if search suggestions disable fails
        }

        private async Task DisablePrivacyPermissions()
        {
            try
            {
                string script = @"
                    # Disable advertising ID
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord -Force
                    
                    # Disable language list access
                    Set-ItemProperty -Path 'HKCU:\Control Panel\International\User Profile' -Name 'HttpAcceptLanguageOptOut' -Value 1 -Type DWord -Force
                    
                    # Disable app launch tracking
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'Start_TrackProgs' -Value 0 -Type DWord -Force
                    
                    # Disable suggested content in settings
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338393Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-353694Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338389Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-310093Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'ShowSyncProviderNotifications' -Value 0 -Type DWord -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "DisablePrivacyPermissions.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                }

                // Clean up
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { } // Continue if privacy permissions disable fails
        }

        private async Task EnableEndTask()
        {
            try
            {
                string script = @"
                    # Enable End Task in Settings
                    if (!(Test-Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings')) {
                        New-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Force
                    }
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Name 'TaskbarEndTask' -Value 1 -Type DWord -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "EnableEndTask.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                }

                // Clean up
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { } // Continue if end task enable fails
        }

        private async Task SetThisPCDefault()
        {
            try
            {
                string script = @"
                    # Remove Home and Gallery from File Explorer
                    if (!(Test-Path 'HKCU:\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}')) {
                        New-Item -Path 'HKCU:\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}' -Force
                    }
                    Set-ItemProperty -Path 'HKCU:\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}' -Name 'System.IsPinnedToNameSpaceTree' -Value 0 -Type DWord -Force

                    # Disable Gallery View
                    if (!(Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced')) {
                        New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Force
                    }
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'LaunchTo' -Value 1 -Type DWord -Force

                    # Restart Explorer to apply changes
                    Stop-Process -Name explorer -Force
                    Start-Process explorer
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "SetThisPCDefault.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                }

                // Clean up
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { } // Continue if set this PC default fails
        }

        private async Task RestoreClassicMenu()
        {
            try
            {
                string script = @"
                    # Restore classic context menu
                    if (!(Test-Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32')) {
                        New-Item -Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' -Force | Out-Null
                    }
                    Set-ItemProperty -Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' -Name '(Default)' -Value '' -Type String -Force

                    # Restart Explorer to apply changes
                    Stop-Process -Name explorer -Force
                    Start-Process explorer
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "RestoreClassicMenu.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                }

                // Clean up
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { } // Continue if restore classic menu fails
        }

        private void OnWindowsOptimizationClick(object sender, RoutedEventArgs e)
        {
            OptimizationOverlay.Visibility = Visibility.Visible;
            _showOptimizationOverlay.Begin(this);
        }

        private void OnCloseOptimizationClick(object sender, RoutedEventArgs e)
        {
            _hideOptimizationOverlay.Begin(this, isControllable: false);
            _hideOptimizationOverlay.Completed += (s, _) =>
            {
                OptimizationOverlay.Visibility = Visibility.Collapsed;
            };
        }

        private async void OnTweakSettingsClick(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.Show(
                "This will modify various Windows settings to optimize your system. A restore point will be created before making changes. Do you want to continue?",
                "Confirm Windows Settings Optimization",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _optimizationService.TweakWindowsSettings();
            }
        }

        private async void OnRemoveBloatwareClick(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.Show(
                "This will remove unnecessary Windows apps while keeping essential system components and useful applications.\n\n" +
                "A system restore point will be created before making changes.\n\n" +
                "Do you want to continue?",
                "Confirm Windows Bloatware Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _bloatwareService.RemoveWindowsBloatwareWithStartMenuChoice();
            }
        }

        private async void OnRunOptimizationClick(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.Show(
                "This will:\n\n" +
                "1. Optimize Windows settings (privacy, performance, services)\n" +
                "2. Remove Windows AI components (Copilot, Recall, etc.)\n" +
                "3. Remove unnecessary Windows bloatware\n\n" +
                "A system restore point will be created before making changes.\n\n" +
                "Do you want to continue?",
                "Confirm Full Windows Optimization",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Show Copilot+ PC warning
                var copilotWarning = CustomMessageBox.Show(
                    "Windows Copilot+ PC Warning\n\n" +
                    "If you have a Windows Copilot+ PC with Recall enabled, you may need to manually terminate a service during AI removal:\n\n" +
                    "1. Right-click the taskbar → Task Manager\n" +
                    "2. Search for 'wsaifabricsvc' \n" +
                    "3. Right-click it → End Task\n" +
                    "4. Click 'Shut down' on the Windows warning popup\n" +
                    "5. Return here and click OK\n\n" +
                    "If you don't have a Copilot+ PC or Recall enabled, you can safely click OK to continue.",
                    "Windows Copilot+ PC Warning",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (copilotWarning == MessageBoxResult.Cancel)
                    return;

                try
                {
                    // Run Windows settings optimization
                    await _optimizationService.TweakWindowsSettings();
                    
                    // Run Windows AI components removal
                    await _optimizationService.RemoveWindowsAIOnly();
                    
                    // Run bloatware removal
                    await _bloatwareService.RemoveWindowsBloatwareWithStartMenuChoice();

                    CustomMessageBox.Show(
                        "Full Windows optimization completed successfully!\n\n" +
                        "The optimization included:\n" +
                        "• Windows settings optimization\n" +
                        "• Windows AI components removal\n" +
                        "• Windows bloatware removal\n\n" +
                        "Some changes may require a system restart to take full effect.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(
                        $"Error during optimization: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void OnRecommendedAddonsClick(object sender, RoutedEventArgs e)
        {
            _showAddonsOverlay.Begin();
        }

        private void OnCloseAddonsClick(object sender, RoutedEventArgs e)
        {
            _hideAddonsOverlay.Begin(this, isControllable: false);
            _hideAddonsOverlay.Completed += (s, _) =>
            {
                AddonsOverlay.Visibility = Visibility.Collapsed;
            };
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void OnAutoLoginClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button during operation
                AutoLoginButton.IsEnabled = false;

                // Show a confirmation dialog
                var result = CustomMessageBox.Show(
                    "This will launch Microsoft's Autologon tool to configure automatic login.\n\n" +
                    "Are you sure you want to continue?",
                    "Auto Login Configuration",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Create directory if it doesn't exist
                        Directory.CreateDirectory(Path.GetDirectoryName(_autologonPath));

                        // Download Autologon if it doesn't exist
                        if (!File.Exists(_autologonPath))
                        {
                            using (var client = new HttpClient())
                            {
                                var response = await client.GetAsync("https://download.sysinternals.com/files/AutoLogon.zip");
                                response.EnsureSuccessStatusCode();

                                var zipPath = Path.Combine(Path.GetDirectoryName(_autologonPath), "Autologon.zip");
                                using (var fs = new FileStream(zipPath, FileMode.Create))
                                {
                                    await response.Content.CopyToAsync(fs);
                                }

                                // Extract the zip
                                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, Path.GetDirectoryName(_autologonPath), true);
                                
                                // Clean up zip file
                                File.Delete(zipPath);
                            }
                        }

                        // Launch Autologon
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = _autologonPath,
                            UseShellExecute = true,
                            Verb = "runas" // Run as administrator
                        });
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show(
                            $"Error downloading or launching Autologon: {ex.Message}\n\n" +
                            "Please download and run Autologon manually from:\n" +
                            "https://learn.microsoft.com/en-us/sysinternals/downloads/autologon",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error configuring auto login: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                AutoLoginButton.IsEnabled = true;
            }
        }

        private async void OnKeepAppsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button during operation
                KeepAppsButton.IsEnabled = false;

                // Load installed apps if not already loaded
                if (_installedApps == null)
                {
                    var loadedApps = await _bloatwareService.GetInstalledApps();
                    var sortedApps = loadedApps.OrderBy(app => (app.DisplayName ?? app.Name)).ToList();
                    _installedApps = new ObservableCollection<WindowsApp>(sortedApps);
                    AppsListView.ItemsSource = _installedApps;
                    _originalKeepAppsList = sortedApps;
                }
                else
                {
                    var sortedApps = _installedApps.OrderBy(app => (app.DisplayName ?? app.Name)).ToList();
                    _installedApps = new ObservableCollection<WindowsApp>(sortedApps);
                    AppsListView.ItemsSource = _installedApps;
                    _originalKeepAppsList = sortedApps;
                }

                // Show the overlay
                KeepAppsOverlay.Visibility = Visibility.Visible;
                _showKeepAppsOverlay.Begin();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error configuring apps to keep: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                KeepAppsButton.IsEnabled = true;
            }
        }

        private void OnCloseKeepAppsClick(object sender, RoutedEventArgs e)
        {
            _hideKeepAppsOverlay.Begin(this, isControllable: false);
            _hideKeepAppsOverlay.Completed += (s, _) =>
            {
                KeepAppsOverlay.Visibility = Visibility.Collapsed;
            };
        }

        private async void OnApplyKeepAppsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button during operation
                ApplyKeepAppsButton.IsEnabled = false;

                var result = CustomMessageBox.Show(
                    "This will update the list of protected apps for this session.\n\n" +
                    "Protected apps will be kept when using:\n" +
                    "- Remove Windows Bloatware\n" +
                    "- Run Optimization\n" +
                    "- Run Clear Glass\n\n" +
                    "Note: Protected apps will reset to defaults when you restart the application.\n\n" +
                    "Do you want to continue?",
                    "Update Protected Apps",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Update the essential apps list with selected apps
                    _bloatwareService.UpdateSessionEssentialApps(_installedApps);

                    CustomMessageBox.Show(
                        "Protected apps list has been updated successfully!\n\n" +
                        "These apps will be kept when removing bloatware.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    // Close the overlay after successful operation
                    OnCloseKeepAppsClick(sender, e);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error updating protected apps list: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ApplyKeepAppsButton.IsEnabled = true;
            }
        }

        private void OnSupportUsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button during operation
                SupportUsButton.IsEnabled = false;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://ko-fi.com/daniel1017",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error opening Ko-fi page: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SupportUsButton.IsEnabled = true;
            }
        }

        private void OnTweaksClick(object sender, RoutedEventArgs e)
        {
            TweaksOverlay.Visibility = Visibility.Visible;
            _showTweaksOverlay.Begin();
        }

        private void OnCloseTweaksClick(object sender, RoutedEventArgs e)
        {
            _hideTweaksOverlay.Begin(this, isControllable: false);
            _hideTweaksOverlay.Completed += (s, _) =>
            {
                TweaksOverlay.Visibility = Visibility.Collapsed;
            };
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:",
                UseShellExecute = true
            });
        }

        private void OnOpenControlPanelClick(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "control.exe",
                UseShellExecute = true
            });
        }

        private void OnOpenImageBackupClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "/name Microsoft.BackupAndRestore",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Failed to open Backup and Restore: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnOpenRegistryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "regedit.exe",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Failed to open Registry Editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenUserAccountsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "nusrmgr.cpl",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Failed to open User Accounts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnRemoveOneDriveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = CustomMessageBox.Show(
                    "This will completely remove Microsoft OneDrive from your system.\n\n" +
                    "Before proceeding:\n" +
                    "1. Make sure to stop OneDrive sync\n" +
                    "2. Back up any important files from OneDrive folders\n" +
                    "3. Move files back to local folders if needed\n\n" +
                    "Do you want to continue?",
                    "Remove OneDrive",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Step 1: Kill OneDrive processes
                    foreach (var process in Process.GetProcessesByName("OneDrive"))
                    {
                        process.Kill();
                    }

                    // Step 2: Try uninstalling via winget first
                    try
                    {
                        if (await _wingetService.IsWingetInstalled())
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = "winget",
                                Arguments = "uninstall \"Microsoft OneDrive\" --silent",
                                UseShellExecute = true,
                                Verb = "runas" // Run as administrator
                            };

                            using (var process = Process.Start(startInfo))
                            {
                                if (process != null)
                                {
                                    await process.WaitForExitAsync();
                                }
                            }
                        }
                    }
                    catch (Exception) { } // Continue if winget fails

                    // Step 3: Try uninstalling via built-in uninstaller
                    try
                    {
                        var oneDrivePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive", "OneDrive.exe");
                        if (File.Exists(oneDrivePath))
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = oneDrivePath,
                                Arguments = "/uninstall",
                                UseShellExecute = true,
                                Verb = "runas"
                            };

                            using (var process = Process.Start(startInfo))
                            {
                                if (process != null)
                                {
                                    await process.WaitForExitAsync();
                                }
                            }
                        }
                    }
                    catch (Exception) { } // Continue if uninstaller fails

                    // Step 4: Remove OneDrive from Windows Store if present
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "Get-AppxPackage *Microsoft.OneDrive* | Remove-AppxPackage",
                            UseShellExecute = true,
                            Verb = "runas"
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                await process.WaitForExitAsync();
                            }
                        }
                    }
                    catch (Exception) { } // Continue if Store removal fails

                    // Step 5: Clean up registry entries
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "reg",
                            Arguments = "delete \"HKEY_CLASSES_ROOT\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}\" /f",
                            UseShellExecute = true,
                            Verb = "runas"
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                await process.WaitForExitAsync();
                            }
                        }

                        startInfo.Arguments = "delete \"HKEY_CLASSES_ROOT\\Wow6432Node\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}\" /f";
                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                await process.WaitForExitAsync();
                            }
                        }
                    }
                    catch (Exception) { } // Continue if registry cleanup fails

                    CustomMessageBox.Show(
                        "OneDrive has been removed from your system.\n\n" +
                        "Note: If you want to remove OneDrive folders, you'll need to manually move your files first and then delete the folders.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error removing OneDrive: {ex.Message}\n\n" +
                    "You may need to try alternative removal methods or contact system administrator.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task<string> GetLibreWolfDownloadUrl()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ClearGlass");
                
                // Get the latest release from GitHub
                var response = await client.GetAsync("https://api.github.com/repos/librewolf-community/browser-windows/releases/latest");
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                
                // Find the Windows installer asset
                var startIndex = jsonResponse.IndexOf("browser_download_url") + "browser_download_url".Length + 3;
                var endIndex = jsonResponse.IndexOf(".exe", startIndex) + 4;
                if (startIndex <= "browser_download_url".Length + 3 || endIndex <= 4)
                {
                    throw new Exception("Could not find LibreWolf download URL in the release information.");
                }
                var downloadUrl = jsonResponse.Substring(startIndex, endIndex - startIndex);
                return downloadUrl;
            }
        }

        private async Task InstallAppWithWinget(string packageId, string appName, Button? button = null)
        {
            // First check if the app is installed
            var checkInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"list {packageId} --exact --accept-source-agreements",
                UseShellExecute = true,
                RedirectStandardOutput = false,
                Verb = "runas"
            };

            bool isInstalled = false;
            try
            {
                using (var process = Process.Start(checkInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        isInstalled = process.ExitCode == 0;
                    }
                }
            }
            catch { } // Ignore errors in check, proceed with install/upgrade

            if (isInstalled)
            {
                if (button != null)
                {
                    button.Content = $"Checking for {appName} updates...";
                }

                // Try to upgrade if installed
                var upgradeInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"upgrade {packageId} --source winget --silent --accept-source-agreements --accept-package-agreements",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using (var process = Process.Start(upgradeInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        // Exit code 0 means upgrade successful
                        // Exit code -1978335189 means no upgrade available
                        if (process.ExitCode != 0 && process.ExitCode != -1978335189)
                        {
                            throw new Exception($"Winget upgrade failed with exit code: {process.ExitCode}");
                        }
                    }
                }
            }
            else
            {
                // Install if not present
                if (button != null)
                {
                    button.Content = $"Installing {appName}...";
                }

                var installInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install {packageId} --source winget --silent --accept-source-agreements --accept-package-agreements",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using (var process = Process.Start(installInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Winget installation failed with exit code: {process.ExitCode}");
                        }
                    }
                }
            }
        }

        private async void OnLibreWolfDownloadClick(object sender, RoutedEventArgs e)
        {
            Button? button = null;
            try
            {
                // Disable button during installation
                button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Checking installation...";
                }

                // Check and install winget if needed
                if (!await _wingetService.IsWingetInstalled())
                {
                    button.Content = "Installing Winget...";
                    await _wingetService.InstallWinget();
                }

                // Install/Update LibreWolf
                await InstallAppWithWinget("LibreWolf.LibreWolf", "LibreWolf", button);

                CustomMessageBox.Show(
                    "LibreWolf has been installed/updated successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error installing LibreWolf: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Reset button state
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Download";
                }
            }
        }

        private async void OnRevoDownloadClick(object sender, RoutedEventArgs e)
        {
            Button? button = null;
            try
            {
                // Disable button during installation
                button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Checking installation...";
                }

                // Check and install winget if needed
                if (!await _wingetService.IsWingetInstalled())
                {
                    button.Content = "Installing Winget...";
                    await _wingetService.InstallWinget();
                }

                // Install/Update Revo Uninstaller
                await InstallAppWithWinget("RevoUninstaller.RevoUninstallerPro", "Revo Uninstaller Pro", button);

                CustomMessageBox.Show(
                    "Revo Uninstaller Pro has been installed/updated successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error installing Revo Uninstaller Pro: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Reset button state
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Download";
                }
            }
        }

        private async void OnBraveDownloadClick(object sender, RoutedEventArgs e)
        {
            Button? button = null;
            try
            {
                // Disable button during installation
                button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Checking installation...";
                }

                // Check and install winget if needed
                if (!await _wingetService.IsWingetInstalled())
                {
                    button.Content = "Installing Winget...";
                    await _wingetService.InstallWinget();
                }

                // Install/Update Brave Browser
                await InstallAppWithWinget("Brave.Brave", "Brave Browser", button);

                CustomMessageBox.Show(
                    "Brave Browser has been installed/updated successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error installing Brave Browser: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Reset button state
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Download";
                }
            }
        }

        private async void OnDownloadBundleClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button during installation
                DownloadBundleButton.IsEnabled = false;
                DownloadBundleButton.Content = "Checking installations...";

                // Show the browser choice dialog
                var dialog = new BrowserChoiceDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    // Check and install winget if needed
                    if (!await _wingetService.IsWingetInstalled())
                    {
                        DownloadBundleButton.Content = "Installing Winget...";
                        await _wingetService.InstallWinget();
                    }

                    // Install/Update selected apps, continue even if some fail
                    List<string> failedApps = new List<string>();

                    // Install browsers based on user choice
                    if (dialog.Choice == BrowserChoice.Both || dialog.Choice == BrowserChoice.LibreWolf)
                    {
                        try
                        {
                            await InstallAppWithWinget("LibreWolf.LibreWolf", "LibreWolf", DownloadBundleButton);
                        }
                        catch (Exception ex)
                        {
                            failedApps.Add($"LibreWolf: {ex.Message}");
                        }
                    }

                    if (dialog.Choice == BrowserChoice.Both || dialog.Choice == BrowserChoice.Brave)
                    {
                        try
                        {
                            await InstallAppWithWinget("Brave.Brave", "Brave Browser", DownloadBundleButton);
                        }
                        catch (Exception ex)
                        {
                            failedApps.Add($"Brave Browser: {ex.Message}");
                        }
                    }

                    // Always install Revo Uninstaller
                    try
                    {
                        await InstallAppWithWinget("RevoUninstaller.RevoUninstallerPro", "Revo Uninstaller Pro", DownloadBundleButton);
                    }
                    catch (Exception ex)
                    {
                        failedApps.Add($"Revo Uninstaller Pro: {ex.Message}");
                    }

                    if (failedApps.Count > 0)
                    {
                        CustomMessageBox.Show(
                            $"Installation completed with some errors:\n\n{string.Join("\n", failedApps)}",
                            "Installation Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    else
                    {
                        CustomMessageBox.Show(
                            "All selected applications have been installed/updated successfully!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error during installation: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Reset button state
                DownloadBundleButton.IsEnabled = true;
                DownloadBundleButton.Content = "Download Bundle";
            }
        }

        private void OnDisableSearchSuggestionsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = CustomMessageBox.Show(
                    "This will disable web search suggestions and Bing integration in the Windows search box.\n\n" +
                    "Do you want to continue?",
                    "Disable Search Suggestions",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Run PowerShell commands with elevated privileges
                    string script = @"
                        Write-Host 'Disabling search suggestions...'
                        
                        # Disable web search in Windows Search
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'BingSearchEnabled' -Value 0 -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'CortanaConsent' -Value 0 -Type DWord -Force
                        
                        # Disable search suggestions
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -Value 1 -Type DWord -Force
                        
                        # Disable web results in search
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'AllowSearchToUseLocation' -Value 0 -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'AllowCortana' -Value 0 -Type DWord -Force
                        
                        Write-Host 'Search suggestions have been disabled successfully!'
                    ";

                    // Save the script to a temporary file
                    string scriptPath = Path.Combine(Path.GetTempPath(), "ClearGlassDisableSearch.ps1");
                    File.WriteAllText(scriptPath, script);

                    // Run PowerShell with elevated privileges
                    ProcessStartInfo startInfo = new ProcessStartInfo()
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = false,
                        RedirectStandardOutput = false
                    };

                    using (Process process = Process.Start(startInfo))
                    {
                        if (process == null)
                        {
                            throw new InvalidOperationException("Failed to start PowerShell process");
                        }
                        process.WaitForExit();
                    }

                    CustomMessageBox.Show(
                        "Search suggestions have been disabled successfully!\n\n" +
                        "You may need to restart your computer for all changes to take effect.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error disabling search suggestions: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnRemoveAppsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _installedAppsCollection.Clear();
                _originalAppsList.Clear();
                
                var apps = await _wingetService.GetInstalledApps();
                // Sort the apps alphabetically by name
                var sortedApps = apps.OrderBy(app => app.Name).ToList();
                foreach (var app in sortedApps)
                {
                    _installedAppsCollection.Add(app);
                    _originalAppsList.Add(app);
                }

                _showRemoveAppsOverlay.Begin();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error loading installed applications: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void OnCloseRemoveAppsClick(object sender, RoutedEventArgs e)
        {
            _hideRemoveAppsOverlay.Begin();
        }

        private async void OnUninstallAppsClick(object sender, RoutedEventArgs e)
        {
            // Use the full, unfiltered list to get all selected apps
            var selectedApps = _originalAppsList.Where(app => app.IsSelected).ToList();
            if (!selectedApps.Any())
            {
                CustomMessageBox.Show(
                    "Please select at least one application to uninstall.",
                    "No Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = CustomMessageBox.Show(
                $"Are you sure you want to uninstall {selectedApps.Count} selected application(s)?\n\n" +
                "The uninstallation process will:\n" +
                "1. Create a system restore point\n" +
                "2. Run the application's native uninstaller or platform-specific uninstaller\n" +
                "3. Scan for and remove leftover files\n" +
                "4. Scan for and remove leftover registry entries\n\n" +
                "Would you like to automatically remove leftover files and registry entries?",
                "Confirm Uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                UninstallAppsButton.IsEnabled = false;

                var progress = new Progress<string>(status =>
                {
                    UninstallAppsButton.Content = status;
                });

                var failedApps = new List<string>();
                var steamApps = new List<string>();
                bool restorePointCreated = false;
                for (int i = 0; i < selectedApps.Count; i++)
                {
                    var app = selectedApps[i];

                    // Detect Steam games by 'Steam App' in app.Id
                    if (app.Id != null && app.Id.Contains("Steam App "))
                    {
                        // Extract the Steam App ID number
                        var match = System.Text.RegularExpressions.Regex.Match(app.Id, @"Steam App (\d+)");
                        if (match.Success)
                        {
                            string steamAppId = match.Groups[1].Value;
                            string? steamExe = FindSteamExePath();
                            if (!string.IsNullOrEmpty(steamExe))
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = steamExe,
                                        Arguments = $"steam://uninstall/{steamAppId}",
                                        UseShellExecute = true
                                    });
                                    steamApps.Add(app.Name);
                                    _installedAppsCollection.Remove(app);
                                    // Sequential prompt
                                    CustomMessageBox.Show(
                                        $"You can only uninstall one Steam game at a time. Click OK to continue to the next game.",
                                        "Steam Uninstall Notice",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information
                                    );
                                }
                                catch (Exception ex)
                                {
                                    failedApps.Add($"{app.Name} (Steam): {ex.Message}");
                                }
                            }
                            else
                            {
                                failedApps.Add($"{app.Name} (Steam): Could not find steam.exe. Please uninstall manually from Steam.");
                            }
                        }
                        else
                        {
                            failedApps.Add($"{app.Name} (Steam): Could not extract Steam App ID. Please uninstall manually from Steam.");
                        }
                        continue;
                    }

                    // Handle non-Steam apps
                    try
                    {
                        await _uninstallService.UninstallAppThoroughly(app.Id, app.Name, progress, !restorePointCreated, true);
                        restorePointCreated = true;
                        _installedAppsCollection.Remove(app);
                    }
                    catch (Exception ex)
                    {
                        // Check for winget single-package error
                        if (ex.Message.Contains("can only be used for single package") || ex.Message.Contains("only be used for single package"))
                        {
                            failedApps.Add($"{app.Name}: This application cannot be uninstalled automatically. Please uninstall it manually from its platform or the Windows Control Panel.");
                        }
                        else
                        {
                            failedApps.Add($"{app.Name}: {ex.Message}");
                        }
                    }
                }

                // Show summary
                string summary = "";
                if (steamApps.Any())
                {
                    summary += $"The following Steam games were opened in Steam for uninstallation:\n- {string.Join("\n- ", steamApps)}\n\n";
                }
                if (failedApps.Any())
                {
                    summary += $"Some applications could not be uninstalled automatically:\n- {string.Join("\n- ", failedApps)}\n\n";
                }
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = "Selected applications have been uninstalled.";
                }
                else
                {
                    summary += "Other selected applications have been uninstalled.";
                }

                CustomMessageBox.Show(
                    summary,
                    "Uninstall Summary",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error during uninstallation: {ex.Message}",
                    "Uninstall Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                UninstallAppsButton.IsEnabled = true;
                UninstallAppsButton.Content = "Uninstall Selected";
            }
        }

        private async void OnDisablePrivacyPermissionsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = CustomMessageBox.Show(
                    "This will disable personalized ads, local content based on language, app launch tracking, and suggested content in settings. Do you want to continue?",
                    "Confirm Privacy Settings Change",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    string script = @"
                        # Disable advertising ID
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord -Force
                        
                        # Disable language list access
                        Set-ItemProperty -Path 'HKCU:\Control Panel\International\User Profile' -Name 'HttpAcceptLanguageOptOut' -Value 1 -Type DWord -Force
                        
                        # Disable app launch tracking
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'Start_TrackProgs' -Value 0 -Type DWord -Force
                        
                        # Disable suggested content in settings
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338393Enabled' -Value 0 -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-353694Enabled' -Value 0 -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338389Enabled' -Value 0 -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-310093Enabled' -Value 0 -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 0 -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'ShowSyncProviderNotifications' -Value 0 -Type DWord -Force
                    ";

                    // Save the script to a temporary file
                    string scriptPath = Path.Combine(Path.GetTempPath(), "DisablePrivacyPermissions.ps1");
                    await File.WriteAllTextAsync(scriptPath, script);

                    // Run PowerShell with elevated privileges
                    var startInfo = new ProcessStartInfo()
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = false
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        
                        if (process.ExitCode == 0)
                        {
                            CustomMessageBox.Show(
                                "Privacy settings have been updated successfully!",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            CustomMessageBox.Show(
                                "Some privacy settings may not have been updated successfully. Please check Windows Settings for more information.",
                                "Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }

                    // Clean up the temporary script file
                    File.Delete(scriptPath);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error updating privacy settings: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnSetThisPCDefaultClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = CustomMessageBox.Show(
                    "This will set 'This PC' as the default view in File Explorer by removing Home and Gallery. Do you want to continue?",
                    "Confirm File Explorer Change",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    string script = @"
                        # Remove Home and Gallery from File Explorer
                        if (!(Test-Path 'HKCU:\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}')) {
                            New-Item -Path 'HKCU:\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}' -Force
                        }
                        Set-ItemProperty -Path 'HKCU:\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}' -Name 'System.IsPinnedToNameSpaceTree' -Value 0 -Type DWord -Force

                        # Disable Gallery View
                        if (!(Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced')) {
                            New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Force
                        }
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'LaunchTo' -Value 1 -Type DWord -Force

                        # Restart Explorer to apply changes
                        Stop-Process -Name explorer -Force
                        Start-Process explorer
                    ";

                    // Save the script to a temporary file
                    string scriptPath = Path.Combine(Path.GetTempPath(), "SetThisPCDefault.ps1");
                    await File.WriteAllTextAsync(scriptPath, script);

                    // Run PowerShell with elevated privileges
                    var startInfo = new ProcessStartInfo()
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = false
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        
                        if (process.ExitCode == 0)
                        {
                            CustomMessageBox.Show(
                                "File Explorer has been set to show 'This PC' by default.\nExplorer has been restarted to apply the changes.",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            CustomMessageBox.Show(
                                "Failed to set 'This PC' as default view. Please try again or check system permissions.",
                                "Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }

                    // Clean up the temporary script file
                    File.Delete(scriptPath);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error setting File Explorer default view: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnRestoreClassicMenuClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = CustomMessageBox.Show(
                    "This will restore the classic (full) right-click context menu, removing the 'Show more options' step. Do you want to continue?",
                    "Confirm Context Menu Change",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    string script = @"
                        # Restore classic context menu
                        if (!(Test-Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32')) {
                            New-Item -Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' -Force | Out-Null
                        }
                        Set-ItemProperty -Path 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' -Name '(Default)' -Value '' -Type String -Force

                        # Restart Explorer to apply changes
                        Stop-Process -Name explorer -Force
                        Start-Process explorer
                    ";

                    // Save the script to a temporary file
                    string scriptPath = Path.Combine(Path.GetTempPath(), "RestoreClassicMenu.ps1");
                    await File.WriteAllTextAsync(scriptPath, script);

                    // Run PowerShell with elevated privileges
                    var startInfo = new ProcessStartInfo()
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = false
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        
                        if (process.ExitCode == 0)
                        {
                            CustomMessageBox.Show(
                                "Classic context menu has been restored.\nExplorer has been restarted to apply the changes.",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            CustomMessageBox.Show(
                                "Failed to restore classic context menu. Please try again or check system permissions.",
                                "Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }

                    // Clean up the temporary script file
                    File.Delete(scriptPath);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error restoring classic context menu: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnAppSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = AppSearchBox.Text.ToLower();
            
            _installedAppsCollection.Clear();
            var filteredApps = string.IsNullOrWhiteSpace(searchText)
                ? _originalAppsList
                : _originalAppsList.Where(app => app.Name.ToLower().Contains(searchText));
                
            foreach (var app in filteredApps)
            {
                _installedAppsCollection.Add(app);
            }
        }

        private void OnKeepAppsSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = KeepAppsSearchBox.Text.ToLower();
            if (_installedApps == null) return;
            _installedApps.Clear();
            var filteredApps = string.IsNullOrWhiteSpace(searchText)
                ? _originalKeepAppsList
                : _originalKeepAppsList.Where(app => (app.DisplayName ?? app.Name).ToLower().Contains(searchText));
            foreach (var app in filteredApps.OrderBy(app => (app.DisplayName ?? app.Name)))
            {
                _installedApps.Add(app);
            }
        }

        // Helper to find steam.exe
        private string? FindSteamExePath()
        {
            // Common locations
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe")
            };
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }
            // Optionally, check registry (not implemented here for brevity)
            return null;
        }

        private async void CheckForUpdatesAsync()
        {
            try
            {
                var (hasUpdate, latestVersion, downloadUrl) = await _updateService.CheckForUpdates();
                if (hasUpdate)
                {
                    var result = MessageBox.Show(
                        $"A new version of Clear Glass ({latestVersion}) is available. Would you like to update now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        await _updateService.DownloadAndInstallUpdate(downloadUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
            }
        }

        private void OnOpenBackupClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "control",
                    Arguments = "/name Microsoft.BackupAndRestore",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error opening Windows Backup: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnOpenRegistryEditorClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "regedit.exe",
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error opening Registry Editor: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnEnableEndTaskClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = CustomMessageBox.Show(
                    "This will enable the 'End Task' option in taskbar context menu. Do you want to continue?",
                    "Confirm End Task Feature",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    string script = @"
                        # Enable End Task in Settings
                        if (!(Test-Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings')) {
                            New-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Force
                        }
                        Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Name 'TaskbarEndTask' -Value 1 -Type DWord -Force
                    ";

                    // Save the script to a temporary file
                    string scriptPath = Path.Combine(Path.GetTempPath(), "EnableEndTask.ps1");
                    await File.WriteAllTextAsync(scriptPath, script);

                    // Run PowerShell with elevated privileges
                    var startInfo = new ProcessStartInfo()
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = false
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        
                        if (process.ExitCode == 0)
                        {
                            CustomMessageBox.Show(
                                "End Task feature has been enabled in taskbar context menu.",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            CustomMessageBox.Show(
                                "Failed to enable End Task feature.",
                                "Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }

                    // Clean up the temporary script file
                    File.Delete(scriptPath);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error enabling End Task feature: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnRemoveWindowsAIClick(object sender, RoutedEventArgs e)
        {
            // Confirmation for AI removal
            var result = CustomMessageBox.Show(
                "This will remove Windows AI components including Copilot, Recall, and related features.\n\n" +
                "**What this does:**\n" +
                "• Kills AI processes\n" +
                "• Removes AppX packages\n" +
                "• Disables registry keys and policies\n" +
                "• Cleans up files and scheduled tasks\n\n" +
                "Do you want to continue?",
                "Remove Windows AI Components",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Show Copilot+ PC warning
                var copilotWarning = CustomMessageBox.Show(
                    "Windows Copilot+ PC Warning\n\n" +
                    "If you have a Windows Copilot+ PC with Recall enabled, you may need to manually terminate a service during AI removal:\n\n" +
                    "1. Right-click the taskbar → Task Manager\n" +
                    "2. Search for 'wsaifabricsvc' \n" +
                    "3. Right-click it → End Task\n" +
                    "4. Click 'Shut down' on the Windows warning popup\n" +
                    "5. Return here and click OK\n\n" +
                    "If you don't have a Copilot+ PC or Recall enabled, you can safely click OK to continue.",
                    "Windows Copilot+ PC Warning",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (copilotWarning == MessageBoxResult.Cancel)
                    return;

                try
                {
                    // Use the silent version to avoid popups
                    var optimizationService = new OptimizationService();
                    await optimizationService.RemoveWindowsAIOnlySilent();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error removing Windows AI components: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Silent versions of tweak methods for unified progress experience
        private async Task RemoveOneDriveSilent()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("OneDrive"))
                {
                    process.Kill();
                }

                try
                {
                    if (await _wingetService.IsWingetInstalled())
                    {
                        var oneDriveStartInfo = new ProcessStartInfo
                        {
                            FileName = "winget",
                            Arguments = "uninstall \"Microsoft OneDrive\" --silent",
                            UseShellExecute = true,
                            Verb = "runas",
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(oneDriveStartInfo))
                        {
                            if (process != null)
                            {
                                await process.WaitForExitAsync();
                            }
                        }
                    }
                }
                catch (Exception) { }

                string script = @"
                    Remove-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' -Name 'OneDrive' -ErrorAction SilentlyContinue
                    Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' -Name 'OneDrive' -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive' -Name 'DisablePersonalSync' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive' -Name 'DisableFileSyncNGSC' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace' -Name '{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}' -Value '' -ErrorAction SilentlyContinue
                    Remove-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}' -Recurse -Force -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace' -Name '{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}' -Value '' -ErrorAction SilentlyContinue
                    Remove-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}' -Recurse -Force -ErrorAction SilentlyContinue
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "RemoveOneDriveSilent.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var oneDriveScriptStartInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var process = Process.Start(oneDriveScriptStartInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }

                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { }
        }

        private async Task DisableSearchSuggestionsSilent()
        {
            try
            {
                string script = @"
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'BingSearchEnabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'CortanaConsent' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -Value 1 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'AllowSearchToUseLocation' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search' -Name 'AllowCortana' -Value 0 -Type DWord -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "DisableSearchSilent.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var searchScriptStartInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var process = Process.Start(searchScriptStartInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }

                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { }
        }

        private async Task DisablePrivacyPermissionsSilent()
        {
            try
            {
                string script = @"
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\Control Panel\International\User Profile' -Name 'HttpAcceptLanguageOptOut' -Value 1 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'Start_TrackProgs' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338393Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-353694Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338389Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-310093Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'ShowSyncProviderNotifications' -Value 0 -Type DWord -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "DisablePrivacySilent.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var privacyScriptStartInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var process = Process.Start(privacyScriptStartInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }

                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { }
        }

        private async Task EnableEndTaskSilent()
        {
            try
            {
                string script = @"
                    if (!(Test-Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings')) {
                        New-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Force
                    }
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' -Name 'TaskbarEndTask' -Value 1 -Type DWord -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "EnableEndTaskSilent.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var endTaskScriptStartInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var process = Process.Start(endTaskScriptStartInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }

                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { }
        }

        private async Task SetThisPCDefaultSilent()
        {
            try
            {
                string script = @"
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'LaunchTo' -Value 1 -Type DWord -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "SetThisPCDefaultSilent.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }

                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { }
        }

        private async Task RestoreClassicMenuSilent()
        {
            try
            {
                string script = @"
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' -Name '(Default)' -Value '' -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "RestoreClassicMenuSilent.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                var startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }

                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception) { }
        }

        private async Task ApplyClearGlassThemeSilent()
        {
            try
            {
                // Show desktop icons first
                DesktopIconsToggle.IsChecked = true;
                _themeService.AreDesktopIconsVisible = true;
                await Task.Delay(200);

                // Apply dark theme first as it's a major change
                ThemeToggle.IsChecked = true;
                await Task.Run(() => _themeService.IsDarkMode = true);
                await Task.Delay(500);

                // First shell refresh after theme change
                _themeService.RefreshWindows();
                await Task.Delay(500);

                // Apply taskbar settings
                TaskbarAlignmentToggle.IsChecked = false;
                _themeService.IsTaskbarCentered = false;
                await Task.Delay(200);

                // Apply task view settings
                TaskViewToggle.IsChecked = false;
                _themeService.IsTaskViewEnabled = false;
                await Task.Delay(100);

                // Show search first to ensure proper state, then hide
                SearchToggle.IsChecked = true;
                _themeService.IsSearchVisible = true;
                await Task.Delay(200);

                SearchToggle.IsChecked = false;
                _themeService.IsSearchVisible = false;
                await Task.Delay(200);

                // Second shell refresh after UI changes
                _themeService.RefreshWindows();
                await Task.Delay(500);

                // Hide desktop icons
                DesktopIconsToggle.IsChecked = false;
                _themeService.AreDesktopIconsVisible = false;
                await Task.Delay(200);

                // Final step: Apply Clear Glass wallpaper after all UI changes are complete
                await Task.Delay(300); // Give UI a moment to fully settle
                await EnsureWallpaperAsync();
                await Task.Delay(200); // Short delay after wallpaper change
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying theme silently: {ex.Message}");
            }
        }
    }
} 
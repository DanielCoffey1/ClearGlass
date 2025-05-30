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

namespace ClearGlass
{
    public partial class MainWindow : Window
    {
        private readonly ThemeService _themeService;
        private readonly OptimizationService _optimizationService;
        private readonly BloatwareService _bloatwareService;
        private readonly WingetService _wingetService;
        private readonly UninstallService _uninstallService;
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
            _bloatwareService = new BloatwareService();
            _wingetService = new WingetService();
            _uninstallService = new UninstallService(_wingetService);
            
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

                // Apply task view and search settings
                TaskViewToggle.IsChecked = false;
                _themeService.IsTaskViewEnabled = false;
                await Task.Delay(100);

                SearchToggle.IsChecked = false;
                _themeService.IsSearchVisible = false;
                await Task.Delay(100);

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
                "2. Remove unnecessary Windows bloatware\n" +
                "3. Apply the Clear Glass theme (dark mode, centered taskbar, etc.)\n\n" +
                "A system restore point will be created before making changes.\n\n" +
                "Do you want to continue?",
                "Apply Complete Clear Glass Experience",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    ClearGlassButton.IsEnabled = false;

                    // Run Windows settings optimization
                    await _optimizationService.TweakWindowsSettings();
                    
                    // Run bloatware removal
                    await _bloatwareService.RemoveWindowsBloatware();

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

                    // Apply task view and search settings
                    TaskViewToggle.IsChecked = false;
                    _themeService.IsTaskViewEnabled = false;
                    await Task.Delay(100);

                    SearchToggle.IsChecked = false;
                    _themeService.IsSearchVisible = false;
                    await Task.Delay(100);

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

                    CustomMessageBox.Show(
                        "Clear Glass experience has been fully applied!\n\n" +
                        "Some changes may require a system restart to take full effect.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
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
                await _bloatwareService.RemoveWindowsBloatware();
            }
        }

        private async void OnRunOptimizationClick(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.Show(
                "This will:\n\n" +
                "1. Optimize Windows settings (privacy, performance, services)\n" +
                "2. Remove unnecessary Windows bloatware\n\n" +
                "A system restore point will be created before making changes.\n\n" +
                "Do you want to continue?",
                "Confirm Full Windows Optimization",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Run Windows settings optimization
                    await _optimizationService.TweakWindowsSettings();
                    
                    // Run bloatware removal
                    await _bloatwareService.RemoveWindowsBloatware();

                    CustomMessageBox.Show(
                        "Full Windows optimization completed successfully!\n\n" +
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
                    "This will remove Microsoft OneDrive from your system using winget.\n\n" +
                    "Do you want to continue?",
                    "Remove OneDrive",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Check if winget is installed first
                    if (!await _wingetService.IsWingetInstalled())
                    {
                        var installResult = CustomMessageBox.Show(
                            "Winget is not installed. Would you like to install it now?\n\n" +
                            "Winget is required to remove OneDrive.",
                            "Install Winget",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (installResult == MessageBoxResult.Yes)
                        {
                            await _wingetService.InstallWinget();
                        }
                        else
                        {
                            return;
                        }
                    }

                    // Run winget uninstall command
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
                            CustomMessageBox.Show(
                                "OneDrive has been successfully removed from your system.",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error removing OneDrive: {ex.Message}",
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
                await InstallAppWithWinget("RevoUninstaller.RevoUninstaller", "Revo Uninstaller", button);

                CustomMessageBox.Show(
                    "Revo Uninstaller has been installed/updated successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error installing Revo Uninstaller: {ex.Message}",
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

                var result = CustomMessageBox.Show(
                    "This will install or update all recommended applications:\n\n" +
                    "• LibreWolf Browser\n" +
                    "• Revo Uninstaller\n\n" +
                    "Do you want to continue?",
                    "Install All Applications",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Check and install winget if needed
                    if (!await _wingetService.IsWingetInstalled())
                    {
                        DownloadBundleButton.Content = "Installing Winget...";
                        await _wingetService.InstallWinget();
                    }

                    // Install/Update all apps, continue even if some fail
                    List<string> failedApps = new List<string>();

                    try
                    {
                        await InstallAppWithWinget("LibreWolf.LibreWolf", "LibreWolf", DownloadBundleButton);
                    }
                    catch (Exception ex)
                    {
                        failedApps.Add($"LibreWolf: {ex.Message}");
                    }

                    try
                    {
                        await InstallAppWithWinget("RevoUninstaller.RevoUninstaller", "Revo Uninstaller", DownloadBundleButton);
                    }
                    catch (Exception ex)
                    {
                        failedApps.Add($"Revo Uninstaller: {ex.Message}");
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
                            "All applications have been installed/updated successfully!",
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
                "4. Scan for and remove leftover registry entries",
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

                // Create restore point only for the first app (if not a Steam game)
                int firstNonSteamIndex = selectedApps.FindIndex(a => !a.IsSteamGame);
                if (firstNonSteamIndex >= 0)
                {
                    try
                    {
                        await _uninstallService.UninstallAppThoroughly(selectedApps[firstNonSteamIndex].Id, selectedApps[firstNonSteamIndex].Name, progress, true);
                        _installedAppsCollection.Remove(selectedApps[firstNonSteamIndex]);
                    }
                    catch (Exception ex)
                    {
                        failedApps.Add($"{selectedApps[firstNonSteamIndex].Name}: {ex.Message}");
                    }
                }

                // Uninstall remaining apps
                for (int i = 0; i < selectedApps.Count; i++)
                {
                    if (i == firstNonSteamIndex) continue;
                    var app = selectedApps[i];
                    try
                    {
                        if (app.IsSteamGame)
                        {
                            // Launch Steam uninstall protocol
                            string steamAppId = app.Id.Replace("Steam App ", "");
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = $"steam://uninstall/{steamAppId}",
                                    UseShellExecute = true
                                });
                                steamApps.Add(app.Name);
                                _installedAppsCollection.Remove(app);
                            }
                            catch (Exception ex)
                            {
                                failedApps.Add($"{app.Name} (Steam): {ex.Message}");
                            }
                        }
                        else
                        {
                            await _uninstallService.UninstallAppThoroughly(app.Id, app.Name, progress, false);
                            _installedAppsCollection.Remove(app);
                        }
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
                var button = (Button)sender;
                var originalContent = button.Content;
                button.Content = "Disabling...";
                button.IsEnabled = false;

                // Create PowerShell script to disable privacy settings
                string script = @"
                    # Create a restore point
                    Checkpoint-Computer -Description 'Before Privacy Settings Change' -RestorePointType 'MODIFY_SETTINGS'

                    # Disable Advertising ID
                    New-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Force | Out-Null
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord -Force
                    
                    # Disable Website Language Access
                    Set-ItemProperty -Path 'HKCU:\Control Panel\International\User Profile' -Name 'HttpAcceptLanguageOptOut' -Value 1 -Type DWord -Force
                    
                    # Disable App Launch Tracking
                    New-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Force | Out-Null
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'Start_TrackProgs' -Value 0 -Type DWord -Force
                    
                    # Disable Suggested Content in Settings
                    New-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Force | Out-Null
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338393Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-353694Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-353696Enabled' -Value 0 -Type DWord -Force
                    
                    # Disable Settings Notifications (all types)
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338389Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-310093Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-314563Enabled' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 0 -Type DWord -Force
                    
                    # Additional notification settings
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\PushNotifications' -Name 'ToastEnabled' -Value 0 -Type DWord -Force
                    
                    # Disable Custom Inking and Typing Dictionary
                    New-Item -Path 'HKCU:\SOFTWARE\Microsoft\InputPersonalization' -Force | Out-Null
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\InputPersonalization' -Name 'RestrictImplicitInkCollection' -Value 1 -Type DWord -Force
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\InputPersonalization' -Name 'RestrictImplicitTextCollection' -Value 1 -Type DWord -Force
                    
                    New-Item -Path 'HKCU:\SOFTWARE\Microsoft\InputPersonalization\TrainedDataStore' -Force | Out-Null
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\InputPersonalization\TrainedDataStore' -Name 'HarvestContacts' -Value 0 -Type DWord -Force
                    
                    # Disable Inking & Typing Personalization
                    New-Item -Path 'HKCU:\SOFTWARE\Microsoft\Personalization\Settings' -Force | Out-Null
                    Set-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Personalization\Settings' -Name 'AcceptedPrivacyPolicy' -Value 0 -Type DWord -Force
                    
                    # Save current taskbar settings
                    $explorerProcess = Get-Process -Name explorer -ErrorAction SilentlyContinue
                    if ($explorerProcess) {
                        $taskbarSettings = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -ErrorAction SilentlyContinue
                        
                        # Store all relevant taskbar settings
                        $taskbarAlignment = $taskbarSettings.TaskbarAl
                        $taskbarSmallIcons = $taskbarSettings.TaskbarSmallIcons
                        $taskbarSearch = $taskbarSettings.SearchboxTaskbarMode
                        $showTaskView = $taskbarSettings.ShowTaskViewButton
                        
                        Stop-Process -Name explorer -Force
                        Start-Sleep -Seconds 2
                        
                        # Restore all taskbar settings
                        Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarAl' -Value $taskbarAlignment -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarSmallIcons' -Value $taskbarSmallIcons -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'SearchboxTaskbarMode' -Value $taskbarSearch -Type DWord -Force
                        Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowTaskViewButton' -Value $showTaskView -Type DWord -Force
                        
                        # Explicitly disable Copilot
                        Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'ShowCopilotButton' -Value 0 -Type DWord -Force
                        
                        # Additional Copilot-related settings
                        New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\Microsoft\CopilotSettings' -Force | Out-Null
                        Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\Microsoft\CopilotSettings' -Name 'IsEnabled' -Value 0 -Type DWord -Force
                        
                        Start-Process explorer
                    }
                ";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        CustomMessageBox.Show(
                            "Privacy permissions have been successfully disabled.",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        CustomMessageBox.Show(
                            "Some settings may not have been changed successfully. Please check your privacy settings manually.",
                            "Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                button.Content = originalContent;
                button.IsEnabled = true;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error disabling privacy permissions: {ex.Message}",
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
    }
} 
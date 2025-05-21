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
using ClearGlass.Models;

namespace ClearGlass
{
    public partial class MainWindow : Window
    {
        private readonly ThemeService _themeService;
        private readonly OptimizationService _optimizationService;
        private readonly BloatwareService _bloatwareService;
        private readonly WingetService _wingetService;
        private bool _isThemeChanging = false;
        private readonly string _wallpaperUrl = "https://raw.githubusercontent.com/DanielCoffey1/ClearGlassWallpapers/main/glassbackground.png";
        private readonly string _wallpaperPath;
        private readonly string _hashPath;
        private readonly string _autologonPath;
        private Storyboard _showAddonsOverlay = null!;
        private Storyboard _hideAddonsOverlay = null!;
        private Storyboard _showOptimizationOverlay = null!;
        private Storyboard _hideOptimizationOverlay = null!;
        private Storyboard _showKeepAppsOverlay = null!;
        private Storyboard _hideKeepAppsOverlay = null!;
        private Storyboard _showTweaksOverlay = null!;
        private Storyboard _hideTweaksOverlay = null!;
        private ObservableCollection<WindowsApp>? _installedApps;

        public MainWindow()
        {
            InitializeComponent();
            _themeService = new ThemeService();
            _optimizationService = new OptimizationService();
            _bloatwareService = new BloatwareService();
            _wingetService = new WingetService();
            
            // Store in Windows' tools directory
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _autologonPath = Path.Combine(commonAppData, "ClearGlass", "Tools", "Autologon.exe");
                
            // Store in Windows' Wallpaper cache directory
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _wallpaperPath = Path.Combine(appData, "Microsoft", "Windows", "Themes", "ClearGlass", "wallpaper.png");
            
            string? wallpaperDir = Path.GetDirectoryName(_wallpaperPath);
            if (wallpaperDir == null)
            {
                throw new InvalidOperationException("Invalid wallpaper path");
            }
            _hashPath = Path.Combine(wallpaperDir, "wallpaper.hash");
                
            LoadCurrentSettings();

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
            
            // Ensure overlays are hidden initially
            AddonsOverlay.Opacity = 0;
            AddonsOverlay.Margin = new Thickness(0, 600, 0, -600);
            OptimizationOverlay.Opacity = 0;
            OptimizationOverlay.Margin = new Thickness(0, 600, 0, -600);
            KeepAppsOverlay.Opacity = 0;
            KeepAppsOverlay.Margin = new Thickness(0, 600, 0, -600);
            TweaksOverlay.Opacity = 0;
            TweaksOverlay.Margin = new Thickness(0, 600, 0, -600);
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
                            MessageBox.Show(
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
            try
            {
                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(_wallpaperPath));

                bool needsDownload = true;
                string currentHash = null;

                // Check if wallpaper exists and get its hash
                if (File.Exists(_wallpaperPath) && File.Exists(_hashPath))
                {
                    currentHash = await File.ReadAllTextAsync(_hashPath);
                    using var client = new HttpClient();
                    var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, _wallpaperUrl));
                    var etag = response.Headers.ETag?.Tag;
                    
                    if (!string.IsNullOrEmpty(etag) && etag == currentHash)
                    {
                        needsDownload = false;
                    }
                }

                if (needsDownload)
                {
                    using (var client = new HttpClient())
                    {
                        var response = await client.GetAsync(_wallpaperUrl);
                        response.EnsureSuccessStatusCode();

                        // Save the ETag
                        var etag = response.Headers.ETag?.Tag;
                        if (!string.IsNullOrEmpty(etag))
                        {
                            await File.WriteAllTextAsync(_hashPath, etag);
                        }

                        // Save the wallpaper
                        using (var fs = new FileStream(_wallpaperPath, FileMode.Create))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
                    }
                }

                // Set the wallpaper
                _themeService.SetWallpaper(_wallpaperPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error setting wallpaper: {ex.Message}",
                    "Wallpaper Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
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

                // Apply Clear Glass wallpaper
                await EnsureWallpaperAsync();
                await Task.Delay(500);

                // Wait for everything to stabilize
                await Task.Delay(500);

                // Finally hide desktop icons
                DesktopIconsToggle.IsChecked = false;
                _themeService.AreDesktopIconsVisible = false;
                await Task.Delay(200);

                MessageBox.Show(
                    "Clear Glass Theme applied successfully!\n\n" +
                    "Some changes may take a few seconds to fully apply.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
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
            var result = MessageBox.Show(
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

                    // Apply Clear Glass wallpaper
                    await EnsureWallpaperAsync();
                    await Task.Delay(500);

                    // Wait for everything to stabilize
                    await Task.Delay(500);

                    // Finally hide desktop icons
                    DesktopIconsToggle.IsChecked = false;
                    _themeService.AreDesktopIconsVisible = false;
                    await Task.Delay(200);

                    MessageBox.Show(
                        "Clear Glass experience has been fully applied!\n\n" +
                        "Some changes may require a system restart to take full effect.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
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
            var result = MessageBox.Show(
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
            var result = MessageBox.Show(
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
            var result = MessageBox.Show(
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

                    MessageBox.Show(
                        "Full Windows optimization completed successfully!\n\n" +
                        "Some changes may require a system restart to take full effect.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
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
                var result = MessageBox.Show(
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
                        MessageBox.Show(
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
                MessageBox.Show(
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
                    _installedApps = await _bloatwareService.GetInstalledApps();
                    AppsListView.ItemsSource = _installedApps;
                }

                // Show the overlay
                KeepAppsOverlay.Visibility = Visibility.Visible;
                _showKeepAppsOverlay.Begin();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
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

                var result = MessageBox.Show(
                    "This will update the list of protected apps for this session.\n\n" +
                    "Protected apps will be kept when using:\n" +
                    "- Remove Windows Bloatware\n" +
                    "- Run Optimization\n" +
                    "- Clear Glass button\n\n" +
                    "Note: Protected apps will reset to defaults when you restart the application.\n\n" +
                    "Do you want to continue?",
                    "Update Protected Apps",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Update the essential apps list with selected apps
                    _bloatwareService.UpdateSessionEssentialApps(_installedApps);

                    MessageBox.Show(
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
                MessageBox.Show(
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

                // TODO: Implement support options
                MessageBox.Show(
                    "Thank you for considering supporting Clear Glass!\n\n" +
                    "Support options will be available in a future update.",
                    "Support Clear Glass",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error showing support options: {ex.Message}",
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
                MessageBox.Show($"Failed to open Registry Editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Failed to open User Accounts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnRemoveOneDriveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
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
                        var installResult = MessageBox.Show(
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
                            MessageBox.Show(
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
                MessageBox.Show(
                    $"Error removing OneDrive: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnLibreWolfDownloadClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Would you like to install LibreWolf using winget?\n\n" +
                    "Click Yes to install automatically using winget.\n" +
                    "Click No to open the LibreWolf website instead.",
                    "Install LibreWolf",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _wingetService.InstallApp("LibreWolf.LibreWolf", "LibreWolf");
                    MessageBox.Show(
                        "LibreWolf has been installed successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://librewolf.net/",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnRevoDownloadClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Would you like to install Revo Uninstaller using winget?\n\n" +
                    "Click Yes to install automatically using winget.\n" +
                    "Click No to open the Revo Uninstaller website instead.",
                    "Install Revo Uninstaller",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _wingetService.InstallApp("RevoUninstaller.RevoUninstaller", "Revo Uninstaller");
                    MessageBox.Show(
                        "Revo Uninstaller has been installed successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://www.revouninstaller.com/products/revo-uninstaller-free/",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OnDownloadBundleClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button during installation
                DownloadBundleButton.IsEnabled = false;
                DownloadBundleButton.Content = "Installing...";

                var result = MessageBox.Show(
                    "This will install all recommended applications using winget:\n\n" +
                    "• LibreWolf Browser\n" +
                    "• Revo Uninstaller\n\n" +
                    "Do you want to continue?",
                    "Install All Applications",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Check if winget is installed first
                    if (!await _wingetService.IsWingetInstalled())
                    {
                        await _wingetService.InstallWinget();
                        return;
                    }

                    // Install LibreWolf
                    DownloadBundleButton.Content = "Installing LibreWolf...";
                    await _wingetService.InstallApp("LibreWolf.LibreWolf", "LibreWolf");

                    // Install Revo Uninstaller
                    DownloadBundleButton.Content = "Installing Revo Uninstaller...";
                    await _wingetService.InstallApp("RevoUninstaller.RevoUninstaller", "Revo Uninstaller");

                    MessageBox.Show(
                        "All applications have been installed successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
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
    }
} 
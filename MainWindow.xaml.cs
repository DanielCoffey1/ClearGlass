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

namespace ClearGlass
{
    public partial class MainWindow : Window
    {
        private readonly ThemeService _themeService;
        private readonly OptimizationService _optimizationService;
        private readonly BloatwareService _bloatwareService;
        private bool _isThemeChanging = false;
        private readonly string _wallpaperUrl = "https://raw.githubusercontent.com/DanielCoffey1/ClearGlassWallpapers/main/glassbackground.png";
        private readonly string _wallpaperPath;
        private readonly string _hashPath;
        private Storyboard _showAddonsOverlay;
        private Storyboard _hideAddonsOverlay;
        private Storyboard _showOptimizationOverlay;
        private Storyboard _hideOptimizationOverlay;

        public MainWindow()
        {
            InitializeComponent();
            _themeService = new ThemeService();
            _optimizationService = new OptimizationService();
            _bloatwareService = new BloatwareService();
            
            // Store in Windows' Wallpaper cache directory
            _wallpaperPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft\\Windows\\Themes\\ClearGlass",
                "wallpaper.png");
            _hashPath = Path.Combine(
                Path.GetDirectoryName(_wallpaperPath),
                "wallpaper.hash");
                
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
            
            // Ensure overlays are hidden initially
            AddonsOverlay.Opacity = 0;
            AddonsOverlay.Margin = new Thickness(0, 600, 0, -600);
            OptimizationOverlay.Opacity = 0;
            OptimizationOverlay.Margin = new Thickness(0, 600, 0, -600);
        }

        private void LoadCurrentSettings()
        {
            TaskbarAlignmentToggle.IsChecked = _themeService.IsTaskbarCentered;
            TaskViewToggle.IsChecked = _themeService.IsTaskViewEnabled;
            WidgetsToggle.IsChecked = _themeService.AreWidgetsEnabled;
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

        private void OnWidgetsToggle(object sender, RoutedEventArgs e)
        {
            _themeService.AreWidgetsEnabled = WidgetsToggle.IsChecked ?? false;
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

                // Apply Clear Glass theme
                await EnsureWallpaperAsync();
                await Task.Delay(100); // Give Windows time to process the wallpaper change

                // Apply Clear Glass theme settings
                TaskbarAlignmentToggle.IsChecked = false; // Left alignment
                _themeService.IsTaskbarCentered = false;
                await Task.Delay(100); // Wait for taskbar change

                TaskViewToggle.IsChecked = false; // Hide
                _themeService.IsTaskViewEnabled = false;
                await Task.Delay(100); // Wait for task view change

                SearchToggle.IsChecked = false; // Hide
                _themeService.IsSearchVisible = false;
                await Task.Delay(100); // Wait for search change

                DesktopIconsToggle.IsChecked = false; // Hide
                _themeService.AreDesktopIconsVisible = false;
                await Task.Delay(100); // Wait for desktop icons change

                ThemeToggle.IsChecked = true; // Dark theme
                await Task.Run(() => _themeService.IsDarkMode = true);
                await Task.Delay(400); // Give more time for theme change

                // Force a Windows shell refresh
                _themeService.RefreshWindows();

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

                    // Apply Clear Glass theme
                    await EnsureWallpaperAsync();

                    // Apply Clear Glass theme settings
                    TaskbarAlignmentToggle.IsChecked = false; // Left alignment
                    _themeService.IsTaskbarCentered = false;

                    TaskViewToggle.IsChecked = false; // Hide
                    _themeService.IsTaskViewEnabled = false;

                    SearchToggle.IsChecked = false; // Hide
                    _themeService.IsSearchVisible = false;

                    DesktopIconsToggle.IsChecked = false; // Hide
                    _themeService.AreDesktopIconsVisible = false;

                    ThemeToggle.IsChecked = true; // Dark theme
                    await Task.Run(() => _themeService.IsDarkMode = true);

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
            AddonsOverlay.Visibility = Visibility.Visible;
            _showAddonsOverlay.Begin(this);
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
            this.Close();
        }
    }
} 
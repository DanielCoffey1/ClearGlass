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
        private bool _isThemeChanging = false;
        private readonly string _wallpaperUrl = "https://raw.githubusercontent.com/DanielCoffey1/ClearGlassWallpapers/main/glassbackground.png";
        private readonly string _wallpaperPath;
        private readonly string _hashPath;
        private Storyboard _showAddonsOverlay;
        private Storyboard _hideAddonsOverlay;

        public MainWindow()
        {
            InitializeComponent();
            _themeService = new ThemeService();
            
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
            
            // Ensure overlay is hidden initially
            AddonsOverlay.Opacity = 0;
            AddonsOverlay.Margin = new Thickness(0, 600, 0, -600);
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

                // Ensure wallpaper is available and set it
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
                    "Clear Glass Theme applied successfully!",
                    "Clear Glass",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error applying Clear Glass Theme: {ex.Message}",
                    "Theme Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                ClearGlassThemeButton.IsEnabled = true;
            }
        }

        private void OnWindowsOptimizationClick(object sender, RoutedEventArgs e)
        {
            // Placeholder for Windows Optimization functionality
            MessageBox.Show("Windows Optimization button clicked. Functionality coming soon!", "Clear Glass", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnClearGlassClick(object sender, RoutedEventArgs e)
        {
            // Placeholder for Clear Glass functionality
            MessageBox.Show("Clear Glass button clicked. Functionality coming soon!", "Clear Glass", MessageBoxButton.OK, MessageBoxImage.Information);
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
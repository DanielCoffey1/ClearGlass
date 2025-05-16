using System.Windows;
using System.Threading.Tasks;
using ClearGlass.Services;
using System;
using System.Windows.Threading;

namespace ClearGlass
{
    public partial class MainWindow : Window
    {
        private readonly ThemeService _themeService;
        private bool _isThemeChanging = false;

        public MainWindow()
        {
            InitializeComponent();
            _themeService = new ThemeService();
            LoadCurrentSettings();
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
    }
} 
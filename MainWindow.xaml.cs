using System.Windows;
using ClearGlass.Services;

namespace ClearGlass
{
    public partial class MainWindow : Window
    {
        private readonly ThemeService _themeService;

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
    }
} 
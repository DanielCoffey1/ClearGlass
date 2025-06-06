using System.Windows;

namespace ClearGlass
{
    public enum BrowserChoice
    {
        Both,
        LibreWolf,
        Brave,
        Cancel
    }

    public partial class BrowserChoiceDialog : Window
    {
        public BrowserChoice Choice { get; private set; }

        public BrowserChoiceDialog()
        {
            InitializeComponent();
            Choice = BrowserChoice.Cancel;
        }

        private void OnBothBrowsersClick(object sender, RoutedEventArgs e)
        {
            Choice = BrowserChoice.Both;
            DialogResult = true;
            Close();
        }

        private void OnLibreWolfClick(object sender, RoutedEventArgs e)
        {
            Choice = BrowserChoice.LibreWolf;
            DialogResult = true;
            Close();
        }

        private void OnBraveClick(object sender, RoutedEventArgs e)
        {
            Choice = BrowserChoice.Brave;
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Choice = BrowserChoice.Cancel;
            DialogResult = false;
            Close();
        }
    }
} 
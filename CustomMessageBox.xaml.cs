using System.Windows;
using System.Windows.Media.Imaging;

namespace ClearGlass
{
    public partial class CustomMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        private CustomMessageBox()
        {
            InitializeComponent();
            
            // Enable window dragging
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                    this.DragMove();
            };
        }

        public static MessageBoxResult Show(string message, string title = "Clear Glass", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, System.Windows.TextAlignment alignment = System.Windows.TextAlignment.Left)
        {
            var msgBox = new CustomMessageBox();
            msgBox.Title = title;
            msgBox.MessageText.Text = message;
            msgBox.MessageText.TextAlignment = alignment;

            // Configure buttons
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    msgBox.OkButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.OKCancel:
                    msgBox.OkButton.Visibility = Visibility.Visible;
                    msgBox.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    msgBox.YesButton.Visibility = Visibility.Visible;
                    msgBox.NoButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    msgBox.YesButton.Visibility = Visibility.Visible;
                    msgBox.NoButton.Visibility = Visibility.Visible;
                    msgBox.CancelButton.Visibility = Visibility.Visible;
                    break;
            }

            // Set icon
            string iconPath = "";
            switch (icon)
            {
                case MessageBoxImage.Warning:
                    iconPath = "pack://application:,,,/Images/warning.png";
                    break;
                case MessageBoxImage.Question:
                    iconPath = "pack://application:,,,/Images/question.png";
                    break;
                case MessageBoxImage.Information:
                    iconPath = "pack://application:,,,/Images/info.png";
                    break;
                case MessageBoxImage.Error:
                    iconPath = "pack://application:,,,/Images/error.png";
                    break;
            }

            if (!string.IsNullOrEmpty(iconPath))
            {
                try
                {
                    msgBox.MessageIcon.Source = new BitmapImage(new System.Uri(iconPath));
                    msgBox.MessageIcon.Visibility = Visibility.Visible;
                }
                catch
                {
                    msgBox.MessageIcon.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                msgBox.MessageIcon.Visibility = Visibility.Collapsed;
            }

            msgBox.ShowDialog();
            return msgBox._result;
        }

        private void OnYesClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Yes;
            Close();
        }

        private void OnNoClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.No;
            Close();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.OK;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
            Close();
        }
    }
} 
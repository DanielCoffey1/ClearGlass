using System;
using System.Windows;
using System.Windows.Threading;

namespace ClearGlass
{
    public partial class ProgressDialog : Window
    {
        public ProgressDialog()
        {
            InitializeComponent();
        }

        public void UpdateProgress(string status, int percentage)
        {
            // Ensure we're on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => UpdateProgress(status, percentage)));
                return;
            }

            StatusText.Text = status;
            ProgressBar.Value = percentage;
            ProgressText.Text = $"{percentage}%";
        }
    }
} 
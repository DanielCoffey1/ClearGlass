using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;

namespace ClearGlass
{
    public partial class ProgressDialog : Window
    {
        private readonly Action<string> _updateProgressAction;
        private readonly Action _onCompleteAction;

        public ProgressDialog(Action<string> updateProgressAction, Action onCompleteAction)
        {
            InitializeComponent();
            _updateProgressAction = updateProgressAction;
            _onCompleteAction = onCompleteAction;

            // Start fade in animation
            var fadeInAnimation = (Storyboard)FindResource("FadeInAnimation");
            fadeInAnimation.Begin();
        }

        public void UpdateProgress(string step, int percentage)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressTitle.Text = step;
                ProgressBar.Value = percentage;
                ProgressDescription.Text = $"{percentage}%";
            });
        }

        public void Complete()
        {
            Dispatcher.Invoke(() =>
            {
                // Start fade out animation
                var fadeOutAnimation = (Storyboard)FindResource("FadeOutAnimation");
                fadeOutAnimation.Completed += (s, e) =>
                {
                    _onCompleteAction?.Invoke();
                    Close();
                };
                fadeOutAnimation.Begin();
            });
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Make the window click-through
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, extendedStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED);
            
            // Ensure the window is properly positioned and sized
            WindowState = WindowState.Maximized;
            Top = 0;
            Left = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }
    }

    internal static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_LAYERED = 0x80000;

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
} 
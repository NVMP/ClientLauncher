using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ClientLauncher.Windows
{
    /// <summary>
    /// Interaction logic for EOSAuthenticate.xaml
    /// </summary>
    public partial class EOSAuthenticate : Window
    {
        protected MainWindow ParentWindow;
        protected IDictionary<string, string> FailureReasonsToDisplayReason = new Dictionary<string, string>()
        {
            { "AccessDenied", "Authorization rejected for Epic Games account" }
        };

        public bool Succeeded { get; private set; } = false;

        internal void TryToPresentFailureReason(string reason)
        {
            ShowingSeamlessWindow(false);

            if (FailureReasonsToDisplayReason.TryGetValue(reason, out string value))
            {
                ErrorMessageLabel.Text = value;
            }
            else
            {
#if DEBUG
                ErrorMessageLabel.Text = $"DEV ONLY: {reason}";
#endif
            }
        }

        internal void HideFailureReason()
        {
            ErrorMessageLabel.Text = "";
        }

        public void ShowingSeamlessWindow(bool seamless)
        {
            if (seamless)
            {
                // If we are seamless, the window is fully transparent and just the NV:MP logo is shown.
                AllowsTransparency = true;
                WindowStyle = WindowStyle.None;
                Background = Brushes.Transparent;

                NVMPLogo.Visibility = Visibility.Visible;

                Authorizing_Button.Visibility = Visibility.Hidden;
                SignIn_Button.Visibility = Visibility.Hidden;
                OfflineMode_Label.Visibility = Visibility.Hidden;
                BriefingLabel.Visibility = Visibility.Hidden;
            }
            else
            {
                // If we are not seamless, then the window is showing the full WPF window contents for login instructions.
                AllowsTransparency = false;
                WindowStyle = WindowStyle.ToolWindow;
                Background = new BrushConverter().ConvertFrom("#FF1A1A1A") as SolidColorBrush;
                NVMPLogo.Visibility = Visibility.Hidden;
            }
        }

        public EOSAuthenticate(MainWindow parentWindow)
        {
            //Visibility = Visibility.Hidden;

            ParentWindow = parentWindow;
            InitializeComponent();

            ShowingSeamlessWindow(true);

            ParentWindow.EOSManager.TryAutoLogin((result) =>
            {
                Activate();

                if (!result.Success)
                {
                    Authorizing_Button.Visibility = Visibility.Hidden;
                    SignIn_Button.Visibility = Visibility.Visible;
                    OfflineMode_Label.Visibility = Visibility.Visible;

                    // If the error can be understood, present the error message
                    TryToPresentFailureReason(result.FailureReason);
                }
                else
                {
                    Close(); // Close the window as authentication is already handled.
                    Succeeded = true;
                }
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            HideFailureReason();

            // Before we try to log in as a new user through flow (non-auto), we want to flush any persistent login from EOS.
            ParentWindow.EOSManager.LogoutFromPersistent();

            ParentWindow.EOSManager.PresentLogin((result) =>
            {
                Activate();

                if (result.Success)
                {
                    Close();
                    Succeeded = true;
                }
                else
                {
                    Authorizing_Button.Visibility = Visibility.Hidden;
                    SignIn_Button.Visibility = Visibility.Visible;
                    OfflineMode_Label.Visibility = Visibility.Visible;

                    // If the error can be understood, present the error message
                    TryToPresentFailureReason(result.FailureReason);
                }
            });
        }

        private void OfflineMode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            Succeeded = true;
        }
    }
}

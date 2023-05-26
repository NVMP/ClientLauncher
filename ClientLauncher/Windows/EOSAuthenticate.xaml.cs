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

        internal void TryToPresentFailureReason(string reason)
        {
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

        public EOSAuthenticate(MainWindow parentWindow)
        {
            //Visibility = Visibility.Hidden;

            ParentWindow = parentWindow;
            InitializeComponent();

            Activate();
            Topmost = true;  // important
            Topmost = false; // important
            Focus();         // important

            ParentWindow.EOSManager.TryAutoLogin((result) =>
            {
                Activate();

                if (!result.Success)
                {
                    Authorizing_Button.Visibility = Visibility.Hidden;
                    SignIn_Button.Visibility = Visibility.Visible;

                    // If the error can be understood, present the error message
                    TryToPresentFailureReason(result.FailureReason);
                }
                else
                {
                    Close(); // Close the window as authentication is already handled.
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
                }
                else
                {
                    Authorizing_Button.Visibility = Visibility.Hidden;
                    SignIn_Button.Visibility = Visibility.Visible;

                    // If the error can be understood, present the error message
                    TryToPresentFailureReason(result.FailureReason);
                }
            });
        }
    }
}

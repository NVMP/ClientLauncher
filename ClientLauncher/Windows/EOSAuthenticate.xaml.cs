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

        public EOSAuthenticate(MainWindow parentWindow)
        {
            //Visibility = Visibility.Hidden;

            ParentWindow = parentWindow;
            InitializeComponent();

            ParentWindow.EOSManager.TryAutoLogin((result) =>
            {
                if (!result)
                {
                    Authorizing_Button.Visibility = Visibility.Hidden;
                    SignIn_Button.Visibility = Visibility.Visible;
                }
                else
                {
                    Close(); // Close the window as authentication is already handled.
                }
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ParentWindow.EOSManager.PresentLogin((result) =>
            {

                if (result)
                {
                    Close();
                }
            });
        }
    }
}

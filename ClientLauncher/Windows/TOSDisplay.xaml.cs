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
    /// Interaction logic for TOSDisplay.xaml
    /// </summary>
    public partial class TOSDisplay : Window
    {
        public bool IsAccepted { get; set; } = false;

        public TOSDisplay(string content)
        {
            InitializeComponent();
            Browser.NavigateToString(content);

            Topmost = true;  // important
            Activate();
        }

        private void Agree_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted = true;
            Close();
        }
        private void Deny_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted = false;
            Close();
        }
    }
}

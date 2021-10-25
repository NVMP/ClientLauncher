using System;
using System.Windows;
using System.Windows.Controls;

namespace ClientLauncher.Windows
{
    /// <summary>
    /// Interaction logic for JoiningServerDisplay.xaml
    /// </summary>
    public partial class JoiningServerDisplay : Window
    {
        public JoiningServerDisplay()
        {
            InitializeComponent();
        }

        public void Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}

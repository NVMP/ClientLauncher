using ClientLauncher.Core;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ClientLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ManuallyJoinServerDisplay.xaml
    /// </summary>
    public partial class ManuallyJoinServerDisplay : Window
    {
        protected MainWindow ParentWindow;

        public ManuallyJoinServerDisplay(MainWindow parentWindow)
        {
            InitializeComponent();
            ParentWindow = parentWindow;

            

            if (ParentWindow.StorageService.PreviousCustomIP != null &&
                ParentWindow.StorageService.PreviousCustomIP.Length != 0)
            {
                ConnectionInformation.Text = ParentWindow.StorageService.PreviousCustomIP;
            }
        }

        public void Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        public void Connect_Click(object sender, EventArgs e)
        {
            // Try to parse the current server information
            var info = ConnectionInformation.Text.Split(':');
            if (info.Length != 2)
            {
                return;
            }

            ushort port = 0;
            try
            {
                port = ushort.Parse(info[1]);
            } catch (Exception)
            {
                return;
            }

            Close();

            try
            {
                ParentWindow.StorageService.PreviousCustomIP = ConnectionInformation.Text;

                ParentWindow.JoinServer(new Dtos.DtoGameServer
                {
                    IP = info[0],
                    Port = port,
                    IsPrivate = true
                });
            } catch (Exception ex) {
                Trace.WriteLine(ex.ToString());
            }
        }
    }
}

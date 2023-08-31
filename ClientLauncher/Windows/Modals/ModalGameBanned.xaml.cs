using ClientLauncher.Core.EOS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ClientLauncher.Windows.Modals
{
    /// <summary>
    /// Interaction logic for ModalGameBanned.xaml
    /// </summary>
    public partial class ModalGameBanned : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public ModalGameBanned(
#if EOS_SUPPORTED
            IEOSUserSanction sanction
#endif
            )
        {
            InitializeComponent();

            Topmost = true;  // important
            Activate();

#if EOS_SUPPORTED
            if (!sanction.ExpiresAt.HasValue)
            {
                AdditionalDetails.Text = "This ban cannot be appealed, and the decision is final.";
            }
            else
            {
                AdditionalDetails.Text = $"This ban expires on {sanction.ExpiresAt.Value:F}";
            }
#endif

            Loaded += ModalGameBanned_Loaded;
        }

        private void ModalGameBanned_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

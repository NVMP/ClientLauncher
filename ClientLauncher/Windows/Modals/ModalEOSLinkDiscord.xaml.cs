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

#if EOS_SUPPORTED
using ClientLauncher.Core.EOS;
#endif 

namespace ClientLauncher.Windows.Modals
{
    /// <summary>
    /// Interaction logic for ModalEOSLinkDiscord.xaml
    /// </summary>
    public partial class ModalEOSLinkDiscord : Window
    {
        protected IEOSManager EOSManager;
        protected IDictionary<string, string> FailureReasonsToDisplayReason = new Dictionary<string, string>()
        {
            { "LinkedToAnotherAccount", "This Discord account is already linked to another account." }
        };

        internal void TryToPresentFailureReason(string reason)
        {
            if (FailureReasonsToDisplayReason.TryGetValue(reason, out string value))
            {
                AdditionalDetails.Text = value;
            }
            else
            {
#if DEBUG
                AdditionalDetails.Text = $"DEV ONLY: {reason}";
#endif
            }
        }

        public ModalEOSLinkDiscord(
#if EOS_SUPPORTED
            IEOSManager eosManager
#endif
            )
        {
            InitializeComponent();
            EOSManager = eosManager;

            Activate();
            Topmost = true;  // important
            Topmost = false; // important
            Focus();         // important

        }

        private void TryPresentError(string reason)
        {

        }

        private void LinkExternally_Click(object sender, RoutedEventArgs e)
        {
            AdditionalDetails.Text = "";

            EOSManager.ConnectExternalLoginType(EOSLoginType.Discord, (IEOSLinkageResult result) =>
            {
                if (result.Success)
                {
                    Close();
                }
                else
                {
                    TryPresentError(result.FailureReason);
                }
            });
        }
    }
}

using System.Windows;
using System.Windows.Controls;

namespace ClientLauncher.Windows
{
    /// <summary>
    /// Interaction logic for PatchDisplay.xaml
    /// </summary>
    public partial class PatchDisplay : Window
    {
        public enum EPatchStatus
        {
            kPatchStatus_DownloadingManifest,
            kPatchStatus_Validating,
            kPatchStatus_Downloading,
            kPatchStatus_Applying,
            kPatchStatus_Restarting,
            kPatchStatus_Failed,

            kPatchStatus_DownloadingServerFile
        }

        private EPatchStatus PatchStatus;

        public PatchDisplay()
        {
            InitializeComponent();
        }
        
        public void SetStatus( EPatchStatus status, string message = "", int value = 0)
        {
            PatchStatus = status;

            switch (status)
            {
                case EPatchStatus.kPatchStatus_DownloadingManifest:
                    ProgressTitle.Content = "Downloading manifest...";
                    break;
                case EPatchStatus.kPatchStatus_Validating:
                    ProgressTitle.Content = "Validating file " + message + "...";
                    break;
                case EPatchStatus.kPatchStatus_Downloading:
                    ProgressTitle.Content = "Downloading "     + message + "...";
                    break;
                case EPatchStatus.kPatchStatus_Applying:
                    ProgressTitle.Content = "Applying patch "  + message + "...";
                    break;
                case EPatchStatus.kPatchStatus_Restarting:
                    ProgressTitle.Content = "Restarting client...";
                    break;
                case EPatchStatus.kPatchStatus_DownloadingServerFile:
                    ProgressTitle.Content = "Downloading server resource '" + message + "'...";
                    break;
                case EPatchStatus.kPatchStatus_Failed:
                    ProgressTitle.Content = "Download failure:\n" + message;
                    break;
                default:
                    ProgressTitle.Content = "Unknown status";
                    break;
            }

            if (value == 0)
                return;

            ProgressBar.Value = value;
        }

        public void SetValue( int value )
        {
            if (value == 0)
                return;

            ProgressBar.Value = value;
        }
    }
}

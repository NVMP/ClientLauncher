using ClientLauncher.Core;
using ClientLauncher.Dtos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ClientLauncher.Windows
{
    public partial class DownloadModsDisplay : Window
    {
        protected class ServerModDisplay : INotifyPropertyChanged
        {
            public DtoServerModInfo ModInfo;

            public event PropertyChangedEventHandler PropertyChanged;

            internal DownloadModsDisplay ParentWindow;

            internal bool _IsDownloaded = false;
            internal bool _IsDownloading = false;
            internal bool _HasProcessed = false;
            internal float _FileSizeMB = 0.0f;

            public bool IsDownloaded
            {
                get => _IsDownloaded;
                set
                {
                    _IsDownloaded = value;
                    OnPropertyChanged();
                }
            }

            public bool IsDownloading
            {
            
                get => _IsDownloading || !HasProcessed;
                set
                {
                    _IsDownloading = value;
                    OnPropertyChanged();
                }
            }

            public bool HasProcessed
            {

                get => _HasProcessed;
                set
                {
                    _HasProcessed = value;
                    OnPropertyChanged();
                }
            }

            public float FileSizeMB
            {

                get => _FileSizeMB;
                set
                {
                    _FileSizeMB = value;
                    OnPropertyChanged();
                }
            }

            public Task AcquisionTask { get; set; }

            public string StateMessage { get; set; }

            /// <summary>
            /// Defines if the mod is downloadable by the current machine, but if the mod is already downloaded then disregard.
            /// </summary>
            public bool NotDownloadable => HasProcessed && !IsDownloaded && !ModInfo.Downloadable;

            public bool IsDownloadable => HasProcessed && !IsDownloaded && ModInfo.Downloadable;

            public ServerModDisplay(DownloadModsDisplay parentWindow, DtoServerModInfo modInfo)
            {
                ParentWindow = parentWindow;
                ModInfo = modInfo;
                IsDownloaded = false;
                HasProcessed = false;
            }

            public string FilePath
            {
                get
                {
                    return ModInfo.FilePath;
                }
                set
                {
                    ModInfo.FilePath = value;
                }
            }

            public string FileSizeMBText
            {
                get
                {
                    return $"{FileSizeMB} MB";
                }
            }

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private ObservableCollection<ServerModDisplay> ServerModList;

        public IEnumerable<DtoServerModInfo> ServerMods
        {
            set
            {
                // Transform FilePath to be the local filepath
                var falloutDir = FalloutFinder.GameDir(StorageService);
                var rgx = new Regex("[^a-z.A-Z0-9 -_]");

                var rebuild = new List<ServerModDisplay>();

                foreach (DtoServerModInfo item in value)
                {
                    item.FilePath = $"{falloutDir}\\Data\\{item.Name}";
                    item.Name = rgx.Replace(item.Name, "");

                    rebuild.Add(new ServerModDisplay(this, item));
                }

                ServerModList = new ObservableCollection<ServerModDisplay>(rebuild);
                ModsList.ItemsSource = ServerModList;
            }
        }

        public bool DependenciesResolved;
        public bool IsClosed { get; private set; }
        public string DownloadResourceURL { get; set; }
        public LocalStorage StorageService { get; set; }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            IsClosed = true;
        }

        public DownloadModsDisplay()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void UpdateModStates(bool acquireMods = false)
        {
            Trace.WriteLine("Updating mod states...");
            bool hasAllModsInstalled = true;
            bool anyBlockedDownloads = false;

            string falloutDir = FalloutFinder.GameDir(StorageService);
            foreach (ServerModDisplay serverMod in ServerModList)
            {
                //
                // Kick off any download tasks
                //
                if (serverMod.AcquisionTask == null || (serverMod.ModInfo.Downloadable && !serverMod.IsDownloaded && acquireMods))
                {
                    serverMod.StateMessage = "Looking up...";
                    serverMod.HasProcessed = false;

                    serverMod.AcquisionTask = Task.Run(() =>
                    {
                        string modDownloadUrl = $"{DownloadResourceURL}/{serverMod.ModInfo.Name}";

                        // Is this mod on disk?
                        bool validMod = false;

                        string fileName = $"{falloutDir}\\Data\\{serverMod.ModInfo.Name}";
                        if (File.Exists(fileName))
                        {
                            // Is the digest the same as the one reported?
                            string digest = null;

                            try
                            {
                                using (var file = File.OpenRead(fileName))
                                {
                                    using (var digester = MD5.Create())
                                    {
                                        byte[] hashBytes = digester.ComputeHash(file);
                                        StringBuilder sb = new StringBuilder();

                                        for (int i = 0; i < hashBytes.Length; i++)
                                        {
                                            sb.Append(hashBytes[i].ToString("x2"));
                                        }

                                        digest = sb.ToString();

                                        validMod = serverMod.ModInfo.Digest == "*" || digest == serverMod.ModInfo.Digest;
                                        Trace.WriteLine($"Our digest is {digest}, server is {serverMod.ModInfo.Digest}");
                                    }
                                }
                            } catch (Exception)
                            {
                                Trace.Write("Digest Run Fail");
                            }
                        }

                        if (validMod)
                        {
                            Trace.WriteLine($"{serverMod.ModInfo.FilePath} downloaded");
                            serverMod.IsDownloaded = true;
                            serverMod.IsDownloading = false;
                        }
                        else
                        {
                            // Failures? If we can download this, then download
                            Trace.WriteLine($"{serverMod.ModInfo.FilePath} not downloaded");
                            serverMod.IsDownloaded = false;
                            serverMod.IsDownloading = false;

                            if (serverMod.ModInfo.Downloadable)
                            {
                                if (acquireMods)
                                {
                                    // Download
                                    serverMod.IsDownloading = true;

                                    bool canDownload = true;

                                    if (File.Exists(fileName))
                                    {
                                        // If we are downloading and the file exists (this can happen if the CRC is invalid), then
                                        // make sure we prompt this before we overwrite
                                        var mboxResult = MessageBox.Show($"{serverMod.ModInfo.Name} already exists locally, overwrite with server copy?", "Mod Conflict",
                                            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                                        
                                        if (mboxResult != MessageBoxResult.OK)
                                        {
                                            canDownload = false;
                                        }
                                    }

                                    if (canDownload)
                                    {
                                        try
                                        {
                                            WebRequest req = WebRequest.Create(modDownloadUrl);
                                            using (var resp = req.GetResponse())
                                            {
                                                using (var fs = File.Create(fileName))
                                                {
                                                    resp.GetResponseStream().CopyTo(fs);

                                                    Trace.WriteLine($"{serverMod.ModInfo.FilePath} downloaded");
                                                    serverMod.IsDownloading = false;
                                                    serverMod.IsDownloaded = true;
                                                }
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            MessageBox.Show($"Failed to download {serverMod.ModInfo.Name} due to internal error", "Download Error", 
                                                MessageBoxButton.OK, MessageBoxImage.Error);

                                            serverMod.IsDownloading = false;
                                            serverMod.IsDownloaded = false;
                                        }
                                    }
                                }
                                else
                                {
                                    // Query the size to let the player know
                                    try
                                    {
                                        WebRequest req = WebRequest.Create(modDownloadUrl);
                                        req.Method = "HEAD";
                                        using (var resp = req.GetResponse())
                                        {
                                            if (long.TryParse(resp.Headers.Get("Content-Length"), out long size))
                                            {
                                                serverMod.FileSizeMB = (float)Math.Round(size / 1024.0f / 1024.0f, 2);
                                            }
                                        }
                                    } catch (Exception)
                                    {
                                    }
                                }
                            }
                        }

                        serverMod.HasProcessed = true;
                        
                        Dispatcher.Invoke(() =>
                        {
                            UpdateModStates();
                        });
                    });
                }

                //
                // State Updates
                //
                if (!serverMod.IsDownloaded && !serverMod.ModInfo.Downloadable)
                {
                    //Trace.WriteLine($"{serverMod.ModInfo.FilePath} not downloadable");
                    anyBlockedDownloads = true;
                }

                if (!serverMod.IsDownloaded)
                {
                    //Trace.WriteLine($"{serverMod.ModInfo.FilePath} not downloaded");
                    hasAllModsInstalled = false;
                }
            }

            bool anyPendingAcquires = ServerModList.Any(mod => mod.AcquisionTask != null && !mod.HasProcessed);
            Install_All.IsEnabled = !anyBlockedDownloads && !anyPendingAcquires;

            if (hasAllModsInstalled)
            {
                Close();
            }

            DependenciesResolved = hasAllModsInstalled;
        }

        public void Install_All_Click(object sender, EventArgs e)
        {
            Install_All.IsEnabled = false;
            UpdateModStates(true);
        }

        public void Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}

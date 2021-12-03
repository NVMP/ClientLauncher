using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Timers;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using RestSharp;
using Newtonsoft.Json;
using ClientLauncher.Core.XNative;
using ClientLauncher.Dtos;
using ClientLauncher.Core;
using System.Linq;

namespace ClientLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Global constants

#if DEBUG
        private static readonly int TimerIntervalQueryServers = 6 * 1000;
        private static readonly int TimerIntervalPingServers = 1 * 1000;
        private static readonly string BroadcastServer = "http://localhost:5000/";
#else
        private static readonly int TimerIntervalQueryServers = 60 * 1000;
        private static readonly int TimerIntervalPingServers = 10 * 1000;
        private static readonly string BroadcastServer = "https://nv-mp.com/";
#endif

        // Services and instances for client auth
#if !NEXUS_CANDIDATE
        public GithubPatchService   PatchService;
#endif 
        public ProgramVersioning    ProgramVersion;
        public DiscordAuthenticator DiscordAuthenticatorService;
        public LocalStorage         StorageService;

        static public string[] VanillaMods = new string[]
        {
            "FalloutNV.esm",
            "DeadMoney.esm",
            "HonestHearts.esm",
            "OldWorldBlues.esm",
            "LonesomeRoad.esm",
            "GunRunnersArsenal.esm",
            "Fallout3.esm",
            "Anchorage.esm",
            "ThePitt.esm",
            "BrokenSteel.esm",
            "PointLookout.esm",
            "Zeta.esm",
            "CaravanPack.esm",
            "ClassicPack.esm",
            "MercenaryPack.esm",
            "TribalPack.esm"
        };

        // Data
        private bool HasGamePatched;
        private Windows.About                AboutWindowInstance;
        private Windows.JoiningServerDisplay JoiningWindowInstance;
        private Windows.ManuallyJoinServerDisplay ManuallyJoinServerWindowInstance;

        private System.Timers.Timer QueryTimer;
        private System.Timers.Timer PingTimer;

        private bool  IsQuerying;
        private bool  IsPinging;
        private int   BlurLevel;

        public string GamePathOverride;

        private ObservableCollection<DtoGameServer> ServerListCollection;

        public MainWindow()
        {
            HasGamePatched = false;
            IsQuerying = false;
            IsPinging = false;
            AboutWindowInstance = null;
            Closing += OnWindowClosing;
            BlurLevel = 0;

            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            {
                MessageBox.Show($"The NV:MP launcher ({Process.GetCurrentProcess().ProcessName}) is already running! ", "New Vegas: Multiplayer");
                Close();
                return;
            }

            ENet.Managed.ManagedENet.Startup();

            // Needed for communication to HTTPS supporting websites. This could either be the version checker, or the patch service.
            // Regardless this needs to stay enabled even if we do a Nexus submission
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.Expect100Continue = true;

            StorageService = new LocalStorage();
            StorageService.TryLoadSavedData();

            string falloutDir = FalloutFinder.GameDir(StorageService);
            if (falloutDir == null)
            {
                MessageBox.Show("No fallout installation was found on this system. NV:MP can't start up without a valid game directory",
                    "New Vegas: Multiplayer", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            ProgramVersion = new ProgramVersioning(falloutDir);

            // Start the viewer up.
            InitializeComponent();
            LoadCustomBackground();

            VersionLabel.DataContext = ProgramVersion;

#if !NEXUS_CANDIDATE
            // Patch before initialising components of the main window.
            PatchService = new GithubPatchService(this, falloutDir);
#else
            // If we are out of sync with the Nexus candidate, throw up a warning overlay and prevent the player from 
            // connecting to servers. They will be incompatible. 
            if (ProgramVersion.IsOutOfDate)
            {
                MessageBox.Show($"NV:MP ({ProgramVersion.CurrentVersion}) is out of date. Please update NV:MP via the Nexus mod page to version {ProgramVersion.LatestRelease.tag_name}, or through your mod manager."
                    , "New Vegas: Multiplayer", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Close();
                return;
            }
#endif
            DiscordAuthenticatorService = new DiscordAuthenticator(this, StorageService);
            
            DiscordAuthenticatorService.Initialize();

            CustomToken.Password = StorageService.CustomToken;

            // Patch if token is here.
            if (!HasGamePatched)
            {
#if !DEBUG && !NEXUS_CANDIDATE
                if (!Debugger.IsAttached)
                {
                    PatchService.Patch();
                }
#endif
                HasGamePatched = true;

                QueryTimer = new System.Timers.Timer
                {
                    Interval = TimerIntervalQueryServers
                };
                QueryTimer.Elapsed += QueryServers;
                QueryTimer.Start();

                PingTimer = new System.Timers.Timer
                {
                    Interval = TimerIntervalPingServers
                };
                PingTimer.Elapsed += PingServers;
                PingTimer.Start();

                new Thread(delegate ()
                {
                    QueryServers(null, null);
                    PingServers(null, null);
                    try
                    {
                        GetAndVerifyInstallation();
                    } catch (Exception e)
                    {
                        Trace.Write(e.Message);
                        Trace.Write(e.StackTrace);
                        Dispatcher.Invoke(() =>
                        {
                            ShowError(e.Message);
                        });
                    }
                }).Start();
            }


            Thread.Sleep(500);

            if (!IsVisible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            Topmost = true;  // important
            Topmost = false; // important
            Focus();         // important
        }

        /// <summary>
        /// Reads the data folder for a custom background image
        /// and if it exists, applies it to the browser window.
        /// </summary>
        public void LoadCustomBackground()
        {
            string GameDir = FalloutFinder.GameDir(StorageService);

            if (GameDir == null)
                return;

            if (!Directory.Exists(GameDir + "\\nvmp\\res"))
                return;

            if (File.Exists(GameDir + "\\nvmp\\res\\LauncherBackground.png"))
            {
                try {
                    BackgroundPanel.ImageSource = new BitmapImage(new Uri(GameDir + "\\nvmp\\res\\LauncherBackground.png"));
                } catch (Exception e)
                {
                    MessageBox.Show("Failed to load custom background, exception thrown. " + e.ToString());
                }
            }
        }

        public void OnWindowClosing(object sender, EventArgs e)
        {
            DiscordAuthenticatorService?.Shutdown();
        }

        private void TryToInstallMSVC(string GameDir)
        {
            if (!Directory.Exists(GameDir + "\\nvmp\\redist"))
            {
                Trace.WriteLine("Redist dir not found");
                return;
            }

            string InstallerExe = GameDir + "\\nvmp\\redist\\vc_redist.x86.exe";
            if (!File.Exists(InstallerExe))
            {
                Trace.WriteLine("Redist file not found");
                return;
            }

            Process installer = new Process();
            installer.StartInfo.FileName         = InstallerExe;
            installer.StartInfo.UseShellExecute  = true;
            installer.StartInfo.WorkingDirectory = GameDir + "\\nvmp\\redist";
            installer.StartInfo.Verb             = "runas";
            installer.StartInfo.Arguments        = "/q"; // Silent installation
            installer.EnableRaisingEvents        = true;
            installer.Start();
            installer.WaitForExit(); // Make sure this process is completed before starting NV:MP
        }

        private void DisplayServerList(List<DtoGameServer> servers)
        {
            if (servers.Count == 0)
            {
                NoServersMessage.Visibility = Visibility.Visible;
            }
            else
            {
                int SoftReturnPosition = ServerList.SelectedIndex;

                NoServersMessage.Visibility = Visibility.Hidden;

                ServerListCollection = new ObservableCollection<DtoGameServer>(servers);
                ServerList.ItemsSource = ServerListCollection;

                ServerList.SelectedIndex = SoftReturnPosition;
            }
        }
        private void PingServers(Object source, System.Timers.ElapsedEventArgs e)
        {
            if (IsPinging)
            {
                return;
            }

            if (ServerListCollection == null)
            {
                return;
            }

            IsPinging = true;

            var pingInstance = new Ping();
            foreach (DtoGameServer server in ServerListCollection)
            {
                PingReply reply;
                try
                {
                    reply = pingInstance.Send(server.IP, 1000);
                    switch (reply.Status)
                    {
                        case IPStatus.Success:
                            {
                                server.DisplayPing = reply.RoundtripTime.ToString() + " ms";
                                break;
                            }
                        default:
                            {
                                server.DisplayPing = "TMOUT";
                                break;
                            }
                    }
                }
                catch (Exception)
                {
                    server.DisplayPing = "ERR";
                }
            }

            Dispatcher.Invoke(() =>
            {
                ServerList.Items.Refresh();
            });

            IsPinging = false;
        }

        private async void QueryServers(object source, ElapsedEventArgs e)
        {
            if (IsQuerying)
            {
                return;
            }

            IsQuerying = true;
            try
            {
                var client = new RestClient(BroadcastServer);
                var request = new RestRequest("/serverlist", Method.GET);

                IRestResponse response = await client.ExecuteAsync(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var servers = JsonConvert.DeserializeObject<List<DtoGameServer>>(response.Content);
                    if (servers != null && servers.Count > 0)
                    {
                        // Sort the servers
                        servers.Sort(delegate (DtoGameServer a, DtoGameServer b)
                        {
                            return b.NumPlayers - a.NumPlayers;
                        });

                        Dispatcher.Invoke(() =>
                        {
                            DisplayServerList(servers);
                            NoServersMessage.Visibility = Visibility.Hidden;
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (ServerListCollection != null)
                            {
                                ServerListCollection.Clear();
                            }
                            NoServersMessage.Visibility = Visibility.Visible;
                        });
                    }
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (ServerListCollection != null)
                        {
                            ServerListCollection.Clear();
                        }

                        NoServersMessage.Visibility = Visibility.Visible;
                        ShowError("Server Update HTTP Exception: " + response.ErrorMessage);
                        Trace.WriteLine(response.ErrorMessage);
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowError("Server Update Exception: " + ex.Message);

                    if (ServerListCollection != null)
                    {
                        ServerListCollection.Clear();
                    }
                    NoServersMessage.Visibility = Visibility.Visible;
                });
            }

            Dispatcher.Invoke(() =>
            {
                LoadingServersMessage.Visibility = Visibility.Hidden;
            });

            IsQuerying = false;
        }

        private void FalloutClosed(object sender, System.EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Play_Control.IsEnabled       = true;
#if !NEXUS_CANDIDATE
                Repair_Control.IsEnabled     = true;
#endif
                RepairSteam_Control.IsEnabled = true;
            });
        }

        public void PushWindowBlur()
        {
            if (BlurLevel == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    var effect = new BlurEffect();
                    effect.KernelType = KernelType.Gaussian;
                    effect.Radius = 10;
                    effect.RenderingBias = RenderingBias.Performance;

                    Effect = effect;
                });
            }
            ++BlurLevel;
        }

        public void ClearWindowBlur()
        {
            Dispatcher.Invoke(() =>
            {
                Effect = null;
            });

            BlurLevel = 0;
        }

        public void PopWindowBlur()
        {
            if (BlurLevel > 0)
            {
                --BlurLevel;

                if (BlurLevel == 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        Effect = null;
                    });
                }
            }
        }

#if !NEXUS_CANDIDATE
        /// <summary>
        /// Click event that should start a forceful resync of NV:MP binaries from GitHub
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Repair_Click(object sender, RoutedEventArgs e)
        {
            string GameDirectory;

            try
            {
                GameDirectory = FalloutFinder.GameDir(StorageService);

                if (GameDirectory == null)
                    throw new Exception("FO:NV or Steam is not installed.");

                TryToInstallMSVC(GameDirectory);
            }
            catch (Exception)
            {
            }

            // Do the patching.
            PatchService.Patch( true );
        }
#endif

        /// <summary>
        /// Repairs via Steam (temp hack)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void SteamRepair_Click(object sender, RoutedEventArgs e)
        {
            Process proc =  Process.Start("steam://validate/22380");
            if (proc != null)
            {
                MessageBox.Show("Please check your Steam client to check if Fallout has been validated");
            }
            else
            {
                MessageBox.Show("Could not start the Steam client");
            }
        }

        /// <summary>
        /// Click event that should open the about page and show license/attribution
        /// information.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void About_Click(object sender, RoutedEventArgs e)
        {
            if (AboutWindowInstance == null)
            {
                // Apply blurred effect whilst about window is open.
                PushWindowBlur();

                AboutWindowInstance = new Windows.About();
                AboutWindowInstance.Closed += DialogBoxClosed;
                AboutWindowInstance.ShowDialog();
            }
        }

        private void DialogBoxClosed(object sender, EventArgs e)
        {
            PopWindowBlur();
            AboutWindowInstance = null;
            ManuallyJoinServerWindowInstance = null;
        }

        public void CustomToken_Changed(object sender, EventArgs e)
        {
            StorageService.CustomToken = CustomToken.Password;
        }

        public void CopyToken_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(CustomToken.Password.ToString());
        }

        public void OverrideGameDir_Click(object sender, RoutedEventArgs e)
        {
            string currentPath = null;

            ShowError(null);

            try
            {
                currentPath = GetAndVerifyInstallation().GameDirectory;
            } catch (Exception)
            {
            }

            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (currentPath != null)
                {
                    dialog.SelectedPath = currentPath;
                }

                dialog.ShowDialog();
                if (dialog.SelectedPath != null)
                {
                    StorageService.GamePathOverride = dialog.SelectedPath;

                    try
                    {
                        GetAndVerifyInstallation();
                    }
                    catch (Exception ex)
                    {
                        ShowError(ex.Message);
                        StorageService.GamePathOverride = null;
                    }
                }
            }
        }

        protected class StartupInfo
        {
            public string GameDirectory { get; set; }
            public string GameExe { get; set; }
            public int GameID { get; set; }
            public string NVMPExe { get; set; }
        }

        protected StartupInfo GetAndVerifyInstallation()
        {
            StartupInfo result = new StartupInfo();

            if (result.GameDirectory == null)
            {
                result.GameDirectory = FalloutFinder.GameDir(StorageService);
            }

            if (result.GameDirectory == null)
            {
                throw new Exception("FO:NV or Steam is not installed");
            }

            result.GameExe = result.GameDirectory + "\\FalloutNV.exe";

            // Only require steam if the folder has a steam api present
            if (File.Exists(result.GameDirectory + "\\steam_api.dll"))
            {
                Process[] SteamInstance = Process.GetProcessesByName("Steam");
                if (SteamInstance.Length == 0)
                {
                    throw new Exception("Steam is not running, please ensure it is running");
                }
            }

            // Validate the Fallout: New Vegas game exists.
            if (!File.Exists(result.GameExe))
            {
                throw new Exception("Fallout: New Vegas is not installed");
            }

            // Validate the Fallout: New Vegas game files are the right
            // checksums (to prevent "no gore" edition being ran).

            string ValidityError = FalloutChecksum.IsGameCorrectVersion(result.GameDirectory);
            if (ValidityError != null)
            {
                throw new Exception("FO:NV copy invalid (" + ValidityError + ")");
            }

            // Validate the NVMP executable exists.
            result.NVMPExe = result.GameDirectory + "\\nvmp_start.exe";
            if (!File.Exists(result.NVMPExe))
            {
                throw new Exception("NV:MP binaries missing, please try to repair");
            }

            result.GameID = FalloutChecksum.LookupGameID(result.GameDirectory);
            if (result.GameID == 0)
            {
                throw new Exception("GameID cannot be calculated");
            }

            return result;
        }

        public void JoinServer(DtoGameServer server, string token = "")
        {
            if (JoiningWindowInstance != null)
            {
                Dispatcher.Invoke(() =>
                {
                    JoiningWindowInstance.Close();
                    JoiningWindowInstance = null;
                });
            }

            var installation = GetAndVerifyInstallation();

            string modsList = "*";
            if (server.Mods != null)
            {
                modsList = String.Join(",", server.Mods.Select(mod => mod.Name));
            }

            if (token.Length == 0)
            {
                token = "null";
            }

            // If the mods list ins't correctly populated, try to PROBE the server to see if we can grab this information
            // directly. We don't do this if we have the mods list already, as this can be delivered elsewhere in the launcher (server list, maybe URL?)
            if (modsList == "*")
            {
                //
                // Try to probe
                // 
                try
                {
                    var probe = new ServerProbe(server);
                    var result = probe.Connect();

                    if (result.State != ServerProbe.ProbeStatus.ProbeState.Replied)
                    {
                        var msg = "Unknown";
                        switch (result.State)
                        {
                            case ServerProbe.ProbeStatus.ProbeState.AwaitingReply:
                                msg = "Awaiting Reply";
                                break;
                            case ServerProbe.ProbeStatus.ProbeState.Unreachable:
                                msg = "Unreachable";
                                break;
                            default: break;
                        }

                        MessageBox.Show($"Could not connect to {server.IP}:{server.Port} [{msg}]", "New Vegas: Multiplayer", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }

                    // The first entries of the result CSV are the mods available. If modsList doesnt exist (from a public server entry), then use the probe result
                    // as this allows for internal reporting to always be a reliable fallback.
                    if (result.CSVEntries != null && result.CSVEntries.Count != 0)
                    {
                        modsList = String.Join(",", result.CSVEntries.FirstOrDefault().Split('\n'));
                    }
                }
                catch (Exception e)
                {
                    ShowError(e.Message);
                    return;
                }
            }

            //
            // Sort Available Mods - Always put default mods first
            //
            var modFileListPaths = new List<string>();
            if (modsList == "*")
            {
                // Query for all availalbe mods types
                var files = Directory
                    .EnumerateFiles($"{installation.GameDirectory}\\Data", "*.*", SearchOption.AllDirectories)
                    .Where(s => new string[] { "esp", "esm" }.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()));

                modFileListPaths = files.ToList();

                // var mods = Directory
                //     .EnumerateFiles($"{installation.GameDirectory}\\Data", "*.*", SearchOption.AllDirectories)
                //     .Where(s => new string[] { "esp", "esm" }.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                //     .Select(s => Path.GetFileName(s));
                // 
                // modsList = "FalloutNV.esm," + String.Join(",", mods);
            }
            else
            {
                modFileListPaths = modsList.Split(',').Select(mod => $"{installation.GameDirectory}\\Data\\{mod}").ToList();
            }

            //
            // Sort the mod file paths to ensure vanilla content is first
            //
            var vanillaContent = modFileListPaths
                .Where(c => VanillaMods.Contains(Path.GetFileName(c)))
                .OrderBy(x => x)
                .ToArray();

            var nonVanillaContent = modFileListPaths
                .Where(c => !VanillaMods.Contains(Path.GetFileName(c)))
                .OrderBy(x => x)
                .ToArray();

            modFileListPaths = vanillaContent.Concat(nonVanillaContent).ToList();

            int index = 0;
            foreach (var modFilePath in modFileListPaths)
            {
                try
                {
                    File.SetLastWriteTime(modFilePath, new DateTime(2000, (index / 29) + 1, index + 1));
                    ++index;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                }
            }

            //
            // Start the game
            //
            Process game = new Process();
            game.StartInfo.FileName             = installation.NVMPExe;
            game.StartInfo.Arguments            = $"{server.IP} {server.Port} {installation.GameID} {HttpUtility.UrlEncode(token)} \"{modsList}\"";
            game.StartInfo.UseShellExecute      = true;
            game.StartInfo.WorkingDirectory     = installation.GameDirectory;
            game.StartInfo.Verb                 = "runas";
            game.EnableRaisingEvents            = true;
            game.Exited += new EventHandler(FalloutClosed);
            game.Start();

            Dispatcher.Invoke(() =>
            {
                Play_Control.IsEnabled = false;
#if !NEXUS_CANDIDATE
                Repair_Control.IsEnabled = false;
#endif
                RepairSteam_Control.IsEnabled = false;
            });
        }

        protected void CancelJoiningServer(object sender, EventArgs e)
        {
            DiscordAuthenticatorService.CancelAuthorization();
            JoiningWindowInstance = null;
        }

        public void ServerItem_Selected(object sender, EventArgs e)
        {
            if (ServerList.SelectedIndex == -1)
            {
                return;
            }

            DtoGameServer server = ServerListCollection[ServerList.SelectedIndex];

            if (server.Authenticator != "" && server.Authenticator != "basic")
            {
                Play_Control.Content = "Authenticate";
                CustomToken.IsEnabled = false;
            }
            else
            {
                Play_Control.Content = "Join";
                CustomToken.IsEnabled = true;
            }
        }

        public void Play_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ServerList.SelectedIndex == -1)
                {
                    return;
                }

                // Do a key flush firstly, so that we don't try to hit the server with a key that is now expired
                StorageService.TryFlushExpiredKeys();

                DtoGameServer server = ServerListCollection[ ServerList.SelectedIndex ];

                //
                // Acquire Mods
                //
                if (server.Mods != null && server.Mods.Count != 0)
                {
                    var downloadModsWindow = new Windows.DownloadModsDisplay
                    {
                        StorageService = StorageService,
                        ServerMods = server.Mods,
                        DownloadResourceURL = server.ModsDownloadURL
                    };

                    downloadModsWindow.UpdateModStates();

                    if (!downloadModsWindow.IsClosed)
                    {
                        downloadModsWindow.ShowDialog();
                    }

                    if (!downloadModsWindow.DependenciesResolved)
                    {
                        ShowError("Failed to download server dependencies");
                        return;
                    }
                }

                //
                // Request Authentication
                //
                if (server.Authenticator != "" && server.Authenticator != "basic")
                {
                    // The server has a custom authentication module, see if we can support it locally
                    string existingToken = null;
                    bool capturingInBrowser = false;

                    switch (server.Authenticator)
                    {
                        case "discord":
                            {
                                DiscordAuthenticator.AuthorizationStatus status = DiscordAuthenticatorService.AuthorizeToServer(server, ref existingToken);
                                if (status == DiscordAuthenticator.AuthorizationStatus.InBrowserCaptive)
                                {
                                    capturingInBrowser = true;
                                }
                                else if (status == DiscordAuthenticator.AuthorizationStatus.ExistingKeyFound)
                                {
                                    capturingInBrowser = false;
                                }
                                break;
                            }
                        default:
                            {
                                ShowError("Unsupported authentication type '" + server.Authenticator + "'");
                                break;
                            }
                    }

                    if (capturingInBrowser)
                    {
                        JoiningWindowInstance = new Windows.JoiningServerDisplay
                        {
                            Title = $"NV:MP - Joining {server.Name}..."
                        };
                        JoiningWindowInstance.JoinStatus.Content = "Authorizing, please check your default browser...";
                        JoiningWindowInstance.Closed += CancelJoiningServer;
                        JoiningWindowInstance.ShowDialog();
                        ShowError(null);
                    }
                    else if (existingToken != null)
                    {
                        JoinServer(server, existingToken);
                        ShowError(null);
                    }
                    else
                    {
                        ShowError("Authentication Protocol Error");
                    }
                }
                else
                {
                    JoinServer(server, CustomToken.Password);
                    ShowError(null);
                }
            }
            catch (Exception exc)
            {
                ShowError(exc.Message + "\n" + exc.StackTrace);
            }
        }
        
        public void ShowError(string errorMsg)
        {
            try
            {
                if (errorMsg != null)
                {
                    File.WriteAllText("nvmp_launcher_last_error.log", $"message: {errorMsg}\ncallstack:\n{Environment.StackTrace}");
                }
            } catch (Exception)
            {
            }

            Dispatcher.Invoke(() =>
            {
                if (errorMsg == null)
                {
                    MessageBorder.Visibility = Visibility.Hidden;
                }
                else
                {
                    MessageBorder.Visibility = Visibility.Visible;
                    MessageText_Control.Text = errorMsg;
                }
            });
        }

        private void ManualJoin_Click(object sender, RoutedEventArgs e)
        {
            if (ManuallyJoinServerWindowInstance == null)
            {
                // Apply blurred effect whilst about window is open.
                PushWindowBlur();

                ManuallyJoinServerWindowInstance = new Windows.ManuallyJoinServerDisplay(this);
                ManuallyJoinServerWindowInstance.Closed += DialogBoxClosed;
                ManuallyJoinServerWindowInstance.ShowDialog();
            }
        }
    }
}

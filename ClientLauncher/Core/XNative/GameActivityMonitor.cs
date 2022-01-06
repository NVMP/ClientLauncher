using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace ClientLauncher.Core.XNative
{
    public class GameActivityMonitor
    {
        public class Activity
        {
            public Process Game { get; set; }
            public Dtos.DtoGameServer Server { get; set; }
            public DateTime Started { get; set; }
        }

        public Activity CurrentActivity { get; set; }

        protected object ActivityMutex;
        protected Thread UpdateThread;
        protected bool Running;

        protected DiscordRichPresence DiscordRP;
        protected MainWindow ParentMainWindow;

        public GameActivityMonitor(MainWindow mainWindow)
        {
            ParentMainWindow = mainWindow;
            CurrentActivity = null;
            UpdateThread = new Thread(UpdateTrackingThread);
            ActivityMutex = new object();
            Running = true;

            DiscordRP = new DiscordRichPresence();

            UpdateThread.Start();
        }

        public void Shutdown()
        {
            Running = false;

            if (UpdateThread != null)
            {
                UpdateThread.Abort();
                UpdateThread.Join();
                UpdateThread = null;
            }

            DiscordRP.Shutdown();
        }

        public void UpdateRichPresence()
        {
            if (CurrentActivity == null)
            {
                DiscordRP.SetState(DiscordRichPresence.RichState.InActive);
                ParentMainWindow.Dispatcher.Invoke(() =>
                {
                    ParentMainWindow.ShowInTaskbar = true;
                    ParentMainWindow.Show();
                    ParentMainWindow.NotifyIcon.Visibility = System.Windows.Visibility.Collapsed;
                });
                return;
            }

            ParentMainWindow.Dispatcher.Invoke(() =>
            {
                ParentMainWindow.ShowInTaskbar = true;
                ParentMainWindow.Hide();
                ParentMainWindow.NotifyIcon.Visibility = System.Windows.Visibility.Visible;
            });
            DiscordRP.SetState(DiscordRichPresence.RichState.InGame, CurrentActivity);
        }

        public void TrackNewInstance(Process process, Dtos.DtoGameServer server)
        {
            ShutdownCurrentActivity();

            Trace.WriteLine($"GAM: Tracking {process.Id}... ({server.Name})");

            lock (ActivityMutex)
            {
                CurrentActivity = new Activity
                {
                    Game = process,
                    Server = server,
                    Started = DateTime.UtcNow
                };
            }

            UpdateRichPresence();
        }

        private void UpdateTrackingThread()
        {
            while (Running)
            {
                Process proc = null;
                lock (ActivityMutex)
                {
                    proc = CurrentActivity?.Game;
                }

                // This is not straightforward as querying for HasExited. At the time of programming this, NV:MP runs its client in an elevated mode, 
                // meaning we may not be able to query it due to UAC. This is just a loop that checks for the process PID in the task list.
                if (proc != null)
                {
                    try
                    {
                        Process.GetProcessById(proc.Id);
                    }
                    catch (ArgumentException)
                    {
                        ShutdownCurrentActivity();
                    }
                }

                Thread.Sleep(1000);
            }
        }

        public void ShutdownCurrentActivity()
        {
            lock (ActivityMutex)
            {
                if (CurrentActivity != null)
                {
                    Trace.WriteLine($"GAM: Untracking {CurrentActivity.Game?.Id}... ({CurrentActivity.Server.Name})");

                    CurrentActivity = null;
                    UpdateRichPresence();
                }
            }
        }
    }
}

using ClientLauncher.Dtos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ClientLauncher.Core.XNative
{
    public class DiscordAuthenticator
    {
        protected DiscordTcpListener ListenServer;
        protected MainWindow ParentWindow;

        protected DtoGameServer AuthorizingAgainst;
        protected LocalStorage StorageService;
        protected DateTimeOffset? LastAuthorizationAttempt;

        public DiscordAuthenticator(MainWindow parentWindow, LocalStorage storageService)
        {
            ListenServer = new DiscordTcpListener();
            StorageService = storageService;
            ParentWindow = parentWindow;
            LastAuthorizationAttempt = null;

            ListenServer.DiscordAuthReceived += OnAuthorizationReceived;
        }

        [DataContract]
        protected class AuthResponse
        {
            public AuthResponse()
            {
            }

            [DataMember(Name = "access_token")]
            public string AccessToken { get; set; }

            [DataMember(Name = "refresh_token")]
            public string RefreshToken { get; set; }

            [DataMember(Name = "expires_in")]
            public int ExpiresIn { get; set; }
        }

        protected void OnAuthorizationReceived(DiscordTcpListener.AuthorizationReceivedArgs args)
        {
            if (AuthorizingAgainst != null &&
                AuthorizingAgainst.AuthenticatorClientID == args.ServerClientID)
            {
                // Parse the authorization token 
                try
                {
                    var authResponse = JsonConvert.DeserializeObject<AuthResponse>(args.ServerAuthResponse);

                    // Acquire focus
                    ParentWindow.Dispatcher.Invoke(() =>
                    {
                        ParentWindow.Focus();
                    });

                    // Make a note
                    StorageService.AuthKeys[AuthorizingAgainst.AuthenticatorClientID] = new LocalStorage.AuthenticationKey
                    {
                        AuthenticatorType = "discord",
                        AuthorizationBlob = JsonConvert.SerializeObject(authResponse),
                        ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(authResponse.ExpiresIn)
                    };

                    StorageService.Save();

                    // Join the server
                    ParentWindow.JoinServer(AuthorizingAgainst, authResponse.AccessToken);
                    ParentWindow.ShowError(null);

                    AuthorizingAgainst = null;
                }
                catch (Exception e)
                {
                    ParentWindow.ShowError("Failed to join server, " + e.Message);
                }

                ParentWindow.PopWindowBlur();
            }
            else
            {
                ParentWindow.ShowError($"An unauthorized server attempted local Discord authorization");
            }
        }

        protected bool IsAuthenticationURLSafe(DtoGameServer server)
        {
            Uri uri;
            if (!Uri.TryCreate(server.AuthenticatorURL, UriKind.Absolute, out uri))
            {
                return false;
            }

            IPAddress[] authenticationUrlEntries = Dns.GetHostAddresses(uri.Host);
            IPAddress[] serverHostEntries = Dns.GetHostAddresses(server.IP);

            if (authenticationUrlEntries.Where(c => serverHostEntries.Contains(c)).Any())
            {
                return true;
            }

            return false;
        }

        public enum AuthorizationStatus
        {
            InBrowserCaptive,
            ExistingKeyFound,
            Failure
        };

        public AuthorizationStatus AuthorizeToServer(DtoGameServer server, ref string existingToken)
        {
            if (!IsAuthenticationURLSafe(server))
            {
                ParentWindow.ShowError("Could not authenticate safely");
                return AuthorizationStatus.Failure;
            }

            StorageService.TryFlushExpiredKeys();

            // Check if there is an existing key that can be used on this server. If we have recently attempted to join though (in the past minute, then
            // clear the key as it might be bugged and we'll take the hit).
            if (StorageService.AuthKeys.ContainsKey(server.AuthenticatorClientID))
            {
                var now = DateTimeOffset.UtcNow;
                if (!LastAuthorizationAttempt.HasValue || (now - LastAuthorizationAttempt.Value).TotalMinutes > 1)
                {
                    existingToken = StorageService.AuthKeys[server.AuthenticatorClientID].AuthorizationBlob;

                    LastAuthorizationAttempt = now;

                    return AuthorizationStatus.ExistingKeyFound;
                }
                else
                {
                    // Remove the key, and fall through to grab a new one
                    StorageService.AuthKeys.Remove(server.AuthenticatorClientID);
                    StorageService.Save();
                }
            }

            // Store the serer locally so we can track any requests to /discord/ safely and verify the server is the one we are authenticating against
            AuthorizingAgainst = server;

            ParentWindow.PushWindowBlur();

            var encodedURL = HttpUtility.UrlEncode(server.AuthenticatorURL);
            Process.Start($"https://discord.com/api/oauth2/authorize?response_type=code&client_id={server.AuthenticatorClientID}&scope=identify&redirect_uri={encodedURL}&prompt=consent");

            return AuthorizationStatus.InBrowserCaptive;
        }

        public void CancelAuthorization()
        {
            if (AuthorizingAgainst != null)
            {
                ParentWindow.PopWindowBlur();
            }

            AuthorizingAgainst = null;
        }

        public void Initialize()
        {
            ListenServer.Initialize();
        }

        public void Shutdown()
        {
            ListenServer.Shutdown();
        }
    }
}

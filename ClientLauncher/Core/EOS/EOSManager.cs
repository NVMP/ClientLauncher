#if EOS_SUPPORTED
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Epic.OnlineServices;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.UserInfo;

namespace ClientLauncher.Core.EOS
{
    public interface IEOSLinkedDiscord
    {
        string Username { get; }
        ulong Id { get; }
    }

    public interface IEOSCurrentUser
    {
        /// <summary>
        /// The display name used across product and Epic.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The account ID of the user on Epic.
        /// </summary>
        string AccountId { get; }

        /// <summary>
        /// The product ID of the user on NV:MP.
        /// </summary>
        string ProductId { get; }

        /// <summary>
        /// An optional linked Discord structure.
        /// </summary>
        IEOSLinkedDiscord LinkedDiscord { get; }
    }

    public enum EOSLoginType
    {
        /// <summary>
        /// The default login type. The target server will accept an epic account. By default all servers
        /// require an Epic Account, so this option is usually always set.
        /// </summary>
        Epic,

        /// <summary>
        /// The login requires a Discord account linked to it. Some servers require you to join their server,
        /// so this ensures that the account has Discord linkage. 
        /// </summary>
        Discord,
    }

    public interface IEOSManager : IDisposable
    {
        /// <summary>
        /// Initializes the manager stack
        /// </summary>
        /// <returns></returns>
        bool Initialize();

        /// <summary>
        /// Obtains the current login information, or NULL if the user is not logged in.
        /// </summary>
        /// <returns></returns>
        IEOSCurrentUser User { get; }

        /// <summary>
        /// Connects the specified login type to the current EOS account. This spins off into callbacks, so it is not safe
        /// to block against it.
        /// </summary>
        /// <param name="loginTypes"></param>
        void ConnectExternalLoginType(EOSLoginType loginType);

        /// <summary>
        /// Presents a login window, and if the window authorizes correctly then the manager will authorize
        /// </summary>
        void PresentLogin(Action<bool> loggedIn = null);

        /// <summary>
        /// Attempts to auto-login via persistent storage.
        /// </summary>
        /// <param name="loggedIn"></param>
        void TryAutoLogin(Action<bool> loggedIn = null);

        /// <summary>
        /// Logs the user out from session storage.
        /// </summary>
        void Logout();

        /// <summary>
        /// Updates any backend processing requests.
        /// </summary>
        void Tick();
    }

    public class EOSManager : IEOSManager
    {
        internal PlatformInterface PlatformInterfaceInstance;
        internal UserInfoInterface UserInfoInterfaceInstance;

        internal Epic.OnlineServices.Auth.AuthInterface AuthInterfaceInstance;
        internal Epic.OnlineServices.Connect.ConnectInterface ConnectInterfaceInstance;

        internal class EOSLinkedDiscord : IEOSLinkedDiscord
        {
            public string Username { get; internal set; }

            public ulong Id { get; internal set; }
    }

        internal class EOSCurrentUser : IEOSCurrentUser
        {
            public string DisplayName { get; internal set; }

            // This is the internal account ID used by the local player post-authentication. This can be used to query information provided the account
            // has been authorized previously. This is the ID on Epic Games.
            public string AccountId { get; internal set; }

            // This is the product ID of the user currently connected to NV:MP. If this is not set, but the account ID is, then the user does not
            // have an account created with us yet.
            public string ProductId { get; internal set; }

            public IEOSLinkedDiscord LinkedDiscord { get; internal set; }
        }

        internal EOSCurrentUser CurrentUser;

        public IEOSCurrentUser User { get => CurrentUser; }


        internal Discord.Discord DiscordSDKInstance;

        // Configurations
        internal Epic.OnlineServices.Auth.AuthScopeFlags DefaultScopeFlags = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile;

        public EOSManager()
        {
            DiscordSDKInstance = new Discord.Discord(536724603054325760, (ulong)Discord.CreateFlags.NoRequireDiscord);
        }

        public void Dispose()
        {
            PlatformInterface.Shutdown();
            PlatformInterfaceInstance = null;

            DiscordSDKInstance.Dispose();
            DiscordSDKInstance = null;
        }

        public void Tick()
        {
            try
            {
                PlatformInterfaceInstance?.Tick();
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
            }

            try
            {
                DiscordSDKInstance?.RunCallbacks();
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
            }
        }

        public bool Initialize()
        {
            /* setup platform */
            var platformFlags = PlatformFlags.DisableOverlay | PlatformFlags.DisableSocialOverlay;
            var initializeOptions = new InitializeOptions()
            {
                ProductName = "NVMP X",
                ProductVersion = Assembly.GetEntryAssembly().GetName().Version.ToString()

            };

            Trace.WriteLine("EOS: Initializing PlatformInterface...");
            var result = PlatformInterface.Initialize(ref initializeOptions);
            if (result != Result.Success)
            {
                return false;
            }

#if DEBUG
            LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.Info);
            LoggingInterface.SetCallback((ref LogMessage message) => Trace.WriteLine($"EOS_SDK: [{message.Level}] {message.Message}"));
#endif

            /* setup interface */
            var options = new WindowsOptions()
            {
                ProductId = "98889e97a3684417838293f71731bec7", /* NVMP X */
                SandboxId = "4a035316785c4a86a60112b79f833d1b", /* LIVE */

                ClientCredentials = new ClientCredentials()
                {
                    ClientId = "xyza7891MHsF1oZYXMzF4JxjLZPHDK5N", /* NATIVE CLIENT */
                    ClientSecret = "045go/ANQYzTSRXnVb/R3VswHnT44ti+LT79EPPedVc", /* Not so secret, but it is fine */
                },
                DeploymentId = "1a3f1e545d9c4a728399c0b4809243c7", /* LIVE */
                Flags = platformFlags,
                IsServer = false
            };

            Trace.WriteLine("EOS: Creating PlatformInterface...");
            PlatformInterfaceInstance = PlatformInterface.Create(ref options);
            if (PlatformInterfaceInstance == null)
                return false;

            Trace.WriteLine("EOS: PlatformInterface Ready");
            AuthInterfaceInstance = PlatformInterfaceInstance.GetAuthInterface();
            UserInfoInterfaceInstance = PlatformInterfaceInstance.GetUserInfoInterface();
            ConnectInterfaceInstance = PlatformInterfaceInstance.GetConnectInterface();

            return true;
        }

        public void Logout()
        {
            if (CurrentUser == null)
                throw new Exception("Can only call this when the user is logged in!");

            var logoutOptions = new Epic.OnlineServices.Auth.LogoutOptions()
            {
                LocalUserId = EpicAccountId.FromString( CurrentUser.AccountId )
            };
            AuthInterfaceInstance.Logout(ref logoutOptions, null, null);
        }

        public void TryAutoLogin(Action<bool> loggedIn = null)
        {
            CurrentUser = null;

            var loginOptions = new Epic.OnlineServices.Auth.LoginOptions()
            {
                Credentials = new Epic.OnlineServices.Auth.Credentials()
                {
                    Type = Epic.OnlineServices.Auth.LoginCredentialType.PersistentAuth
                },
                ScopeFlags = DefaultScopeFlags
            };

            AuthInterfaceInstance.Login(ref loginOptions, null, (ref Epic.OnlineServices.Auth.LoginCallbackInfo callbackInfo) =>
            {
                if (callbackInfo.ResultCode == Result.Success)
                {
                    CurrentUser = new EOSCurrentUser()
                    {
                        AccountId = callbackInfo.LocalUserId.ToString()
                    };



                    // Establish NV:MP user account connection.
                    GetOrCreateProductUser(loggedIn);
                }
                else
                {
                    loggedIn?.Invoke(false);
                }
            });
        }

        public void PresentLogin(Action<bool> loggedIn = null)
        {
            CurrentUser = null;

            // This opens the Epic Games authentication browser window, and hits the callback when the user authenticates.
            var loginOptions = new Epic.OnlineServices.Auth.LoginOptions()
            {
                Credentials = new Epic.OnlineServices.Auth.Credentials() { Type = Epic.OnlineServices.Auth.LoginCredentialType.AccountPortal },
                ScopeFlags = DefaultScopeFlags
            };

            // Note, this logs in with Epic Games. It does not guarentee there is an account on NV:MP yet.
            AuthInterfaceInstance.Login(ref loginOptions, null, (ref Epic.OnlineServices.Auth.LoginCallbackInfo callbackInfo) =>
            {
                if (callbackInfo.ResultCode == Result.Success)
                {
                    CurrentUser = new EOSCurrentUser()
                    {
                        AccountId = callbackInfo.LocalUserId.ToString()
                    };

                    // Establish NV:MP user account connection.
                    GetOrCreateProductUser(loggedIn);
                }
                else
                {
                    loggedIn?.Invoke(false);
                }
            });
        }

        internal void GetOrCreateProductUser(Action<bool> callback)
        {
            // For this, we need to attempt to log the current user into their product account.

            // The main procedure that starts authenticating the Discord token with Epic, for confirmation of how we need to go about
            // linking it up to the current user.
            var copyAuthTokenOptions = new Epic.OnlineServices.Auth.CopyUserAuthTokenOptions()
            {
            };

            Trace.WriteLine("[EOS] Gathering ID token...");
            if (AuthInterfaceInstance.CopyUserAuthToken(ref copyAuthTokenOptions, EpicAccountId.FromString( User.AccountId ), out Epic.OnlineServices.Auth.Token? authToken) == Result.Success)
            {
                // Now we have the Epic token, we can attempt to authenticate with it.
                var loginOptions = new Epic.OnlineServices.Connect.LoginOptions
                {
                    Credentials = new Epic.OnlineServices.Connect.Credentials
                    {
                        Type = ExternalCredentialType.Epic,
                        Token = authToken.Value.AccessToken
                    }
                };

                Trace.WriteLine("[EOS] Attempting to log into any Product account...");
                ConnectInterfaceInstance.Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo callbackInfo) =>
                {
                    if (callbackInfo.ResultCode == Result.Success)
                    {
                        Trace.WriteLine("[EOS] Product account found and authenticated");

                        // Product account authenticated.
                        CurrentUser.ProductId = callbackInfo.LocalUserId.ToString();

                        callback(true);
                    }
                    else if (callbackInfo.ResultCode == Result.InvalidUser)
                    {
                        Trace.WriteLine("[EOS] No product account found, attempting to create one...");

                        // No account found, let's create one.
                        var createUserOptions = new Epic.OnlineServices.Connect.CreateUserOptions()
                        {
                            ContinuanceToken = callbackInfo.ContinuanceToken
                        };

                        ConnectInterfaceInstance.CreateUser(ref createUserOptions, null, (ref Epic.OnlineServices.Connect.CreateUserCallbackInfo data) =>
                        {
                            if (data.ResultCode == Result.Success)
                            {
                                CurrentUser.ProductId = data.LocalUserId.ToString();

                                callback(true);
                            }
                            else
                            {
                                Trace.WriteLine($"[EOS] Unknown error whilst trying to create new product account, error {data.ResultCode}...");
                                callback(false);
                            }
                        });
                    }
                    else
                    {
                        Trace.WriteLine($"[EOS] Unknown error whilst trying to authenticate into product account, error {callbackInfo.ResultCode}...");
                        callback(false);
                    }
                });
            }
            else
            {
                callback(false);
            }

        }

        static internal void Internal_Callback_DiscordAuthorizationResult()
        {
            Trace.WriteLine($"TEST!");
        }

        internal void InternalConnectDiscordLogin()
        {
            if (EOSCurrentSessionAccountId == null)
                throw new Exception("Can only call this when logged in.");

            DiscordSDKInstance.GetApplicationManager().GetOAuth2Token((Discord.Result result, ref Discord.OAuth2Token token) =>
            {
                if (result == Discord.Result.Ok)
                {
                    string accessToken = token.AccessToken;

                    // The main procedure that starts authenticating the Discord token with Epic, for confirmation of how we need to go about
                    // linking it up to the current user.
                    Action startDiscordAuthorization = () =>
                    {
                        Trace.WriteLine("[EOS] Starting Discord authentication...");

                        // For this function, we want to log in using the Discord OAuth token, then decide a few options.
                        var loginOptions = new Epic.OnlineServices.Connect.LoginOptions()
                        {
                            Credentials = new Epic.OnlineServices.Connect.Credentials()
                            {
                                Type = ExternalCredentialType.DiscordAccessToken,
                                Token = accessToken
                            }
                        };

                        ConnectInterfaceInstance.Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo callbackInfo) =>
                        {
                            if (callbackInfo.ResultCode == Result.InvalidUser)
                            {
                                // This means the Discord account is not linked to any account currently. So we can now link it to our current portal session.
                                Trace.WriteLine($"[EOS] Discord account supplied is not linked to any Epic account, attempting to link to current portal session...");

                                ContinuanceToken continuanceTokenFromDiscordAuth = callbackInfo.ContinuanceToken;

                                // We use the contiuance token to now log-in again to the Epic games account, which then we can immediately call LinkAccount
                                var copyAuthTokenOptions = new Epic.OnlineServices.Auth.CopyIdTokenOptions()
                                {
                                    AccountId = EOSCurrentSessionAccountId
                                };

                                if (AuthInterfaceInstance.CopyIdToken(ref copyAuthTokenOptions, out Epic.OnlineServices.Auth.IdToken? authToken) == Result.Success)
                                {
                                    loginOptions = new Epic.OnlineServices.Connect.LoginOptions
                                    {
                                        Credentials = new Epic.OnlineServices.Connect.Credentials
                                        {
                                            Type = ExternalCredentialType.Epic,
                                            Token = authToken.Value.JsonWebToken
                                        }
                                    };

                                    ConnectInterfaceInstance.Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo secondCallbackInfo) =>
                                    {
                                        if (secondCallbackInfo.ResultCode == Result.Success)
                                        {
                                            // Now link!
                                            var linkOptions = new Epic.OnlineServices.Connect.LinkAccountOptions()
                                            {
                                                ContinuanceToken = continuanceTokenFromDiscordAuth,
                                                LocalUserId = secondCallbackInfo.LocalUserId
                                            };
                                            
                                            ConnectInterfaceInstance.LinkAccount(ref linkOptions, null, (ref Epic.OnlineServices.Connect.LinkAccountCallbackInfo data) =>
                                            {
                                                if (data.ResultCode == Result.Success)
                                                {
                                                    // LINKED!
                                                    Trace.WriteLine($"[EOS] Linking Success!");
                                                }
                                                else
                                                {
                                                    Trace.WriteLine($"[EOS] Linking Failed.");
                                                }
                                            });
                                        }
                                        else
                                        {
                                            Trace.WriteLine($"[EOS] Failed to authenticate into the target Epic Games account");
                                        }
                                    });
                                }
                                else
                                {
                                    Trace.WriteLine($"[EOS] Gathering current auth session failed...");

                                }
                            }
                            else if (callbackInfo.ResultCode == Result.Success)
                            {
                                // This means the Discord account is already bound to an account.
                                // TODO: Just fail this. I don't think it's a good idea to allow this at a client level.
                                if (callbackInfo.LocalUserId == EOSCurrentSessionAccountId)
                                {
                                    // The Discord account is already part of our account, so this entire call was redundant.
                                    Trace.WriteLine($"[EOS] Discord account supplied is already linked to our current portal session");
                                }
                                else
                                {
                                    // The Discord account is bound to another account, so we need to unlink it and relink it.
                                    Trace.WriteLine($"[EOS] Discord account supplied is bound to another account. We will unlink and try again.");


                                }
                            }
                            else
                            {
                                // Some other issue occured, back out entirely.
                                Trace.WriteLine($"[EOS] When attempting to log in the Discord account, an unknown error occured ({callbackInfo.ResultCode})");
                            }
                        });
                    };

                    // If we are logged into an account, we need to log-out first.
                    startDiscordAuthorization();

                }
            });

            //var loginOptions = new Epic.OnlineServices.Auth.LoginOptions()
            //{
            //    Credentials = new Epic.OnlineServices.Auth.Credentials()
            //    {
            //        Type = Epic.OnlineServices.Auth.LoginCredentialType.ExternalAuth,
            //        ExternalType = ExternalCredentialType.DiscordAccessToken,
            //    },
            //    ScopeFlags = DefaultScopeFlags
            //};
            //
            //AuthInterfaceInstance.Login(ref loginOptions, null, (ref Epic.OnlineServices.Auth.LoginCallbackInfo callbackInfo) =>
            //{
            //    if (callbackInfo.ResultCode == Result.Success)
            //    {
            //        
            //    }
            //    else
            //    {
            //        loggedIn?.Invoke(false);
            //    }
            //});

            // Log out of any current session, and attempt to log into the Discord account

            // Next, we want to link 
            // AuthInterfaceInstance.LinkAccount
        }

        public void ConnectExternalLoginType(EOSLoginType loginType)
        {
            switch (loginType)
            {
                case EOSLoginType.Discord:
                    InternalConnectDiscordLogin();
                    break;
            }
        }
    }
}
#endif
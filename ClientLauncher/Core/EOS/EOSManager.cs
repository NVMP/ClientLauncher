#if EOS_SUPPORTED
using Epic.OnlineServices;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.UserInfo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace ClientLauncher.Core.EOS
{
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
        EOSLoginType[] LinkedAuths { get; }

        /// <summary>
        /// Sanctions on the current user.
        /// </summary>
        IEOSUserSanction[] Sanctions { get; }
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

    public interface IEOSLoginResult
    {
        /// <summary>
        /// Defines whether the login succeeded or not.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// If it did not succeed, then this string contains the human readable.
        /// </summary>
        string FailureReason { get; }
    }

    public enum EOSUserSanctionType
    {
        /// <summary>
        /// Undefined.
        /// </summary>
        Unknown,

        /// <summary>
        /// Restricts from joining any new servers.
        /// </summary>
        MatchmakingRestriction,

        /// <summary>
        /// Restricts from joining any new servers, but also evicts them from all sessions they are actively in.
        /// </summary>
        GameBan
    }

    public interface IEOSUserSanction
    {
        /// <summary>
        /// The type of sanction in effect.
        /// </summary>
        EOSUserSanctionType Type { get; }

        /// <summary>
        /// The date time it was issued.
        /// </summary>
        DateTimeOffset StartedAt { get; }

        /// <summary>
        /// The date time it expires at, or if perma, this is NULL.
        /// </summary>
        DateTimeOffset? ExpiresAt { get; }
    }

    public interface IEOSLinkageResult
    {
        /// <summary>
        /// Defines whether the login succeeded or not.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// The target login target type.
        /// </summary>
        EOSLoginType Target { get; }

        /// <summary>
        /// If it did not succeed, then this string contains the human readable.
        /// </summary>
        string FailureReason { get; }
    }

    public delegate void EOSUserUpdatedHandler(IEOSCurrentUser user);

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
        /// An event subscriber for when the user property is updated remotely.
        /// </summary>
        event EOSUserUpdatedHandler UserUpdated;

        /// <summary>
        /// Connects the specified login type to the current EOS account. This spins off into callbacks, so it is not safe
        /// to block against it.
        /// </summary>
        /// <param name="loginTypes"></param>
        void ConnectExternalLoginType(EOSLoginType loginType, Action<IEOSLinkageResult> linkageCallback = null);

        /// <summary>
        /// Presents a login window, and if the window authorizes correctly then the manager will authorize
        /// </summary>
        void PresentLogin(Action<IEOSLoginResult> loggedIn = null);

        /// <summary>
        /// Obtains the user's auth token.
        /// </summary>
        /// <returns></returns>
        string GetProductAuthToken();

        /// <summary>
        /// Attempts to auto-login via persistent storage.
        /// </summary>
        /// <param name="loggedIn"></param>
        void TryAutoLogin(Action<IEOSLoginResult> loggedIn = null);

        /// <summary>
        /// Logs the user out from session storage.
        /// </summary>
        void Logout();

        /// <summary>
        /// Logs the user out if they are logged in, and delete's their persistent storage.
        /// </summary>
        void LogoutFromPersistent(Action<IEOSLoginResult> loggedOut = null);

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
        internal Epic.OnlineServices.Sanctions.SanctionsInterface SanctionsInterfaceInstance;

        internal class EOSLoginResult : IEOSLoginResult
        {
            public bool Success { get; internal set; }

            public string FailureReason { get; internal set; }
        }

        internal class EOSUserSanction : IEOSUserSanction
        {
            public EOSUserSanctionType Type { get; internal set; }

            public DateTimeOffset StartedAt { get; internal set; }

            public DateTimeOffset? ExpiresAt { get; internal set; }
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

            // Any linked auths.
            public EOSLoginType[] LinkedAuths => InternalLinkedAuths.ToArray();

            // List of sanctions queried.
            public IEOSUserSanction[] Sanctions { get; internal set; }

            internal List<EOSLoginType> InternalLinkedAuths = new List<EOSLoginType>();
        }

        internal class EOSLinkageResult : IEOSLinkageResult
        {
            public bool Success { get; internal set; }

            public EOSLoginType Target { get; internal set; }

            public string FailureReason { get; internal set; }
        }

        internal EOSCurrentUser CurrentUser;

        public IEOSCurrentUser User { get => CurrentUser; }


        internal Discord.Discord DiscordSDKInstance;

        // Configurations
        internal Epic.OnlineServices.Auth.AuthScopeFlags DefaultScopeFlags = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile | Epic.OnlineServices.Auth.AuthScopeFlags.FriendsList;

        public event EOSUserUpdatedHandler UserUpdated;

        public EOSManager()
        {
            try
            {
                DiscordSDKInstance = new Discord.Discord(536724603054325760, (ulong)Discord.CreateFlags.NoRequireDiscord);
            }
            catch { }
        }

        public void Dispose()
        {
            PlatformInterface.Shutdown();
            PlatformInterfaceInstance = null;

            DiscordSDKInstance?.Dispose();
            DiscordSDKInstance = null;
        }

        internal bool IsTicking = false;

        public void Tick()
        {
            if (IsTicking)
                return;

            IsTicking = true;

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

            IsTicking = false;
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
            LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.VeryVerbose);
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
            SanctionsInterfaceInstance = PlatformInterfaceInstance.GetSanctionsInterface();

            return true;
        }

        public void Logout()
        {
            if (CurrentUser == null)
                return;

            var logoutOptions = new Epic.OnlineServices.Auth.LogoutOptions()
            {
                LocalUserId = EpicAccountId.FromString( CurrentUser.AccountId )
            };
            AuthInterfaceInstance.Logout(ref logoutOptions, null, null);

            CurrentUser = null;
            UserUpdated?.Invoke(CurrentUser);
        }

        public void LogoutFromPersistent(Action<IEOSLoginResult> loggedOut = null)
        {
            if (CurrentUser == null || CurrentUser.AccountId == null)
            {
                // Just go for persistent purge,
                // Wipe persistent
                var deletePersistentAuthOptions = new Epic.OnlineServices.Auth.DeletePersistentAuthOptions()
                {
                };

                AuthInterfaceInstance.DeletePersistentAuth(ref deletePersistentAuthOptions, null, (ref Epic.OnlineServices.Auth.DeletePersistentAuthCallbackInfo _) =>
                {
                    loggedOut?.Invoke(new EOSLoginResult { Success = true });
                });

                CurrentUser = null;
                UserUpdated?.Invoke(CurrentUser);

                return;
            }

            var logoutOptions = new Epic.OnlineServices.Auth.LogoutOptions()
            {
                LocalUserId = EpicAccountId.FromString( CurrentUser.AccountId )
            };

            AuthInterfaceInstance.Logout(ref logoutOptions, null, (ref Epic.OnlineServices.Auth.LogoutCallbackInfo data) =>
            {
                // Wipe persistent
                var deletePersistentAuthOptions = new Epic.OnlineServices.Auth.DeletePersistentAuthOptions()
                {
                };

                AuthInterfaceInstance.DeletePersistentAuth(ref deletePersistentAuthOptions, null, (ref Epic.OnlineServices.Auth.DeletePersistentAuthCallbackInfo _) =>
                {
                    loggedOut?.Invoke(new EOSLoginResult { Success = true });
                });

                CurrentUser = null;
                UserUpdated?.Invoke(CurrentUser);
            });
        }

        public void TryAutoLogin(Action<IEOSLoginResult> loggedIn = null)
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

                    UserUpdated?.Invoke(CurrentUser);

                    // Query account details
                    QueryAccountDetails(callbackInfo.LocalUserId);

                    // Establish NV:MP user account connection.
                    GetOrCreateProductUser(loggedIn);
                }
                else
                {
                    var reason = new EOSLoginResult
                    {
                        Success = false,
                        FailureReason = callbackInfo.ResultCode.ToString()
                    };

                    loggedIn?.Invoke(reason);
                }
            });
        }

        public string GetProductAuthToken()
        {
            ProductUserId currentProductUserId = ProductUserId.FromString(CurrentUser.ProductId);

            var copyIdTokenOptions = new Epic.OnlineServices.Connect.CopyIdTokenOptions();
            copyIdTokenOptions.LocalUserId = currentProductUserId;

            if (ConnectInterfaceInstance.CopyIdToken(ref copyIdTokenOptions, out Epic.OnlineServices.Connect.IdToken? outIdToken) == Result.Success)
            {
                return outIdToken.Value.JsonWebToken.ToString();
            }

            return null;
        }

        public void PresentLogin(Action<IEOSLoginResult> loggedIn = null)
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

                    UserUpdated?.Invoke(CurrentUser);

                    QueryAccountDetails(callbackInfo.LocalUserId);

                    // Establish NV:MP user account connection.
                    GetOrCreateProductUser(loggedIn);
                }
                else
                {
                    var reason = new EOSLoginResult
                    {
                        Success = false,
                        FailureReason = callbackInfo.ResultCode.ToString()
                    };

                    UserUpdated?.Invoke(CurrentUser);

                    loggedIn?.Invoke(reason);
                }
            });
        }

        internal void QueryAccountDetails(EpicAccountId currentEpicAccountId)
        {
            // Send off a delegate to query the user's username for display purposes
            var queryUserInfoOptions = new QueryUserInfoOptions()
            {
                LocalUserId = currentEpicAccountId,
                TargetUserId = currentEpicAccountId
            };

            UserInfoInterfaceInstance.QueryUserInfo(ref queryUserInfoOptions, null, (ref QueryUserInfoCallbackInfo data) =>
            {
                if (data.ResultCode != Result.Success)
                {
                    return;
                }

                var copyUserInfoOptions = new CopyUserInfoOptions()
                {
                    LocalUserId = currentEpicAccountId,
                    TargetUserId = currentEpicAccountId
                };

                // Copy the user info across (display name)
                if (UserInfoInterfaceInstance.CopyUserInfo(ref copyUserInfoOptions, out UserInfoData? outUserInfo) == Result.Success)
                {
                    CurrentUser.DisplayName = outUserInfo.Value.DisplayName;
                    UserUpdated?.Invoke(CurrentUser);
                }

                //// Copy the external account flags 
                //var getExternalUserInfoCountOptions = new GetExternalUserInfoCountOptions()
                //{
                //    LocalUserId = currentEpicAccountId,
                //    TargetUserId = currentEpicAccountId
                //};

                //uint uNumExternalAccounts = UserInfoInterfaceInstance.GetExternalUserInfoCount(ref getExternalUserInfoCountOptions);

                //for (uint i = 0; i < uNumExternalAccounts; ++i)
                //{
                //    var copyExternalUserInfoByIndexOptions = new CopyExternalUserInfoByIndexOptions()
                //    {
                //        LocalUserId = currentEpicAccountId,
                //        TargetUserId = currentEpicAccountId,
                //        Index = i
                //    };

                //    if (UserInfoInterfaceInstance.CopyExternalUserInfoByIndex(ref copyExternalUserInfoByIndexOptions, out ExternalUserInfo? externalUserInfo) == Result.Success)
                //    {
                //        switch (externalUserInfo.Value.AccountType)
                //        {
                //            case ExternalAccountType.Discord:
                //                CurrentUser.InternalLinkedAuths.Add(EOSLoginType.Discord);
                //                break;
                //            default: break;
                //        }
                //    }
                //}

                //if (uNumExternalAccounts != 0)
                //{
                //    UserUpdated?.Invoke(CurrentUser);
                //}
            });
        }

        internal void QueryExternalMappings(Action<bool> callback)
        {
            Trace.WriteLine("[EOS] Querying external mappingings for current user's product ID...");
            if (User == null || User.ProductId == null)
            {
                Trace.WriteLine("[EOS] No valid product ID!");
                callback(false);
                return;
            }

            ProductUserId currentProductUserId = ProductUserId.FromString( CurrentUser.ProductId );

            var queryProductUserIdMappingsOptions = new Epic.OnlineServices.Connect.QueryProductUserIdMappingsOptions()
            {
                LocalUserId = currentProductUserId,
                ProductUserIds = new ProductUserId[] { currentProductUserId }
            };

            ConnectInterfaceInstance.QueryProductUserIdMappings(ref queryProductUserIdMappingsOptions, null,
                (ref Epic.OnlineServices.Connect.QueryProductUserIdMappingsCallbackInfo data) =>
            {
                if (data.ResultCode != Result.Success)
                {
                    callback(false);
                    return;
                }

                // Copy the external account flags 
                var getProductUserExternalAccountCountOptions = new Epic.OnlineServices.Connect.GetProductUserExternalAccountCountOptions()
                {
                    TargetUserId = currentProductUserId
                };

                uint uNumExternalAccounts = ConnectInterfaceInstance.GetProductUserExternalAccountCount(ref getProductUserExternalAccountCountOptions);

                for (uint i = 0; i < uNumExternalAccounts; ++i)
                {
                    var copyProductUserExternalAccountByIndexOptions = new Epic.OnlineServices.Connect.CopyProductUserExternalAccountByIndexOptions()
                    {
                        ExternalAccountInfoIndex = i,
                        TargetUserId = currentProductUserId
                    };

                    if (ConnectInterfaceInstance.CopyProductUserExternalAccountByIndex(ref copyProductUserExternalAccountByIndexOptions, out Epic.OnlineServices.Connect.ExternalAccountInfo? externalAccountInfo) == Result.Success)
                    {
                        switch (externalAccountInfo.Value.AccountIdType)
                        {
                            case ExternalAccountType.Discord:
                                CurrentUser.InternalLinkedAuths.Add(EOSLoginType.Discord);
                                break;
                            default: break;
                        }
                    }
                }

                if (uNumExternalAccounts != 0)
                {
                    UserUpdated?.Invoke(CurrentUser);
                }

                callback(true);
            });
        }

        internal void GetOrCreateProductUser(Action<IEOSLoginResult> callback)
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

                ConnectInterfaceInstance.Login(ref loginOptions, null,
                    (ref Epic.OnlineServices.Connect.LoginCallbackInfo callbackInfo) =>
                {
                    if (callbackInfo.ResultCode == Result.Success)
                    {
                        Trace.WriteLine("[EOS] Product account found and authenticated");

                        // Product account authenticated.
                        ProductUserId currentUserProductId = callbackInfo.LocalUserId;
                        CurrentUser.ProductId = currentUserProductId.ToString();

                        // Query external accounts, if this fails, not the worst case as we go ahead with it again
                        QueryExternalMappings((bool _) =>
                        {
                            // Query sanctions, existing users have sanctions
                            var queryActivePlayerSanctionsOptions = new Epic.OnlineServices.Sanctions.QueryActivePlayerSanctionsOptions()
                            {
                                LocalUserId = currentUserProductId,
                                TargetUserId = currentUserProductId
                            };

                            SanctionsInterfaceInstance.QueryActivePlayerSanctions(ref queryActivePlayerSanctionsOptions, null, 
                                (ref Epic.OnlineServices.Sanctions.QueryActivePlayerSanctionsCallbackInfo data) =>
                            {
                                if (data.ResultCode != Result.Success)
                                {
                                    // Sanctions could not be loaded. This should just fail the current user entirely from loading.
                                    CurrentUser = null;

                                    var reason = new EOSLoginResult
                                    {
                                        Success = false,
                                        FailureReason = data.ResultCode.ToString()
                                    };

                                    UserUpdated?.Invoke(CurrentUser);
                                    Trace.WriteLine($"[EOS] Sanction query failed, {data.ResultCode}...");

                                    callback(reason);
                                    return;
                                }

                                // Else, now we should go through the sanctions list and populate the current user with them.
                                var getPlayerSanctionCountOptions = new Epic.OnlineServices.Sanctions.GetPlayerSanctionCountOptions()
                                {
                                    TargetUserId = currentUserProductId
                                };

                                uint uNumTotalSanctions = SanctionsInterfaceInstance.GetPlayerSanctionCount(ref getPlayerSanctionCountOptions);
                                if (uNumTotalSanctions != 0)
                                {
                                    var sanctions = new List<EOSUserSanction>();
                                    for (uint i = 0; i < uNumTotalSanctions; ++i)
                                    {
                                        var copyPlayerSanctionByIndexOptions = new Epic.OnlineServices.Sanctions.CopyPlayerSanctionByIndexOptions()
                                        {
                                            TargetUserId = currentUserProductId,
                                            SanctionIndex = i
                                        };

                                        if (SanctionsInterfaceInstance.CopyPlayerSanctionByIndex(ref copyPlayerSanctionByIndexOptions, out Epic.OnlineServices.Sanctions.PlayerSanction? sanction) == Result.Success)
                                        {
                                            var sanctInstance = new EOSUserSanction();
                                            switch (sanction.Value.Action)
                                            {
                                                case "RESTRICT_GAME_ACCESS":
                                                    sanctInstance.Type = EOSUserSanctionType.GameBan;
                                                    break;
                                                case "RESTRICT_MATCHMAKING":
                                                    sanctInstance.Type = EOSUserSanctionType.MatchmakingRestriction;
                                                    break;
                                                default:
                                                    sanctInstance.Type = EOSUserSanctionType.Unknown;
                                                    break;
                                            }

                                            sanctInstance.StartedAt = DateTimeOffset.FromUnixTimeSeconds(sanction.Value.TimePlaced);

                                            if (sanction.Value.TimeExpires != 0)
                                            {
                                                sanctInstance.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(sanction.Value.TimeExpires);
                                            }

                                            sanctions.Add(sanctInstance);
                                            Trace.WriteLine($"[EOS] Sanction Found");
                                        }
                                        else
                                        {
                                            // Failed to copy, so fail the user.
                                            CurrentUser = null;

                                            var reason = new EOSLoginResult
                                            {
                                                Success = false,
                                                FailureReason = data.ResultCode.ToString()
                                            };

                                            UserUpdated?.Invoke(CurrentUser);
                                            Trace.WriteLine($"[EOS] Sanction query failed on copy...");

                                            callback(reason);
                                            return;
                                        }
                                    }

                                    CurrentUser.Sanctions = sanctions.ToArray();
                                }

                                UserUpdated?.Invoke(CurrentUser);
                                Trace.WriteLine($"[EOS] Sanction Query succeeded");

                                callback(new EOSLoginResult
                                {
                                    Success = true,
                                });
                            });
                        });
                    }
                    else if (callbackInfo.ResultCode == Result.InvalidUser)
                    {
                        Trace.WriteLine("[EOS] No product account found, attempting to create one...");

                        // No account found, let's create one.
                        var createUserOptions = new Epic.OnlineServices.Connect.CreateUserOptions()
                        {
                            ContinuanceToken = callbackInfo.ContinuanceToken
                        };

                        ConnectInterfaceInstance.CreateUser(ref createUserOptions, null,
                            (ref Epic.OnlineServices.Connect.CreateUserCallbackInfo data) =>
                        {
                            if (data.ResultCode == Result.Success)
                            {
                                CurrentUser.ProductId = data.LocalUserId.ToString();

                                var reason = new EOSLoginResult
                                {
                                    Success = true,
                                };

                                UserUpdated?.Invoke(CurrentUser);

                                callback(reason);
                            }
                            else
                            {
                                var reason = new EOSLoginResult
                                {
                                    Success = false,
                                    FailureReason = data.ResultCode.ToString()
                                };

                                UserUpdated?.Invoke(CurrentUser);
                                Trace.WriteLine($"[EOS] Unknown error whilst trying to create new product account, error {data.ResultCode}...");
                                callback(reason);
                            }
                        });
                    }
                    else
                    {
                        var reason = new EOSLoginResult
                        {
                            Success = false,
                            FailureReason = callbackInfo.ResultCode.ToString()
                        };

                        UserUpdated?.Invoke(CurrentUser);
                        Trace.WriteLine($"[EOS] Unknown error whilst trying to authenticate into product account, error {callbackInfo.ResultCode}...");
                        callback(reason);
                    }
                });
            }
            else
            {
                var reason = new EOSLoginResult
                {
                    Success = false,
                    FailureReason = "AuthTokenFailure"
                };

                UserUpdated?.Invoke(CurrentUser);
                callback(reason);
            }

        }

        internal void InternalConnectDiscordLogin(Action<IEOSLinkageResult> linkageCallback)
        {
            if (User == null || User.AccountId == null)
                throw new Exception("Can only call this when logged in.");

            if (DiscordSDKInstance == null)
                throw new Exception("Discord is not running");

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
                                var copyAuthTokenOptions = new Epic.OnlineServices.Auth.CopyUserAuthTokenOptions();

                                if (AuthInterfaceInstance.CopyUserAuthToken(ref copyAuthTokenOptions, EpicAccountId.FromString( User.AccountId ), out Epic.OnlineServices.Auth.Token? authToken) == Result.Success)
                                {
                                    loginOptions = new Epic.OnlineServices.Connect.LoginOptions
                                    {
                                        Credentials = new Epic.OnlineServices.Connect.Credentials
                                        {
                                            Type = ExternalCredentialType.Epic,
                                            Token = authToken.Value.AccessToken
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
                                                    linkageCallback?.Invoke(new EOSLinkageResult { Success = true, Target = EOSLoginType.Discord });
                                                }
                                                else
                                                {
                                                    Trace.WriteLine($"[EOS] Linking Failed.");
                                                    linkageCallback?.Invoke(new EOSLinkageResult { Success = false, Target = EOSLoginType.Discord, FailureReason = data.ResultCode.ToString() });
                                                }
                                            });
                                        }
                                        else
                                        {
                                            Trace.WriteLine($"[EOS] Failed to authenticate into the target Epic Games account");
                                            linkageCallback?.Invoke(new EOSLinkageResult { Success = false, Target = EOSLoginType.Discord, FailureReason = secondCallbackInfo.ResultCode.ToString() });
                                        }
                                    });
                                }
                                else
                                {
                                    Trace.WriteLine($"[EOS] Gathering current auth session failed...");
                                    linkageCallback?.Invoke(new EOSLinkageResult { Success = false, FailureReason = callbackInfo.ResultCode.ToString(), Target = EOSLoginType.Discord });
                                }
                            }
                            else if (callbackInfo.ResultCode == Result.Success)
                            {
                                // This means the Discord account is already bound to an account.
                                // TODO: Just fail this. I don't think it's a good idea to allow this at a client level.
                                if (callbackInfo.LocalUserId == ProductUserId.FromString( User.ProductId ))
                                {
                                    // The Discord account is already part of our account, so this entire call was redundant.
                                    Trace.WriteLine($"[EOS] Discord account supplied is already linked to our current portal session");
                                    linkageCallback?.Invoke(new EOSLinkageResult { Success = true, Target = EOSLoginType.Discord });
                                }
                                else
                                {
                                    // The Discord account is bound to another account, so we need to unlink it and relink it.
                                    Trace.WriteLine($"[EOS] Discord account supplied is bound to another account. We will unlink and try again.");
                                    linkageCallback?.Invoke(new EOSLinkageResult { Success = false, FailureReason = "LinkedToAnotherAccount", Target = EOSLoginType.Discord });
                                }
                            }
                            else
                            {
                                // Some other issue occured, back out entirely.
                                Trace.WriteLine($"[EOS] When attempting to log in the Discord account, an unknown error occured ({callbackInfo.ResultCode})");
                                linkageCallback?.Invoke(new EOSLinkageResult { Success = false, FailureReason = callbackInfo.ResultCode.ToString(), Target = EOSLoginType.Discord });
                            }
                        });
                    };

                    // If we are logged into an account, we need to log-out first.
                    startDiscordAuthorization();

                }
                else
                {
                    linkageCallback?.Invoke(new EOSLinkageResult { Success = false, FailureReason = result.ToString(), Target = EOSLoginType.Discord });
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

        public void ConnectExternalLoginType(EOSLoginType loginType, Action<IEOSLinkageResult> linkageCallback = null)
        {
            switch (loginType)
            {
                case EOSLoginType.Discord:
                    InternalConnectDiscordLogin((IEOSLinkageResult result) =>
                    {
                        if (result.Success)
                        {
                            QueryExternalMappings((bool v) =>
                            {
                                linkageCallback?.Invoke(result);
                            });
                        }
                        else
                        {
                            linkageCallback?.Invoke(result);
                        }
                    });
                    break;
            }
        }
    }
}
#endif
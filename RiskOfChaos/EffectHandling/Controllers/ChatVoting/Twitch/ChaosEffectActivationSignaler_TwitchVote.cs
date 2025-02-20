﻿using RoR2;
using RoR2.UI;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using UnityEngine;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectHandling.Controllers.ChatVoting.Twitch
{
    [ChaosEffectActivationSignaler(Configs.ChatVoting.ChatVotingMode.Twitch)]
    public class ChaosEffectActivationSignaler_TwitchVote : ChaosEffectActivationSignaler_ChatVote
    {
        static ChaosEffectActivationSignaler_TwitchVote _instance;
        public static ChaosEffectActivationSignaler_TwitchVote Instance => _instance;

        public static bool IsConnectionMessageToken(string token)
        {
            switch (token)
            {
                case "TWITCH_EFFECT_VOTING_LOGIN_FAIL_FORMAT":
                case "TWITCH_LOGIN_FAIL_NOT_LOGGED_IN":
                case "TWITCH_EFFECT_VOTING_CONNECTION_ERROR":
                case "TWITCH_EFFECT_VOTING_GENERIC_CLIENT_CONNECT_FAIL":
                case "TWITCH_EFFECT_VOTING_CLIENT_CONNECT_FAIL_NO_PERMISSION":
                case "TWITCH_EFFECT_VOTING_CLIENT_CONNECT_FAIL_INCORRECT_LOGIN":
                case "TWITCH_EFFECT_VOTING_LOGIN_SUCCESS":
                    return true;
                default:
                    return false;
            }
        }

        static TwitchLoginCredentials _loginCredentials = TwitchLoginCredentials.TryReadFromFile();

        [SystemInitializer]
        static void Init()
        {
            Configs.ChatVoting.OnReconnectButtonPressed += () =>
            {
                if (Configs.ChatVoting.VotingMode.Value == Configs.ChatVoting.ChatVotingMode.Twitch && !_loginCredentials.IsValid())
                {
                    SimpleDialogBox notLoggedInDialog = SimpleDialogBox.Create();

                    notLoggedInDialog.headerToken = new SimpleDialogBox.TokenParamsPair("ROC_ATTEMPT_RECONNECT_NOT_LOGGED_IN_HEADER");
                    notLoggedInDialog.descriptionToken = new SimpleDialogBox.TokenParamsPair("ROC_ATTEMPT_RECONNECT_NOT_LOGGED_IN_DESCRIPTION_TWITCH");

                    notLoggedInDialog.AddCancelButton(CommonLanguageTokens.ok);
                }
            };
        }

        const string ROC_TWITCH_LOGIN_COMMAND = "roc_twitch_login";
        const string ROC_TWITCH_LOGIN_COMMAND_USAGE = $"{ROC_TWITCH_LOGIN_COMMAND} [username] [oauth]";

        [ConCommand(commandName = ROC_TWITCH_LOGIN_COMMAND, helpText = $"Saves Twitch connection credentials so the mod can connect to your Twitch channel. Usage: {ROC_TWITCH_LOGIN_COMMAND_USAGE}")]
        static void CCLogin(ConCommandArgs args)
        {
            if (args.Count == 0)
            {
                if (_loginCredentials.IsValid())
                {
                    Debug.Log($"Logged in as {_loginCredentials.Username}");
                }
                else
                {
                    Debug.Log($"Not currently logged in, command usage: {ROC_TWITCH_LOGIN_COMMAND_USAGE}");
                }

                return;
            }

            args.CheckArgumentCount(2);

            string username = args[0];
            string oauth = args[1];

            TwitchLoginCredentials newLoginCredentials = new TwitchLoginCredentials(username, oauth);
            if (_loginCredentials != newLoginCredentials)
            {
                _loginCredentials = newLoginCredentials;
                _loginCredentials.WriteToFile();

                onClientCredentialsChanged();

                Debug.Log($"Saved new login: {username}");
            }
            else
            {
                Debug.Log($"Already logged in as {username}");
            }

            Debug.Log("Login info is successfully saved, please also run the `clear` command if you are streaming to avoid accidentally showing the auth token");
        }

        [ConCommand(commandName = "roc_twitch_logout", helpText = "Removes the active twitch login credentials")]
        static void CCLogout(ConCommandArgs args)
        {
            if (_loginCredentials != TwitchLoginCredentials.Empty)
            {
                string oldLoginUsername = _loginCredentials.Username;

                _loginCredentials = TwitchLoginCredentials.Empty;
                _loginCredentials.WriteToFile();

                onClientCredentialsChanged();

                Debug.Log($"Logged out {oldLoginUsername}");
            }
            else
            {
                Debug.Log("Cannot log out: Not currently logged in");
            }
        }

        static TwitchClient _client;
        static void createClient()
        {
            if (_loginCredentials.IsValid())
            {
                WebSocketClient socketClient = new WebSocketClient();

                BepInExLogger<TwitchClient> clientLogger = new BepInExLogger<TwitchClient>(!Configs.ChatVoting.ExtendedClientLogging.Value);
                Configs.ChatVoting.ExtendedClientLogging.SettingChanged += (s, e) =>
                {
                    clientLogger.TreatInfoLogsAsDebug = !e.NewValue;
                };

                _client = new TwitchClient(socketClient, ClientProtocol.WebSocket, clientLogger);
                _client.Initialize(_loginCredentials.ConnectionCredentials);
                _client.RemoveChatCommandIdentifier('!');

                _client.OnConnectionError += onConnectionError;
                _client.OnError += onClientError;
                _client.OnFailureToReceiveJoinConfirmation += onFailureToReceiveJoinConfirmation;
                _client.OnIncorrectLogin += onIncorrectLogin;
                _client.OnNoPermissionError += onNoPermissionError;

                if (!_client.Connect())
                {
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                    {
                        baseToken = "TWITCH_EFFECT_VOTING_CONNECTION_ERROR",
                        paramTokens = new string[] { Language.GetString("TWITCH_EFFECT_VOTING_GENERIC_CLIENT_CONNECT_FAIL") }
                    });
                }
            }
        }

        static void onNoPermissionError(object sender, System.EventArgs e)
        {
            if (!NetworkServer.active)
                return;

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "TWITCH_EFFECT_VOTING_CONNECTION_ERROR",
                paramTokens = new string[] { Language.GetString("TWITCH_EFFECT_VOTING_CLIENT_CONNECT_FAIL_NO_PERMISSION") }
            });
        }

        static void onIncorrectLogin(object sender, OnIncorrectLoginArgs e)
        {
            if (!NetworkServer.active)
                return;

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "TWITCH_EFFECT_VOTING_CONNECTION_ERROR",
                paramTokens = new string[] { Language.GetString("TWITCH_EFFECT_VOTING_CLIENT_CONNECT_FAIL_INCORRECT_LOGIN") }
            });
        }

        static void onFailureToReceiveJoinConfirmation(object sender, OnFailureToReceiveJoinConfirmationArgs e)
        {
            if (!NetworkServer.active)
                return;

            string details;
            if (!string.IsNullOrWhiteSpace(e.Exception.Details))
            {
                details = e.Exception.Details;
            }
            else
            {
                details = Language.GetString("TWITCH_EFFECT_VOTING_GENERIC_CLIENT_CONNECT_FAIL");
            }

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "TWITCH_EFFECT_VOTING_CONNECTION_ERROR",
                paramTokens = new string[] { details }
            });
        }

        static void onClientError(object sender, OnErrorEventArgs e)
        {
            if (!NetworkServer.active)
                return;

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "TWITCH_EFFECT_VOTING_CONNECTION_ERROR",
                paramTokens = new string[] { e.Exception.GetType().Name }
            });
        }

        static void onConnectionError(object s, OnConnectionErrorArgs e)
        {
            if (!NetworkServer.active)
                return;

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "TWITCH_EFFECT_VOTING_CONNECTION_ERROR",
                paramTokens = new string[] { e.Error.Message }
            });
        }

        static void onClientCredentialsChanged()
        {
            if (_client == null)
            {
                if (_instance)
                {
                    createClient();
                }

                return;
            }

            if (_loginCredentials.IsValid())
            {
                bool wasConnected = false;
                if (_client.IsConnected)
                {
                    _client.Disconnect();
                    wasConnected = true;
                }

                _client.SetConnectionCredentials(_loginCredentials.ConnectionCredentials);

                if (wasConnected || _instance)
                {
                    _client.Connect();
                }
            }
            else
            {
                _client.Disconnect();
            }
        }

        float _scheduledAttemptJoinChannelTime;
        bool _channelJoinAttemptScheduled;

        void scheduleAttemptJoinChannel(float waitTime)
        {
            _channelJoinAttemptScheduled = true;
            _scheduledAttemptJoinChannelTime = Time.time + waitTime;
        }

        string _joinedChannel;
        bool _addedClientListeners;

        protected override void OnEnable()
        {
            base.OnEnable();

            SingletonHelper.Assign(ref _instance, this);

            if (_client == null)
            {
                createClient();
            }

            if (_client != null)
            {
                addClientListeners();
            }

            scheduleAttemptJoinChannel(1.5f);

            Configs.ChatVoting.OnReconnectButtonPressed += onReconnectButtonPressed;
        }

        void addClientListeners()
        {
            if (_addedClientListeners)
                return;

            _client.OnConnected += onConnected;
            _client.OnJoinedChannel += onJoinedChannel;
            _client.OnMessageReceived += onMessageReceived;

            _addedClientListeners = true;
        }

        protected override void Update()
        {
            base.Update();

            if (_channelJoinAttemptScheduled && Time.time >= _scheduledAttemptJoinChannelTime)
            {
                _channelJoinAttemptScheduled = false;

                if (!_loginCredentials.IsValid())
                {
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                    {
                        baseToken = "TWITCH_EFFECT_VOTING_LOGIN_FAIL_FORMAT",
                        paramTokens = new string[] { Language.GetString("TWITCH_LOGIN_FAIL_NOT_LOGGED_IN") }
                    });

                    scheduleAttemptJoinChannel(5f);
                }
                else if (_client != null)
                {
                    if (_client.IsConnected)
                    {
                        addClientListeners();

                        _client.JoinChannel(_loginCredentials.Username);
                    }
                    else
                    {
                        scheduleAttemptJoinChannel(2f);
                    }
                }
            }
        }

        void onReconnectButtonPressed()
        {
            if (_client == null)
                return;

            string channel = _joinedChannel;
            if (channel != null)
            {
                _client.LeaveChannel(channel);

                scheduleAttemptJoinChannel(1f);
            }
        }

        void onConnected(object sender, OnConnectedArgs e)
        {
            if (!_channelJoinAttemptScheduled)
            {
                scheduleAttemptJoinChannel(1f);
            }
        }

        void onJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "TWITCH_EFFECT_VOTING_LOGIN_SUCCESS",
                paramTokens = new string[] { e.Channel }
            });

            _joinedChannel = e.Channel;
        }

        void onMessageReceived(object s, OnMessageReceivedArgs e)
        {
            processVoteMessage(e.ChatMessage.UserId, e.ChatMessage.Message);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            SingletonHelper.Unassign(ref _instance, this);

            if (_client != null)
            {
                _client.OnConnected -= onConnected;
                _client.OnJoinedChannel -= onJoinedChannel;
                _client.OnMessageReceived -= onMessageReceived;
            }

            _addedClientListeners = false;

            if (_joinedChannel != null)
            {
                _client.LeaveChannel(_joinedChannel);
                _joinedChannel = null;
            }

            Configs.ChatVoting.OnReconnectButtonPressed -= onReconnectButtonPressed;
        }
    }
}

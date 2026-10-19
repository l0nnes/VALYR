using UnityEngine;
using UnityEngine.InputSystem;
using PurrNet;
using PurrNet.Transports;
using PurrNet.Steam;
using PurrNet.Lobby;
#if STEAMWORKS
using Steamworks;
using PurrNet.Lobby.Steam;
#endif

namespace VALYR.Multiplayer
{
    /// <summary>
    /// In-game HUD providing Steam session overview, invite triggers,
    /// and clean return-to-menu functionality inside SampleScene.
    /// Toggleable with TAB or ESC key.
    /// </summary>
    public class MultiplayerLobbyHUD : MonoBehaviour
    {
        [Header("UI Key Binding")]
        public Key ToggleKey = Key.Tab;
        public Key AltToggleKey = Key.Escape;

        [Header("Direct Connect Fallback (Steam ID)")]
        [Tooltip("SteamID64 of the host to connect directly if not using Steam Lobby.")]
        public string TargetSteamId = "";

        [Header("LAN / Direct Connect Configuration")]
        public string TargetIpAddress = "127.0.0.1";
        public ushort TargetPort = 5003;

        [Header("Window Layout")]
        public Rect WindowRect = new Rect(24, 24, 380, 370);

        private bool _isOpen = false;
        private int _transportTab = 0; // 0 = LAN UDP, 1 = Steam
        private GUIStyle _windowStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _accentButtonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _miniButtonStyle;

        private void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[ToggleKey].wasPressedThisFrame || Keyboard.current[AltToggleKey].wasPressedThisFrame)
                {
                    _isOpen = !_isOpen;
                }
            }
        }

        private void OnGUI()
        {
            InitStyles();

            if (!_isOpen)
            {
                // Collapsed indicator button in upper left corner
                string collapsedLabel = "Multiplayer (TAB)";
#if STEAMWORKS
                if (SteamRuntime.isInitialized)
                {
                    collapsedLabel = $"Steam: {SteamFriends.GetPersonaName()} (TAB)";
                }
#endif
                if (GUI.Button(new Rect(24, 24, 220, 30), collapsedLabel, _miniButtonStyle))
                {
                    _isOpen = true;
                }
                return;
            }

            WindowRect = GUI.Window(99201, WindowRect, DrawWindowContents, "VALYR Multiplayer Lobby", _windowStyle);
        }

        private void InitStyles()
        {
            if (_windowStyle != null) return;

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fixedHeight = 30,
                margin = new RectOffset(4, 4, 3, 3)
            };

            _accentButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 32,
                margin = new RectOffset(4, 4, 3, 3)
            };

            _miniButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawWindowContents(int windowId)
        {
            var net = NetworkManager.main;
            var lobby = MultiplayerLobbyManager.Instance;

#if STEAMWORKS
            if (SteamRuntime.isInitialized)
            {
                GUILayout.Label($"Steam User: {SteamFriends.GetPersonaName()}", _statusStyle);
            }
            else
            {
                GUILayout.Label("Steam: Not Initialized (Offline)", _labelStyle);
            }
#endif

            // Network Session Status
            string statusText = "Offline";
            Color statusColor = Color.gray;

            if (net != null)
            {
                if (net.isHost)
                {
                    int playerCount = lobby != null ? lobby.PlayerVehicles.Count : 1;
                    statusText = $"Hosting (Players: {playerCount})";
                    statusColor = Color.green;
                }
                else if (net.isClient)
                {
                    statusText = "Connected as Client";
                    statusColor = Color.cyan;
                }
            }

            var prevColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label($"Status: {statusText}", _statusStyle);
            GUI.color = prevColor;

            // Active Lobby Info
            var activeLobby = GameOrchestrator.active?.activeLobby;
            if (activeLobby != null)
            {
                GUILayout.Label($"Steam Lobby ID: {activeLobby.id}", _labelStyle);
            }

            GUILayout.Space(8);

            // In-Match Controls
            if (net != null && !net.isOffline)
            {
                if (GUILayout.Button("Disconnect / Stop Session", _accentButtonStyle))
                {
                    lobby?.Disconnect();
                }

                GUILayout.Space(4);

                if (GUILayout.Button("Invite Friends (Steam Overlay)", _buttonStyle))
                {
                    SteamMultiplayerHelper.OpenSteamInviteOverlay();
                }

                GUILayout.Space(6);

                if (GUILayout.Button("Leave to Main Menu", _buttonStyle))
                {
                    SteamMultiplayerHelper.LeaveCurrentGameToMenu();
                }
            }
            else
            {
                _transportTab = GUILayout.Toolbar(_transportTab, new[] { "LAN / UDP", "Steam P2P" });
                GUILayout.Space(6);

                if (_transportTab == 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("IP:", _labelStyle, GUILayout.Width(30));
                    TargetIpAddress = GUILayout.TextField(TargetIpAddress);
                    GUILayout.Label("Port:", _labelStyle, GUILayout.Width(35));
                    string pStr = GUILayout.TextField(TargetPort.ToString(), GUILayout.Width(55));
                    if (ushort.TryParse(pStr, out var parsedPort)) TargetPort = parsedPort;
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4);

                    if (GUILayout.Button("Host Server (LAN / UDP)", _accentButtonStyle))
                    {
                        lobby?.StartHostUDP(TargetPort);
                    }

                    if (GUILayout.Button("Connect as Client (UDP)", _buttonStyle))
                    {
                        lobby?.StartClientUDP(TargetIpAddress, TargetPort);
                    }
                }
                else
                {
                    bool steamReady = false;
#if STEAMWORKS
                    steamReady = SteamRuntime.isInitialized;
#endif
                    if (!steamReady)
                    {
                        GUI.color = new Color(1f, 0.75f, 0.4f);
                        GUILayout.Label("Steam is offline! Use LAN / UDP tab for local testing.", _labelStyle);
                        GUI.color = prevColor;
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Host ID:", _labelStyle, GUILayout.Width(55));
                    TargetSteamId = GUILayout.TextField(TargetSteamId);
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4);

                    GUI.enabled = steamReady;
                    if (GUILayout.Button("Host Game (Steam P2P)", _buttonStyle))
                    {
                        lobby?.StartHostSteam();
                    }

                    if (GUILayout.Button("Join by SteamID", _buttonStyle))
                    {
                        if (!string.IsNullOrEmpty(TargetSteamId))
                        {
                            lobby?.StartClientSteam(TargetSteamId);
                        }
                    }
                    GUI.enabled = true;
                }

                GUILayout.Space(6);

                if (GUILayout.Button("Spawn / Reset Test Vehicle (Offline)", _miniButtonStyle))
                {
                    lobby?.SpawnOfflineVehicle();
                }

                GUILayout.Space(4);
                if (GUILayout.Button("Open Main Menu Scene", _buttonStyle))
                {
                    SteamMultiplayerHelper.LeaveCurrentGameToMenu();
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("Press TAB to close this panel", _labelStyle);

            GUI.DragWindow();
        }
    }
}

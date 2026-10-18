using UnityEngine;
using UnityEngine.InputSystem;
using PurrNet;
using PurrNet.Transports;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// In-game HUD providing connection controls (Host, Join, Disconnect)
    /// and network status overview directly in SampleScene.
    /// Toggleable with the TAB key.
    /// </summary>
    public class MultiplayerLobbyHUD : MonoBehaviour
    {
        [Header("UI Key Binding")]
        public Key ToggleKey = Key.Tab;

        [Header("Default Connection")]
        public string ServerAddress = "127.0.0.1";
        public string ServerPort = "6112";

        [Header("Window Layout")]
        public Rect WindowRect = new Rect(20, 20, 320, 260);

        private bool _isOpen;
        private GUIStyle _windowStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _miniButtonStyle;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[ToggleKey].wasPressedThisFrame)
            {
                _isOpen = !_isOpen;
            }
        }

        private void OnGUI()
        {
            InitStyles();

            if (!_isOpen)
            {
                // Collapsed watermark button
                if (GUI.Button(new Rect(20, 20, 160, 28), "Multiplayer (TAB)", _miniButtonStyle))
                {
                    _isOpen = true;
                }
                return;
            }

            WindowRect = GUI.Window(99201, WindowRect, DrawWindowContents, "VALYR Multiplayer (PurrNet)", _windowStyle);
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
                fixedHeight = 28,
                margin = new RectOffset(4, 4, 3, 3)
            };

            _miniButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
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

            if (net == null)
            {
                GUILayout.Label("NetworkManager not found in scene!", _labelStyle);
                GUI.DragWindow();
                return;
            }

            // Connection State Display
            string statusText = "Offline (Singleplayer)";
            Color statusColor = Color.gray;

            if (net.isHost)
            {
                statusText = $"Hosting (Players: {(lobby != null ? lobby.PlayerVehicles.Count : 1)})";
                statusColor = Color.green;
            }
            else if (net.isClient)
            {
                statusText = "Connected as Client";
                statusColor = Color.cyan;
            }
            else if (net.isServerOnly)
            {
                statusText = "Dedicated Server Running";
                statusColor = Color.yellow;
            }

            var prevColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label($"Status: {statusText}", _statusStyle);
            GUI.color = prevColor;

            GUILayout.Space(6);

            if (net.isOffline)
            {
                // Offline Lobby Controls
                GUILayout.BeginHorizontal();
                GUILayout.Label("IP:", _labelStyle, GUILayout.Width(35));
                ServerAddress = GUILayout.TextField(ServerAddress);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Port:", _labelStyle, GUILayout.Width(35));
                ServerPort = GUILayout.TextField(ServerPort);
                GUILayout.EndHorizontal();

                GUILayout.Space(8);

                ushort.TryParse(ServerPort, out ushort port);
                if (port == 0) port = 6112;

                if (GUILayout.Button("Host Game (Server + Driver)", _buttonStyle))
                {
                    if (lobby != null)
                    {
                        lobby.DefaultAddress = ServerAddress;
                        lobby.DefaultPort = port;
                        lobby.StartHost();
                    }
                    else
                    {
                        net.StartHost();
                    }
                }

                if (GUILayout.Button("Join Game (Client)", _buttonStyle))
                {
                    if (lobby != null)
                    {
                        lobby.StartClient(ServerAddress, port);
                    }
                    else
                    {
                        var udp = net.GetComponent<UDPTransport>();
                        if (udp != null)
                        {
                            udp.address = ServerAddress;
                            udp.serverPort = port;
                        }
                        net.StartClient();
                    }
                }

                if (GUILayout.Button("Start Server Only (Dedicated)", _buttonStyle))
                {
                    if (lobby != null)
                    {
                        lobby.DefaultPort = port;
                        lobby.StartServer();
                    }
                    else
                    {
                        net.StartServer();
                    }
                }
            }
            else
            {
                // Active Session Controls
                if (GUILayout.Button("Disconnect / Stop", _buttonStyle))
                {
                    if (lobby != null)
                    {
                        lobby.Disconnect();
                    }
                    else
                    {
                        if (net.isServer) net.StopServer();
                        if (net.isClient) net.StopClient();
                    }
                }

                GUILayout.Space(4);
                GUILayout.Label("Press TAB to hide / show menu", _labelStyle);
            }

            GUI.DragWindow();
        }
    }
}

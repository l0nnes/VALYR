using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if STEAMWORKS
using Steamworks;
using PurrNet.Lobby.Steam;
#endif
using PurrNet;
using PurrNet.Lobby;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Persistent helper for Steam Overlay invitations, command-line invite handling,
    /// and Steam matchmaking integration for VALYR.
    /// Runs on its own dedicated DontDestroyOnLoad GameObject.
    /// </summary>
    public class SteamMultiplayerHelper : MonoBehaviour
    {
        public static SteamMultiplayerHelper Instance { get; private set; }

        public const string MENU_SCENE_NAME = "MenuScene";

        [Header("Settings")]
        [Tooltip("Automatically check for Steam command line (+connect_lobby) at startup.")]
        public bool CheckCommandLineOnStartup = true;

#if STEAMWORKS
        private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequestedCallback;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[SteamMultiplayerHelper]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<SteamMultiplayerHelper>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

#if STEAMWORKS
            if (SteamRuntime.EnsureInitialized())
            {
                _lobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
                Debug.Log($"[SteamMultiplayerHelper] Steam initialized successfully. Local user: {SteamFriends.GetPersonaName()} (ID: {SteamUser.GetSteamID()})");
            }
            else
            {
                Debug.LogWarning("[SteamMultiplayerHelper] Steam is not running or failed to initialize.");
            }
#endif

            if (CheckCommandLineOnStartup)
            {
                CheckCommandLineForLobby();
            }
        }

        private void OnDestroy()
        {
#if STEAMWORKS
            _lobbyJoinRequestedCallback?.Dispose();
            _lobbyJoinRequestedCallback = null;
#endif
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Checks if the game was launched with "+connect_lobby <SteamID>" from an invite.
        /// </summary>
        public void CheckCommandLineForLobby()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i].Equals("+connect_lobby", StringComparison.OrdinalIgnoreCase))
                    {
                        string lobbyIdStr = args[i + 1];
                        Debug.Log($"[SteamMultiplayerHelper] Detected +connect_lobby argument: {lobbyIdStr}");
                        JoinLobbyById(lobbyIdStr);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamMultiplayerHelper] Error parsing command line args: {ex.Message}");
            }
        }

#if STEAMWORKS
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            string lobbyIdStr = callback.m_steamIDLobby.m_SteamID.ToString();
            Debug.Log($"[SteamMultiplayerHelper] Received overlay invite accept for Steam Lobby ID: {lobbyIdStr}");
            JoinLobbyById(lobbyIdStr);
        }
#endif

        /// <summary>
        /// Joins a Steam lobby by its 64-bit ID string.
        /// If currently in a match scene, disconnects and loads MenuScene first.
        /// </summary>
        public void JoinLobbyById(string lobbyIdStr)
        {
            if (string.IsNullOrEmpty(lobbyIdStr)) return;

            var currentScene = SceneManager.GetActiveScene();
            if (currentScene.name != MENU_SCENE_NAME)
            {
                LeaveCurrentGameToMenu(() =>
                {
                    TryJoinInMenu(lobbyIdStr);
                });
                return;
            }

            TryJoinInMenu(lobbyIdStr);
        }

        private void TryJoinInMenu(string lobbyIdStr)
        {
            var lobbyManager = FindAnyObjectByType<LobbyManager>();
            if (lobbyManager != null)
            {
                var orch = GameOrchestrator.active;
                if (orch != null && orch.lobbyProvider != null)
                {
                    orch.lobbyProvider.JoinLobby(lobbyIdStr);
                }
            }
        }

        /// <summary>
        /// Opens the Steam In-Game Overlay directly to the Invite Friends dialog.
        /// </summary>
        public static void OpenSteamInviteOverlay()
        {
#if STEAMWORKS
            if (!SteamRuntime.isInitialized)
            {
                Debug.LogWarning("[SteamMultiplayerHelper] Steam is not initialized; cannot open overlay.");
                return;
            }

            var activeLobby = GameOrchestrator.active?.activeLobby;
            if (activeLobby != null && ulong.TryParse(activeLobby.id, out ulong lobbyId) && lobbyId > 0)
            {
                SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobbyId));
            }
            else
            {
                SteamFriends.ActivateGameOverlay("Friends");
            }
#else
            Debug.LogWarning("[SteamMultiplayerHelper] Steamworks.NET is not compiled in.");
#endif
        }

        /// <summary>
        /// Leaves the current game session, stops network host/client, and loads MenuScene.
        /// </summary>
        public static void LeaveCurrentGameToMenu(Action onMenuLoaded = null)
        {
            var net = NetworkManager.main;
            if (net != null)
            {
                if (net.isServer) net.StopServer();
                if (net.isClient) net.StopClient();
            }

            if (GameSession.instance != null)
            {
                GameSession.instance.LeaveToMenu();
            }
            else
            {
                var asyncOp = SceneManager.LoadSceneAsync(MENU_SCENE_NAME);
                if (asyncOp != null && onMenuLoaded != null)
                {
                    asyncOp.completed += _ => onMenuLoaded.Invoke();
                }
            }
        }
    }
}

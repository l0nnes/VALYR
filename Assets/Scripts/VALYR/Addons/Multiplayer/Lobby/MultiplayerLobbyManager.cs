using System;
using System.Collections.Generic;
using UnityEngine;
using PurrNet;
using PurrNet.Transports;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Multiplayer Lobby and vehicle spawner manager.
    /// Controls PurrNet NetworkManager lifecycle (Host, Client, Server, Disconnect)
    /// and instantiates client-owned vehicles for joining players at designated spawn points.
    /// </summary>
    public class MultiplayerLobbyManager : MonoBehaviour
    {
        public static MultiplayerLobbyManager Instance { get; private set; }

        [Header("Vehicle Prefab & Scene Vehicle")]
        [Tooltip("Prefab of the vehicle to instantiate for joining players.")]
        public NetworkVehicle VehiclePrefab;

        [Tooltip("Optional existing scene vehicle to assign to the host player.")]
        public NetworkVehicle SceneVehicle;

        [Header("Spawn Points")]
        [Tooltip("Transforms in scene where new vehicles will be spawned.")]
        public Transform[] SpawnPoints;

        [Header("Default Network Configuration")]
        public string DefaultAddress = "127.0.0.1";
        public ushort DefaultPort = 6112;

        private readonly Dictionary<PlayerID, NetworkVehicle> _playerVehicles = new();
        private int _spawnCounter = 0;

        public IReadOnlyDictionary<PlayerID, NetworkVehicle> PlayerVehicles => _playerVehicles;

        private void Awake()
        {
            VehiclePredictedRegistration.Register();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (SceneVehicle == null)
            {
                var allVehicles = FindObjectsByType<NetworkVehicle>(FindObjectsInactive.Include);
                for (int i = 0; i < allVehicles.Length; i++)
                {
                    if (allVehicles[i].gameObject.scene == gameObject.scene)
                    {
                        SceneVehicle = allVehicles[i];
                        break;
                    }
                }
            }

            if (SpawnPoints == null || SpawnPoints.Length == 0)
            {
                AutoFindSpawnPoints();
            }
        }

        private void Start()
        {
            if (SceneVehicle != null && !SceneVehicle.gameObject.activeSelf)
            {
                SceneVehicle.gameObject.SetActive(true);
                SceneVehicle.UpdateAuthorityState();
            }
        }

        private void OnEnable()
        {
            SubscribeToNetworkEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromNetworkEvents();
        }

        private void AutoFindSpawnPoints()
        {
            var points = FindObjectsByType<MultiplayerSpawnPoint>();
            if (points != null && points.Length > 0)
            {
                SpawnPoints = new Transform[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    SpawnPoints[i] = points[i].transform;
                }
            }
        }

        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.main != null)
            {
                NetworkManager.main.onPlayerJoined += HandlePlayerJoined;
                NetworkManager.main.onPlayerLeft += HandlePlayerLeft;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.main != null)
            {
                NetworkManager.main.onPlayerJoined -= HandlePlayerJoined;
                NetworkManager.main.onPlayerLeft -= HandlePlayerLeft;
            }
        }

        /// <summary>
        /// Starts multiplayer as Host (Listen Server + Local Client).
        /// </summary>
        public void StartHost()
        {
            var net = NetworkManager.main;
            if (net == null)
            {
                Debug.LogError("[MultiplayerLobbyManager] No NetworkManager found in scene!");
                return;
            }

            ConfigureTransport(DefaultAddress, DefaultPort);
            net.StartHost();
        }

        /// <summary>
        /// Connects to a remote server as a Client.
        /// </summary>
        public void StartClient(string address, ushort port)
        {
            var net = NetworkManager.main;
            if (net == null)
            {
                Debug.LogError("[MultiplayerLobbyManager] No NetworkManager found in scene!");
                return;
            }

            ConfigureTransport(address, port);
            net.StartClient();
        }

        /// <summary>
        /// Starts dedicated server without a local player.
        /// </summary>
        public void StartServer()
        {
            var net = NetworkManager.main;
            if (net == null)
            {
                Debug.LogError("[MultiplayerLobbyManager] No NetworkManager found in scene!");
                return;
            }

            ConfigureTransport(DefaultAddress, DefaultPort);
            net.StartServer();
        }

        /// <summary>
        /// Disconnects and shuts down current network session.
        /// </summary>
        public void Disconnect()
        {
            var net = NetworkManager.main;
            if (net == null) return;

            if (net.isServer)
                net.StopServer();

            if (net.isClient)
                net.StopClient();

            _playerVehicles.Clear();

            if (SceneVehicle != null)
            {
                SceneVehicle.gameObject.SetActive(true);
                SceneVehicle.UpdateAuthorityState();
            }
        }

        private void ConfigureTransport(string address, ushort port)
        {
            var net = NetworkManager.main;
            if (net == null) return;

            var udp = net.GetComponent<UDPTransport>();
            if (udp != null)
            {
                udp.address = string.IsNullOrEmpty(address) ? DefaultAddress : address;
                udp.serverPort = port > 0 ? port : DefaultPort;
            }
        }

        private void HandlePlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            if (!asServer)
                return;

            var net = NetworkManager.main;
            if (net == null) return;

            // If this is the local host player and we have a valid SceneVehicle, assign ownership to host
            if (net.isHost && player == net.localPlayer && SceneVehicle != null)
            {
                if (!SceneVehicle.gameObject.activeSelf)
                    SceneVehicle.gameObject.SetActive(true);

                SceneVehicle.AssignedPlayerId = player;
                SceneVehicle.GiveOwnership(player);
                if (SceneVehicle.TryGetComponent<PredictedVehicle>(out var predVeh))
                {
                    predVeh.owner = player;
                }
                _playerVehicles[player] = SceneVehicle;
                SceneVehicle.RebindCinemachineCameras();
                return;
            }

            // Otherwise, spawn a new vehicle prefab for the joining player
            if (VehiclePrefab == null)
            {
                if (SceneVehicle != null && !_playerVehicles.ContainsKey(player))
                {
                    SceneVehicle.AssignedPlayerId = player;
                    SceneVehicle.GiveOwnership(player);
                    if (SceneVehicle.TryGetComponent<PredictedVehicle>(out var predVeh))
                    {
                        predVeh.owner = player;
                    }
                    _playerVehicles[player] = SceneVehicle;
                }
                return;
            }

            Transform spawn = GetNextSpawnPoint();
            Vector3 pos = spawn != null ? spawn.position : new Vector3((_spawnCounter * 4.5f), 0.6f, 0f);
            Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

            var predMgr = FindAnyObjectByType<PurrNet.Prediction.PredictionManager>();
            if (predMgr != null && predMgr.hierarchy != null)
            {
                if (predMgr.hierarchy.TryCreateAndGet<NetworkVehicle>(VehiclePrefab.gameObject, pos, rot, out var predictedCar, player))
                {
                    predictedCar.name = $"NetworkTruck_Player_{player.id}";
                    predictedCar.AssignedPlayerId = player;
                    _playerVehicles[player] = predictedCar;

                    if (net.isHost && _playerVehicles.TryGetValue(net.localPlayer, out var hostCar) && hostCar != null)
                    {
                        hostCar.RebindCinemachineCameras();
                    }
                    return;
                }
            }

            // Fallback to standard PurrNet Instantiate & Spawn
            NetworkVehicle newCar = Instantiate(VehiclePrefab, pos, rot);
            newCar.name = $"NetworkTruck_Player_{player.id}";
            newCar.AssignedPlayerId = player;

            if (!newCar.isSpawned)
            {
                net.Spawn(newCar.gameObject);
            }

            newCar.GiveOwnership(player);
            _playerVehicles[player] = newCar;

            // If this is host, guarantee that Host camera rig remains focused on the Host vehicle
            if (net.isHost && _playerVehicles.TryGetValue(net.localPlayer, out var currentHostCar) && currentHostCar != null)
            {
                currentHostCar.RebindCinemachineCameras();
            }
        }

        private void HandlePlayerLeft(PlayerID player, bool asServer)
        {
            if (!asServer)
                return;

            if (_playerVehicles.TryGetValue(player, out var vehicle))
            {
                if (vehicle != null && vehicle != SceneVehicle)
                {
                    var predMgr = FindAnyObjectByType<PurrNet.Prediction.PredictionManager>();
                    if (predMgr != null && predMgr.hierarchy != null && predMgr.hierarchy.TryGetId(vehicle.gameObject, out var pid))
                    {
                        predMgr.hierarchy.Delete(pid);
                    }
                    else
                    {
                        Destroy(vehicle.gameObject);
                    }
                }
                _playerVehicles.Remove(player);
            }
        }

        public Transform GetNextSpawnPoint()
        {
            if (SpawnPoints == null || SpawnPoints.Length == 0)
                return null;

            int idx = _spawnCounter++ % SpawnPoints.Length;
            return SpawnPoints[idx];
        }
    }
}

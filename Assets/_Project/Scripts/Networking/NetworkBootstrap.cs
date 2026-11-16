using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace AnimeFpsGame.Networking
{
    /// <summary>Host/join entry points over Unity Transport (LAN / IP).</summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;
        [SerializeField] string defaultAddress = "127.0.0.1";
        [SerializeField] ushort port = 7777;
        [SerializeField] string listenAddress = "0.0.0.0";
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] bool startSinglePlayer;

        public string Address { get; private set; }
        public string Status { get; private set; } = "Not connected";
        public bool IsConnected => networkManager != null && (networkManager.IsHost || networkManager.IsClient);

        int nextSpawnIndex;

        void Awake()
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            Address = defaultAddress;
        }

        void Start()
        {
            // NetworkManager.Singleton is assigned in NetworkManager.OnEnable, which can run after
            // this component's Awake, so resolve and subscribe here.
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback += OnConnected;
                networkManager.OnClientDisconnectCallback += OnDisconnected;

                // Setter throws if a callback is already registered; this is the only one.
                if (networkManager.ConnectionApprovalCallback == null)
                {
                    networkManager.ConnectionApprovalCallback = ApproveConnection;
                }
            }

            var sessionStarted = ApplyCommandLine();
            if (startSinglePlayer && !sessionStarted && !IsConnected)
            {
                Host();
            }
        }

        /// <summary>Dev convenience: `-host` or `-join [address]` starts a session without the menu.</summary>
        bool ApplyCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host")
                {
                    Host();
                    return true;
                }

                if (args[i] == "-join")
                {
                    var address = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : defaultAddress;
                    Join(address);
                    return true;
                }
            }

            return false;
        }

        void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= OnConnected;
            networkManager.OnClientDisconnectCallback -= OnDisconnected;

            if (networkManager.ConnectionApprovalCallback == ApproveConnection)
            {
                networkManager.ConnectionApprovalCallback = null;
            }
        }

        /// <summary>Approves every client and gives each one its own spawn marker.</summary>
        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = true;

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            var point = spawnPoints[nextSpawnIndex++ % spawnPoints.Length];
            if (point != null)
            {
                response.Position = point.position;
                response.Rotation = point.rotation;
            }
        }

        public void Host()
        {
            if (!Prepare(Address))
            {
                return;
            }

            Status = "Starting host...";
            if (!networkManager.StartHost())
            {
                Status = "Host failed";
                Debug.LogWarning("[Net] " + Status);
            }
        }

        public void Join(string address)
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                Address = address.Trim();
            }

            if (!Prepare(Address))
            {
                return;
            }

            Status = "Connecting...";
            if (!networkManager.StartClient())
            {
                Status = "Join failed";
            }
        }

        bool Prepare(string address)
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (networkManager == null)
            {
                Status = "No NetworkManager in scene";
                return false;
            }

            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Status = "No UnityTransport on NetworkManager";
                return false;
            }

            transport.SetConnectionData(address, port, listenAddress);
            return true;
        }

        void OnConnected(ulong clientId)
        {
            Status = $"Connected (client {clientId})";
            Debug.Log($"[Net] connected client {clientId}");
        }

        void OnDisconnected(ulong clientId)
        {
            Status = "Disconnected";
            Debug.Log($"[Net] disconnected client {clientId}");
        }
    }
}

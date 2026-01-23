using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;

    [SerializeField] GameObject teamManagerPrefab;

    Lobby activeLobby;
    bool isHost;
    string thisPlayerId;
    string playerName;

    void Awake()
    {
        if (Instance == null) 
        {
            Instance = this; 
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    async void Start()
    {
        await UnityServices.InitializeAsync();

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        thisPlayerId = AuthenticationService.Instance.PlayerId;
        playerName = "Player_"+thisPlayerId.Substring(0,5);
    }

 
    public void StartGame()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.SceneManager.LoadScene("Game Scene",LoadSceneMode.Single );
    }

    [Rpc(SendTo.Everyone)]
    async void StartGameForEveryoneRpc()
    {
        await SceneManager.LoadSceneAsync(1);
    }

    public async Task<bool> CreateLobby(int maxPlayers, string lobbyName)
    {
        try
        {
            //setup relay
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { "PlayerName", new DataObject(DataObject.VisibilityOptions.Member, playerName) }
                }
            };
            //start lobby
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers,options);
            Debug.Log("Lobby Created Code " + lobby.LobbyCode);
            activeLobby = lobby;
            isHost = true;

            SetupTransport(allocation);

            NetworkManager.Singleton.StartHost();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            GameObject go = Instantiate(teamManagerPrefab);
            go.GetComponent<NetworkObject>().Spawn();

            TeamManager.Instance.SetTeam(NetworkManager.Singleton.LocalClientId);

            InvokeRepeating(nameof(LobbyHeartbeat), 15f, 15f);
            return true;

        }
        catch (LobbyServiceException ex)
        {
            Debug.LogError(ex);
            return false;
        }
    }

    public async Task<QueryResponse> SearchLobby()
    {
        try
        {
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();
            //Debug.Log(response.Results[0].LobbyCode + " " + response.Results[0].Id);     //lobby code is private share only code not available to clients only for host
            return response;
        }
        catch (LobbyServiceException ex) { 
            Debug.LogError(ex);
            return null;
        }
        
    }

    public async Task<bool> JoinLobby(string joinCode)
    {
        try
        {
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(joinCode);
            activeLobby = joinedLobby;
            if (joinedLobby.Data.ContainsKey("RelayCode"))
            {
                string relayJoinCode = joinedLobby.Data["RelayCode"].Value;
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

                SetupTransport(joinAllocation);
                NetworkManager.Singleton.StartClient();
                return true;
            }
            isHost = false;
            return true;
        }
        catch (LobbyServiceException ex) { 
            Debug.LogError(ex);
            return false;
        }
    }

    public async Task<bool> LeaveLobby()
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(activeLobby.Id, thisPlayerId);
            if (isHost) CancelInvoke(nameof(LobbyHeartbeat));
            activeLobby = null;
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Lobby Left");
            return true;
        }
        catch (LobbyServiceException ex) { 
            Debug.LogError(ex);
            return false;
        }
    }

    void OnClientConnected(ulong cId)
    {
        TeamManager.Instance.SetTeam(cId);
    }

    private void SetupTransport(Allocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );
    }

    private void SetupTransport(JoinAllocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData
        );
    }

    async void LobbyHeartbeat()
    {
        if (activeLobby != null && isHost)
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(activeLobby.Id);
            }
            catch (LobbyServiceException ex)
            {
                Debug.LogError(ex);
                await LobbyService.Instance.SendHeartbeatPingAsync(activeLobby.Id);
            }
        }
    }
}

using System;
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
    [SerializeField] GameObject popupGO;

    GameObject loadingScreen;
    Transform[] teamALineups;
    Transform[] teamBLineups;

    Lobby activeLobby;
    bool isHost;
    string thisPlayerId;
    string playerName;
    string relayJoinCode;
    
    public Action PlayerSignedIn;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else 
        {
            Destroy(gameObject); 
        }
    }

    async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception ex) {
            Debug.LogError(ex);
            PopupSetter("Error!", "Could not sign in to the servers!");
        }

        thisPlayerId = AuthenticationService.Instance.PlayerId;
        if (!PlayerPrefs.HasKey("PlayerName")) playerName = "Playa" + thisPlayerId.Substring(0, 5);
        else playerName = PlayerPrefs.GetString("PlayerName");
        PlayerSignedIn?.Invoke();       //subscribed by UI script to get logged in name
    }
 
    //called by host to start the game after lobby is filled
    public async Task StartGame()
    {
        if(TeamManager.Instance.teamA_Ids.Count + TeamManager.Instance.teamB_Ids.Count <= 1)
        {
            Debug.Log("EMPTY TEAM");
            PopupSetter("Insufficient Players!", "Cannot start a game with less than 2 players.");
            return;
        }
        if (TeamManager.Instance.CheckTeamImbalance())
        {
            Debug.Log("TEAM IMBALANCE");
            //rpc method is called for UI popup
            return;
        }
        CancelInvoke(nameof(LobbyHeartbeat));
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        TeamManager.Instance.OnNetworkDespawn();
        await Task.Yield();     //to wait one frame for despawn methond to remove all subs
        if(UnityEngine.Random.value < 0.5f) TeamManager.Instance.SwapTeams();
        await Task.Yield();
        NetworkManager.Singleton.SceneManager.LoadScene("Game Scene",LoadSceneMode.Single);
    }
  

    public async Task<bool> CreateLobby(int maxPlayers)
    {
        try
        {
            //setup relay
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                },
            };
            //start lobby
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(playerName, maxPlayers,options);
            Debug.Log("Lobby Created Code " + lobby.LobbyCode);
            activeLobby = lobby;
            isHost = true;

            SetupTransport(allocation);
            NetworkManager.Singleton.StartHost();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            GameObject go = Instantiate(teamManagerPrefab);
            go.GetComponent<NetworkObject>().Spawn();       //spawn teammanger so it also spawns on clients

            //set host stuff into the data
            TeamManager.Instance.SetTeam(NetworkManager.Singleton.LocalClientId);

            TeamManager.Instance.AddPlayerName(NetworkManager.Singleton.LocalClientId, playerName + "," + PlayerPrefs.GetString("CustomCharacter").ToString());     

            InvokeRepeating(nameof(LobbyHeartbeat), 15f, 15f);
            return true;

        }
        catch (LobbyServiceException ex)
        {
            Debug.LogError(ex);
            PopupSetter("ERROR", ex.Message);
            return false;
        }
    }

    public async Task<QueryResponse> SearchLobby()
    {
        try
        {
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();
            return response;
        }
        catch (LobbyServiceException ex) { 
            Debug.LogError(ex);
            PopupSetter("ERROR", ex.Message);
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
                relayJoinCode = joinedLobby.Data["RelayCode"].Value;
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
            PopupSetter("ERROR", ex.Message);
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
            PopupSetter("ERROR", ex.Message);
            return false;
        }
    }

    void OnClientConnected(ulong cId)
    {
        TeamManager.Instance.SetTeam(cId);
    }

    void OnClientDisconnected(ulong cId)
    {
        if (cId == NetworkManager.Singleton.LocalClientId) return;      //host cant remove himself as his network is already gone
        TeamManager.Instance.RemovePlayerNameRpc(cId);
    }

    void SetupTransport(Allocation allocation)
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

    void SetupTransport(JoinAllocation allocation)
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

    public string PlayerNameGetter() {
        return playerName;
    }

    public void PlayerNameSetter(string name) { 
        playerName = name;
        PlayerPrefs.SetString("PlayerName", name);
    }


    public void LineupPositionsSetter(Transform[] a, Transform[] b)
    {
        teamALineups = a;
        teamBLineups = b;
    }

    public void LoadingScreenSetter(GameObject go)
    {
        loadingScreen = go;
    }

    public void LoadingScreenStatus(bool status)
    {
        loadingScreen.SetActive(status);
    }

    public (Transform[], Transform[]) LineupPositionsGetter()
    {
        return (teamALineups, teamBLineups);
    }

    void PopupSetter(string headerT, string bodyT)
    {
        GameObject go = Instantiate(popupGO);
        go.GetComponent<PopupScript>().PopupActivation(headerT, bodyT);
    }
}

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
using Unity.Networking.Transport.Relay;
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
    [HideInInspector]
    public bool signInComplete;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            PlayerPrefs.SetInt("HostLeft", 0);      //dont show popup on new game start
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            NetworkManager[] managers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            if (managers.Length > 1)
            {
                foreach (NetworkManager manager in managers)
                {
                    if (NetworkManager.Singleton != manager) Destroy(manager.gameObject);
                }
            }
            if (PlayerPrefs.GetInt("HostLeft", 0) == 1)
            {
                Instance.PopupSetter("Game Ended Abruptly!", "Your previous match ended because the host left the lobby!");
                PlayerPrefs.SetInt("HostLeft", 0);
            }
            Destroy(gameObject);
            return;
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
            PopupSetter("Error!", "Could not sign in to the servers!\nPlease restart the game.");
            return;
        }
        signInComplete = true;
        thisPlayerId = AuthenticationService.Instance.PlayerId;
        if (!PlayerPrefs.HasKey("PlayerName")) playerName = "Playa" + thisPlayerId.Substring(0, 5);
        else playerName = PlayerPrefs.GetString("PlayerName");
        PlayerSignedIn?.Invoke();       //subscribed by UI script to get logged in name
    }

    //called by host to start the game after lobby is filled
    public async Task StartGame()
    {
        if (TeamManager.Instance.teamA_Ids.Count + TeamManager.Instance.teamB_Ids.Count <= 1)
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
        await LobbyService.Instance.DeleteLobbyAsync(activeLobby.Id);
        activeLobby = null;
        TeamManager.Instance.RemoveLobbySceneRefs();
        await Task.Yield();     //to wait one frame for despawn methond to remove all subs
        if (UnityEngine.Random.value < 0.5f) TeamManager.Instance.SwapTeams();
        await Task.Yield();
        NetworkManager.Singleton.SceneManager.LoadScene("Game Scene", LoadSceneMode.Single);
    }

    public async Task EndGameCreateLobby()
    {
        //just start lobby as relay is still active
        try
        {
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                },
            };
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(playerName, 10, options);     //hardcoding 10 because i aint adding variable teamsize
            Debug.Log("Lobby Created Code " + lobby.LobbyCode);
            activeLobby = lobby;
            InvokeRepeating(nameof(LobbyHeartbeat), 15f, 15f);
            EndGameJoinLobbyRpc(lobby.LobbyCode);

            await Task.Delay(1000);
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby Scene", LoadSceneMode.Single);
        }
        catch (LobbyServiceException ex)
        {
            Debug.LogError(ex);
            PopupSetter("Error", ex.Message);
            NetworkManager.Singleton.Shutdown();
            activeLobby = null;
            SceneManager.LoadScene(0);
        }
    }

    [Rpc(SendTo.NotServer)]
    async void EndGameJoinLobbyRpc(string code)
    {
        try
        {
            if (!NetworkManager.Singleton.IsHost) activeLobby = await LobbyService.Instance.JoinLobbyByIdAsync(code);
        }
        catch (LobbyServiceException ex)
        {
            Debug.LogError(ex);
            PopupSetter("Error", ex.Message);
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(0);
        }
    }

    public async Task<bool> CreateLobby(int maxPlayers)
    {
        try
        {
            //setup relay
            activeLobby = null;
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
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(playerName, maxPlayers, options);
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
            PopupSetter("Error", ex.Message);
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
            PopupSetter("Error", ex.Message);
            return null;
        }

    }

    public async Task<bool> JoinLobby(string joinCode)
    {
        try
        {
            activeLobby = null;
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(joinCode);

            activeLobby = joinedLobby;
            if (joinedLobby.Data.ContainsKey("RelayCode"))
            {
                relayJoinCode = joinedLobby.Data["RelayCode"].Value;
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

                SetupTransport(joinAllocation);
                NetworkManager.Singleton.StartClient();
                SubForHostDisconnection();      //so that even if host disconnects and lobby is alive
                return true;
            }
            isHost = false;
            return true;
        }
        catch (LobbyServiceException ex) {
            Debug.LogError(ex);
            PopupSetter("Error", ex.Message);
            return false;
        }
    }

    public async Task<bool> LeaveLobby()
    {
        try
        {
            OnDestroy();
            NetworkManager.Singleton.OnClientDisconnectCallback -= HostDisconnected;
            if (isHost) { 
                CancelInvoke(nameof(LobbyHeartbeat));
                await LobbyService.Instance.DeleteLobbyAsync(activeLobby.Id); 
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(activeLobby.Id, thisPlayerId);
            }
            activeLobby = null;
            if(NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            isHost = false;
            Debug.Log("Lobby Left");
            if (PlayerPrefs.GetInt("HostLeft", 0) == 1)
            {
                PopupSetter("ERROR", "Host has disbanded the lobby!");
                PlayerPrefs.SetInt("HostLeft", 0);
            }
            return true;
        }
        catch (LobbyServiceException ex) {
            Debug.LogError(ex);
            if (ex.ErrorCode != 16001) PopupSetter("ERROR", ex.Message);
            else PopupSetter("ERROR", "Host has disbanded the lobby!");
            activeLobby = null;
            isHost = false;
            return false;
        }
    } 

    public void LeaveLobbyAfterHostDisconnection()
    {
        if(activeLobby != null) FindFirstObjectByType<LobbyUIScript>().LeaveLobby();        //to avoid double call after pressing back by host as network disconnects
    }

    void OnClientConnected(ulong cId)
    {
        TeamManager.Instance.SetTeam(cId);
    }

    void OnClientDisconnected(ulong cId)
    {
        Debug.LogError($"Player {TeamManager.Instance.playerNames[cId]} with id {cId} left!");
        if (cId == NetworkManager.ServerClientId) return;      //host cant remove himself as his network is already gone
        TeamManager.Instance.RemovePlayerNameRpc(cId);
    }

    void SetupTransport(Allocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        // for android
        string connectionType = "dtls";
        transport.UseWebSockets = false;
#if UNITY_WEBGL
        connectionType = "wss"; 
        transport.UseWebSockets = true;
#endif

        RelayServerData relayServerData = allocation.ToRelayServerData(connectionType);
        transport.SetRelayServerData(relayServerData);
    }

    void SetupTransport(JoinAllocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        string connectionType = "dtls";
        transport.UseWebSockets = false;
#if UNITY_WEBGL
        connectionType = "wss"; 
        transport.UseWebSockets = true;
#endif

        RelayServerData relayServerData = allocation.ToRelayServerData(connectionType);
        transport.SetRelayServerData(relayServerData);
    }

    /*void SetupTransport(Allocation allocation)
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
    }*/

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
    GameObject go;
    void PopupSetter(string headerT, string bodyT)
    {
        if (go != null) return;
        //not a new object each time to have single popup at a time
        go = Instantiate(popupGO);
        go.GetComponent<PopupScript>().PopupActivation(headerT, bodyT);
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    public void SubForHostDisconnection()
    {
        if(NetworkManager.Singleton!=null) NetworkManager.Singleton.OnClientDisconnectCallback += HostDisconnected;
    }

    public void HostDisconnected(ulong disconnectedClientId)
    {
        if (disconnectedClientId == NetworkManager.ServerClientId || disconnectedClientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.LogError("HOST DISCONNECTED or YOU LEFT!");
            NetworkManager.Singleton.Shutdown();
            NetworkManager.Singleton.OnClientDisconnectCallback -= HostDisconnected;
            PlayerPrefs.SetInt("HostLeft", 1);
            if (SceneManager.GetActiveScene().buildIndex != 0)
            {
                SceneManager.LoadScene(0);
            }
            else 
            { 
                LeaveLobbyAfterHostDisconnection();
                //PopupSetter("ERROR", "Host has disbanded the lobby!");
            }
        }
    }
}

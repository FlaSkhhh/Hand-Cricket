using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class TeamManager : NetworkBehaviour
{

    public static TeamManager Instance;
    public NetworkList<ulong> teamA_Ids;
    public NetworkList<ulong> teamB_Ids;

    public NetworkVariable<FixedString32Bytes> teamAName;
    public NetworkVariable<FixedString32Bytes> teamBName;

    public Dictionary<ulong,string> playerNames = new Dictionary<ulong,string>();       //player names are saved as "name,characterselectionvalues" 
                                                                                        //                       where charvalues are faceIndex+accessoryIndex+hue
    public Dictionary<ulong,GameObject> playerCharacters = new();                       

    [SerializeField] GameObject popupGO;

    bool isLocalPlayerTeamA;
    bool needsTeamLineupUpdate;
    LobbyUIScript lobbyUIScript;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else { Destroy(gameObject); return; }

        teamA_Ids = new NetworkList<ulong>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        teamB_Ids = new NetworkList<ulong>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        teamAName = new NetworkVariable<FixedString32Bytes>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        teamBName = new NetworkVariable<FixedString32Bytes>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        //CharacterSelect.instance.SpawnPlayerCharacters(PlayerPrefs.GetString("CustomCharacter").ToString());
        lobbyUIScript = FindObjectsByType<LobbyUIScript>(FindObjectsSortMode.None)[0];  
        playerNames.Clear();
        playerCharacters.Clear();

        teamA_Ids.OnListChanged += OnTeamAListChanged;
        teamB_Ids.OnListChanged += OnTeamBListChanged;
        teamAName.OnValueChanged += OnTeamANameChanged;
        teamBName.OnValueChanged += OnTeamBNameChanged;
        if (!IsHost) 
        {
            //lobbyUIScript.TeamsNameUpdate();    //because host triggers the name change anyway
            FixedString32Bytes _ = LobbyManager.Instance.PlayerNameGetter() +","+ PlayerPrefs.GetString("CustomCharacter").ToString();
            SendNameToServerRpc(_);
            return; 
        }
        teamAName.Value = "TMA";
        teamBName.Value = "TMB";
    }

    #region Lobby Scene Stuff
    public void TeamNameChanged(string name)
    {
        TeamNameUpdateRpc(name, isLocalPlayerTeamA);
    }

    [Rpc(SendTo.Server)]
    void TeamNameUpdateRpc(string name, bool teamA)
    {
        if(teamA) teamAName.Value = name;
        else teamBName.Value = name;
    }

    void OnTeamAListChanged(NetworkListEvent<ulong> changeEvent)
    {
        //redo all character lineup positions
        ChangeCharacterPositionForTeamLineup();
        /*switch (changeEvent.Type)
        {
            case NetworkListEvent<ulong>.EventType.Add:
                break;

            case NetworkListEvent<ulong>.EventType.Remove:
            case NetworkListEvent<ulong>.EventType.RemoveAt:
                break;

            case NetworkListEvent<ulong>.EventType.Clear:
                break;
        }*/
    }
    void OnTeamBListChanged(NetworkListEvent<ulong> changeEvent)
    {
        ChangeCharacterPositionForTeamLineup();
    }

    void OnTeamANameChanged(FixedString32Bytes prev, FixedString32Bytes current)
    {
        lobbyUIScript.TeamsNameUpdate(true);
    }

    void OnTeamBNameChanged(FixedString32Bytes prev, FixedString32Bytes current)
    {
        lobbyUIScript.TeamsNameUpdate(false);
    }

    public void SetTeam(ulong cId)
    {
        if (teamA_Ids.Count <= teamB_Ids.Count)
        {
            teamA_Ids.Add(cId);
            Debug.Log($"{cId} added to Team A");
        }
        else
        {
            teamB_Ids.Add(cId);
            Debug.Log($"{cId} added to Team B");
        }
    }

    //this is to change batting and bowling sides as current scripts always take team A as batting in first inning
    public void SwapTeams()
    {
        List<ulong> tempA = new List<ulong>();
        foreach (ulong id in teamA_Ids) tempA.Add(id);

        List<ulong> tempB = new List<ulong>();
        foreach (ulong id in teamB_Ids) tempB.Add(id);

        teamA_Ids.Clear();
        teamB_Ids.Clear();

        foreach (ulong id in tempB) teamA_Ids.Add(id);

        foreach (ulong id in tempA) teamB_Ids.Add(id);

        string tempAName = teamAName.Value.ToString();
        string tempBName = teamBName.Value.ToString();

        teamAName.Value = tempBName;
        teamBName.Value = tempAName;
    }

    [Rpc(SendTo.Server)]
    void SendNameToServerRpc(FixedString32Bytes clientName, RpcParams rpcParams = default)
    {
        SendNameToAllClientsRpc(rpcParams.Receive.SenderClientId, clientName);  

        SendClientAllJoinedNames(rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Everyone)]
    void SendNameToAllClientsRpc(ulong cId, FixedString32Bytes clientName)
    {
        AddPlayerName(cId, clientName.ToString());
        ChangeCharacterPositionForTeamLineup();
    }

    void SendClientAllJoinedNames(ulong cId)
    {
        List<ulong> ids = new();
        List<FixedString32Bytes> names = new();

        foreach(var k in playerNames)
        {
            if (k.Key == cId) continue;
            ids.Add(k.Key);
            names.Add(k.Value);
        }

        SyncNamesToClientRpc(ids.ToArray(), names.ToArray(), RpcTarget.Single(cId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void SyncNamesToClientRpc(ulong[] ids, FixedString32Bytes[] names, RpcParams rpcParams = default)
    {
        StartCoroutine(AddPlayersToList(ids, names));
    }

    IEnumerator AddPlayersToList(ulong[] ids, FixedString32Bytes[] names)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            AddPlayerName(ids[i], names[i].ToString());
            yield return null;
        }
        ChangeCharacterPositionForTeamLineup();
    }

    public void AddPlayerName(ulong cId, string playerName) 
    {
        playerNames.Add(cId, playerName);
        Debug.Log($"PLAYER {playerName} ADDED AS ID {cId}");
        GameObject character = CharacterSelect.instance.SpawnPlayerCharacters(playerName.Split(',')[1]);
        playerCharacters.Add(cId, character);
        if (cId == NetworkManager.Singleton.LocalClientId) character.GetComponent<CharacterCustomiseHelper>().IsLocalPlayer();
    }

    [Rpc(SendTo.Server)]
    public void ChangeTeamForPlayerRpc(ulong cId)
    {
        if(teamA_Ids.Contains(cId) && teamB_Ids.Count <= 5)
        {
            teamA_Ids.Remove(cId);
            if (!teamB_Ids.Contains(cId)) teamB_Ids.Add(cId);
        }
        else if(teamB_Ids.Contains(cId) && teamA_Ids.Count <= 5)
        {
            teamB_Ids.Remove(cId);
            if (!teamA_Ids.Contains(cId)) teamA_Ids.Add(cId);
        }
    }

    void ChangeCharacterPositionForTeamLineup()
    {
        needsTeamLineupUpdate = true;
    }

    void LineupUpdate()
    {
        LocalPlayerTeamASetter(teamA_Ids.Contains(NetworkManager.Singleton.LocalClientId));
        lobbyUIScript.TeamsUIListUpdate();

        if (teamA_Ids.Count > 0 && teamA_Ids[0] == NetworkManager.LocalClientId)
        {
            //team A captain
            lobbyUIScript.ChangeTeamNameIFStatus(true, true);
        }
        else if(teamB_Ids.Count >0 && teamB_Ids[0] == NetworkManager.LocalClientId)
        {
            //team B captain
            lobbyUIScript.ChangeTeamNameIFStatus(true, false);
        }
        else
        {
            lobbyUIScript.ChangeTeamNameIFStatus(false, false);
        }

        int counter = 0;
        Transform[] teamALineup;
        Transform[] teamBLineup;

        (teamALineup, teamBLineup) = LobbyManager.Instance.LineupPositionsGetter();
        foreach (ulong cId in teamA_Ids)
        {
            if (!playerCharacters.ContainsKey(cId)) continue;       //continue if player characters doesnt have user added yet
            playerCharacters[cId].GetComponent<CharacterCustomiseHelper>().MoveToTeamLineup(teamALineup[counter].position);
            counter++;
        }
        counter = 0;
        foreach (ulong cId in teamB_Ids)
        {
            if (!playerCharacters.ContainsKey(cId)) continue;
            playerCharacters[cId].GetComponent<CharacterCustomiseHelper>().MoveToTeamLineup(teamBLineup[counter].position);
            counter++;
        }
    }
    public bool CheckTeamImbalance()
    {
        if (Mathf.Abs(teamA_Ids.Count - teamB_Ids.Count) > 1)
        {
            TeamImbalanceSignalRpc();
            return true;
        }
        else return false;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void TeamImbalanceSignalRpc()
    {
        GameObject go = Instantiate(popupGO);
        go.GetComponent<PopupScript>().PopupActivation("TEAM IMBALANCED", "MAKE SURE THE TEAMS ARE OF EQUAL STRENGTH!");
    }


    [Rpc(SendTo.ClientsAndHost)]
    public void RemovePlayerNameRpc(ulong cid) 
    {
        if (IsHost)
        {
            _ = teamA_Ids.Contains(cid) ? teamA_Ids.Remove(cid) : teamB_Ids.Remove(cid);
        }
        Destroy(playerCharacters[cid]);
        playerCharacters.Remove(cid);
        playerNames.Remove(cid);
    }

    //is called when game starts to avoid the UI scene carryover subs
    public override void OnNetworkDespawn()
    {
        foreach(GameObject go in playerCharacters.Values) { Destroy(go); }
        teamA_Ids.OnListChanged -= OnTeamAListChanged;
        teamB_Ids.OnListChanged -= OnTeamBListChanged;
        teamAName.OnValueChanged -= OnTeamANameChanged;
        teamBName.OnValueChanged -= OnTeamBNameChanged;
    }


    /// <summary>
    /// Returns true if local player is in team A
    /// </summary>
    /// <returns></returns>
    public bool LocalPlayerTeamAGetter()
    {
        return isLocalPlayerTeamA;
    }

    public void LocalPlayerTeamASetter(bool val)
    {
        isLocalPlayerTeamA = val;
    }
    #endregion

    #region Game Scene Stuff

    public async Task SpawnPlayerCharactersInGameScene()
    {
        foreach (GameObject character in playerCharacters.Values)
        {
            Destroy(character);
        }
        playerCharacters.Clear();
        int counter = 0;
        foreach (ulong cid in teamA_Ids)
        {
            if (playerNames.TryGetValue(cid, out string playerData)) { 
                GameObject guy = CharacterSelect.instance.SpawnPlayerCharactersGameScene(playerData.Split(',')[1]);
                playerCharacters.Add(cid, guy);
                counter++;
                await Task.Yield(); 
            }
        }
        counter = 0;
        foreach(ulong cid in teamB_Ids)
        {
            if (playerNames.TryGetValue(cid, out string playerDataB))
            {
                GameObject guy = CharacterSelect.instance.SpawnPlayerCharactersGameScene(playerDataB.Split(',')[1]);
                playerCharacters.Add(cid, guy);
                counter++;
                await Task.Yield();
            }
        }
        playerCharacters[NetworkManager.LocalClientId].GetComponent<CharacterCustomiseHelper>().IsLocalPlayer();
    }

    #endregion

    void Update()
    {
        if (needsTeamLineupUpdate)
        {
            LineupUpdate();
            needsTeamLineupUpdate = false;
        }
    }
}

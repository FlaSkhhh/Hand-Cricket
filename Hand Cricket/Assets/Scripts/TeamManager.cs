using System.Collections.Generic;
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

    public Dictionary<ulong,string> playerNames = new Dictionary<ulong,string>();
    public Dictionary<ulong, GameObject> playerCharacters = new();
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
        CharacterSelect.instance.DestroyCustomisablePrefabClone();
        teamA_Ids.OnListChanged += OnTeamAListChanged;
        teamB_Ids.OnListChanged += OnTeamBListChanged;
        if (!IsHost) 
        {
            FixedString32Bytes _ = LobbyManager.Instance.PlayerNameGetter() +","+ PlayerPrefs.GetString("CustomCharacter").ToString();
            SendNameToServerRpc(_);
            return; 
        }
        teamAName.Value = "TMA";
        teamBName.Value = "TMB";
    }

    void OnTeamAListChanged(NetworkListEvent<ulong> changeEvent)
    {
        ulong cId = changeEvent.Value;

        ChangeCharacterTeam();
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
        ulong cId = changeEvent.Value;

        ChangeCharacterTeam();
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
        ChangeCharacterTeam();
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
        for(int i= 0; i<ids.Length; i++)
        {
            AddPlayerName(ids[i], names[i].ToString());
        }
        ChangeCharacterTeam();
    }

    public void AddPlayerName(ulong cId, string playerName) 
    {
        playerNames.Add(cId, playerName);
        Debug.Log($"PLAYER {playerName} ADDED AS ID {cId}");
        GameObject character = CharacterSelect.instance.SpawnPlayerCharacters(playerName.Split(',')[1]);
        playerCharacters.Add(cId, character);
    }

    void ChangeCharacterTeam()
    {
        int counter = 0;
        foreach(ulong cId in teamA_Ids)
        {
            if (!playerCharacters.ContainsKey(cId)) continue;
            playerCharacters[cId].transform.position = new Vector3(-8f + counter * 2, -5.5f, 0);
            playerCharacters[cId].transform.rotation = Quaternion.Euler(0, 180, 0);
            counter++;
        }
        counter = 0;
        foreach (ulong cId in teamB_Ids)
        {
            if (!playerCharacters.ContainsKey(cId)) continue;
            playerCharacters[cId].transform.position = new Vector3(8f - counter * 2, -5.5f, 0); 
            playerCharacters[cId].transform.rotation = Quaternion.Euler(0, 180, 0);
            counter++;
        }
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

    public override void OnNetworkDespawn()
    {
        teamA_Ids.OnListChanged -= OnTeamAListChanged;
        teamB_Ids.OnListChanged -= OnTeamBListChanged;
    }
}

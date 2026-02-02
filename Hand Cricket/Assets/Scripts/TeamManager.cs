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
        teamAName = new NetworkVariable<FixedString32Bytes>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        teamBName = new NetworkVariable<FixedString32Bytes>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsHost) 
        {
            FixedString32Bytes _ = LobbyManager.Instance.PlayerNameGetter();
            SendNameToServerRpc(_);
            return; 
        }
        teamAName.Value = "TMA";
        teamBName.Value = "TMB";
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
    }

    public void AddPlayerName(ulong cId, string playerName) 
    {
        playerNames.Add(cId, playerName);
    }

    public void RemovePlayerName(ulong cid) 
    {
        playerNames.Remove(cid);
    }
}

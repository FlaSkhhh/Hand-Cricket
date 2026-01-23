using Unity.Netcode;
using UnityEngine;

public class TeamManager : NetworkBehaviour
{

    public static TeamManager Instance;
    public NetworkList<ulong> teamA_Ids;
    public NetworkList<ulong> teamB_Ids;
        
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
}

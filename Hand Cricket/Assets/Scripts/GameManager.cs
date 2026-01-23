using System.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Linq;

public class GameManager : NetworkBehaviour
{

    int[] batsmanRun = null;
    int[] bowlerRun = null;

    int totalRuns = 0;
    int targetRuns = 0;

    int teamA_index;
    int teamB_index;

    enum MatchState
    {
        Inning1,
        Inning2,
        MatchOver
    }

    MatchState currentMatchState;

    [SerializeField] GameUIScript gameUIScript;

    void Start()
    {
        currentMatchState = MatchState.Inning1;
        teamA_index = 0;
        teamB_index = 0;
        ResetInning();
    }

    public void SubmitRunSelection(int[] runSel)
    {
        SubmitRunRpc(runSel);
    }

    [Rpc(SendTo.Server)]
    void SubmitRunRpc(int[] runSelected, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        //bool isBatsman = inningChange ? senderId != NetworkManager.ServerClientId : senderId == NetworkManager.ServerClientId;
        bool isBatsman = currentMatchState == MatchState.Inning2 ? TeamManager.Instance.teamB_Ids.Contains(senderId) : TeamManager.Instance.teamA_Ids.Contains(senderId);
        if(isBatsman)
        {
            //sent by host/batsman for first inning
            batsmanRun = runSelected;
        }
        else
        {
            //sent by client/bowler for first inning
            bowlerRun = runSelected;
        }

        if (bowlerRun != null && batsmanRun != null) 
        {

            ReadRunRpc(batsmanRun,bowlerRun);
            batsmanRun = null;
            bowlerRun = null;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ReadRunRpc(int[] batsmanRun, int[] bowlerRun)
    {
        //Debug.Log("Getting Result");
        StartCoroutine(OverResultDisplay(batsmanRun,bowlerRun));
    }

    IEnumerator OverResultDisplay(int[] batsmanRun,int[] bowlerRun)
    {
        bool isBatsman = currentMatchState == MatchState.Inning2 ? TeamManager.Instance.teamB_Ids.Contains(NetworkManager.LocalClientId) : TeamManager.Instance.teamA_Ids.Contains(NetworkManager.LocalClientId); ;

        for (int i = 0; i < batsmanRun.Length; i++)
        {
            if (isBatsman)
            {
                gameUIScript.SetOpponentRun(bowlerRun[i]);

            }
            else
            {
                gameUIScript.SetOpponentRun(batsmanRun[i]);
            }
            if (batsmanRun[i] == bowlerRun[i])
            {
                //out
                gameUIScript.SetMatchUI("BATSMAN IS OUT AT " + totalRuns.ToString() + " RUNS", "0");
                
                if(currentMatchState == MatchState.Inning1)
                {
                    if (teamA_index >= TeamManager.Instance.teamA_Ids.Count - 1)
                    {
                        targetRuns = totalRuns + 1;
                        currentMatchState = MatchState.Inning2;
                        yield return new WaitForSeconds(1);
                        ResetInning();
                        yield break;
                    }
                    else
                    {
                        teamA_index++;
                    }
                }
                else
                {
                    if (teamB_index >= TeamManager.Instance.teamB_Ids.Count - 1)
                    {
                        //winner decided here based on target runs and total runs as team 
                        MatchWinnerCheck();
                        yield return new WaitForSeconds(1);
                        yield break;
                    }
                    else
                    {
                        teamB_index++;
                    }
                }
                break;
            }
            else
            {
                totalRuns += batsmanRun[i];
                gameUIScript.SetMatchUI(totalRuns.ToString() + " RUNS SCORED BY BATSMAN!", totalRuns.ToString());
                if(currentMatchState == MatchState.Inning2)
                {
                    if(totalRuns >= targetRuns)
                    {
                        MatchWinnerCheck();
                        yield return new WaitForSeconds(1);
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(1f);
        }

        if (currentMatchState == MatchState.Inning1) teamB_index = teamB_index >= TeamManager.Instance.teamB_Ids.Count - 1 ? 0 : teamB_index + 1;
        else teamA_index = teamA_index >= TeamManager.Instance.teamA_Ids.Count - 1 ? 0 : teamA_index + 1;

        Invoke(nameof(ResetRunSelection), 2f);
    }

    void MatchWinnerCheck()
    {
        currentMatchState = MatchState.MatchOver;
        string winnerTeam;
        if (totalRuns >= targetRuns)
        {
            winnerTeam = "Team B";
        }
        else
        {
            winnerTeam = "Team A";
        }
        gameUIScript.SetMatchUI(winnerTeam + " has won the match!", totalRuns.ToString());
    }

    void ResetInning()
    {
        teamB_index = 0;
        teamA_index = 0;
        totalRuns = 0;
        ResetRunSelection();
    }
    
    void ResetRunSelection()
    {
        batsmanRun = null;
        bowlerRun = null;
        if (TeamManager.Instance.teamA_Ids[teamA_index] == NetworkManager.LocalClientId || TeamManager.Instance.teamB_Ids[teamB_index] == NetworkManager.LocalClientId)
        {
            gameUIScript.ResetUI();
        }
        else
        {
            gameUIScript.DisableUI();
        }
    }
}

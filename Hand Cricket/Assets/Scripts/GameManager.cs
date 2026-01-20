using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{

    int[] batsmanRun = null;
    int[] bowlerRun = null;

    int totalRuns = 0;

    bool inningChange;

    [SerializeField] GameUIScript gameUIScript;

    public void SubmitRunSelection(int[] runSel)
    {
        SubmitRunRpc(runSel);
    }

    [Rpc(SendTo.Server)]
    void SubmitRunRpc(int[] runSelected, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        bool isBatsman = inningChange ? senderId != NetworkManager.ServerClientId : senderId == NetworkManager.ServerClientId;
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
        bool isBatsman = inningChange ? !IsHost : IsHost;

        bool isOut = false;
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
                totalRuns = 0;
                inningChange = !inningChange;
                isOut = true;
            }
            else
            {
                totalRuns += batsmanRun[i];
                gameUIScript.SetMatchUI(totalRuns.ToString() + " RUNS SCORED BY BATSMAN!", totalRuns.ToString());
            }

            
            if (isOut) break;
            yield return new WaitForSeconds(1f);
        }

        Invoke(nameof(ResetRunSelection), 2f);
    }
    
    void ResetRunSelection()
    {
        batsmanRun = null;
        bowlerRun = null;
        gameUIScript.ResetUI();
    }
}

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    int[] batsmanRun = null;
    int[] bowlerRun = null;

    int totalRuns = 0;
    int targetRuns = 0;

    int currentBatsmanRuns = 0;
    int currentBatsmanBalls = 0;

    Dictionary<int,BowlerStats> bowlingTeamStats = new Dictionary<int,BowlerStats>();

    int wickets = 0;

    [SerializeField] Animator teamAHand;
    [SerializeField] Animator teamBHand;
    //batsman and bowler index as team A is batting always
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

    void Awake()
    {
        currentMatchState = MatchState.Inning1;
        teamA_index = 0;
        teamB_index = 0;
        wickets = 0;
    }

    void Start()
    {
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


    //main coroutine that handles the over gameplay with user inputs
    IEnumerator OverResultDisplay(int[] batsmanRun, int[] bowlerRun)
    {
        int _ = 0;      //discardable int for teamindex holder
        for (int i = 0; i < batsmanRun.Length; i++)
        {
            currentBatsmanBalls++;  //add ball each time
            gameUIScript.SetBatsmanStats(currentBatsmanRuns,currentBatsmanBalls);
            //hand animations
            //reset first then use the animation
            HandAnimationsSetter(-1, -1);
            HandAnimationsSetter(batsmanRun[i], bowlerRun[i]);
            //if wicket
            if (batsmanRun[i] == bowlerRun[i])
            {
                wickets++;
                gameUIScript.SetMatchUI("BATSMAN IS OUT AT " + totalRuns.ToString() + " RUNS", totalRuns.ToString(), wickets.ToString());

                //show a small wicket UI here with batsman balls and runs before resetting them
                currentBatsmanBalls = 0;
                currentBatsmanRuns = 0;

                if (currentMatchState == MatchState.Inning1)
                {
                    bowlingTeamStats[teamB_index].wicketsTaken++;
                }
                else
                {
                    bowlingTeamStats[teamA_index].wicketsTaken++;
                }

                if (currentMatchState == MatchState.Inning1)
                {
                    if (teamA_index >= TeamManager.Instance.teamA_Ids.Count - 1)
                    {
                        targetRuns = totalRuns + 1;
                        currentMatchState = MatchState.Inning2;
                        yield return new WaitForSeconds(3);
                        gameUIScript.SetTargetRuns(targetRuns);
                        ResetInning();      //state should be changed to inning 2 before calling this
                        yield break;
                    }
                    else
                    {
                        teamA_index++;
                        gameUIScript.SetOverText(true, false, -1);      //non final batsman out so skip rest of over
                    }
                }
                else
                {
                    if (teamB_index >= TeamManager.Instance.teamB_Ids.Count - 1)
                    {
                        //winner decided here based on target runs and total runs as team 
                        MatchWinnerCheck();
                        yield return new WaitForSeconds(3);
                        yield break;
                    }
                    else
                    {
                        teamB_index++;
                        gameUIScript.SetOverText(true, false, -1);
                    }
                }

                break;      //breaking for loop of balls if wicket
            }
            else
            {   //batsman scores runs
                totalRuns += batsmanRun[i];
                currentBatsmanRuns += batsmanRun[i];
                gameUIScript.SetBatsmanStats(currentBatsmanRuns, currentBatsmanBalls);
                if (currentMatchState == MatchState.Inning1)
                {
                    bowlingTeamStats[teamB_index].runsScoredAgainst += batsmanRun[i];
                    _ = teamB_index;
                }
                else
                {
                    bowlingTeamStats[teamA_index].runsScoredAgainst += batsmanRun[i];
                    _ = teamA_index;
                }
                //using discardable var for single reference instead of 2
                gameUIScript.SetBowlerStats(bowlingTeamStats[_].runsScoredAgainst, bowlingTeamStats[_].oversCompleted.ToString() + "." +(i+1).ToString(), bowlingTeamStats[_].wicketsTaken);

                gameUIScript.SetMatchUI(totalRuns.ToString() + " RUNS SCORED BY BATSMAN!", totalRuns.ToString(), wickets.ToString());
                gameUIScript.SetOverText(false, false, i + 1);

                if (currentMatchState == MatchState.Inning2)
                {
                    if (totalRuns >= targetRuns)
                    {
                        MatchWinnerCheck();
                        yield return new WaitForSeconds(2);
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(3f);
        }


        //after over is complete/ cut short by wicket
        if (currentMatchState == MatchState.Inning1) 
        {
            bowlingTeamStats[teamB_index].oversCompleted++;     //over end/wicket fall bowler change stats
            _ = teamB_index;
            teamB_index = teamB_index >= TeamManager.Instance.teamB_Ids.Count - 1 ? 0 : teamB_index + 1; 
        }
        else
        {
            bowlingTeamStats[teamA_index].oversCompleted++;
            _ = teamA_index;
            teamA_index = teamA_index >= TeamManager.Instance.teamA_Ids.Count - 1 ? 0 : teamA_index + 1; 
        }
        //
        gameUIScript.SetBowlerStats(bowlingTeamStats[_].runsScoredAgainst, bowlingTeamStats[_].oversCompleted.ToString(), bowlingTeamStats[_].wicketsTaken);

        //reset gameplay loop
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
        gameUIScript.SetMatchUI(winnerTeam + " has won the match!", totalRuns.ToString(), wickets.ToString());
    }

    void ResetInning()
    {
        //reset team stats for display
        bowlingTeamStats.Clear();
        currentBatsmanBalls = 0;
        currentBatsmanRuns = 0;
        //setting bowler team in reset 
        int count = 0;
 
        if (currentMatchState == MatchState.Inning1) 
        {
            count = TeamManager.Instance.teamB_Ids.Count;
        }
        else
        {
            count = TeamManager.Instance.teamA_Ids.Count;
        }
        for (int i = 0; i < count; i++)
        {
            bowlingTeamStats.Add(i, new BowlerStats { runsScoredAgainst = 0, wicketsTaken = 0, oversCompleted = 0 });
        }
        teamB_index = 0;
        teamA_index = 0;
        totalRuns = 0;
        wickets = 0;

        gameUIScript.SetBatsmanStats(0, 0);

        ResetRunSelection();
        gameUIScript.SetMatchUI("", totalRuns.ToString(), wickets.ToString());
        gameUIScript.SetOverText(false, true, -1);

        //REMOVING THIS BECAUSE TEAMS DONT CHANGE SEATS AFTER INNINGS SO NO NEED TO CHANGE UI POSITIONS
        gameUIScript.TeamSideSet(currentMatchState == MatchState.Inning2 ? TeamManager.Instance.teamB_Ids.Contains(NetworkManager.LocalClientId) : TeamManager.Instance.teamA_Ids.Contains(NetworkManager.LocalClientId));
    }

    bool skipFirstChangeSeatCall;
    void ResetRunSelection()
    {
        batsmanRun = null;
        bowlerRun = null;
        string batsmanName = "Batsman";
        string bowlerName = "Bowler";
        int _ = 0;

        //reset hand animations
        HandAnimationsSetter(-1, -1);

        //display next players names and stats
        if (currentMatchState == MatchState.Inning1)
        {
            _ = teamB_index;
            batsmanName = TeamManager.Instance.playerNames[TeamManager.Instance.teamA_Ids[teamA_index]];
            bowlerName = TeamManager.Instance.playerNames[TeamManager.Instance.teamB_Ids[teamB_index]];
        }
        else
        {
            _ = teamA_index;
            batsmanName = TeamManager.Instance.playerNames[TeamManager.Instance.teamB_Ids[teamB_index]];
            bowlerName = TeamManager.Instance.playerNames[TeamManager.Instance.teamA_Ids[teamA_index]];
        }
        gameUIScript.SetBatsmanName(batsmanName.Split(',')[0]);
        gameUIScript.SetBowlerName(bowlerName.Split(',')[0]);

        gameUIScript.SetBowlerStats(bowlingTeamStats[_].runsScoredAgainst, bowlingTeamStats[_].oversCompleted.ToString(), bowlingTeamStats[_].wicketsTaken);

        if(skipFirstChangeSeatCall) gameUIScript.ChangePlayerSeats();       //need to skip first call as when game starts, UI script handles it with spawning chars
        skipFirstChangeSeatCall = true;

        if (TeamManager.Instance.teamA_Ids[teamA_index] == NetworkManager.LocalClientId || TeamManager.Instance.teamB_Ids[teamB_index] == NetworkManager.LocalClientId)
        {
            gameUIScript.ResetUI();
        }
        else
        {
            gameUIScript.DisableUI();
        }
    }

    void HandAnimationsSetter(int batsmanRun, int bowlerRun)
    {
        string batsmanHandString = HandAnimationStringGetter(batsmanRun);
        string bowlerHandString = HandAnimationStringGetter(bowlerRun);

        if (currentMatchState == MatchState.Inning1)
        {
            teamAHand.Play(batsmanHandString);
            teamBHand.Play(bowlerHandString);
        }
        else
        {
            teamAHand.Play(bowlerHandString);
            teamBHand.Play(batsmanHandString);
        }
    }

    string HandAnimationStringGetter(int runIndex)
    {
        switch (runIndex) 
        {
            case 1:
                return "1";
            case 2:
                return "2";
            case 3:
                return "3";
            case 4:
                return "4";
            case 5:
                return "5";
            case 6:
                return "6";
            default:
                return "D";
        }
    }

    public (int,int) CurrentActivePlayersGetter()
    {
        return (teamA_index, teamB_index);
    }
}

public class BowlerStats 
{
    public int runsScoredAgainst;
    public int wicketsTaken;
    public int oversCompleted;
}

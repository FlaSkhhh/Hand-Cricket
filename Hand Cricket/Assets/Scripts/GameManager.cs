using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    HashSet<ulong> playersLoaded = new HashSet<ulong>();

    int[] batsmanRun = null;
    int[] bowlerRun = null;

    int totalRuns = 0;
    int targetRuns = 0;

    int currentBatsmanRuns = 0;
    int currentBatsmanBalls = 0;

    Dictionary<ulong, BowlerStats> bowlingTeamStats = new Dictionary<ulong, BowlerStats>();

    int wickets = 0;

    [SerializeField] Animator teamAHand;
    [SerializeField] Animator teamBHand;
    [SerializeField] TextMeshPro handAText;
    [SerializeField] TextMeshPro handBText;
    //batsman and bowler index as team A is batting always
    int teamA_index;
    int teamB_index;

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
        gameUIScript.LoadingScreenStatus(true);
        SendLoadingAckRpc(NetworkManager.LocalClientId);
        TeamManager.Instance.SubToPlayerLists();
        if (NetworkManager.IsServer)
        {
            Invoke(nameof(PlayerConnectionTimeout), 30f);
        }
    }

    [Rpc(SendTo.Server)]
    void SendLoadingAckRpc(ulong cId)
    {
        playersLoaded.Add(cId);
        if (playersLoaded.Count >= NetworkManager.ConnectedClientsList.Count)
        {
            CancelInvoke(nameof(PlayerConnectionTimeout));
            StartGameplayRpc();
        }
    }
    void PlayerConnectionTimeout()
    {
        for (int i = TeamManager.Instance.teamA_Ids.Count - 1; i >= 0; i--)
        {
            ulong id = TeamManager.Instance.teamA_Ids[i];
            if (playersLoaded.Contains(id)) continue;
            TeamManager.Instance.teamA_Ids.Remove(id);
        }
        for (int i = TeamManager.Instance.teamB_Ids.Count - 1; i >= 0; i--)
        {
            ulong id = TeamManager.Instance.teamB_Ids[i];
            if (playersLoaded.Contains(id)) continue;
            TeamManager.Instance.teamB_Ids.Remove(id);
        }
        StartGameplayRpc();
    }

    [Rpc(SendTo.Everyone)]
    void StartGameplayRpc()
    {
        ResetInning();
        gameUIScript.LoadingScreenStatus(false);
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
        if (isBatsman)
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

            ReadRunRpc(batsmanRun, bowlerRun);
            batsmanRun = null;
            bowlerRun = null;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ReadRunRpc(int[] batsmanRun, int[] bowlerRun)
    {
        //Debug.Log("Getting Result");
        StartCoroutine(OverResultDisplay(batsmanRun, bowlerRun));
    }


    bool overResultDisplayPhase;
    //main coroutine that handles the over gameplay with user inputs
    IEnumerator OverResultDisplay(int[] batsmanRun, int[] bowlerRun)
    {
        overResultDisplayPhase = true;
        ulong batsmanId;
        ulong bowlerId;
        string batsmanName;
        string bowlerName;

        gameUIScript.DisableLocalPlayerStatus();

        if(currentMatchState == MatchState.Inning1)
        {
            batsmanId = TeamManager.Instance.teamA_Ids[teamA_index];
            bowlerId = TeamManager.Instance.teamB_Ids[teamB_index];
            batsmanName = TeamManager.Instance.playerNames[batsmanId].Split(',')[0];
            bowlerName = TeamManager.Instance.playerNames[bowlerId].Split(',')[0];
        }
        else
        {
            bowlerId = TeamManager.Instance.teamA_Ids[teamA_index];
            batsmanId = TeamManager.Instance.teamB_Ids[teamB_index];
            batsmanName = TeamManager.Instance.playerNames[batsmanId].Split(',')[0];
            bowlerName = TeamManager.Instance.playerNames[bowlerId].Split(',')[0];
        }

        for (int i = 0; i < batsmanRun.Length; i++)
        {
            currentBatsmanBalls++;  //add ball each time
            gameUIScript.SetBatsmanStats(currentBatsmanRuns, currentBatsmanBalls);
            //hand animations
            //reset first then use the animation
            HandAnimationsSetter(-1, -1);
            yield return null;
            HandAnimationsSetter(batsmanRun[i], bowlerRun[i]);
            yield return new WaitForSeconds(1f);        //wait 1 sec for animation to finish
            //if wicket
            if (batsmanRun[i] == bowlerRun[i])
            {
                wickets++;
                gameUIScript.SetRunsWicketUI(totalRuns.ToString(), wickets.ToString());

                //show a small wicket UI here with batsman balls and runs before resetting them
                bowlingTeamStats[bowlerId].wicketsTaken++;

                gameUIScript.SetScoreStatus($"{batsmanName} is out at {currentBatsmanRuns} runs for {currentBatsmanBalls} balls!");

                currentBatsmanBalls = 0;
                currentBatsmanRuns = 0;

                yield return new WaitForSeconds(1f);

                if (currentMatchState == MatchState.Inning1)
                {
                    if (teamA_index >= TeamManager.Instance.teamA_Ids.Count - 1)
                    {
                        //all batsman out for team A in first inning
                        targetRuns = totalRuns + 1;
                        currentMatchState = MatchState.Inning2;
                        yield return new WaitForSeconds(2);
                        gameUIScript.SetTargetRuns(targetRuns);
                        ResetInning();      //state should be changed to inning 2 before calling this
                        overResultDisplayPhase = false;
                        yield break;
                    }
                    else
                    {
                        if(!batsmanLeftDuringSimulation) teamA_index++;
                        gameUIScript.SetOverText(true, false, -1);      //non final batsman out so skip rest of over
                    }
                }
                else
                {
                    if (teamB_index >= TeamManager.Instance.teamB_Ids.Count - 1)
                    {
                        //winner decided here based on target runs and total runs as team 
                        MatchWinnerCheck();
                        yield return new WaitForSeconds(2);
                        overResultDisplayPhase = false;
                        yield break;
                    }
                    else
                    {
                        if (!batsmanLeftDuringSimulation) teamB_index++;
                        gameUIScript.SetOverText(true, false, -1);
                    }
                }

                break;      //breaking for loop of balls if wicket is taken
            }
            else
            {
                //batsman scores runs
                totalRuns += batsmanRun[i];
                currentBatsmanRuns += batsmanRun[i];
                gameUIScript.SetBatsmanStats(currentBatsmanRuns, currentBatsmanBalls);

                bowlingTeamStats[bowlerId].runsScoredAgainst += batsmanRun[i];
                //using discardable var for single reference instead of 2
                //update match stats for batsman and bowler before next 
                gameUIScript.SetBowlerStats(bowlingTeamStats[bowlerId].runsScoredAgainst, bowlingTeamStats[bowlerId].oversCompleted.ToString() + "." + (i + 1).ToString(), bowlingTeamStats[bowlerId].wicketsTaken);

                gameUIScript.SetRunsWicketUI(totalRuns.ToString(), wickets.ToString());
                gameUIScript.SetScoreStatus($"{batsmanRun[i]} runs scored by {batsmanName}");

                gameUIScript.SetOverText(false, false, i + 1);

                if (currentMatchState == MatchState.Inning2)
                {
                    if (totalRuns >= targetRuns)
                    {
                        MatchWinnerCheck();
                        yield return new WaitForSeconds(2);
                        overResultDisplayPhase = false;
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(3f);
        }


        //after over is complete/ cut short by wicket
        if (currentMatchState == MatchState.Inning1)
        {
            bowlingTeamStats[bowlerId].oversCompleted++;     //over end/wicket fall bowler change stats
            teamB_index = teamB_index >= TeamManager.Instance.teamB_Ids.Count - 1 ? 0 : teamB_index + 1;
        }
        else
        {
            bowlingTeamStats[bowlerId].oversCompleted++;
            teamA_index = teamA_index >= TeamManager.Instance.teamA_Ids.Count - 1 ? 0 : teamA_index + 1;
        }
        //after over set the stats for last time in simulation
        gameUIScript.SetBowlerStats(bowlingTeamStats[bowlerId].runsScoredAgainst, bowlingTeamStats[bowlerId].oversCompleted.ToString(), bowlingTeamStats[bowlerId].wicketsTaken);

        //reset gameplay loop
        yield return new WaitForSeconds(2f);
        ResetRunSelection();
        overResultDisplayPhase = false;
    }

    void MatchWinnerCheck()
    {
        currentMatchState = MatchState.MatchOver;
        string winnerTeam;
        if (totalRuns >= targetRuns)
        {
            winnerTeam = TeamManager.Instance.teamBName.Value.ToString();
        }
        else if (totalRuns < targetRuns - 1)
        {
            winnerTeam = TeamManager.Instance.teamAName.Value.ToString();
        }
        else        //when total runs is targer - 1
        {
            winnerTeam = "No one";
        }
        gameUIScript.SetRunsWicketUI(totalRuns.ToString(), wickets.ToString());
        gameUIScript.SetScoreStatus(winnerTeam + " has won this match!");
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
            for (int i = 0; i < count; i++)
            {
                bowlingTeamStats.Add(TeamManager.Instance.teamB_Ids[i], new BowlerStats { runsScoredAgainst = 0, wicketsTaken = 0, oversCompleted = 0 });
            }
        }
        else
        {
            count = TeamManager.Instance.teamA_Ids.Count;
            for (int i = 0; i < count; i++)
            {
                bowlingTeamStats.Add(TeamManager.Instance.teamA_Ids[i], new BowlerStats { runsScoredAgainst = 0, wicketsTaken = 0, oversCompleted = 0 });
            }
        }
        
        teamB_index = 0;
        teamA_index = 0;
        totalRuns = 0;
        wickets = 0;

        gameUIScript.SetBatsmanStats(0, 0);

        ResetRunSelection();
        gameUIScript.SetRunsWicketUI(totalRuns.ToString(), wickets.ToString());
        gameUIScript.SetScoreStatus("");
        if (currentMatchState == MatchState.Inning2) gameUIScript.SetOverText(false, true, -1);     //this changes the teamAvsteamB so skip it at first reset at start

        //REMOVE THIS BECAUSE TEAMS DONT CHANGE SEATS AFTER INNINGS SO NO NEED TO CHANGE UI POSITIONS
        gameUIScript.TeamSideSet(currentMatchState == MatchState.Inning2 ? TeamManager.Instance.teamB_Ids.Contains(NetworkManager.LocalClientId) : TeamManager.Instance.teamA_Ids.Contains(NetworkManager.LocalClientId));
        gameUIScript.LocalPlayerStatusSideSet(currentMatchState);
    }


    bool skipFirstChangeSeatCall;
    bool batsmanLeftDuringSimulation;
    void ResetRunSelection()
    {
        //reset inputs
        batsmanRun = null;
        bowlerRun = null;

        string batsmanName = "Batsman";
        string bowlerName = "Bowler";
        ulong bowlerId;
        bool isBatsman = false;
        //reset hand animations
        HandAnimationsSetter(-1, -1);

        //display next players names and stats
        if (currentMatchState == MatchState.Inning1)
        {
            bowlerId = TeamManager.Instance.teamB_Ids[teamB_index];
            batsmanName = TeamManager.Instance.playerNames[TeamManager.Instance.teamA_Ids[teamA_index]];
            bowlerName = TeamManager.Instance.playerNames[TeamManager.Instance.teamB_Ids[teamB_index]];
            if(TeamManager.Instance.teamA_Ids.Contains(NetworkManager.Singleton.LocalClientId)) isBatsman = true;
        }
        else
        {
            bowlerId = TeamManager.Instance.teamA_Ids[teamA_index];
            batsmanName = TeamManager.Instance.playerNames[TeamManager.Instance.teamB_Ids[teamB_index]];
            bowlerName = TeamManager.Instance.playerNames[TeamManager.Instance.teamA_Ids[teamA_index]];
            if (TeamManager.Instance.teamB_Ids.Contains(NetworkManager.Singleton.LocalClientId)) isBatsman = true;
        }

        gameUIScript.SetBatsmanName(batsmanName.Split(',')[0]);
        gameUIScript.SetBowlerName(bowlerName.Split(',')[0]);

        gameUIScript.SetBowlerStats(bowlingTeamStats[bowlerId].runsScoredAgainst, bowlingTeamStats[bowlerId].oversCompleted.ToString(), bowlingTeamStats[bowlerId].wicketsTaken);


        if (batsmanLeftDuringSimulation)
        {
            BatsmanLeftChanges();       //syncing stuff when batsman leaves in middle of over
            batsmanLeftDuringSimulation = false;
        }

        if (skipFirstChangeSeatCall) gameUIScript.ChangePlayerSeats();       //need to skip first call as when game starts, UI script handles it with spawning chars
        skipFirstChangeSeatCall = true;

        if (TeamManager.Instance.teamA_Ids[teamA_index] == NetworkManager.LocalClientId || TeamManager.Instance.teamB_Ids[teamB_index] == NetworkManager.LocalClientId)
        {
            gameUIScript.ResetUI(isBatsman);     //reset run selection UI for active player 
        }
        else
        {
            gameUIScript.DisableUI();   //disable run selection UI for spectator
        }
    }

    void BatsmanLeftChanges()
    {
        wickets++;      //so that UI change
        currentBatsmanBalls = 0;
        currentBatsmanRuns = 0;
        gameUIScript.SetRunsWicketUI(totalRuns.ToString(), wickets.ToString());
        int totalBatsman = currentMatchState == MatchState.Inning1 ? TeamManager.Instance.teamA_Ids.Count : TeamManager.Instance.teamB_Ids.Count;
        int batsmanIndex = currentMatchState == MatchState.Inning1 ? teamA_index : teamB_index;
        if (batsmanIndex >= totalBatsman)
        {
            if(currentMatchState == MatchState.Inning1)
            {
                targetRuns = totalRuns + 1;
                currentMatchState = MatchState.Inning2;
                gameUIScript.SetTargetRuns(targetRuns);
                ResetInning();
            }
            else
            {
                currentMatchState = MatchState.MatchOver;
                MatchWinnerCheck();
            }
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
            handAText.text = batsmanHandString;
            handBText.text = bowlerHandString;
        }
        else
        {
            teamAHand.Play(bowlerHandString);
            teamBHand.Play(batsmanHandString);
            handAText.text = bowlerHandString;
            handBText.text = batsmanHandString;
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

    public (int, int) CurrentActivePlayersGetter()
    {
        return (teamA_index, teamB_index);
    }

    public void HandlePlayerRemoved(bool teamA, int index, int teamCount)
    {
        if (teamCount == 0 && NetworkManager.IsServer) 
        {
            //GameOver and send everyone back to lobby with popup
            //add here when gameover logic is made
        }
        if (teamCount == 0 && !NetworkManager.IsServer) return;
        bool didActivePlayerLeave = teamA ? index == teamA_index : index == teamB_index;
        bool isBattingTeam = (currentMatchState == MatchState.Inning1 && teamA) ||
                         (currentMatchState == MatchState.Inning2 && !teamA);

        if (teamA)
        {
            //if removed player was before the active player, list goes down by 1 for everyone after
            if (index < teamA_index)
            {
                teamA_index--;
            }
            //if removed player was after the active player or the active player, list still goes down by 1 but the next player goes to active index
            
        }
        else
        {
            if (index < teamB_index)
            {
                teamB_index--;
            }
        }

        //bowler leaves in middle of simulation we still decrement because when over ends naturally, it will correct the index by adding or reseting to 0
        if (!isBattingTeam && didActivePlayerLeave && overResultDisplayPhase)
        {
            if(teamA)teamA_index--;
            else teamB_index--;
        }
        //for when last indexed player leaves, we reset it to 0 again for ResetRunSelection 
        if (!overResultDisplayPhase)
        {
            if (teamA && teamA_index >= teamCount) teamA_index = 0;
            else if (!teamA && teamB_index >= teamCount) teamB_index = 0;
        }
        
        if (!overResultDisplayPhase && didActivePlayerLeave)
        { //only reset UI if not already in simulation 
            ResetRunSelection(); 
        }    
        else if (overResultDisplayPhase && didActivePlayerLeave && isBattingTeam)       //so current batsman left during simulation
        {
            batsmanLeftDuringSimulation = true;     //this is used in resetrunselection to bring in new batsman after over
        }
    }
}
public enum MatchState
{
    Inning1,
    Inning2,
    MatchOver
}
public class BowlerStats 
{
    public int runsScoredAgainst;
    public int wicketsTaken;
    public int oversCompleted;
}

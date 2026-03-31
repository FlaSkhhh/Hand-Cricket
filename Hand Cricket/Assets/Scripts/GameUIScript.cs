using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameUIScript : MonoBehaviour
{
    [SerializeField] GameManager gameManager;
    [Header("Teams Stuff")]
    [SerializeField] Transform teamALineups;
    [SerializeField] Transform teamBLineups;
    [SerializeField] Transform mainGameTable;
    [SerializeField] Transform teamAHotSeat;
    [SerializeField] Transform teamBHotSeat;
    [SerializeField] TextMeshPro boardText;

    [Header("GameObjects")]
    [SerializeField] GameObject runSelectionParent;
    [SerializeField] GameObject runSelectionGroupGO;
    [SerializeField] GameObject localPlayerStatusGO;
    [SerializeField] GameObject selectionButtonsPopup;
    [SerializeField] GameObject loadingScreen;

    [Header("Buttons")]
    [SerializeField] Button submitRun;
    [SerializeField] Button[] runButtons;
    [SerializeField] Button[] selectionButtons;

    [Header("Sprites")]
    [SerializeField] Sprite[] runDiesSprites;

    [Header("Texts")]
    [SerializeField] TextMeshProUGUI teamNames;
    [SerializeField] TextMeshProUGUI totalRunsText;
    [SerializeField] TextMeshProUGUI targetRunsText;
    [SerializeField] TextMeshProUGUI currentOverText;
    [SerializeField] TextMeshProUGUI currentBallStatusText;
    [SerializeField] TextMeshProUGUI bowlerStatsText;
    [SerializeField] TextMeshProUGUI bowlerNameText;
    [SerializeField] TextMeshProUGUI batsmanNameText;
    [SerializeField] TextMeshProUGUI batsmanStatsText;

    [Header("Animations")]
    [SerializeField] Animator runSelectionBarAnimator;
    [SerializeField] Animator[] diceAnimators;

    Image selectedButtonImage;
    bool changeColour;
    float hue;
    int overNumber;
    int runButtonSelectedIndex;

    bool isBattingTeam;

    int[] run = new int[6] {1,1,1,1,1,1};

    public void LoadingScreenStatus(bool status)
    {
        loadingScreen.SetActive(status);
    }

    void Awake()
    {
        for (int i = 0; i < runButtons.Length; i++) 
        {
            int index = i;
            runButtons[i].onClick.AddListener(() => BallButtonClicked(index));
        }

        for (int j = 0; j < runButtons.Length; j++)
        {
            int indexj = j;
            selectionButtons[j].onClick.AddListener(() => SelectRunForBall(indexj));
        }
        submitRun.onClick.AddListener(SubmitRunSelectionFromUI);
        selectedButtonImage = runButtons[0].transform.parent.GetComponent<Image>();
        selectionButtonsPopup.SetActive(false);
        targetRunsText.text = "1st INNING";
        teamNames.text = TeamManager.Instance.teamBName.Value.ToString() + " vs " + "<size=150%>" + TeamManager.Instance.teamAName.Value.ToString() + "</size>";
        boardText.text = TeamManager.Instance.teamAName.Value.ToString() + " vs " + TeamManager.Instance.teamBName.Value.ToString();    //board has team A to left and team B to right
        overNumber = 0;
        SpawnPlayerCharactersToSeat();      //only spawn once as teams dont change so players will sit on same side but UI will change
    }

    async void SpawnPlayerCharactersToSeat()
    {
        await TeamManager.Instance.SpawnPlayerCharactersInGameScene();
        ChangePlayerSeats();
    }

    public void ChangePlayerSeats()
    {
        int counter = 0;        //counter for 4 chairs for supporting guys
        (int teamAIndex, int teamBIndex) = gameManager.CurrentActivePlayersGetter();
        for (int i = 0; i < TeamManager.Instance.teamA_Ids.Count; i++)
        {
            if (i == teamAIndex)        //current player on table
            {
                Transform pos = TeamManager.Instance.playerCharacters[TeamManager.Instance.teamA_Ids[i]].transform;
                pos.parent = teamAHotSeat;
                pos.localPosition = Vector3.zero;
                pos.localRotation = Quaternion.Euler(Vector3.zero);
                pos.GetComponent<Animator>().SetTrigger("sit");
            }
            else
            {
                Transform pos = TeamManager.Instance.playerCharacters[TeamManager.Instance.teamA_Ids[i]].transform;
                pos.parent = teamALineups.GetChild(counter);
                pos.localPosition = new Vector3(0.1f, 0.2f, 0);
                pos.rotation = Quaternion.LookRotation(Vector3.Normalize(mainGameTable.position - pos.position));
                pos.GetComponent<Animator>().SetTrigger("sit");
                counter++;
            }
            //adding a bit of offset to get butt on chair
        }
        counter = 0;
        for (int i = 0; i < TeamManager.Instance.teamB_Ids.Count; i++)
        {
            if (i == teamBIndex) 
            {
                Transform bos = TeamManager.Instance.playerCharacters[TeamManager.Instance.teamB_Ids[i]].transform;
                bos.parent = teamBHotSeat;
                bos.localPosition = Vector3.zero;
                bos.localRotation = Quaternion.Euler(Vector3.zero);
                bos.GetComponent<Animator>().SetTrigger("sit");
            }
            else
            {
                Transform bos = TeamManager.Instance.playerCharacters[TeamManager.Instance.teamB_Ids[i]].transform;
                bos.parent = teamBLineups.GetChild(counter);
                bos.localPosition = new Vector3(-0.375f, 0.2f, 0);
                bos.rotation = Quaternion.LookRotation(Vector3.Normalize(mainGameTable.position - bos.position));
                bos.GetComponent<Animator>().SetTrigger("sit");
                counter++;
            }
        }
    }

    public void TeamSideSet(bool isBatting)
    {
        isBattingTeam = isBatting;
        RectTransform rectTransform = runSelectionGroupGO.GetComponent<RectTransform>();
        Vector2 pos = rectTransform.anchoredPosition;
        pos.x = Mathf.Abs(pos.x);
        if (isBattingTeam)                                  //batting team will get balls runs section on left
        {
            rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
            rectTransform.anchorMax = new Vector2(0, rectTransform.anchorMax.y);    //changing anchors to left side
        }
        else
        {
            pos.x *= -1;                                                            
            rectTransform.anchorMin = new Vector2(1, rectTransform.anchorMin.y);    //to right side
            rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
        } 
        runSelectionGroupGO.GetComponent<RectTransform>().anchoredPosition = pos;
        RunSelectionPopupScaling(isBatting);
    }

    void RunSelectionPopupScaling(bool leftSide)
    {
        Transform parent = selectionButtonsPopup.transform.GetChild(0);     //this is parent of all 6 run dice buttons
        if (leftSide)
        {
            foreach(Transform child in parent)
            {
                child.localScale = new(1, 1, 1);
            }
        }
        else
        {
            foreach (Transform child in parent)
            {
                child.localScale = new(-1, -1, -1);
            }
        }
    }

    public void LocalPlayerStatusSideSet(MatchState state)
    {
        RectTransform rectTransform = localPlayerStatusGO.GetComponent<RectTransform>();
        bool rightSide = false;

        if (state == MatchState.Inning1 && TeamManager.Instance.teamA_Ids.Contains(NetworkManager.Singleton.LocalClientId)) rightSide = true;
       
        if (state == MatchState.Inning2 && TeamManager.Instance.teamB_Ids.Contains(NetworkManager.Singleton.LocalClientId)) rightSide= true;

        if (rightSide)
        {
            rectTransform.anchorMin = new(1, 1);
            rectTransform.anchorMax = new(1, 1);
            rectTransform.anchoredPosition = new(-5, 0);
            rectTransform.pivot = new(1, 1);
        }
        else
        {
            rectTransform.anchorMin = new(0, 1);
            rectTransform.anchorMax = new(0, 1);
            rectTransform.anchoredPosition = new(5, 0);
            rectTransform.pivot = new(0, 1);
        }
    }

    public void DisableLocalPlayerStatus()
    {
        localPlayerStatusGO.SetActive(false);
    }

    void Update()
    {
        if (changeColour)
        {
            hue = (Time.time * 0.4f) % 1.0f;
            selectedButtonImage.color = Color.HSVToRGB(hue, 1, 1);
        }
    }

    void BallButtonClicked(int buttonIndex)
    {
        selectedButtonImage.color = Color.black;        //reset previous button colour

        selectionButtonsPopup.SetActive(true);
        if(isBattingTeam) runSelectionBarAnimator.Play("RunSelectionBarOpen", -1, 0f);
        else runSelectionBarAnimator.Play("RunSelectionBarOpenRight", -1, 0f);
        selectionButtonsPopup.transform.SetParent(runButtons[buttonIndex].transform.parent,false);

        foreach (Animator anim in diceAnimators) {
            anim.Play("Default Button State", -1, 0f);      //reset animator state
        }
        foreach (Button diceButt in selectionButtons)
        {
            diceButt.interactable = true;                   //reenable buttons
        }
        runButtonSelectedIndex = buttonIndex;
        selectedButtonImage = runButtons[runButtonSelectedIndex].transform.parent.GetComponent<Image>();
        changeColour = true;
    }

    void SelectRunForBall(int runS)
    {
        run[runButtonSelectedIndex] = runS + 1;
        runButtons[runButtonSelectedIndex].image.sprite = runDiesSprites[runS];
        diceAnimators[runS].SetTrigger("DiceButtonPress");
        foreach(Button diceButt in selectionButtons)
        {
            diceButt.interactable = false;  
        }
    }

    void SubmitRunSelectionFromUI()
    {
        changeColour = false;
        selectedButtonImage.color = Color.black;
        selectionButtonsPopup.SetActive(false);
        gameManager.SubmitRunSelection(run);
        runSelectionParent.SetActive(false);
        //submitRun.gameObject.SetActive(false);
    }

    public void SetRunsWicketUI(string total, string wickets)
    {
        totalRunsText.text = total+"-"+wickets;
    }

    public void SetScoreStatus(string status)
    {
        currentBallStatusText.gameObject.SetActive(true);
        currentBallStatusText.text = status;
        Invoke(nameof(DisableStatusText),1.5f);
    }

    void DisableStatusText()
    {
        currentBallStatusText.gameObject.SetActive(false);
        currentBallStatusText.text = string.Empty;
    }

    public void SetOverText(bool wicket, bool inningReset, int ballNo)  //this over number and texts are reset during main coroutine 
    {
        if (inningReset) 
        { 
            currentOverText.text = "0"; overNumber = 0;     //also change batting team name as larger to Team B
            teamNames.text = TeamManager.Instance.teamAName.Value.ToString() + " vs " + "<size=150%>" + TeamManager.Instance.teamBName.Value.ToString() + "</size>";
            return; 
        } 

        if (!wicket)
        {
            if (ballNo < 6) { currentOverText.text = overNumber.ToString() + "." + ballNo.ToString(); }
            else { overNumber++; currentOverText.text = overNumber.ToString(); }
        }
        else
        {
            overNumber++;
            currentOverText.text = overNumber.ToString();
        }
    }

    public void SetBatsmanName(string name)
    {
        batsmanNameText.text = name;
    }

    public void SetBatsmanStats(int runs, int balls)
    {
        batsmanStatsText.text = runs.ToString() + "  " + "<size=70%>"+balls.ToString()+"</size>";
    }

    public void SetBowlerName(string name)
    {
        bowlerNameText.text = name; 
    }

    public void SetBowlerStats(int runsA, string overs, int wicketsT)
    {
        bowlerStatsText.text = runsA.ToString() + "-" + wicketsT.ToString() + "  " + overs;
    }

    public void SetTargetRuns(int targetR)
    {
        targetRunsText.text = "TARGET " + targetR.ToString();
    }

    public void DisableRunSelectionPopup()
    {
        runSelectionBarAnimator.SetTrigger("RunSelectionBarClose");
        changeColour = false;
        selectedButtonImage.color = Color.black;
    }

    public void ResetUI(bool isBatsman)
    {
        run = new int[6] { 1, 1, 1, 1, 1, 1 };
        foreach(Button butt in runButtons)
        {
            butt.image.sprite = runDiesSprites[0];
        }
        submitRun.gameObject.SetActive(true);
        runSelectionParent.SetActive(true);
        localPlayerStatusGO.SetActive(true);
        string status = isBatsman ? "You are batting. Select your input..." : "You are bowling. Select your input...";
        localPlayerStatusGO.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = status;
    }

    public void DisableUI()
    {
        runSelectionParent.SetActive(false);
        submitRun.gameObject.SetActive(false);
        localPlayerStatusGO.SetActive(true);
        localPlayerStatusGO.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Players are selecting their input...";
    }
}

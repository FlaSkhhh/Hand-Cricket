using TMPro;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public class GameUIScript : MonoBehaviour
{
    [SerializeField] GameManager gameManager;

    [Header("GameObjects")]
    [SerializeField] GameObject runSelectionParent;
    [SerializeField] GameObject runSelectionGroupGO;
    [SerializeField] GameObject selectionButtonsPopup;

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
    [SerializeField] TextMeshProUGUI batsmaNameText;
    [SerializeField] TextMeshProUGUI batsmaStatsText;

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
        overNumber = 0;
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

    public void SetOpponentRun(int i)
    {
        //opponentSelectedRun.text = i.ToString();
    }

    public void SetMatchUI(string status, string total, string wickets)
    {
        currentBallStatusText.text = status;
        totalRunsText.text = total+"-"+wickets;
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
        batsmaNameText.text = name;
    }

    public void SetBatsmanStats(int runs, int balls)
    {
        batsmaStatsText.text = runs.ToString() + "  " + "<size=70%>"+balls.ToString()+"</size>";
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

    public void ResetUI()
    {
        run = new int[6] { 1, 1, 1, 1, 1, 1 };
        foreach(Button butt in runButtons)
        {
            butt.image.sprite = runDiesSprites[0];
        }
        //opponentSelectedRun.text = "-";
        submitRun.gameObject.SetActive(true);
        runSelectionParent.SetActive(true);
        currentBallStatusText.text = " ";
    }

    public void DisableUI()
    {
        runSelectionParent.SetActive(false);
        submitRun.gameObject.SetActive(false);
    }
}

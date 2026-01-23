using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUIScript : MonoBehaviour
{
    [SerializeField] GameManager gameManager;

    [SerializeField] Button submitRun;

    [SerializeField] Button[] runButtons;

    [SerializeField] GameObject selectionButtonsPopup;
    [SerializeField] Button[] selectionButtons;
    [SerializeField] Sprite[] runDiesSprites;

    [SerializeField] TextMeshProUGUI opponentSelectedRun;
    [SerializeField] TextMeshProUGUI totalRunsText;
    [SerializeField] TextMeshProUGUI targetRunsText;
    [SerializeField] TextMeshProUGUI currentBallStatusText;

    int runButtonSelectedIndex;

    int[] run = new int[6] {1,1,1,1,1,1};

    void Start()
    {
        for (int i = 0; i < runButtons.Length; i++) 
        {
            int index = i;
            runButtons[i].onClick.AddListener(() => RunButtonClicked(index));
        }

        for (int j = 0; j < runButtons.Length; j++)
        {
            int indexj = j;
            selectionButtons[j].onClick.AddListener(() => SelectRunForBall(indexj));
        }
        submitRun.onClick.AddListener(SubmitRunSelectionFromUI);
        selectionButtonsPopup.SetActive(false);
    }

    void RunButtonClicked(int buttonIndex)
    {
        selectionButtonsPopup.SetActive(true);
        runButtonSelectedIndex = buttonIndex;
    }

    void SelectRunForBall(int runS)
    {
        run[runButtonSelectedIndex] = runS + 1;
        runButtons[runButtonSelectedIndex].image.sprite = runDiesSprites[runS];
        selectionButtonsPopup.SetActive(false);
    }

    void SubmitRunSelectionFromUI()
    {
        selectionButtonsPopup.SetActive(false);
        gameManager.SubmitRunSelection(run);
        submitRun.gameObject.SetActive(false);
    }

    public void SetOpponentRun(int i)
    {
        //opponentSelectedRun.text = i.ToString();
    }

    public void SetMatchUI(string status, string total)
    {
        currentBallStatusText.text = status;
        totalRunsText.text = total;
    }

    public void ResetUI()
    {
        run = new int[6] { 1, 1, 1, 1, 1, 1 };
        foreach(Button butt in runButtons)
        {
            butt.interactable = true;
            butt.image.sprite = runDiesSprites[0];
        }
        //opponentSelectedRun.text = "-";
        submitRun.gameObject.SetActive(true);
        currentBallStatusText.text = " ";
    }

    public void DisableUI()
    {
        foreach (Button butt in runButtons)
        {
            butt.interactable = false;
        }
        submitRun.gameObject.SetActive(false);
    }
}

using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WebSocketSharp;

public class LobbyUIScript : MonoBehaviour
{
    [SerializeField] LobbyPrefabScript lobbyPrefab;

    [SerializeField] GameObject startUIPage;
    [SerializeField] GameObject lobbySearchUIPage;
    [SerializeField] GameObject lobbyUIPage;
    [SerializeField] GameObject mainBench;

    [SerializeField] GameObject lobbySearhingScrollViewPrefab;

    [SerializeField] Button createLobby;
    [SerializeField] Button searchLobby;
    [SerializeField] Button refreshLobby;
    [SerializeField] Button startGame;
    [SerializeField] Button changeTeams;

    [SerializeField] Button lobbySearchBack;
    [SerializeField] Button lobbyBack;

    [SerializeField] Transform lobbyDisplayContent;

    [SerializeField] TMP_InputField nameField;

    [SerializeField] Transform[] teamALineups; 
    [SerializeField] Transform[] teamBLineups;
    [SerializeField] TextMeshProUGUI teamAName;
    [SerializeField] TextMeshProUGUI teamBName;
    [SerializeField] Transform teamAUIListContent;
    [SerializeField] Transform teamBUIListContent;
    [SerializeField] GameObject teamAPrefab;
    [SerializeField] GameObject teamBPrefab;

    [SerializeField] Animator startupLoadingAnimator;
    [SerializeField] GameObject loadingScreen;

    [SerializeField] GameObject popupGO;
    int maxPlayers;

    void Awake()
    {
        Application.targetFrameRate = 61;
        startupLoadingAnimator.gameObject.SetActive(true);
        loadingScreen.SetActive(false);     //removing loading because new startup loading is used for signin 
        mainBench.SetActive(true);
    }

    void Start()
    {
        startUIPage.SetActive(true);
        lobbySearchUIPage.SetActive(false);
        lobbyUIPage.SetActive(false);
        createLobby.onClick.AddListener(CreateLobby);
        searchLobby.onClick.AddListener(SearchLobby);
        refreshLobby.onClick.AddListener(RefreshLobbies);
        startGame.onClick.AddListener(StartGameHost);
        lobbySearchBack.onClick.AddListener(() => { lobbySearchUIPage.SetActive(false); startUIPage.SetActive(true); mainBench.SetActive(true); });
        lobbyBack.onClick.AddListener(LeaveLobby);
        nameField.onEndEdit.AddListener(NameChanged);
        changeTeams.onClick.AddListener(ChangeTeams);

        LobbyManager.Instance.PlayerSignedIn += PlayerNameChange;

        LobbyManager.Instance.LineupPositionsSetter(teamALineups, teamBLineups);
        LobbyManager.Instance.LoadingScreenSetter(loadingScreen);
    }

    void PlayerNameChange()
    {
        nameField.text = LobbyManager.Instance.PlayerNameGetter();
        startupLoadingAnimator.SetTrigger("LoadingComplete");
    }

    async void CreateLobby()
    {
        maxPlayers = 10;
        LobbyManager.Instance.LoadingScreenStatus(true);
        bool completion = await LobbyManager.Instance.CreateLobby(maxPlayers);
        mainBench.SetActive(!completion);

        startUIPage.SetActive(!completion);
        lobbyUIPage.SetActive(completion);
        startGame.gameObject.SetActive(completion);
        LobbyManager.Instance.LoadingScreenStatus(false);
    }

    async void SearchLobby()
    {
        LobbyManager.Instance.LoadingScreenStatus(true);
        ClearLobbyContent();
        startUIPage.SetActive(false);
        lobbySearchUIPage.SetActive(true);
        Instantiate(lobbySearhingScrollViewPrefab,lobbyDisplayContent);
        QueryResponse response = await LobbyManager.Instance.SearchLobby();
        mainBench.SetActive(false);
        if (response != null) DisplayLobbies(response);
        LobbyManager.Instance.LoadingScreenStatus(false);
    }
    
    void ClearLobbyContent()
    {
        foreach (Transform child in lobbyDisplayContent)
        {
            Destroy(child.gameObject);
        }
    }

    void DisplayLobbies(QueryResponse response)
    {
        ClearLobbyContent();
        for (int i = 0; i < response.Results.Count; i++)
        {
            GameObject go = Instantiate(lobbyPrefab.gameObject, lobbyDisplayContent);
            int aSlots = response.Results[i].AvailableSlots;
            int tSlots = response.Results[i].MaxPlayers;
            string lobbyName = response.Results[i].Name;
            string slots = (tSlots - aSlots).ToString()+"/"+tSlots.ToString();
            go.GetComponent<LobbyPrefabScript>().SetLobbyPrefab(lobbyName, slots, response.Results[i].Id, this, response.Results[i].IsLocked);
        }    
    }

    void NameChanged(string name)
    {
        if (!name.IsNullOrEmpty())
        {
            LobbyManager.Instance.PlayerNameSetter(name);
        }
        else
        {
            nameField.text = LobbyManager.Instance.PlayerNameGetter();
        }
    }

    void StartGameHost()
    {
        LobbyManager.Instance.StartGame();
    }

    async void RefreshLobbies()
    {
        LobbyManager.Instance.LoadingScreenStatus(true);
        ClearLobbyContent();
        Instantiate(lobbySearhingScrollViewPrefab, lobbyDisplayContent);
        QueryResponse response = await LobbyManager.Instance.SearchLobby();
        if(response != null) DisplayLobbies(response);
        LobbyManager.Instance.LoadingScreenStatus(false);
    }

    async void LeaveLobby()
    {
        LobbyManager.Instance.LoadingScreenStatus(true);
        bool result = await LobbyManager.Instance.LeaveLobby();
        lobbyUIPage.SetActive(!result);
        startUIPage.SetActive(result);
        mainBench.SetActive(result);
        LobbyManager.Instance.LoadingScreenStatus(false);
    }

    public void JoinedLobby()
    {
        lobbySearchUIPage.SetActive(false);
        lobbyUIPage.SetActive(true);
        startGame.gameObject.SetActive(false);
        Debug.Log("Joined Lobby!");
    }

    void ChangeTeams()
    {
        if (TeamManager.Instance.teamA_Ids.Contains(NetworkManager.Singleton.LocalClientId))
        {
            if (TeamManager.Instance.teamB_Ids.Count >= 6) { PopupSetter("ERROR", "TEAM IS ALREADY FULL!"); return; }
        }
        else
        {
            if (TeamManager.Instance.teamA_Ids.Count >= 6) { PopupSetter("ERROR", "TEAM IS ALREADY FULL!"); return; }
        }
        TeamManager.Instance.ChangeTeamForPlayerRpc(NetworkManager.Singleton.LocalClientId);
        changeTeams.interactable = false;
        Invoke(nameof(ChangeTeamsButtonTimeout), 2f);
    }

    public void TeamsUIListUpdate()
    {
        int counter = 0;
        foreach(Transform child in teamAUIListContent) 
        {
            if (TeamManager.Instance.teamA_Ids.Count > counter)
            {
                ulong cId = TeamManager.Instance.teamA_Ids[counter];
                child.gameObject.SetActive(true);

                if (TeamManager.Instance.playerNames.TryGetValue(cId, out string fullName))
                {
                    child.GetChild(0).GetComponent<TextMeshProUGUI>().text = fullName.Split(',')[0];
                }
                else
                {
                    child.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Joining...";      //for times when client has been added on list but name has not been synced
                }
            }
            else
            {
                child.gameObject.SetActive(false);
            }
            counter++;
        }
        counter = 0;
        foreach(Transform child in teamBUIListContent) 
        {
            if (TeamManager.Instance.teamB_Ids.Count > counter)
            {
                ulong cId = TeamManager.Instance.teamB_Ids[counter];
                child.gameObject.SetActive(true);

                if (TeamManager.Instance.playerNames.TryGetValue(cId, out string fullName))
                {
                    child.GetChild(0).GetComponent<TextMeshProUGUI>().text = fullName.Split(',')[0];
                }
                else
                {
                    child.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Joining...";
                }
            }
            else
            {
                child.gameObject.SetActive(false);
            }
            counter++;
        }
    }

    public void TeamsNameUpdate()
    {
        teamAName.text = TeamManager.Instance.teamAName.Value.ToString();
        teamBName.text = TeamManager.Instance.teamBName.Value.ToString();
    }

    void ChangeTeamsButtonTimeout()
    {
        changeTeams.interactable = true;
    }

    public void StartupLoadingComplete()
    {
        Destroy(startupLoadingAnimator.gameObject);
    }

    void PopupSetter(string headerT, string bodyT)
    {
        GameObject go = Instantiate(popupGO);
        go.GetComponent<PopupScript>().PopupActivation(headerT, bodyT);
    }
}

using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUIScript : MonoBehaviour
{
    [SerializeField] LobbyPrefabScript lobbyPrefab;

    [SerializeField] GameObject startUIPage;
    [SerializeField] GameObject lobbySearchUIPage;
    [SerializeField] GameObject lobbyUIPage;

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

    [SerializeField] GameObject loadingScreen;

    int maxPlayers;

    void Start()
    {
        Application.targetFrameRate = 61;

        startUIPage.SetActive(true);
        lobbySearchUIPage.SetActive(false);
        lobbyUIPage.SetActive(false);
        createLobby.onClick.AddListener(CreateLobby);
        searchLobby.onClick.AddListener(SearchLobby);
        refreshLobby.onClick.AddListener(RefreshLobbies);
        startGame.onClick.AddListener(StartGameHost);
        lobbySearchBack.onClick.AddListener(() => { lobbySearchUIPage.SetActive(false); startUIPage.SetActive(true); });
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
    }

    async void CreateLobby()
    {
        maxPlayers = 10;
        LobbyManager.Instance.LoadingScreenStatus(true);
        bool completion = await LobbyManager.Instance.CreateLobby(maxPlayers);

        startUIPage.SetActive(!completion);
        lobbyUIPage.SetActive(completion);
        startGame.gameObject.SetActive(true);
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
        if(response != null) DisplayLobbies(response);
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
        LobbyManager.Instance.PlayerNameSetter(name);
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
        bool result = await LobbyManager.Instance.LeaveLobby();
        lobbyUIPage.SetActive(false);
        startUIPage.SetActive(true);
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
        TeamManager.Instance.ChangeTeamForPlayerRpc(NetworkManager.Singleton.LocalClientId);
        changeTeams.interactable = false;
        Invoke(nameof(ChangeTeamsButtonTimeout), 2f);
    }

    void ChangeTeamsButtonTimeout()
    {
        changeTeams.interactable = true;
    }
}

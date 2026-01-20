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

    [SerializeField] Button lobbySearchBack;
    [SerializeField] Button lobbyBack;

    [SerializeField] Transform lobbyDisplayContent;

    void Start()
    {
        startUIPage.SetActive(true);
        lobbySearchUIPage.SetActive(false);
        lobbyUIPage.SetActive(false);
        createLobby.onClick.AddListener(CreateLobby);
        searchLobby.onClick.AddListener(SearchLobby);
        refreshLobby.onClick.AddListener(RefreshLobbies);
        startGame.onClick.AddListener(StartGameHost);
        lobbySearchBack.onClick.AddListener(() => { lobbySearchUIPage.SetActive(false); startUIPage.SetActive(true); });
        lobbyBack.onClick.AddListener(LeaveLobby);
    }

    async void CreateLobby()
    {
        bool completion = await LobbyManager.Instance.CreateLobby(4,"TestLobby");

        startUIPage.SetActive(!completion);
        lobbyUIPage.SetActive(completion);
        startGame.gameObject.SetActive(true);
    }

    async void SearchLobby()
    {
        ClearLobbyContent();
        startUIPage.SetActive(false);
        lobbySearchUIPage.SetActive(true);
        Instantiate(lobbySearhingScrollViewPrefab,lobbyDisplayContent);
        QueryResponse response = await LobbyManager.Instance.SearchLobby();
        if(response != null) DisplayLobbies(response);
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
            string slots = (tSlots - aSlots).ToString()+"/"+tSlots.ToString();
            go.GetComponent<LobbyPrefabScript>().SetLobbyPrefab(response.Results[i].Name, slots, response.Results[i].Id, this, response.Results[i].IsLocked);
        }    
    }

    void StartGameHost()
    {
        LobbyManager.Instance.StartGame();
    }

    async void RefreshLobbies()
    {
        ClearLobbyContent();
        Instantiate(lobbySearhingScrollViewPrefab, lobbyDisplayContent);
        QueryResponse response = await LobbyManager.Instance.SearchLobby();
        if(response != null) DisplayLobbies(response);
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
}

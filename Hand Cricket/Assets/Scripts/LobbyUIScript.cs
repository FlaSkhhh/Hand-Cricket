using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUIScript : MonoBehaviour
{

    [SerializeField] GameObject playerListContainer;
    [SerializeField] GameObject playerNamePrefab;

    [SerializeField] Button createLobby;
    [SerializeField] Button searchLobby;

    async void Start()
    {
        await UnityServices.InitializeAsync();

        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        createLobby.onClick.AddListener(CreateLobby);
        searchLobby.onClick.AddListener(SearchLobby);
    }

    async void CreateLobby()
    {
        try
        {
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync("Test", 4);
            Debug.Log("Lobby Created Code "+lobby.LobbyCode);
            LobbyEventCallbacks callbacks = new LobbyEventCallbacks();
            await LobbyService.Instance.SubscribeToLobbyEventsAsync(lobby.Id, callbacks);
            Debug.Log("Lobby Callback Event Subbed");
            callbacks.PlayerJoined += (List<LobbyPlayerJoined> pj) => { Debug.Log("Player Joined " + pj[0].Player.Id); };

        }
        catch(LobbyServiceException ex)
        {
            Debug.LogError(ex);
        }
    }

    async void SearchLobby()
    {
        QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();
        Debug.Log(response.Results[0].LobbyCode + " "+ response.Results[0].Id);     //lobby code is private share only code not available to clients only for host
        Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(response.Results[0].Id);
    }

}

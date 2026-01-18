using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPrefabScript : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI lobbyNameT;
    [SerializeField] TextMeshProUGUI playerCountT;
    [SerializeField] Button joinLobbyButton;

    LobbyUIScript lobbyUIScript;

    string lobbyIdString;

    public void SetLobbyPrefab(string name,string playerSlots,string lobbyId,LobbyUIScript reference, bool isLocked)
    {
        lobbyNameT.text = name;
        playerCountT.text = playerSlots;
        lobbyIdString = lobbyId;
        lobbyUIScript = reference;

        if(!isLocked)joinLobbyButton.onClick.AddListener(JoinLobby);
        else joinLobbyButton.gameObject.SetActive(false);
    }

    async void JoinLobby()
    {
        bool result = await LobbyManager.Instance.JoinLobby(lobbyIdString);

        if (result) lobbyUIScript.JoinedLobby();

    }
}

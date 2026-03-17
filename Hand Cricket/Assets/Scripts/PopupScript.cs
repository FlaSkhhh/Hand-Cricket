using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupScript : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI header;
    [SerializeField] TextMeshProUGUI body;

    [SerializeField] Button closeButton;

    void Start()
    {
        closeButton.onClick.AddListener(() => { Destroy(gameObject); });
    }

    public void PopupActivation(string headerT, string bodyT)
    {
        header.text = headerT;
        body.text = bodyT;
    }
}

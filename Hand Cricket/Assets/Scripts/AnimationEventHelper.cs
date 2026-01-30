using UnityEngine;

public class AnimationEventHelper : MonoBehaviour
{
    [SerializeField] GameUIScript gameUIScript;
    public void DisableGameObject()
    {
        gameObject.SetActive(false);
    }

    public void DisableRunSelectionPopup()
    {
        gameUIScript.DisableRunSelectionPopup();
    }

   /* void OnDisable()
    {
        gameObject.GetComponent<Animator>().Rebind();
        gameObject.GetComponent<Animator>().Update(0);
    }*/
}

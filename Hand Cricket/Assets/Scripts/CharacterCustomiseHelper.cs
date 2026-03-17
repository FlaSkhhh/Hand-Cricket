using NUnit.Framework;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class CharacterCustomiseHelper : MonoBehaviour
{
    Material colourMaterial;
    Material faceMaterial;
    [SerializeField] Transform accessoryTransform;
    [SerializeField] Transform mesh;

    [SerializeField] Animator animator;
    [SerializeField] TextMeshPro playerText;

    Coroutine movementCor;
    float movementSpeed = 0.15f;

    void Awake()
    {
        colourMaterial = mesh.GetComponent<SkinnedMeshRenderer>().materials[0];
        faceMaterial = mesh.GetComponent<SkinnedMeshRenderer>().materials[1];
    }

    public CharacterCustomisationClass CharacterCustomisationGetter()
    {
        return new CharacterCustomisationClass() {colour = colourMaterial,face = faceMaterial, accessory = accessoryTransform};
    }
    
    public void IsLocalPlayer()
    {
        playerText.gameObject.SetActive(true);
    }

    public void MoveToTeamLineup(Vector3 pos)
    {
        if (movementCor != null) StopCoroutine(movementCor);
        movementCor = StartCoroutine(MovementCoroutine(pos));
    }

    IEnumerator MovementCoroutine(Vector3 pos)
    {
        transform.rotation = Quaternion.LookRotation(pos-transform.position);
        animator.ResetTrigger("idle");
        animator.ResetTrigger("run");
        animator.SetTrigger("run");
        while(Vector3.Distance(transform.position,pos) >= 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, pos, movementSpeed);
            yield return new WaitForFixedUpdate(); 
        }
        animator.ResetTrigger("run");
        animator.SetTrigger("idle");
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, 0) - new Vector3(transform.position.x, 0, 0));
    }

}
public class CharacterCustomisationClass
{
    public Material colour;
    public Material face;
    public Transform accessory;
}
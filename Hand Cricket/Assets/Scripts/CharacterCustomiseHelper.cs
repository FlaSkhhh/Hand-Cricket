using NUnit.Framework;
using UnityEngine;

public class CharacterCustomiseHelper : MonoBehaviour
{
    Material colourMaterial;
    Material faceMaterial;
    [SerializeField] Transform accessoryTransform;
    [SerializeField] Transform mesh;

    void Awake()
    {
        colourMaterial = mesh   .GetComponent<SkinnedMeshRenderer>().materials[0];
        faceMaterial = mesh.GetComponent<SkinnedMeshRenderer>().materials[1];
    }

    public CharacterCustomisationClass CharacterCustomisationGetter()
    {
        return new CharacterCustomisationClass() {colour = colourMaterial,face = faceMaterial, accessory = accessoryTransform };
    }

}
public class CharacterCustomisationClass
{
    public Material colour;
    public Material face;
    public Transform accessory;
}
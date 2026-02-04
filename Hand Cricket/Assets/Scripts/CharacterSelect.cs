using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelect : MonoBehaviour
{
    [SerializeField] GameObject characterPrefab;
    [SerializeField] Texture[] characterFaces;      //3 faces
    Material characterFaceMat;
    Material characterColourMat;
    Transform accessoriesParent;

    [SerializeField] TMP_Dropdown characterAccessoriesSelect;     //4 total with 3 hats
    [SerializeField] TMP_Dropdown characterFaceSelect;     
    [SerializeField] Slider characterColourSelect;

    void Awake()
    {
        characterFaceSelect.onValueChanged.AddListener(FaceChanged);
        characterColourSelect.onValueChanged.AddListener(ColourChanged);
        characterAccessoriesSelect.onValueChanged.AddListener(AccessoryChanged);
    }
    void Start()
    {
        bool hasCustomChar = PlayerPrefs.HasKey("CustomCharacter");
        if(hasCustomChar) GetCharacterFromValues(PlayerPrefs.GetString("CustomCharacter").ToString());

        GameObject guy = Instantiate(characterPrefab,new Vector3(-1,-5.5f,-3),Quaternion.Euler(0,180,0));
        CharacterCustomisationClass vals = guy.GetComponent<CharacterCustomiseHelper>().CharacterCustomisationGetter();
        characterColourMat = vals.colour;
        characterFaceMat = vals.face;
        accessoriesParent = vals.accessory;
        int face = Random.Range(0, 3);
        int accessory = Random.Range(0, 4);
        int hue = Random.Range(0, 360);
        string key = $"{face}{accessory}{hue}";
        GetCharacterFromValues(key);
    }

    void GetCharacterFromValues(string key)
    {
        int face = 0;
        int accessory = 0;
        float hue = 0;
        face = int.Parse(key[0].ToString());
        accessory = int.Parse(key[1].ToString());
        int length = key.Length;
        hue = float.Parse(key.Substring(2,length-3))/360f;
        characterFaceMat.mainTexture = characterFaces[face];
        characterColourMat.color = Color.HSVToRGB(hue,1,1);
        foreach(Transform child in accessoriesParent)
        {
            child.gameObject.SetActive(false);
        }
        if(accessory > 0) accessoriesParent.GetChild(accessory-1).gameObject.SetActive(true);       //0 for no accessory
    }

    void FaceChanged(int value)
    {
        characterFaceMat.mainTexture = characterFaces[value];
    }
    
    void ColourChanged(float value)
    {
        characterColourSelect.image.color = Color.HSVToRGB(value,1,1);
        characterColourMat.color = Color.HSVToRGB(value, 1, 1);
    }
    
    void AccessoryChanged(int value)
    {
        foreach (Transform child in accessoriesParent)
        {
            child.gameObject.SetActive(false);
        }
        if (value > 0) accessoriesParent.GetChild(value - 1).gameObject.SetActive(true);       //0 for no accessory
    }
}

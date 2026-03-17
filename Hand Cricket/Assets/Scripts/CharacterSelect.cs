using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelect : MonoBehaviour
{
    public GameObject characterPrefab;
    [SerializeField] Button characterImage;
    [SerializeField] GameObject customisationMenu;

    [SerializeField] Texture[] characterFaces;      //3 faces
    Material characterFaceMat;
    Material characterColourMat;
    Transform accessoriesParent;

    [SerializeField] TMP_Dropdown characterAccessoriesSelect;     //4 total with 3 hats
    [SerializeField] TMP_Dropdown characterFaceSelect;     
    [SerializeField] Slider characterColourSelect;

    [SerializeField] Image[] bgSprites;

    GameObject customisablePrefabClone;

    public static CharacterSelect instance;

    void Awake()
    {
        instance = this;
        characterImage.onClick.AddListener(CustomisationMenu);
        characterFaceSelect.onValueChanged.AddListener(FaceChanged);
        characterColourSelect.onValueChanged.AddListener(ColourChanged);
        characterAccessoriesSelect.onValueChanged.AddListener(AccessoryChanged);
        customisationMenu.SetActive(false);
    }

    void Start()
    {
        InstantiateCustomisationCharacter();

        float[] uiValues = new float[3];
        if (PlayerPrefs.HasKey("CustomCharacter"))
        {
            uiValues = GetCharacterFromValues(PlayerPrefs.GetString("CustomCharacter").ToString());
        }
        else
        {
            //first time random charactet
            int face = Random.Range(0, 3);
            int accessory = Random.Range(0, 4);
            int hue = Random.Range(0, 360);

            string key = $"{face}{accessory}{hue}";
            //set first character as pref
            PlayerPrefs.SetString("CustomCharacter",key);

            uiValues = GetCharacterFromValues(key);
        }
        characterFaceSelect.value = (int)uiValues[0];
        characterColourSelect.value = uiValues[1];
        characterAccessoriesSelect.value = (int)uiValues[2];
    }

    void InstantiateCustomisationCharacter()
    {
        GameObject guy = Instantiate(characterPrefab, new Vector3(0,-10,0),Quaternion.Euler(0,180,0));
        CharacterCustomisationClass vals = guy.GetComponent<CharacterCustomiseHelper>().CharacterCustomisationGetter();
        //asign variables 
        characterFaceMat = vals.face;
        characterColourMat = vals.colour;
        accessoriesParent = vals.accessory;
        customisablePrefabClone = guy;  
        //remove shadows from customisation character
        foreach(Transform child in accessoriesParent)
        {
            child.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
    //for lineup
    public GameObject SpawnPlayerCharacters(string key)
    {
        GameObject guy = Instantiate(characterPrefab, new Vector3(Random.Range(-2, 2), -5.5f, Random.Range(0, -4)), Quaternion.Euler(0, 90, 0));
        CharacterCustomisationClass vals = guy.GetComponent<CharacterCustomiseHelper>().CharacterCustomisationGetter();

        float[] custom = GetCharacterFromValues(key);
        vals.face.mainTexture = characterFaces[(int)custom[0]];
        vals.colour.color = Color.HSVToRGB(custom[1], 1, 1);
        foreach (Transform child in vals.accessory)
        {
            child.gameObject.SetActive(false);
        }
        if ((int)custom[2] > 0) vals.accessory.GetChild((int)custom[2] - 1).gameObject.SetActive(true);       //0 for no accessory
        return guy;
    }

    float[] GetCharacterFromValues(string key)
    {
        int face = 0;
        int accessory = 0;
        float hue = 0;

        face = int.Parse(key[0].ToString());
        accessory = int.Parse(key[1].ToString());
        int length = key.Length;
        hue = float.Parse(key.Substring(2));

        return new float[] { face, hue/360f, accessory };
  
    }

    void FaceChanged(int value)
    {
        //changing pref here because its already set by the time we get here
        StringBuilder val = new StringBuilder(PlayerPrefs.GetString("CustomCharacter"));
        val[0] = (char)('0' + value);
        PlayerPrefs.SetString("CustomCharacter", val.ToString());

        characterFaceMat.mainTexture = characterFaces[value];
    }
    
    void ColourChanged(float value)
    {
        string key = PlayerPrefs.GetString("CustomCharacter").Substring(0,2);
        string newKey = key + Mathf.CeilToInt(value*360).ToString();
        PlayerPrefs.SetString("CustomCharacter", newKey);

        characterColourSelect.image.color = Color.HSVToRGB(value,1,1);
        foreach(Image image in bgSprites)
        {
            image.color = Color.HSVToRGB(value, 0.75f, 0.75f);    //background image for selection of face and hat
        }
        characterColourMat.color = Color.HSVToRGB(value, 1, 1);
    }
    
    void AccessoryChanged(int value)
    {
        StringBuilder val = new StringBuilder(PlayerPrefs.GetString("CustomCharacter"));
        val[1] = (char)('0' + value);

        PlayerPrefs.SetString("CustomCharacter", val.ToString());

        foreach (Transform child in accessoriesParent)
        {
            child.gameObject.SetActive(false);
        }
        if (value > 0) accessoriesParent.GetChild(value - 1).gameObject.SetActive(true);       //0 for no accessory
    }

    void CustomisationMenu()
    {
        if (customisationMenu.activeInHierarchy)
        {
            customisationMenu.SetActive(false);
        }
        else
        {
            customisationMenu.SetActive(true);
        }
    }

    public void DestroyCustomisablePrefabClone()
    {
        Destroy(customisablePrefabClone);
    }
}

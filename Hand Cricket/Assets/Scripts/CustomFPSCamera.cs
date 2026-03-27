using UnityEngine;

public class CustomFPSCamera : MonoBehaviour
{
   
    [Tooltip("Target framerate for the choppy anime look (e.g., 12 or 8)")]
    public int targetFPS = 12;

    Camera uiCamera;
    private float timer;

    void Start()
    {
        uiCamera = GetComponent<Camera>();
        uiCamera.enabled = false;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float timePerFrame = 1f / targetFPS;

        if (timer >= timePerFrame)
        {
            uiCamera.Render();
            timer -= timePerFrame;
        }
    }
}

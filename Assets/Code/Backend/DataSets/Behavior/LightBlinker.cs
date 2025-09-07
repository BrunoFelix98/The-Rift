using UnityEngine;

public class LightBlinker : MonoBehaviour
{
    public float blinkInterval = 1f;
    private Light lightComponent;
    private float timer;

    void Start()
    {
        lightComponent = GetComponent<Light>();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= blinkInterval)
        {
            lightComponent.enabled = !lightComponent.enabled;
            timer = 0f;
        }
    }
}
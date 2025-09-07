using UnityEngine;

public class LightPulser : MonoBehaviour
{
    public float pulseSpeed = 1f;
    public float minIntensity = 0.5f;
    public float maxIntensity = 2f;
    private Light lightComponent;

    void Start()
    {
        lightComponent = GetComponent<Light>();
    }

    void Update()
    {
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
        lightComponent.intensity = Mathf.Lerp(minIntensity, maxIntensity, pulse);
    }
}
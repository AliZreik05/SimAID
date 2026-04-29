using UnityEngine;

public class ChestBreathingController : MonoBehaviour
{
    [Header("Breathing Settings")]
    [SerializeField] private float normalSpeed = 1.5f;
    [SerializeField] private float distressSpeed = 3.5f;

    [SerializeField] private float normalIntensity = 0.01f;
    [SerializeField] private float distressIntensity = 0.035f;

    [Header("Irregular Breathing")]
    [SerializeField] private bool useIrregularity = true;
    [SerializeField] private float irregularityAmount = 0.25f;
    [SerializeField] private float irregularitySpeed = 2f;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource breathingAudio;

    private Vector3 originalScale;
    private bool isActive = false;
    private bool isDistress = false;

    private float currentSpeed;
    private float currentIntensity;

    private void Awake()
    {
        originalScale = transform.localScale;
        SetNormal();
    }

    private void OnDisable()
    {
        ResetBreathing();
    }

    private void Update()
    {
        if (!isActive)
            return;

        float breath = Mathf.Sin(Time.time * currentSpeed);

        if (useIrregularity && isDistress)
        {
            float noise = Mathf.PerlinNoise(Time.time * irregularitySpeed, 0f);
            breath += (noise - 0.5f) * irregularityAmount;
        }

        breath *= currentIntensity;

        transform.localScale = originalScale + new Vector3(
            0f,
            breath,
            breath
        );
    }

    public void SetNormal()
    {
        isDistress = false;
        currentSpeed = normalSpeed;
        currentIntensity = normalIntensity;
    }

    public void SetDistress()
    {
        isDistress = true;
        currentSpeed = distressSpeed;
        currentIntensity = distressIntensity;
    }

    public void StartBreathing(bool distress)
    {
        isActive = true;

        if (distress)
            SetDistress();
        else
            SetNormal();

        if (distress && breathingAudio != null && !breathingAudio.isPlaying)
            breathingAudio.Play();
    }

    public void StopBreathing()
    {
        isActive = false;

        if (breathingAudio != null && breathingAudio.isPlaying)
            breathingAudio.Stop();

        ResetBreathing();
    }

    private void ResetBreathing()
    {
        transform.localScale = originalScale;
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.XR;

public class ModeSelector : MonoBehaviour
{
    [SerializeField] private GameObject desktopPlayer;
    [SerializeField] private GameObject vrRig;
    [SerializeField] private float xrActivationTimeout = 3f;

    private Coroutine modeRoutine;

    private void OnEnable()
    {
        modeRoutine = StartCoroutine(SelectModeWhenXRIsReady());
    }

    private void OnDisable()
    {
        if (modeRoutine != null)
            StopCoroutine(modeRoutine);

        modeRoutine = null;
    }

    private IEnumerator SelectModeWhenXRIsReady()
    {
        ApplyMode(ShouldUseVRMode());

        float startTime = Time.realtimeSinceStartup;

        while (!ShouldUseVRMode() && Time.realtimeSinceStartup - startTime < xrActivationTimeout)
            yield return null;

        ApplyMode(ShouldUseVRMode());
    }

    private void ApplyMode(bool vrActive)
    {
        if (desktopPlayer != null)
            desktopPlayer.SetActive(!vrActive);

        if (vrRig != null)
            vrRig.SetActive(vrActive);

        Debug.Log("VR Active: " + vrActive);
    }

    private static bool ShouldUseVRMode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return XRSettings.isDeviceActive;
#endif
    }
}

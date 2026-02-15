using UnityEngine;
using UnityEngine.XR;

public class ModeSelector : MonoBehaviour
{
    [SerializeField] private GameObject desktopPlayer;
    [SerializeField] private GameObject vrRig;

    void Start()
    {
        bool vrActive = XRSettings.isDeviceActive;

        desktopPlayer.SetActive(!vrActive);
        vrRig.SetActive(vrActive);

        Debug.Log("VR Active: " + vrActive);
    }
}

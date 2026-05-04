using UnityEngine;

/// <summary>
/// Attaches <see cref="VRCPRInteraction"/> to the CPR_Target GameObject on every scene
/// load, so it works in both the Unity Editor and VR runtime without manual setup.
/// VRRuntimeCompatibility also does this in VR mode, so the null-check prevents duplicates.
/// </summary>
public static class VRCPRAutoInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject target = GameObject.Find("CPR_Target");
        if (target == null || target.GetComponent<VRCPRInteraction>() != null)
            return;

        target.AddComponent<VRCPRInteraction>();
        Debug.Log("[VRCPRAutoInstaller] VRCPRInteraction attached to CPR_Target.");
    }
}

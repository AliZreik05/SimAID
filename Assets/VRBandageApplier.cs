using UnityEngine;

public class VRBandageApplier : MonoBehaviour
{
    private bool used = false;

    private void OnTriggerEnter(Collider other)
    {
        if (used) return;

        var wound = other.GetComponent<WoundInteractable>();
        if (wound != null)
        {
            used = true;

            // Trigger same logic as desktop
            wound.ApplyBandageVR();

            // Destroy or disable bandage
            gameObject.SetActive(false);
        }
    }
}
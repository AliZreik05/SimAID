using UnityEngine;

public class VRConePlacement : MonoBehaviour
{
    private bool placed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (placed) return;

        if (other.CompareTag("ConeZone"))
        {
            placed = true;

            FindFirstObjectByType<ConePlacer>()?.AddCone(1);

            gameObject.SetActive(false);
        }
    }
}
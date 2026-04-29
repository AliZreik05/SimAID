using UnityEngine;

public class VRConePlacement : MonoBehaviour
{
    private bool placed = false;
    [SerializeField] private ConePlacer conePlacer;

    private void OnTriggerEnter(Collider other)
    {
        if (placed) return;

        if (other.CompareTag("ConeZone"))
        {
            placed = true;

            // Step 1: Snap to ground
            SnapToGround();

            // Step 2: Align upright
            AlignUpright();

            // Step 3: Notify GameLoop
            if (conePlacer != null)
{
    conePlacer.RegisterConePlaced();
}

            // Step 4: Freeze
            LockCone();
        }
    }

    private void SnapToGround()
    {
        RaycastHit hit;

        // Cast ray downward from current position
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 2f))
        {
            transform.position = hit.point;
        }
    }

    private void AlignUpright()
    {
        // Keep only Y rotation (remove tilt)
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void LockCone()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null)
        {
            grab.enabled = false;
        }
    }
}
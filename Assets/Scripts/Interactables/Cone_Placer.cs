using UnityEngine;

public class ConePlacer : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject coneWorldPrefab; // real cone (collider + Traffic_Cone)
    [SerializeField] private GameObject coneGhostPrefab; // preview cone (no collider, white/transparent)

    [Header("Placement")]
    [SerializeField] private Camera cam;
    [SerializeField] private float placeDistance = 6f;
    [SerializeField] private LayerMask groundMask;

    [Header("Keys")]
    [SerializeField] private KeyCode placeKey = KeyCode.E;
    [SerializeField] private KeyCode confirmKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode cancelKey = KeyCode.Q;

    [Header("Debug")]
    [SerializeField] private bool autoStartPlacementOnPickup = true;

    private int coneCount;
    private GameObject ghost;
    private bool placing;
private bool hasLastHit;
private Vector3 lastPos;
private Vector3 lastNormal;

    public int ConeCount => coneCount;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    public void AddCone(int amount)
    {
        coneCount = Mathf.Max(0, coneCount + amount);

        // Immediately enter placement mode after pickup (optional)
        if (autoStartPlacementOnPickup && !placing && coneCount > 0)
            BeginPlacement();
    }

    private void Update()
    {
        // Manual start placement (if you don't auto-start or you cancelled)
        if (!placing && coneCount > 0 && Input.GetKeyDown(placeKey))
            BeginPlacement();

        if (!placing) return;

        UpdateGhostPosition();

        if (Input.GetKeyDown(confirmKey))
            ConfirmPlacement();

        if (Input.GetKeyDown(cancelKey))
            CancelPlacement();
    }

    private void BeginPlacement()
    {
        hasLastHit = false;

        if (coneGhostPrefab == null)
        {
            Debug.LogError("ConePlacer: Cone Ghost Prefab is not assigned.", this);
            return;
        }

        if (groundMask.value == 0)
        {
            Debug.LogWarning("ConePlacer: Ground Mask is 'Nothing' so raycast will never hit. Set it to your Ground layer.", this);
        }

        placing = true;

        if (ghost == null)
            ghost = Instantiate(coneGhostPrefab);

        ghost.SetActive(true);
    }

   private void UpdateGhostPosition()
{
    if (ghost == null) return;

    Ray ray = new Ray(cam.transform.position, cam.transform.forward);

    if (Physics.Raycast(ray, out RaycastHit hit, placeDistance, groundMask))
    {
        hasLastHit = true;
        lastPos = hit.point;
        lastNormal = hit.normal;
    }

    // Always apply last known good placement so it doesn't "freeze"
    if (hasLastHit)
    {
        ghost.transform.position = lastPos;
        ghost.transform.up = lastNormal;
    }
}


    private void ConfirmPlacement()
{
    if (ghost == null || coneWorldPrefab == null) return;
    if (!hasLastHit) return;

    Instantiate(coneWorldPrefab, ghost.transform.position, ghost.transform.rotation);

    coneCount = Mathf.Max(0, coneCount - 1);

    if (coneCount <= 0)
        CancelPlacement();
}

    private void CancelPlacement()
    {
        hasLastHit = false;

        placing = false;

        if (ghost != null)
        {
            Destroy(ghost);
            ghost = null;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class InspectionObjectController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera inspectionCamera;
    [SerializeField] private InspectionManager inspectionManager;

    [Header("Rotation Speed")]
    [SerializeField] private float chestRotationSpeed = 0.3f;
    [SerializeField] private float handRotationSpeed = 1f;

    [Header("Chest Rotation - Left/Right Only")]
    [SerializeField] private float chestMinYaw = -60f;
    [SerializeField] private float chestMaxYaw = 60f;

    [Header("Hand Rotation - Up/Down Only")]
    [SerializeField] private float handMinPitch = -360f;
    [SerializeField] private float handMaxPitch = 360f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minFov = 25f;
    [SerializeField] private float maxFov = 55f;
    [SerializeField] private float defaultFov = 40f;

    private Transform currentTarget;
    private InspectionPartType currentPart;

    private Vector3 lockedLocalPosition;
    private Quaternion startLocalRotation;
    private Vector3 startLocalScale;

    private float yaw;
    private float pitch;

    public void SetTarget(Transform target, InspectionPartType partType)
    {
        currentTarget = target;
        currentPart = partType;

        if (currentTarget == null)
            return;

        lockedLocalPosition = currentTarget.localPosition;
        startLocalRotation = currentTarget.localRotation;
        startLocalScale = currentTarget.localScale;

        yaw = 0f;
        pitch = 0f;

        if (inspectionCamera != null)
            inspectionCamera.fieldOfView = defaultFov;
    }

    public void ClearTarget()
    {
        currentTarget = null;
    }

    private void Update()
    {
        if (inspectionManager == null || !inspectionManager.IsInspecting)
            return;

        if (currentTarget == null)
            return;

        if (Mouse.current == null || Keyboard.current == null)
            return;

        // Keep the object fixed in place during inspection.
        currentTarget.localPosition = lockedLocalPosition;
        currentTarget.localScale = startLocalScale;

        HandleRotation();
        HandleZoom();
        HandleReset();
    }

    private void HandleRotation()
    {
        if (!Mouse.current.leftButton.isPressed)
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();

        if (currentPart == InspectionPartType.Chest)
        {
            // Chest: drag left/right only.
            yaw += delta.x * chestRotationSpeed;
            yaw = Mathf.Clamp(yaw, chestMinYaw, chestMaxYaw);

            currentTarget.localRotation =
                startLocalRotation * Quaternion.Euler(0f, yaw, 0f);
        }
        else if (currentPart == InspectionPartType.Hand)
        {
            // Hand: drag up/down only.
            pitch -= delta.y * handRotationSpeed;
            pitch = Mathf.Clamp(pitch, handMinPitch, handMaxPitch);

            currentTarget.localRotation =
                startLocalRotation * Quaternion.Euler(pitch, 0f, 0f);

            /*
             * If the hand rotates around the wrong axis, replace the line above with:
             *
             * Y axis:
             * currentTarget.localRotation =
             *     startLocalRotation * Quaternion.Euler(0f, pitch, 0f);
             *
             * Z axis:
             * currentTarget.localRotation =
             *     startLocalRotation * Quaternion.Euler(0f, 0f, pitch);
             */
        }
    }

    private void HandleZoom()
    {
        if (inspectionCamera == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        inspectionCamera.fieldOfView -= scroll * zoomSpeed * Time.deltaTime * 20f;

        inspectionCamera.fieldOfView = Mathf.Clamp(
            inspectionCamera.fieldOfView,
            minFov,
            maxFov
        );
    }

    private void HandleReset()
    {
        if (!Keyboard.current.rKey.wasPressedThisFrame)
            return;

        yaw = 0f;
        pitch = 0f;

        if (currentTarget != null)
        {
            currentTarget.localPosition = lockedLocalPosition;
            currentTarget.localRotation = startLocalRotation;
            currentTarget.localScale = startLocalScale;
        }

        if (inspectionCamera != null)
            inspectionCamera.fieldOfView = defaultFov;
    }
}
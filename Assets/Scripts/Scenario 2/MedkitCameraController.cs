using UnityEngine;
using UnityEngine.InputSystem;

public class MedkitCameraController : MonoBehaviour
{
    [SerializeField] private Camera medkitCamera;
    [SerializeField] private float rotationSpeed = 0.12f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minFov = 20f;
    [SerializeField] private float maxFov = 60f;

    [Header("Rotation Limits")]
    [SerializeField] private float minYaw = -24f;
    [SerializeField] private float maxYaw = 24f;
    [SerializeField] private float minPitch = -15f;
    [SerializeField] private float maxPitch = 15f;

    private Vector3 startEuler;
    private float yaw;
    private float pitch;

    private void Awake()
    {
        if (medkitCamera == null)
            medkitCamera = GetComponent<Camera>();

        startEuler = transform.localEulerAngles;
    }

    private void OnEnable()
    {
        yaw = 0f;
        pitch = 0f;
        transform.localEulerAngles = startEuler;
    }

    private void Update()
    {
        if (medkitCamera == null || !medkitCamera.enabled)
            return;

        if (Mouse.current == null)
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();

        yaw += delta.x * rotationSpeed;
        pitch -= delta.y * rotationSpeed;

        yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(
            startEuler.x + pitch,
            startEuler.y + yaw,
            startEuler.z
        );

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            medkitCamera.fieldOfView -= scroll * zoomSpeed * Time.deltaTime * 20f;
            medkitCamera.fieldOfView = Mathf.Clamp(medkitCamera.fieldOfView, minFov, maxFov);
        }
    }
}
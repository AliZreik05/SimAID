using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Camera cam;
    private float xRotation = 0f;
    public float xSensitivity = 30;
    public float ySensitivity = 30;

    public Transform playerBody;   // ADD THIS

    public void ProcessLook(Vector2 input)
    {
        float mouseX = input.x;
        float mouseY = input.y;

        xRotation -= mouseY * Time.deltaTime * ySensitivity;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        // Vertical rotation (camera only)
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        // Horizontal rotation (rotate Player root)
        playerBody.Rotate(Vector3.up * mouseX * Time.deltaTime * xSensitivity);
    }
}

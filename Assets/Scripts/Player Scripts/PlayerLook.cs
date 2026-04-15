using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Camera cam;
    public Transform playerBody;

    private float xRotation = 0f;

    public float xSensitivity = 30f;
    public float ySensitivity = 30f;

    private bool forceLookActive = false;
    private Transform forcedLookTarget;
    private float forcedLookSpeed = 2.5f;

    public void ProcessLook(Vector2 input)
    {
        if (forceLookActive)
            return;

        float mouseX = input.x;
        float mouseY = input.y;

        xRotation -= mouseY * Time.deltaTime * ySensitivity;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX * Time.deltaTime * xSensitivity);
    }

    public void BeginForcedLook(Transform target, float speed = 2.5f)
    {
        if (target == null)
            return;

        forceLookActive = true;
        forcedLookTarget = target;
        forcedLookSpeed = speed;
    }

    public void EndForcedLook()
    {
        forceLookActive = false;
        forcedLookTarget = null;

        Vector3 euler = cam.transform.localEulerAngles;
        if (euler.x > 180f)
            euler.x -= 360f;

        xRotation = euler.x;
    }

    private void LateUpdate()
    {
        if (!forceLookActive || forcedLookTarget == null || cam == null || playerBody == null)
            return;

        Vector3 dirToTarget = forcedLookTarget.position - cam.transform.position;
        if (dirToTarget.sqrMagnitude < 0.001f)
            return;

        Quaternion targetCamRot = Quaternion.LookRotation(dirToTarget.normalized);
        cam.transform.rotation = Quaternion.Slerp(
            cam.transform.rotation,
            targetCamRot,
            forcedLookSpeed * Time.deltaTime
        );

        Vector3 bodyDir = forcedLookTarget.position - playerBody.position;
        bodyDir.y = 0f;

        if (bodyDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetBodyRot = Quaternion.LookRotation(bodyDir.normalized);
            playerBody.rotation = Quaternion.Slerp(
                playerBody.rotation,
                targetBodyRot,
                forcedLookSpeed * Time.deltaTime
            );
        }
    }
}
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Camera fallbackCam;

    [Header("Interaction Distance")]
    [SerializeField] private float gameplayDistance = 3f;
    [SerializeField] private float medkitDistance = 10f;

    [SerializeField] private LayerMask mask;

    private PlayerUI playerUI;
    private InputManager inputManager;

    void Start()
    {
        PlayerLook look = GetComponent<PlayerLook>();
        if (look != null)
            fallbackCam = look.cam;

        playerUI = GetComponent<PlayerUI>();
        inputManager = GetComponent<InputManager>();
    }

    void Update()
    {
        Camera currentCamera = Camera.main != null ? Camera.main : fallbackCam;
        if (currentCamera == null)
            return;

        bool inMedkitView = currentCamera.name == "MedkitCamera";
        float currentDistance = inMedkitView ? medkitDistance : gameplayDistance;

        if (playerUI != null)
            playerUI.updateText(string.Empty);

        Ray ray = new Ray(currentCamera.transform.position, currentCamera.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * currentDistance, Color.green);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, currentDistance, mask))
        {
            Interactable interactable = hitInfo.collider.GetComponent<Interactable>();

            if (interactable == null)
                interactable = hitInfo.collider.GetComponentInParent<Interactable>();

            if (interactable != null)
            {
                // Hide the patient prompt once questioning has started
                PatientAssessmentStarter patientStarter = interactable as PatientAssessmentStarter;
                if (patientStarter != null && !patientStarter.CanBeInteractedWith)
                    return;

                if (playerUI != null && !inMedkitView)
                    playerUI.updateText(interactable.promptMessage);

                if (inputManager != null && inputManager.OnFoot.Interact.triggered)
                    interactable.baseInteract();
            }
        }
    }
}
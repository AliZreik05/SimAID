using UnityEngine;
using TMPro;

public class InspectionManager : MonoBehaviour
{
    [Header("Scenario")]
    [SerializeField] private MedicalScenarioManager scenarioManager;

    [Header("Cameras")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera inspectionCamera;

    [Header("Inspection Objects")]
    [SerializeField] private GameObject inspectionBackground;
    [SerializeField] private GameObject chestInspectionObject;
    [SerializeField] private GameObject handInspectionObject;
    [Header("Actual Meshes To Rotate")]
[SerializeField] private Transform chestMeshToRotate;
[SerializeField] private Transform handMeshToRotate;

    [Header("Camera Placement")]
[SerializeField] private float chestDistanceFromCamera = 4f;
[SerializeField] private Vector3 chestViewOffset = new Vector3(0f, -1.2f, 0f);
[SerializeField] private Vector3 chestViewRotation = new Vector3(0f, 180f, 0f);

[SerializeField] private float handDistanceFromCamera = 3f;
[SerializeField] private Vector3 handViewOffset = new Vector3(0f, 0f, 0f);
[SerializeField] private Vector3 handViewRotation = Vector3.zero;

    [Header("Optional World Prompt")]
    [SerializeField] private GameObject interactionPromptUI;

    [Header("Inspection UI")]
    [SerializeField] private GameObject inspectionUI;
    [SerializeField] private TMP_Text inspectionTitleText;
    [SerializeField] private TMP_Text inspectionBodyText;
    [SerializeField] private TMP_Text inspectionExitHintText;

    [Header("Gameplay UI To Hide")]
    [SerializeField] private GameObject objectiveBannerUI;

    [Header("Interaction")]
    [SerializeField] private InspectionObjectController inspectionObjectController;

    [Header("Controls")]
    [SerializeField] private KeyCode exitKey = KeyCode.Escape;

    [Header("Display Anchors")]
    [SerializeField] private Transform chestAnchor;
    [SerializeField] private Transform handAnchor;

    private bool isInspecting = false;
    private InspectionPartType currentPart;

    public bool IsInspecting => isInspecting;

    private void Start()
    {
        if (inspectionCamera != null)
        {
            inspectionCamera.gameObject.SetActive(false);
            inspectionCamera.enabled = false;
        }

        if (inspectionBackground != null)
            inspectionBackground.SetActive(false);

        if (chestInspectionObject != null)
            chestInspectionObject.SetActive(false);

        if (handInspectionObject != null)
            handInspectionObject.SetActive(false);

        if (inspectionUI != null)
            inspectionUI.SetActive(false);
    }

    private void Update()
    {
        if (!isInspecting)
            return;

        if (Input.GetKeyDown(exitKey))
            ExitInspection();
    }

    public void OpenInspection(InspectionPartType partType)
    {
        if (scenarioManager == null)
            return;

        if (!scenarioManager.CanInspect)
            return;

        if (isInspecting)
            return;

        currentPart = partType;
        isInspecting = true;

        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);

        if (objectiveBannerUI != null)
            objectiveBannerUI.SetActive(false);

        if (mainCamera != null)
            mainCamera.enabled = false;

        if (inspectionCamera != null)
        {
            inspectionCamera.gameObject.SetActive(true);
            inspectionCamera.enabled = true;
        }

        if (inspectionBackground != null)
            inspectionBackground.SetActive(true);

        if (inspectionUI != null)
            inspectionUI.SetActive(true);

        if (partType == InspectionPartType.Chest)
        {
            if (handInspectionObject != null)
                handInspectionObject.SetActive(false);

            if (chestInspectionObject != null)
            {
                chestInspectionObject.SetActive(true);

                PlaceObjectInFrontOfCamera(
    chestInspectionObject,
    chestDistanceFromCamera,
    chestViewOffset,
    chestViewRotation
);

                if (inspectionObjectController != null)
                   if (chestMeshToRotate != null)
    inspectionObjectController.SetTarget(chestMeshToRotate, InspectionPartType.Chest);
else
    inspectionObjectController.SetTarget(chestInspectionObject.transform, InspectionPartType.Chest);
            }
        }
        else
        {
            if (chestInspectionObject != null)
                chestInspectionObject.SetActive(false);

            if (handInspectionObject != null)
            {
                handInspectionObject.SetActive(true);

                PlaceObjectInFrontOfCamera(
    handInspectionObject,
    handDistanceFromCamera,
    handViewOffset,
    handViewRotation
);

                if (inspectionObjectController != null)
                   if (handMeshToRotate != null)
    inspectionObjectController.SetTarget(handMeshToRotate, InspectionPartType.Hand);
else
    inspectionObjectController.SetTarget(handInspectionObject.transform, InspectionPartType.Hand);
            }
        }

        string title = partType == InspectionPartType.Chest ? "Chest Inspection" : "Hand Inspection";
        string clue = scenarioManager.GetInspectionClue(partType);

        if (inspectionTitleText != null)
            inspectionTitleText.text = title;

        if (inspectionBodyText != null)
            inspectionBodyText.text = clue;

        if (inspectionExitHintText != null)
            inspectionExitHintText.text = "Press Esc to exit";

        scenarioManager.EnterInspectionView(partType);
    }

    public void ExitInspection()
    {
        if (!isInspecting)
            return;

        isInspecting = false;

        if (inspectionCamera != null)
        {
            inspectionCamera.enabled = false;
            inspectionCamera.gameObject.SetActive(false);
        }

        if (mainCamera != null)
            mainCamera.enabled = true;

        if (inspectionBackground != null)
            inspectionBackground.SetActive(false);

        if (inspectionUI != null)
            inspectionUI.SetActive(false);

        if (chestInspectionObject != null)
            chestInspectionObject.SetActive(false);

        if (handInspectionObject != null)
            handInspectionObject.SetActive(false);

        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(true);

        if (objectiveBannerUI != null)
            objectiveBannerUI.SetActive(true);

        if (inspectionObjectController != null)
            inspectionObjectController.ClearTarget();

        scenarioManager.ExitInspectionView(currentPart);
    }
    private void PlaceObjectInFrontOfCamera(GameObject obj, float distance, Vector3 offset, Vector3 eulerRotation)
{
    if (obj == null || inspectionCamera == null)
        return;

    Transform cam = inspectionCamera.transform;

    obj.transform.position =
        cam.position +
        cam.forward * distance +
        cam.right * offset.x +
        cam.up * offset.y;

    obj.transform.rotation = cam.rotation * Quaternion.Euler(eulerRotation);
}
}
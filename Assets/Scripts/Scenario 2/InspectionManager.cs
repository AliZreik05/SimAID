using UnityEngine;
using TMPro;

public class InspectionManager : MonoBehaviour
{
    [Header("Scenario")]
    [SerializeField] private MedicalScenarioManager scenarioManager;

    [Header("Cameras")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera inspectionCamera;
    [Header("Respiratory Distress Animation")]
[SerializeField] private ChestBreathingController chestBreathingController;

    [Header("Inspection Objects")]
    [SerializeField] private GameObject inspectionBackground;
    [SerializeField] private GameObject chestInspectionObject;
    [SerializeField] private GameObject handInspectionObject;

private bool chestInspected = false;
private bool handInspected = false;

public bool ChestInspected => chestInspected;
public bool HandInspected => handInspected;

    [Header("Actual Meshes To Rotate")]
    [SerializeField] private Transform chestMeshToRotate;
    [SerializeField] private Transform handMeshToRotate;

    [Header("Optional GameObject Visual Findings")]
    [Tooltip("Use this if hand rash is made of separate objects/spots.")]
    [SerializeField] private GameObject handRashVisuals;

    [Tooltip("Use this if chest redness is made of a separate overlay object.")]
    [SerializeField] private GameObject chestRednessVisuals;

    [SerializeField] private GameObject respiratoryDistressVisuals;

    [Header("Material-Based Hand Visuals")]
    [Tooltip("Renderer whose material changes for normal/rash/pale hand.")]
    [SerializeField] private Renderer handRenderer;

    [SerializeField] private int handMaterialIndex = 0;
    [SerializeField] private Material handNormalMaterial;
    [SerializeField] private Material handRashMaterial;
    [SerializeField] private Material handPaleMaterial;

    [Header("Material-Based Chest Visuals")]
    [Tooltip("Renderer whose material changes for normal/red chest.")]
    [SerializeField] private Renderer chestRenderer;

    [SerializeField] private int chestMaterialIndex = 0;
    [SerializeField] private Material chestNormalMaterial;
    [SerializeField] private Material chestRednessMaterial;

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

        HideAllInspectionVisualObjects();
        ResetInspectionMaterials();
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

        HideAllInspectionVisualObjects();
        ResetInspectionMaterials();

        if (partType == InspectionPartType.Chest)
        {
            OpenChestInspection();
        }
        else
        {
            OpenHandInspection();
        }

        ApplyScenarioVisuals(partType);

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

    private void OpenChestInspection()
    {
        if (handInspectionObject != null)
            handInspectionObject.SetActive(false);

        if (chestInspectionObject == null)
            return;

        chestInspectionObject.SetActive(true);

        PlaceObjectInFrontOfCamera(
            chestInspectionObject,
            chestDistanceFromCamera,
            chestViewOffset,
            chestViewRotation
        );

        if (inspectionObjectController == null)
            return;

        if (chestMeshToRotate != null)
            inspectionObjectController.SetTarget(chestMeshToRotate, InspectionPartType.Chest);
        else
            inspectionObjectController.SetTarget(chestInspectionObject.transform, InspectionPartType.Chest);
    }

    private void OpenHandInspection()
    {
        if (chestInspectionObject != null)
            chestInspectionObject.SetActive(false);

        if (handInspectionObject == null)
            return;

        handInspectionObject.SetActive(true);

        PlaceObjectInFrontOfCamera(
            handInspectionObject,
            handDistanceFromCamera,
            handViewOffset,
            handViewRotation
        );

        if (inspectionObjectController == null)
            return;

        if (handMeshToRotate != null)
            inspectionObjectController.SetTarget(handMeshToRotate, InspectionPartType.Hand);
        else
            inspectionObjectController.SetTarget(handInspectionObject.transform, InspectionPartType.Hand);
    }

    private void ApplyScenarioVisuals(InspectionPartType partType)
    {
        if (scenarioManager == null)
            return;

        if (partType == InspectionPartType.Hand)
        {
            ApplyHandVisuals();
        }
        else if (partType == InspectionPartType.Chest)
        {
            ApplyChestVisuals();
        }
    }

    private void ApplyHandVisuals()
    {
        bool showRash = scenarioManager.ShouldShowHandRash();
        bool showPale = scenarioManager.ShouldShowPaleHand();

        // Use this if rash spots/patches are separate GameObjects.
        if (handRashVisuals != null)
            handRashVisuals.SetActive(showRash);

        // Use this if the hand rash/pallor is material-based.
        if (handRenderer != null)
        {
            if (showRash && handRashMaterial != null)
            {
                SetRendererMaterial(handRenderer, handMaterialIndex, handRashMaterial);
            }
            else if (showPale && handPaleMaterial != null)
            {
                SetRendererMaterial(handRenderer, handMaterialIndex, handPaleMaterial);
            }
            else if (handNormalMaterial != null)
            {
                SetRendererMaterial(handRenderer, handMaterialIndex, handNormalMaterial);
            }
        }
    }

    private void ApplyChestVisuals()
{
    bool showRedness = scenarioManager.ShouldShowChestRedness();
    bool showRespiratoryDistress = scenarioManager.ShouldShowRespiratoryDistress();

    if (chestRednessVisuals != null)
        chestRednessVisuals.SetActive(showRedness);

    if (respiratoryDistressVisuals != null)
        respiratoryDistressVisuals.SetActive(showRespiratoryDistress);

    if (chestRenderer != null)
    {
        if (showRedness && chestRednessMaterial != null)
        {
            SetRendererMaterial(chestRenderer, chestMaterialIndex, chestRednessMaterial);
        }
        else if (chestNormalMaterial != null)
        {
            SetRendererMaterial(chestRenderer, chestMaterialIndex, chestNormalMaterial);
        }
    }

    if (chestBreathingController != null)
        chestBreathingController.StartBreathing(showRespiratoryDistress);
}
    private void HideAllInspectionVisualObjects()
    {
        if (handRashVisuals != null)
            handRashVisuals.SetActive(false);

        if (chestRednessVisuals != null)
            chestRednessVisuals.SetActive(false);

        if (respiratoryDistressVisuals != null)
            respiratoryDistressVisuals.SetActive(false);
    }

    private void ResetInspectionMaterials()
    {
        if (handRenderer != null && handNormalMaterial != null)
            SetRendererMaterial(handRenderer, handMaterialIndex, handNormalMaterial);

        if (chestRenderer != null && chestNormalMaterial != null)
            SetRendererMaterial(chestRenderer, chestMaterialIndex, chestNormalMaterial);
    }

    private void SetRendererMaterial(Renderer renderer, int materialIndex, Material material)
    {
        if (renderer == null || material == null)
            return;

        Material[] materials = renderer.materials;

        if (materialIndex < 0 || materialIndex >= materials.Length)
        {
            Debug.LogWarning(
                $"{renderer.name}: Material index {materialIndex} is invalid. Renderer has {materials.Length} material slot(s)."
            );
            return;
        }

        materials[materialIndex] = material;
        renderer.materials = materials;
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

        HideAllInspectionVisualObjects();
        ResetInspectionMaterials();
        if (chestBreathingController != null)
    chestBreathingController.StopBreathing();

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
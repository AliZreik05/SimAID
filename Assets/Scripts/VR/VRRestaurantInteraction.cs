using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// VR adapter for the Restaurant (anaphylaxis) scenario.
/// Mirrors how VRCPRInteraction integrates the city CPR objective into VR
/// without changing desktop gameplay. Responsibilities:
///   - VR ray pointer that fires <see cref="Interactable"/> objects (patient
///     starter, inspection openers, medkit opener, medication items) when the
///     player aims at them and presses the right controller trigger.
///   - Bypass the Inspection / Medkit camera switching (those scripts disable
///     the scene's only XR-tracked Main Camera, which would black out the
///     headset). The XR camera is forced to stay enabled and the inspection
///     object is repositioned in front of the player.
///   - Provide right-stick rotation for inspection objects (replaces the
///     mouse-based InspectionObjectController in VR).
///   - Spawn a world-space medkit canvas with three medication buttons so VR
///     users can choose a medication without the desktop medkit camera.
///   - Continue past the "Press E to continue" prompt with the right trigger.
/// Desktop logic remains untouched: this script is auto-installed only when
/// VR runtime is active in the Restaurant scene.
/// </summary>
public class VRRestaurantInteraction : MonoBehaviour
{
    private const string SceneName = "Restaurant Scene";
    private const float MaxRayDistance = 12f;
    private const float MedkitCanvasDistance = 1.4f;
    private const float InspectionDistance = 0.65f;
    private const float TriggerPressThreshold = 0.55f;
    private const float TriggerReleaseThreshold = 0.35f;
    private const float PromptHideInterval = 1f;
    private const float VrMoveSpeed = 1.25f;
    private const float VrTurnSpeed = 60f;
    private const float ObjectiveHudDistance = 1.25f;
    private const float UiCanvasDistance = 1.55f;
    private const float WorldPointerAssistRadius = 0.7f;
    private const float HandVisualScale = 1f;

    private Camera xrCamera;
    private Transform leftHand;
    private Transform rightHand;
    private Transform xrRigRoot;
    private Transform cameraOffset;
    private CharacterController xrCharacterController;
    private InputManager[] desktopInputManagers;
    private bool[] desktopInputManagersWereEnabled;
    private PlayerLook[] desktopLookScripts;
    private bool[] desktopLookScriptsWereEnabled;
    private InspectionObjectController[] desktopInspectionControllers;
    private bool[] desktopInspectionControllersWereEnabled;

    private MedicalScenarioManager scenarioManager;
    private InspectionManager inspectionManager;

    private GameObject chestInspectObject;
    private GameObject handInspectObject;
    private Transform chestInspectMesh;
    private Transform handInspectMesh;
    private Transform medkitViewRoot;
    private Camera inspectionCamera;
    private Camera medkitCamera;

    private TMP_Text objectiveText;
    private TMP_Text vrObjectiveText;
    private TMP_Text introText;
    private CanvasGroup sourceObjectiveCanvasGroup;
    private Canvas scenarioCanvas;
    private RectTransform questionPanelRect;
    private RectTransform answerPanelRect;
    private RectTransform resultPanelRect;
    private TMP_Text answerText;
    private TMP_Text resultTitleText;
    private TMP_Text resultBodyText;
    private GameObject sourceObjectiveBanner;

    private LineRenderer pointerLine;

    private GameObject medkitCanvasRoot;
    private GameObject objectiveHudRoot;
    private GameObject inspectionClueRoot;
    private GameObject inspectionBackdropRoot;
    private GameObject medkitBackdropRoot;
    private TMP_Text inspectionClueText;
    private CanvasGroup inspectionClueCanvasGroup;

    private bool wasTriggerPressed;
    private bool wasSecondaryPressed;

    private bool inspectionRepositioned;
    private bool vrInspectionActive;
    private InspectionPartType activeInspectionPart;
    private InspectionPartType requestedInspectionPart;
    private float inspectionYaw;
    private float inspectionPitch;
    private Quaternion inspectionStartRotation;

    private float nextPromptHideTime;
    private bool sourceObjectiveHidden;
    private bool scenarioCanvasConfigured;
    private bool desktopCameraInputCached;
    private bool handsConfigured;
    private bool medkitViewOriginalPoseCaptured;
    private Vector3 medkitViewOriginalPosition;
    private Quaternion medkitViewOriginalRotation;
    private Vector3 medkitViewOriginalScale;
    private readonly System.Collections.Generic.Dictionary<Transform, Vector3> medkitItemOriginalScales =
        new System.Collections.Generic.Dictionary<Transform, Vector3>();
    private GameObject scaledInspectionObject;
    private Vector3 scaledInspectionOriginalScale;
    private InspectionPartType pendingInspectionCluePart;
    private bool hasPendingInspectionClue;
    private float inspectionClueFadeStartTime = -1f;

    private const float VrMedkitItemScale = 0.333f;

    private const float pointerLineWidth = 0.005f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        SceneManager.sceneLoaded -= OnAnySceneLoaded;
        SceneManager.sceneLoaded += OnAnySceneLoaded;
        TryInstallForActiveScene();
    }

    private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneName)
            TryInstallInScene(scene);
    }

    private static void TryInstallForActiveScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.name == SceneName)
            TryInstallInScene(active);
    }

    private static void TryInstallInScene(Scene scene)
    {
        if (FindFirstObjectByType<VRRestaurantInteraction>(FindObjectsInactive.Include) != null)
            return;

        if (!IsVRActive())
            return;

        GameObject host = new GameObject("VR Restaurant Interaction");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<VRRestaurantInteraction>();
    }

    private static bool IsVRActive()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return UnityEngine.XR.XRSettings.isDeviceActive;
#endif
    }

    private void Awake()
    {
        // Step 1: Find and suppress auxiliary cameras BEFORE anything reads Camera.main.
        // MedkitCamera and InspectionCamera both start active in the scene file and are
        // tagged MainCamera. If left active, VRRuntimeCompatibility would pick one of them
        // as the XR camera and configure all canvases around the wrong view.
        FindAuxiliaryCameras();
        SuppressAuxiliaryCamerasNow();

        // Step 2: Find the TrackedPoseDriver camera and activate its GameObject.
        // Main Camera starts inactive in the scene, so we must activate it before
        // VRRuntimeCompatibility calls ResolveRuntimeCamera().
        FindXrCamera();

        // Step 3: Resolve remaining scene references.
        scenarioManager = FindFirstObjectByType<MedicalScenarioManager>();
        inspectionManager = FindFirstObjectByType<InspectionManager>();
        FindHands();
        FindInspectionObjects();
        FindMedkitViewRoot();
        FindObjectiveText();
        FindScenarioUiReferences();
        HideStrayLobbyMenu();
        HideDesktopPrompts();
        DisableXriGravityProviders();
        DisableDesktopCameraInputForVr();
        DisableDesktopInspectionControllersForVr();
        ConfigureHandsForNearView();
        BuildObjectiveHud();
        BuildPointerLine();
    }

    private void OnDestroy()
    {
        if (medkitCanvasRoot != null)
            Destroy(medkitCanvasRoot);
        if (objectiveHudRoot != null)
            Destroy(objectiveHudRoot);
        if (inspectionClueRoot != null)
            Destroy(inspectionClueRoot);
        if (inspectionBackdropRoot != null)
            Destroy(inspectionBackdropRoot);
        if (medkitBackdropRoot != null)
            Destroy(medkitBackdropRoot);
        if (sourceObjectiveCanvasGroup != null)
            sourceObjectiveCanvasGroup.alpha = 1f;
        if (objectiveText != null)
            objectiveText.enabled = true;
        if (sourceObjectiveBanner != null)
            sourceObjectiveBanner.SetActive(true);
        RestoreDesktopCameraInput();
        RestoreDesktopInspectionControllers();
    }

    private void FindHands()
    {
        leftHand = FindByName("Left Hand", "LeftHand", "Left Direct Interactor", "Left Controller");
        rightHand = FindByName("Right Hand", "RightHand", "Right Direct Interactor", "Right Controller");
    }

    private void FindXrCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera c in cameras)
        {
            if (c == null)
                continue;
            if (c.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() != null)
            {
                // Main Camera starts inactive in the Restaurant scene; activate it so
                // Camera.main correctly returns it when VRRuntimeCompatibility resolves.
                if (!c.gameObject.activeSelf)
                    c.gameObject.SetActive(true);
                c.enabled = true;
                xrCamera = c;
                xrCamera.nearClipPlane = Mathf.Min(xrCamera.nearClipPlane, 0.005f);
                ResolveXrRigReferences();
                NormalizeXrRigForRestaurant();
                return;
            }
        }

        xrCamera = Camera.main;
        if (xrCamera != null)
            xrCamera.nearClipPlane = Mathf.Min(xrCamera.nearClipPlane, 0.005f);
        ResolveXrRigReferences();
        NormalizeXrRigForRestaurant();
    }

    private void ResolveXrRigReferences()
    {
        if (xrCamera == null)
            return;

        xrCharacterController = xrCamera.GetComponentInParent<CharacterController>();
        xrRigRoot = xrCharacterController != null
            ? xrCharacterController.transform
            : xrCamera.transform.root;

        Transform parent = xrCamera.transform.parent;
        cameraOffset = parent != null && parent.name == "Camera Offset" ? parent : null;
    }

    private void NormalizeXrRigForRestaurant()
    {
        if (xrRigRoot != null)
        {
            Vector3 localScale = xrRigRoot.localScale;
            if (!Mathf.Approximately(localScale.x, 1f) ||
                !Mathf.Approximately(localScale.y, 2f) ||
                !Mathf.Approximately(localScale.z, 1f))
            {
                xrRigRoot.localScale = new Vector3(1f, 2f, 1f);
            }

            Vector3 localPosition = xrRigRoot.localPosition;
            if (localPosition.y > -1.2f)
                xrRigRoot.localPosition = new Vector3(localPosition.x, -1.92f, localPosition.z);
        }

        if (cameraOffset != null)
        {
            Vector3 localPosition = cameraOffset.localPosition;
            if (localPosition.y < 1.4f)
                cameraOffset.localPosition = new Vector3(localPosition.x, 1.71f, localPosition.z);
        }

        if (xrCharacterController != null)
        {
            xrCharacterController.height = 2f;
            xrCharacterController.center = new Vector3(0f, 1f, 0f);
        }
    }

    private void FindAuxiliaryCameras()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera c in cameras)
        {
            if (c == null) continue;
            if (c == xrCamera) continue;

            string lowerName = c.name.ToLowerInvariant();
            if (inspectionCamera == null && lowerName.Contains("inspection")) inspectionCamera = c;
            if (medkitCamera == null && lowerName.Contains("medkit")) medkitCamera = c;
        }
    }

    private void SuppressAuxiliaryCamerasNow()
    {
        if (inspectionCamera != null)
            inspectionCamera.gameObject.SetActive(false);
        if (medkitCamera != null)
            medkitCamera.gameObject.SetActive(false);
    }

    private void FindInspectionObjects()
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            if (t == null) continue;
            string n = t.name;
            if (n == "ChestInspectionRoot")
            {
                if (chestInspectObject == null)
                    chestInspectObject = t.gameObject;
                if (chestInspectMesh == null)
                    chestInspectMesh = t;
            }
            else if (handInspectObject == null && n == "InspectionHandPivot")
            {
                handInspectObject = t.gameObject;
            }
            else if (handInspectMesh == null && n == "HandRotationRoot")
            {
                handInspectMesh = t;
            }
        }
    }

    private void FindMedkitViewRoot()
    {
        if (medkitViewRoot != null)
            return;

        medkitViewRoot = FindExactTransform("MedkitViewRoot");
        if (medkitViewRoot == null)
            return;

        medkitViewOriginalPosition = medkitViewRoot.position;
        medkitViewOriginalRotation = medkitViewRoot.rotation;
        medkitViewOriginalScale = medkitViewRoot.localScale;
        medkitViewOriginalPoseCaptured = true;
    }

    private void FindObjectiveText()
    {
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text != null && text.gameObject.name == "IntroText")
                introText = text;

            if (text != null && text.gameObject.name == "ObjectiveText")
            {
                objectiveText = text;
                sourceObjectiveBanner = FindAncestorNamed(text.transform, "ObjectiveBanner");
            }
        }

        if (objectiveText == null)
            return;

        sourceObjectiveCanvasGroup = objectiveText.GetComponent<CanvasGroup>();
        if (sourceObjectiveCanvasGroup == null)
            sourceObjectiveCanvasGroup = objectiveText.gameObject.AddComponent<CanvasGroup>();
        HideSourceObjectiveText();
    }

    private void FindScenarioUiReferences()
    {
        scenarioCanvas = FindCanvasByName("ScenarioCanvas");
        questionPanelRect = FindRectByName("QuestionPanel");
        answerPanelRect = FindRectByName("AnswerPanel");
        resultPanelRect = FindRectByName("ResultPanel");

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text != null && text.name == "IntroText")
                introText = text;

            if (text != null && text.name == "AnswerText")
            {
                answerText = text;
                continue;
            }

            if (text != null && text.name == "ResultTitleText")
                resultTitleText = text;

            if (text != null && text.name == "ResultBodyText")
                resultBodyText = text;
        }
    }

    private static Transform FindByName(params string[] names)
    {
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go != null) return go.transform;
        }
        return null;
    }

    private void BuildPointerLine()
    {
        GameObject lineObject = new GameObject("VR Restaurant Pointer Line");
        lineObject.transform.SetParent(transform, false);
        pointerLine = lineObject.AddComponent<LineRenderer>();
        pointerLine.useWorldSpace = true;
        pointerLine.positionCount = 2;
        pointerLine.startWidth = pointerLineWidth;
        pointerLine.endWidth = pointerLineWidth;
        pointerLine.numCornerVertices = 0;
        pointerLine.numCapVertices = 0;
        pointerLine.alignment = LineAlignment.View;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");

        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = new Color(0.2f, 0.7f, 1f, 0.85f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.2f, 0.7f, 1f, 0.85f));
            pointerLine.material = mat;
        }

        pointerLine.startColor = new Color(0.2f, 0.7f, 1f, 0.85f);
        pointerLine.endColor = new Color(0.2f, 0.7f, 1f, 0.0f);
        pointerLine.enabled = false;
    }

    private void Update()
    {
        if (xrCamera == null) FindXrCamera();
        if (leftHand == null || rightHand == null) FindHands();
        if (!handsConfigured) ConfigureHandsForNearView();
        if (scenarioManager == null) scenarioManager = FindFirstObjectByType<MedicalScenarioManager>();
        if (inspectionManager == null) inspectionManager = FindFirstObjectByType<InspectionManager>();
        if (medkitViewRoot == null) FindMedkitViewRoot();
        if (scenarioCanvas == null || questionPanelRect == null || answerPanelRect == null || resultPanelRect == null)
            FindScenarioUiReferences();

        // Force XR camera dominance every frame: InspectionManager.OpenInspection() and
        // MedkitOpener.Interact() both disable the main camera and enable an auxiliary
        // camera, which blacks out the headset. This prevents that on any frame where
        // a desktop script re-enables an auxiliary camera.
        EnforceXrCameraDominance();
        DisableXriGravityProviders();
        DisableDesktopCameraInputForVr();
        DriveHandTransformsFromXRDevices();
        HandleVrLocomotion();
        ConfigureVrScenarioUi();
        ApplyQuestionButtonVisuals();

        // Throttle the expensive FindObjectsByType scan to once per second.
        if (Time.time >= nextPromptHideTime)
        {
            HideStrayLobbyMenu();
            HideDesktopPrompts();
            nextPromptHideTime = Time.time + PromptHideInterval;
        }

        bool triggerPressed = ReadRightTrigger(out float triggerValue);
        bool triggerEdge = !wasTriggerPressed && triggerPressed;

        Interactable hovered = UpdatePointerAndGetHovered();

        if (triggerEdge)
            HandleTriggerPress(hovered);

        HandleInspectionMode();
        HandleSecondaryButtonExitInspection();
        HandleMedkitViewFollow();
        HandleObjectiveHudFollow();
        HandleInspectionClueFollow();

        wasTriggerPressed = triggerPressed;
    }

    private void LateUpdate()
    {
        if (!vrInspectionActive && (inspectionManager == null || !inspectionManager.IsInspecting))
            return;

        // Re-assert after desktop inspection helpers and chest breathing have run.
        EnforceXrCameraDominance();
        HandleInspectionMode(false);
    }

    private void EnforceXrCameraDominance()
    {
        if (xrCamera != null && !xrCamera.enabled)
            xrCamera.enabled = true;

        if (inspectionCamera != null && inspectionCamera.enabled)
        {
            inspectionCamera.enabled = false;
            if (inspectionCamera.gameObject.activeSelf)
                inspectionCamera.gameObject.SetActive(false);
        }

        if (medkitCamera != null && medkitCamera.enabled)
        {
            medkitCamera.enabled = false;
            if (medkitCamera.gameObject.activeSelf)
                medkitCamera.gameObject.SetActive(false);
        }
    }

    private static void HideStrayLobbyMenu()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.name != "Level Select Canvas")
                continue;

            canvas.gameObject.SetActive(false);
        }

        foreach (LevelSelectMenu menu in FindObjectsByType<LevelSelectMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (menu != null)
                menu.gameObject.SetActive(false);
        }
    }

    private void HideDesktopPrompts()
    {
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null) continue;

            string textValue = text.text == null ? string.Empty : text.text.Replace("\n", " ");
            bool isPressE = text.name.Contains("Press E") || textValue.Contains("Press E to Interact");
            if (isPressE && text.gameObject.activeSelf)
                text.gameObject.SetActive(false);
        }
    }

    private static void DisableXriGravityProviders()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || !behaviour.enabled)
                continue;

            System.Type type = behaviour.GetType();
            if (type != null && type.Name == "GravityProvider")
                behaviour.enabled = false;
        }
    }

    private void ConfigureHandsForNearView()
    {
        if (xrCamera != null)
            xrCamera.nearClipPlane = Mathf.Min(xrCamera.nearClipPlane, 0.005f);

        Transform leftVisual = FindExactTransform("Left Hand Model");
        Transform rightVisual = FindExactTransform("Right Hand Model");
        ScaleHandVisual(leftVisual);
        ScaleHandVisual(rightVisual);
        handsConfigured = leftVisual != null || rightVisual != null;
    }

    private void DisableDesktopCameraInputForVr()
    {
        if (!desktopCameraInputCached)
        {
            desktopInputManagers = FindObjectsByType<InputManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            desktopInputManagersWereEnabled = new bool[desktopInputManagers.Length];
            for (int i = 0; i < desktopInputManagers.Length; i++)
                desktopInputManagersWereEnabled[i] = desktopInputManagers[i] != null && desktopInputManagers[i].enabled;

            desktopLookScripts = FindObjectsByType<PlayerLook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            desktopLookScriptsWereEnabled = new bool[desktopLookScripts.Length];
            for (int i = 0; i < desktopLookScripts.Length; i++)
                desktopLookScriptsWereEnabled[i] = desktopLookScripts[i] != null && desktopLookScripts[i].enabled;

            desktopCameraInputCached = true;
        }

        if (desktopInputManagers != null)
        {
            foreach (InputManager manager in desktopInputManagers)
            {
                if (manager != null && manager.enabled)
                    manager.enabled = false;
            }
        }

        if (desktopLookScripts != null)
        {
            foreach (PlayerLook look in desktopLookScripts)
            {
                if (look != null && look.enabled)
                    look.enabled = false;
            }
        }
    }

    private void RestoreDesktopCameraInput()
    {
        if (!desktopCameraInputCached)
            return;

        if (desktopInputManagers != null && desktopInputManagersWereEnabled != null)
        {
            for (int i = 0; i < desktopInputManagers.Length && i < desktopInputManagersWereEnabled.Length; i++)
            {
                if (desktopInputManagers[i] != null)
                    desktopInputManagers[i].enabled = desktopInputManagersWereEnabled[i];
            }
        }

        if (desktopLookScripts != null && desktopLookScriptsWereEnabled != null)
        {
            for (int i = 0; i < desktopLookScripts.Length && i < desktopLookScriptsWereEnabled.Length; i++)
            {
                if (desktopLookScripts[i] != null)
                    desktopLookScripts[i].enabled = desktopLookScriptsWereEnabled[i];
            }
        }
    }

    private void DisableDesktopInspectionControllersForVr()
    {
        if (desktopInspectionControllers == null)
        {
            desktopInspectionControllers = FindObjectsByType<InspectionObjectController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            desktopInspectionControllersWereEnabled = new bool[desktopInspectionControllers.Length];
            for (int i = 0; i < desktopInspectionControllers.Length; i++)
                desktopInspectionControllersWereEnabled[i] =
                    desktopInspectionControllers[i] != null && desktopInspectionControllers[i].enabled;
        }

        foreach (InspectionObjectController controller in desktopInspectionControllers)
        {
            if (controller != null && controller.enabled)
                controller.enabled = false;
        }
    }

    private void RestoreDesktopInspectionControllers()
    {
        if (desktopInspectionControllers == null || desktopInspectionControllersWereEnabled == null)
            return;

        for (int i = 0; i < desktopInspectionControllers.Length && i < desktopInspectionControllersWereEnabled.Length; i++)
        {
            if (desktopInspectionControllers[i] != null)
                desktopInspectionControllers[i].enabled = desktopInspectionControllersWereEnabled[i];
        }
    }

    private static void ScaleHandVisual(Transform visual)
    {
        if (visual == null)
            return;

        visual.localScale = new Vector3(HandVisualScale, HandVisualScale * 0.5f, HandVisualScale);

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void ConfigureVrScenarioUi()
    {
        if (scenarioCanvas == null || xrCamera == null)
            return;

        if (!scenarioCanvasConfigured)
        {
            scenarioCanvas.renderMode = RenderMode.WorldSpace;
            scenarioCanvas.worldCamera = xrCamera;
            scenarioCanvas.sortingOrder = 230;

            RectTransform canvasRect = scenarioCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1180f, 780f);
            canvasRect.localScale = Vector3.one * 0.0014f;

            if (questionPanelRect != null)
            {
                questionPanelRect.anchorMin = questionPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
                questionPanelRect.pivot = new Vector2(0.5f, 0.5f);
                questionPanelRect.anchoredPosition = new Vector2(0f, 90f);
                questionPanelRect.sizeDelta = new Vector2(980f, 520f);
            }

            if (answerPanelRect != null)
            {
                answerPanelRect.anchorMin = answerPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
                answerPanelRect.pivot = new Vector2(0.5f, 0.5f);
                answerPanelRect.anchoredPosition = new Vector2(0f, -80f);
                answerPanelRect.sizeDelta = new Vector2(1040f, 300f);
            }

            if (answerText != null)
            {
                answerText.fontSize = Mathf.Max(answerText.fontSize, 34f);
                answerText.textWrappingMode = TextWrappingModes.Normal;
                answerText.alignment = TextAlignmentOptions.Center;
                answerText.color = Color.white;
            }

            foreach (Image image in scenarioCanvas.GetComponentsInChildren<Image>(true))
            {
                if (image == null)
                    continue;

                string lower = image.name.ToLowerInvariant();
                if (lower.Contains("panel") || lower.Contains("background") || lower.Contains("banner"))
                    image.color = new Color(0f, 0f, 0f, 0.72f);
            }

            foreach (TMP_Text text in scenarioCanvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null)
                    continue;

                text.textWrappingMode = TextWrappingModes.Normal;
                text.fontSize = Mathf.Max(text.fontSize, text.name == "AnswerText" ? 34f : 28f);
            }

            scenarioCanvasConfigured = true;
        }

        bool questionActive = IsQuestionPanelActive();
        bool answerActive = IsAnswerPanelActive();
        if (answerPanelRect != null)
        {
            answerPanelRect.anchoredPosition = questionActive
                ? new Vector2(0f, -245f)
                : new Vector2(0f, -55f);
            answerPanelRect.sizeDelta = questionActive
                ? new Vector2(1040f, 145f)
                : new Vector2(1040f, 320f);
        }

        bool resultActive = IsResultPanelActive();
        if (resultActive)
            ConfigureVrResultPanel();

        bool dialogueUiActive =
            questionActive ||
            answerActive ||
            resultActive;

        if (!dialogueUiActive)
            return;

        Vector3 targetPos = xrCamera.transform.position
            + xrCamera.transform.forward * UiCanvasDistance
            + Vector3.up * 0.05f;
        scenarioCanvas.transform.position = targetPos;
        scenarioCanvas.transform.rotation = Quaternion.LookRotation(
            targetPos - xrCamera.transform.position, Vector3.up);
    }

    private void ApplyQuestionButtonVisuals()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        foreach (QuestionButton question in FindObjectsByType<QuestionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (question == null)
                continue;

            Button button = question.GetComponent<Button>();
            Image image = question.GetComponent<Image>();
            TMP_Text label = question.GetComponentInChildren<TMP_Text>(true);
            bool isSelected = selected != null && (selected == question.gameObject || selected.transform.IsChildOf(question.transform));

            if (button != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 0.95f);
                colors.highlightedColor = new Color(0.08f, 0.12f, 0.16f, 1f);
                colors.selectedColor = new Color(0.04f, 0.08f, 0.12f, 1f);
                colors.pressedColor = new Color(0.02f, 0.05f, 0.08f, 1f);
                colors.colorMultiplier = 1f;
                button.colors = colors;
            }

            if (image != null)
            {
                image.color = isSelected
                    ? new Color(0.04f, 0.08f, 0.12f, 1f)
                    : new Color(0.88f, 0.88f, 0.88f, 0.94f);
            }

            if (label != null)
            {
                label.color = isSelected ? Color.white : new Color(0.05f, 0.05f, 0.05f, 1f);
                label.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
            }

            Outline outline = question.GetComponent<Outline>();
            if (outline == null)
                outline = question.gameObject.AddComponent<Outline>();

            outline.effectColor = isSelected
                ? new Color(0.15f, 0.95f, 1f, 1f)
                : new Color(0f, 0f, 0f, 0f);
            outline.effectDistance = isSelected ? new Vector2(4f, -4f) : Vector2.zero;
            outline.enabled = isSelected;
        }
    }

    private bool IsQuestionPanelActive()
    {
        return questionPanelRect != null && questionPanelRect.gameObject.activeInHierarchy;
    }

    private bool IsAnswerPanelActive()
    {
        return answerPanelRect != null && answerPanelRect.gameObject.activeInHierarchy;
    }

    private bool IsResultPanelActive()
    {
        return resultPanelRect != null && resultPanelRect.gameObject.activeInHierarchy;
    }

    private void ConfigureVrResultPanel()
    {
        if (resultPanelRect == null)
            return;

        resultPanelRect.anchorMin = resultPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        resultPanelRect.pivot = new Vector2(0.5f, 0.5f);
        resultPanelRect.anchoredPosition = Vector2.zero;
        resultPanelRect.sizeDelta = new Vector2(960f, 560f);

        foreach (Image image in resultPanelRect.GetComponentsInChildren<Image>(true))
        {
            if (image != null)
                image.color = new Color(0f, 0f, 0f, 0.78f);
        }

        ConfigureResultText(resultTitleText, 42f, TextAlignmentOptions.Center, false);
        ConfigureResultText(resultBodyText, 25f, TextAlignmentOptions.TopLeft, true);
    }

    private static void ConfigureResultText(TMP_Text text, float fontSize, TextAlignmentOptions alignment, bool forceWhite)
    {
        if (text == null)
            return;

        text.gameObject.SetActive(true);
        text.enabled = true;
        if (forceWhite)
            text.color = Color.white;
        text.fontSize = Mathf.Max(text.fontSize, fontSize);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = alignment;
    }

    private void HideSourceObjectiveText()
    {
        if (objectiveText == null || sourceObjectiveHidden)
            return;

        if (sourceObjectiveCanvasGroup != null)
        {
            sourceObjectiveCanvasGroup.alpha = 0f;
            sourceObjectiveCanvasGroup.interactable = false;
            sourceObjectiveCanvasGroup.blocksRaycasts = false;
        }

        objectiveText.enabled = false;
        if (sourceObjectiveBanner != null && sourceObjectiveBanner.activeSelf)
            sourceObjectiveBanner.SetActive(false);

        CanvasRenderer renderer = objectiveText.canvasRenderer;
        if (renderer != null)
            renderer.SetAlpha(0f);

        sourceObjectiveHidden = true;
    }

    private void HandleVrLocomotion()
    {
        if (xrCamera == null)
            return;

        if (xrCharacterController == null || xrRigRoot == null)
            ResolveXrRigReferences();

        Vector2 moveAxis = ReadMoveAxis();
        Vector2 turnAxis = ReadTurnAxis();

        if (ShouldAllowVrTurn() && xrRigRoot != null && Mathf.Abs(turnAxis.x) > 0.25f)
            xrRigRoot.Rotate(Vector3.up, turnAxis.x * VrTurnSpeed * Time.deltaTime, Space.World);

        if (!ShouldAllowVrLocomotion())
            return;

        Vector3 forward = xrCamera.transform.forward;
        Vector3 right = xrCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * moveAxis.y + right * moveAxis.x) * VrMoveSpeed;
        move.y = 0f;

        if (xrCharacterController != null && xrCharacterController.enabled)
        {
            xrCharacterController.Move(move * Time.deltaTime);
        }
        else if (xrRigRoot != null)
        {
            xrRigRoot.position += move * Time.deltaTime;
        }
    }

    private bool ShouldAllowVrTurn()
    {
        if (scenarioManager == null)
            return true;

        return scenarioManager.CurrentState != MedicalScenarioManager.ScenarioState.InMedkitView &&
            scenarioManager.CurrentState != MedicalScenarioManager.ScenarioState.InspectionView;
    }

    private bool ShouldAllowVrLocomotion()
    {
        if (scenarioManager == null)
            return true;

        switch (scenarioManager.CurrentState)
        {
            case MedicalScenarioManager.ScenarioState.WaitingForPatientInteraction:
            case MedicalScenarioManager.ScenarioState.MedicationSelection:
            case MedicalScenarioManager.ScenarioState.Success:
            case MedicalScenarioManager.ScenarioState.Failure:
                return true;
            default:
                return false;
        }
    }

    private static Vector2 ReadAxis(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return Vector2.zero;

        if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 primaryAxis) &&
            primaryAxis.sqrMagnitude > 0.01f)
            return primaryAxis;

        if (device.TryGetFeatureValue(CommonUsages.secondary2DAxis, out Vector2 secondaryAxis) &&
            secondaryAxis.sqrMagnitude > 0.01f)
            return secondaryAxis;

        return Vector2.zero;
    }

    private static Vector2 ReadMoveAxis()
    {
        Vector2 axis = ReadAxis(XRNode.LeftHand);
        if (axis.sqrMagnitude > 0.01f)
            return axis;

        return ReadAxis(XRNode.RightHand);
    }

    private static Vector2 ReadTurnAxis()
    {
        Vector2 axis = ReadAxis(XRNode.RightHand);
        if (axis.sqrMagnitude > 0.01f)
            return axis;

        return Vector2.zero;
    }

    private void DriveHandTransformsFromXRDevices()
    {
        DriveHandTransform(leftHand, XRNode.LeftHand);
        DriveHandTransform(rightHand, XRNode.RightHand);
    }

    private static void DriveHandTransform(Transform hand, XRNode node)
    {
        if (hand == null)
            return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return;

        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition))
            hand.localPosition = localPosition;

        if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation))
            hand.localRotation = localRotation;
    }

    private Interactable UpdatePointerAndGetHovered()
    {
        if (rightHand == null || pointerLine == null)
        {
            if (pointerLine != null) pointerLine.enabled = false;
            return null;
        }

        Vector3 origin = rightHand.position;
        Vector3 direction = rightHand.forward;

        pointerLine.SetPosition(0, origin);

        Interactable hovered = null;
        Vector3 endPoint = origin + direction * MaxRayDistance;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, MaxRayDistance, ~0, QueryTriggerInteraction.Collide))
        {
            endPoint = hit.point;
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable == null)
                interactable = hit.collider.GetComponentInParent<Interactable>();

            if (interactable != null && IsRestaurantInteractable(interactable))
                hovered = interactable;
        }

        if (hovered == null)
        {
            hovered = FindAssistedInteractable(origin, direction);
            if (hovered != null)
                endPoint = hovered.transform.position;
        }

        pointerLine.SetPosition(1, endPoint);
        bool shouldShowPointer = ShouldShowPointerLine(hovered);
        pointerLine.enabled = shouldShowPointer;
        pointerLine.startColor = hovered != null
            ? new Color(0.2f, 1f, 0.4f, 0.95f)
            : new Color(0.2f, 0.7f, 1f, 0.85f);
        pointerLine.endColor = new Color(pointerLine.startColor.r, pointerLine.startColor.g, pointerLine.startColor.b, 0f);

        return hovered;
    }

    private bool ShouldShowPointerLine(Interactable hovered)
    {
        if (hovered != null)
            return true;

        if (medkitCanvasRoot != null && medkitCanvasRoot.activeSelf)
            return true;

        return scenarioManager != null &&
            scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InMedkitView;
    }

    private Interactable FindAssistedInteractable(Vector3 origin, Vector3 direction)
    {
        Interactable best = null;
        float bestScore = float.MaxValue;

        foreach (Interactable interactable in FindObjectsByType<Interactable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (interactable == null || !IsRestaurantInteractable(interactable))
                continue;

            if (!IsInteractableRelevantNow(interactable))
                continue;

            Vector3 toTarget = interactable.transform.position - origin;
            float alongRay = Vector3.Dot(toTarget, direction);
            if (alongRay < 0f || alongRay > MaxRayDistance)
                continue;

            float perpendicular = Vector3.Distance(origin + direction * alongRay, interactable.transform.position);
            float radius = interactable is MedkitOpener ? 2f : WorldPointerAssistRadius;
            if (perpendicular > radius)
                continue;

            float score = alongRay + perpendicular * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }

        return best;
    }

    private bool IsInteractableRelevantNow(Interactable interactable)
    {
        if (scenarioManager == null)
            return true;

        if (interactable is PatientAssessmentStarter)
            return scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.WaitingForPatientInteraction;

        if (interactable is InspectionOpener)
            return scenarioManager.CanInspect;

        if (interactable is MedkitOpener)
            return scenarioManager.CanOpenMedkit;

        if (interactable is MedicationItem)
            return scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InMedkitView;

        return false;
    }

    private static bool IsRestaurantInteractable(Interactable interactable)
    {
        return interactable is PatientAssessmentStarter
            || interactable is InspectionOpener
            || interactable is MedkitOpener
            || interactable is MedicationItem;
    }

    private void HandleTriggerPress(Interactable hovered)
    {
        if (TryHandleContinuePrompt())
            return;

        if (hovered == null)
            return;

        if (hovered is MedkitOpener medkitOpener)
        {
            HandleMedkitOpener(medkitOpener);
        }
        else if (hovered is InspectionOpener inspectionOpener)
        {
            HandleInspectionOpener(inspectionOpener);
        }
        else if (hovered is MedicationItem medicationItem)
        {
            HandleMedicationItem(medicationItem);
        }
        else
        {
            // PatientAssessmentStarter and InspectionOpener route through their normal
            // Interact() flow for audio/animation/state changes. EnforceXrCameraDominance()
            // is called immediately after to cancel any same-frame camera switch before
            // the next render, preventing a one-frame blackout.
            hovered.baseInteract();
            EnforceXrCameraDominance();
        }
    }

    private bool TryHandleContinuePrompt()
    {
        if (scenarioManager == null || objectiveText == null)
            return false;

        if (scenarioManager.CurrentState != MedicalScenarioManager.ScenarioState.Questioning)
            return false;

        string current = objectiveText.text == null ? string.Empty : objectiveText.text;
        if (!current.Contains("Press E to continue"))
            return false;

        scenarioManager.FinishQuestioning();
        return true;
    }

    private void HandleMedkitOpener(MedkitOpener opener)
    {
        if (scenarioManager == null) return;
        if (!scenarioManager.CanOpenMedkit) return;

        scenarioManager.EnterMedkitView();
        DestroyMedkitCanvas();
        ShowRealMedkitView();
    }

    private void HandleInspectionOpener(InspectionOpener opener)
    {
        if (scenarioManager == null || opener == null || !scenarioManager.CanInspect)
            return;

        if (inspectionManager == null)
            inspectionManager = FindFirstObjectByType<InspectionManager>();

        if (inspectionManager == null)
            return;

        InspectionPartType part = ReadInspectionPart(opener);
        requestedInspectionPart = part;
        vrInspectionActive = true;
        inspectionManager.OpenInspection(part);
        EnsureInspectionVisualActive(part);
        pendingInspectionCluePart = part;
        hasPendingInspectionClue = true;
        HideInspectionClueImmediate();
        HideDesktopInspectionBackdropAndUi();
        EnforceXrCameraDominance();
    }

    private static InspectionPartType ReadInspectionPart(InspectionOpener opener)
    {
        System.Reflection.FieldInfo field = typeof(InspectionOpener).GetField(
            "inspectionPart",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (field == null)
            return InspectionPartType.Chest;

        object value = field.GetValue(opener);
        return value is InspectionPartType part ? part : InspectionPartType.Chest;
    }

    private void ShowInspectionClue(InspectionPartType part)
    {
        EnsureInspectionClueCanvas();

        if (inspectionClueText == null)
            return;

        string title = part == InspectionPartType.Chest ? "Chest Inspection" : "Hand Inspection";
        string clue = scenarioManager != null ? scenarioManager.GetInspectionClue(part) : "";
        inspectionClueText.text = $"{title}\n{clue}";

        if (inspectionClueRoot != null)
        {
            inspectionClueRoot.SetActive(true);
            inspectionClueFadeStartTime = -1f;
        }

        if (inspectionClueCanvasGroup != null)
            inspectionClueCanvasGroup.alpha = 1f;
    }

    private void HideInspectionClueImmediate()
    {
        if (inspectionClueRoot != null)
            inspectionClueRoot.SetActive(false);

        if (inspectionClueCanvasGroup != null)
            inspectionClueCanvasGroup.alpha = 0f;
    }

    private void HandleMedicationItem(MedicationItem item)
    {
        if (scenarioManager == null) return;

        string medicationName = ReadMedicationName(item);
        if (string.IsNullOrEmpty(medicationName)) return;

        if (scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InMedkitView)
            scenarioManager.ExitMedkitView();

        scenarioManager.GiveMedication(medicationName);
        DestroyMedkitCanvas();
        HideRealMedkitView();
    }

    private static string ReadMedicationName(MedicationItem item)
    {
        System.Reflection.FieldInfo field = typeof(MedicationItem).GetField(
            "medicationName",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (field == null) return string.Empty;

        object value = field.GetValue(item);
        return value as string ?? string.Empty;
    }

    private void HandleInspectionMode(bool applyInput = true)
    {
        if (!vrInspectionActive && (inspectionManager == null || !inspectionManager.IsInspecting))
        {
            if (inspectionRepositioned)
                inspectionRepositioned = false;
            RestoreInspectionObjectScale();
            return;
        }

        GameObject targetObject = ResolveActiveInspectionObject(out InspectionPartType part);
        Transform rotationRoot = ResolveRotationRoot(part);

        if (targetObject == null || xrCamera == null)
            return;

        EnsureInspectionVisualActive(part);
        HideDesktopInspectionBackdropAndUi();

        if (!inspectionRepositioned || activeInspectionPart != part)
        {
            inspectionRepositioned = true;
            activeInspectionPart = part;
            inspectionYaw = 0f;
            inspectionPitch = 0f;
            if (rotationRoot != null)
                inspectionStartRotation = rotationRoot.localRotation;
            CaptureInspectionObjectScale(targetObject);
        }

        Vector3 flatForward = xrCamera.transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = xrCamera.transform.forward;
        flatForward.Normalize();

        float desiredMaxSize = part == InspectionPartType.Chest ? 0.98f : 0.78f;
        ApplyInspectionScaleForVr(targetObject, desiredMaxSize);

        Quaternion baseRotation = Quaternion.LookRotation(-flatForward, Vector3.up);

        // Chest: pivot and mesh are the same object, so apply yaw via world rotation
        // to avoid the localRotation setter conflicting with the world rotation setter.
        if (part == InspectionPartType.Chest)
        {
            if (applyInput)
            {
                Vector2 chestStick = ReadRightStickInput();
                inspectionYaw = Mathf.Clamp(inspectionYaw + chestStick.x * 90f * Time.deltaTime, -60f, 60f);
            }
            targetObject.transform.rotation = baseRotation * Quaternion.Euler(0f, 180f + inspectionYaw, 0f);
        }
        else
        {
            targetObject.transform.rotation = baseRotation;
        }

        Vector3 desiredCenter = xrCamera.transform.position
            + flatForward * 1.15f
            + Vector3.down * (part == InspectionPartType.Chest ? 0.16f : 0.08f);
        if (TryGetRendererBounds(targetObject, out Bounds bounds))
            targetObject.transform.position += desiredCenter - bounds.center;
        else
            targetObject.transform.position = desiredCenter;

        if (applyInput && part != InspectionPartType.Chest)
            ApplyInspectionStickRotation(rotationRoot, part);
        HandleInspectionBackdropFollow(flatForward, desiredCenter);
    }

    private void HideDesktopInspectionBackdropAndUi()
    {
        Transform rig = FindExactTransform("InspectionCameraRig");
        if (rig == null)
            return;

        foreach (Transform child in rig)
        {
            if (child == null)
                continue;

            if (IsInspectionVisualRoot(child))
                continue;

            if (child.GetComponent<Camera>() != null)
                continue;

            string lowerName = child.name.ToLowerInvariant();
            if (lowerName.Contains("background") || lowerName.Contains("ui"))
                child.gameObject.SetActive(false);
        }

        foreach (TMP_Text text in rig.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null)
                text.gameObject.SetActive(false);
        }
    }

    private bool IsInspectionVisualRoot(Transform candidate)
    {
        return (chestInspectObject != null && candidate == chestInspectObject.transform)
            || (handInspectObject != null && candidate == handInspectObject.transform);
    }

    private GameObject ResolveActiveInspectionObject(out InspectionPartType part)
    {
        if (vrInspectionActive || (inspectionManager != null && inspectionManager.IsInspecting))
        {
            part = requestedInspectionPart;
            if (requestedInspectionPart == InspectionPartType.Chest && chestInspectObject != null)
                return chestInspectObject;
            if (requestedInspectionPart == InspectionPartType.Hand && handInspectObject != null)
                return handInspectObject;
        }

        if (chestInspectObject != null && chestInspectObject.activeInHierarchy)
        {
            part = InspectionPartType.Chest;
            return chestInspectObject;
        }
        if (handInspectObject != null && handInspectObject.activeInHierarchy)
        {
            part = InspectionPartType.Hand;
            return handInspectObject;
        }

        part = InspectionPartType.Chest;
        return null;
    }

    private void EnsureInspectionVisualActive(InspectionPartType part)
    {
        GameObject target = part == InspectionPartType.Chest ? chestInspectObject : handInspectObject;
        GameObject other = part == InspectionPartType.Chest ? handInspectObject : chestInspectObject;

        if (target != null)
        {
            ActivateInspectionHierarchy(target.transform);
            ForceVisibleRenderers(target);
        }

        if (other != null && other.activeSelf)
            other.SetActive(false);
    }

    private static void ActivateInspectionHierarchy(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);

            if (current.name == "InspectionCameraRig")
                break;

            current = current.parent;
        }
    }

    private static void ForceVisibleRenderers(GameObject target)
    {
        if (target == null)
            return;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && !renderer.enabled)
                renderer.enabled = true;
        }
    }

    private void HideInspectionVisuals()
    {
        RestoreInspectionObjectScale();
        inspectionRepositioned = false;

        if (chestInspectObject != null)
            chestInspectObject.SetActive(false);

        if (handInspectObject != null)
            handInspectObject.SetActive(false);
    }

    private Transform ResolveRotationRoot(InspectionPartType part)
    {
        return part == InspectionPartType.Chest ? chestInspectMesh : handInspectMesh;
    }

    private void ApplyInspectionStickRotation(Transform rotationRoot, InspectionPartType part)
    {
        if (rotationRoot == null)
            return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        Vector2 axis = Vector2.zero;
        if (device.isValid)
        {
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis);
            if (axis.sqrMagnitude < 0.04f)
                device.TryGetFeatureValue(CommonUsages.secondary2DAxis, out axis);
        }

        const float rotationSpeed = 90f;

        if (part == InspectionPartType.Chest)
        {
            inspectionYaw = Mathf.Clamp(inspectionYaw + axis.x * rotationSpeed * Time.deltaTime, -60f, 60f);
            rotationRoot.localRotation = inspectionStartRotation * Quaternion.Euler(0f, inspectionYaw, 0f);
        }
        else
        {
            inspectionPitch -= axis.y * rotationSpeed * Time.deltaTime;
            rotationRoot.localRotation = inspectionStartRotation * Quaternion.Euler(inspectionPitch, 0f, 0f);
        }
    }

    private static Vector2 ReadRightStickInput()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!device.isValid)
            return Vector2.zero;
        Vector2 axis = Vector2.zero;
        device.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis);
        if (axis.sqrMagnitude < 0.04f)
            device.TryGetFeatureValue(CommonUsages.secondary2DAxis, out axis);
        return axis;
    }

    private void CaptureInspectionObjectScale(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        RestoreInspectionObjectScale();
        scaledInspectionObject = targetObject;
        scaledInspectionOriginalScale = targetObject.transform.localScale;
    }

    private void RestoreInspectionObjectScale()
    {
        if (scaledInspectionObject != null)
            scaledInspectionObject.transform.localScale = scaledInspectionOriginalScale;

        scaledInspectionObject = null;
    }

    private void ApplyInspectionScaleForVr(GameObject targetObject, float desiredMaxSize)
    {
        if (targetObject == null || scaledInspectionObject != targetObject)
            return;

        targetObject.transform.localScale = scaledInspectionOriginalScale;

        if (!TryGetRendererBounds(targetObject, out Bounds bounds))
            return;

        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDimension <= 0.001f)
            return;

        float factor = Mathf.Clamp(desiredMaxSize / maxDimension, 0.25f, 1.8f);
        targetObject.transform.localScale = scaledInspectionOriginalScale * factor;
    }

    private void HandleInspectionBackdropFollow(Vector3 flatForward, Vector3 desiredCenter)
    {
        // White void is handled by camera clear colour — nothing to update.
    }

    private void HandleSecondaryButtonExitInspection()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool secondaryPressed = false;
        if (device.isValid)
            device.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryPressed);

        if (secondaryPressed && !wasSecondaryPressed)
        {
            if (vrInspectionActive || (inspectionManager != null && inspectionManager.IsInspecting))
            {
                InspectionPartType cluePart = activeInspectionPart;
                vrInspectionActive = false;
                if (inspectionManager != null && inspectionManager.IsInspecting)
                    inspectionManager.ExitInspection();
                else
                    HideInspectionVisuals();

                if (hasPendingInspectionClue)
                {
                    cluePart = pendingInspectionCluePart;
                    hasPendingInspectionClue = false;
                }
                ShowInspectionClue(cluePart);
            }
            else if (medkitCanvasRoot != null && medkitCanvasRoot.activeSelf)
                CancelMedkitCanvas();
            else if (scenarioManager != null &&
                scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InMedkitView)
                CancelMedkitCanvas();
        }

        wasSecondaryPressed = secondaryPressed;
    }

    private bool ReadRightTrigger(out float value)
    {
        value = 0f;
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!device.isValid)
            return false;

        device.TryGetFeatureValue(CommonUsages.trigger, out value);

        bool pressed = wasTriggerPressed
            ? value > TriggerReleaseThreshold
            : value > TriggerPressThreshold;

        return pressed;
    }

    private void HandleMedkitViewFollow()
    {
        if (scenarioManager != null
            && scenarioManager.CurrentState != MedicalScenarioManager.ScenarioState.InMedkitView)
        {
            DestroyMedkitCanvas();
            HideRealMedkitView();
            return;
        }

        if (medkitCanvasRoot != null && medkitCanvasRoot.activeSelf)
            HandleMedkitCanvasFollow();

        if (medkitViewRoot == null || xrCamera == null || scenarioManager == null ||
            scenarioManager.CurrentState != MedicalScenarioManager.ScenarioState.InMedkitView)
            return;

        ShowRealMedkitView();
    }

    private void HandleMedkitCanvasFollow()
    {
        if (medkitCanvasRoot == null || !medkitCanvasRoot.activeSelf || xrCamera == null)
            return;

        Vector3 targetPos = xrCamera.transform.position
            + xrCamera.transform.forward * MedkitCanvasDistance;
        medkitCanvasRoot.transform.position = targetPos;
        medkitCanvasRoot.transform.rotation = Quaternion.LookRotation(
            targetPos - xrCamera.transform.position, Vector3.up);
    }

    private void ShowRealMedkitView()
    {
        if (medkitViewRoot == null || xrCamera == null)
            return;

        medkitViewRoot.gameObject.SetActive(true);
        if (medkitBackdropRoot != null)
            medkitBackdropRoot.SetActive(false);

        // Hide the 3-D scene's own "Choose the…" label (not VR-friendly).
        HideMedkitDesktopText();
        HideMedkitViewDecorations();

        Vector3 forward = xrCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = xrCamera.transform.forward;
        forward.Normalize();

        Vector3 targetCenter = xrCamera.transform.position
            + forward * 1.18f
            + Vector3.down * 0.06f;

        medkitViewRoot.localScale = medkitViewOriginalPoseCaptured
            ? medkitViewOriginalScale
            : Vector3.one;
        ApplyMedkitScaleForVr();
        medkitViewRoot.position = targetCenter;
        medkitViewRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (TryGetMedicationItemBounds(out Bounds bounds))
            medkitViewRoot.position += targetCenter - bounds.center;
        else if (TryGetRendererBounds(medkitViewRoot.gameObject, out bounds))
            medkitViewRoot.position += targetCenter - bounds.center;
        else
            medkitViewRoot.position = targetCenter;

        // Backdrop is camera-parented — no manual positioning needed.
    }

    private void HideRealMedkitView()
    {
        if (medkitViewRoot == null || !medkitViewOriginalPoseCaptured)
            return;

        medkitViewRoot.position = medkitViewOriginalPosition;
        medkitViewRoot.rotation = medkitViewOriginalRotation;
        medkitViewRoot.localScale = medkitViewOriginalScale;
        RestoreMedkitItemScales();
        if (medkitBackdropRoot != null && medkitBackdropRoot.activeSelf)
            medkitBackdropRoot.SetActive(false);
    }

    private void ApplyMedkitScaleForVr()
    {
        if (medkitViewRoot == null)
            return;

        foreach (MedicationItem item in medkitViewRoot.GetComponentsInChildren<MedicationItem>(true))
        {
            if (item == null)
                continue;

            Transform itemRoot = ResolveMedkitItemRoot(item.transform);
            if (itemRoot == null)
                continue;

            if (!medkitItemOriginalScales.ContainsKey(itemRoot))
                medkitItemOriginalScales[itemRoot] = itemRoot.localScale;

            itemRoot.localScale = medkitItemOriginalScales[itemRoot] * VrMedkitItemScale;
        }
    }

    private void RestoreMedkitItemScales()
    {
        foreach (var pair in medkitItemOriginalScales)
        {
            if (pair.Key != null)
                pair.Key.localScale = pair.Value;
        }
    }

    private void EnsureInspectionBackdrop()
    {
        // White void is provided by the camera clear colour; no canvas backdrop needed.
    }

    private void EnsureMedkitBackdrop()
    {
        if (medkitBackdropRoot != null)
            return;

        if (xrCamera == null)
            return;

        // Small title canvas parented to camera — no white background needed because
        // the camera clear colour is already solid white in white-void mode.
        medkitBackdropRoot = new GameObject("VR Medkit Title");
        medkitBackdropRoot.transform.SetParent(xrCamera.transform, false);
        medkitBackdropRoot.transform.localPosition = new Vector3(0f, 0.38f, 1.1f);
        medkitBackdropRoot.transform.localRotation = Quaternion.identity;

        Canvas canvas = medkitBackdropRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 250;
        canvas.worldCamera = xrCamera;

        RectTransform rect = medkitBackdropRoot.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(900f, 120f);
        rect.localScale = Vector3.one * 0.0014f;

        TextMeshProUGUI title = MakeText(medkitBackdropRoot.transform, "MedkitTitle",
            "Choose Medication", Vector2.zero, new Vector2(860f, 100f), 52f, true);
        title.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        title.alignment = TextAlignmentOptions.Center;

        medkitBackdropRoot.SetActive(false);
    }

    private bool TryGetMedicationItemBounds(out Bounds bounds)
    {
        bounds = default;
        if (medkitViewRoot == null)
            return false;

        bool hasBounds = false;
        System.Collections.Generic.HashSet<Transform> includedRoots =
            new System.Collections.Generic.HashSet<Transform>();

        foreach (MedicationItem item in medkitViewRoot.GetComponentsInChildren<MedicationItem>(true))
        {
            if (item == null || !item.gameObject.activeInHierarchy)
                continue;

            Transform itemRoot = ResolveMedkitItemRoot(item.transform);
            if (itemRoot == null || !includedRoots.Add(itemRoot))
                continue;

            foreach (Renderer renderer in itemRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || renderer.GetComponent<TMP_Text>() != null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return hasBounds;
    }

    private Transform ResolveMedkitItemRoot(Transform itemTransform)
    {
        if (itemTransform == null || medkitViewRoot == null)
            return itemTransform;

        Transform current = itemTransform;
        while (current.parent != null && current.parent != medkitViewRoot)
            current = current.parent;

        return current;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        bool hasBounds = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void EnsureInspectionClueCanvas()
    {
        if (inspectionClueRoot != null)
            return;

        inspectionClueRoot = new GameObject("VR Inspection Clue Canvas");
        inspectionClueRoot.transform.SetParent(transform, false);

        Canvas canvas = inspectionClueRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 245;
        canvas.worldCamera = xrCamera;
        inspectionClueCanvasGroup = inspectionClueRoot.AddComponent<CanvasGroup>();

        RectTransform rect = inspectionClueRoot.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(760f, 260f);
        rect.localScale = Vector3.one * 0.0016f;

        Image background = new GameObject("Background").AddComponent<Image>();
        background.transform.SetParent(inspectionClueRoot.transform, false);
        background.color = new Color(0f, 0f, 0f, 0.78f);
        StretchToParent(background.rectTransform);

        inspectionClueText = MakeText(inspectionClueRoot.transform, "Clue", "",
            Vector2.zero, new Vector2(700f, 210f), 28f, true);
        inspectionClueText.color = Color.white;
        inspectionClueText.alignment = TextAlignmentOptions.Center;
    }

    private void HandleInspectionClueFollow()
    {
        if (inspectionClueRoot == null || !inspectionClueRoot.activeSelf || xrCamera == null)
            return;

        if (ShouldFadeInspectionClue())
        {
            FadeInspectionClueOut();
            return;
        }

        Vector3 targetPos = xrCamera.transform.position
            + xrCamera.transform.forward * 1.25f
            + Vector3.up * 0.08f;
        inspectionClueRoot.transform.position = targetPos;
        inspectionClueRoot.transform.rotation = Quaternion.LookRotation(
            targetPos - xrCamera.transform.position, Vector3.up);
    }

    private bool ShouldFadeInspectionClue()
    {
        return scenarioManager != null &&
            (scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.Success ||
             scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.Failure ||
             scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InMedkitView);
    }

    private void FadeInspectionClueOut()
    {
        if (inspectionClueCanvasGroup == null)
        {
            inspectionClueRoot.SetActive(false);
            return;
        }

        if (inspectionClueFadeStartTime < 0f)
            inspectionClueFadeStartTime = Time.time;

        const float fadeDuration = 0.45f;
        float t = Mathf.Clamp01((Time.time - inspectionClueFadeStartTime) / fadeDuration);
        inspectionClueCanvasGroup.alpha = 1f - t;

        if (t >= 1f)
        {
            inspectionClueRoot.SetActive(false);
            inspectionClueFadeStartTime = -1f;
        }
    }

    private void BuildObjectiveHud()
    {
        if (objectiveHudRoot != null || xrCamera == null)
            return;

        objectiveHudRoot = new GameObject("VR Restaurant Objective HUD");
        objectiveHudRoot.transform.SetParent(transform, false);

        Canvas canvas = objectiveHudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 240;
        canvas.worldCamera = xrCamera;

        RectTransform rect = objectiveHudRoot.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(720f, 120f);
        rect.localScale = Vector3.one * 0.0016f;

        Image background = new GameObject("Background").AddComponent<Image>();
        background.transform.SetParent(objectiveHudRoot.transform, false);
        background.color = new Color(0f, 0f, 0f, 0.62f);
        StretchToParent(background.rectTransform);

        vrObjectiveText = MakeText(objectiveHudRoot.transform, "Objective", "",
            Vector2.zero, new Vector2(660f, 86f), 30f, true);
        vrObjectiveText.alignment = TextAlignmentOptions.Center;
        vrObjectiveText.color = Color.white;

        HandleObjectiveHudFollow();
    }

    private void HandleObjectiveHudFollow()
    {
        if (objectiveHudRoot == null || xrCamera == null)
            return;

        HideSourceObjectiveText();

        string displayText = GetVrObjectiveDisplayText();
        if (vrObjectiveText != null)
            vrObjectiveText.text = displayText;

        bool shouldShow =
            !string.IsNullOrWhiteSpace(displayText) &&
            (medkitCanvasRoot == null || !medkitCanvasRoot.activeSelf) &&
            (inspectionManager == null || !inspectionManager.IsInspecting);

        if (objectiveHudRoot.activeSelf != shouldShow)
            objectiveHudRoot.SetActive(shouldShow);

        if (!shouldShow)
            return;

        bool dialogueUiActive = IsQuestionPanelActive() || IsAnswerPanelActive();
        ConfigureObjectiveHudForState(dialogueUiActive);

        float distance = dialogueUiActive ? UiCanvasDistance : ObjectiveHudDistance;
        Vector3 verticalOffset = dialogueUiActive
            ? Vector3.up * 0.72f
            : Vector3.down * 0.28f;

        if (IsIntroTextActive())
            verticalOffset = Vector3.down * 0.08f;

        if (inspectionClueRoot != null && inspectionClueRoot.activeSelf)
            verticalOffset = Vector3.down * 0.38f;

        Vector3 targetPos = xrCamera.transform.position
            + xrCamera.transform.forward * distance
            + verticalOffset;
        objectiveHudRoot.transform.position = targetPos;
        objectiveHudRoot.transform.rotation = Quaternion.LookRotation(
            targetPos - xrCamera.transform.position, Vector3.up);
    }

    private string GetVrObjectiveDisplayText()
    {
        string text;
        if (IsIntroTextActive())
            text = introText.text;
        else
            text = objectiveText != null ? objectiveText.text : "";

        return ToVrPromptText(text);
    }

    private static string ToVrPromptText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("Press E to continue", "Press trigger to continue")
            .Replace("Press E", "Press trigger");
    }

    private bool IsIntroTextActive()
    {
        return introText != null &&
            introText.gameObject.activeInHierarchy &&
            !string.IsNullOrWhiteSpace(introText.text);
    }

    private void ConfigureObjectiveHudForState(bool dialogueUiActive)
    {
        RectTransform rect = objectiveHudRoot.GetComponent<RectTransform>();
        if (rect == null || vrObjectiveText == null)
            return;

        if (dialogueUiActive)
        {
            rect.sizeDelta = new Vector2(780f, 70f);
            rect.localScale = Vector3.one * 0.00115f;
            vrObjectiveText.rectTransform.sizeDelta = new Vector2(720f, 48f);
            vrObjectiveText.fontSize = 21f;
        }
        else if (IsIntroTextActive())
        {
            rect.sizeDelta = new Vector2(920f, 150f);
            rect.localScale = Vector3.one * 0.00155f;
            vrObjectiveText.rectTransform.sizeDelta = new Vector2(850f, 105f);
            vrObjectiveText.fontSize = 31f;
        }
        else
        {
            rect.sizeDelta = new Vector2(760f, 130f);
            rect.localScale = Vector3.one * 0.0015f;
            vrObjectiveText.rectTransform.sizeDelta = new Vector2(700f, 92f);
            vrObjectiveText.fontSize = 29f;
        }
    }

    private void HideMedkitDesktopText()
    {
        Transform medkitUi = FindExactTransform("MedkitUI");
        if (medkitUi != null && medkitUi.gameObject.activeSelf)
            medkitUi.gameObject.SetActive(false);

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null) continue;
            string content = text.text ?? "";
            string lowerName = text.name.ToLowerInvariant();
            bool isMedkitInstruction =
                lowerName.Contains("medkitinstruction") ||
                content.IndexOf("choose medication", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                content.IndexOf("choose the correct", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isMedkitInstruction)
                text.gameObject.SetActive(false);
        }
    }

    private void HideMedkitViewDecorations()
    {
        if (medkitViewRoot == null)
            return;

        foreach (Transform child in medkitViewRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == medkitViewRoot)
                continue;

            if (IsInsideMedicationDisplay(child))
                continue;

            string lowerName = child.name.ToLowerInvariant();
            bool isDecoration =
                lowerName.Contains("background") ||
                lowerName.Contains("title") ||
                lowerName.Contains("instruction") ||
                child.GetComponent<TMP_Text>() != null;

            if (isDecoration && child.gameObject.activeSelf)
                child.gameObject.SetActive(false);
        }
    }

    private bool IsInsideMedicationDisplay(Transform transform)
    {
        if (transform == null || medkitViewRoot == null)
            return false;

        Transform displayRoot = ResolveMedkitItemRoot(transform);
        return displayRoot != null &&
            displayRoot.GetComponentInChildren<MedicationItem>(true) != null;
    }

    private void EnsureMedkitCanvas()
    {
        if (medkitCanvasRoot != null)
        {
            medkitCanvasRoot.SetActive(true);
            return;
        }

        medkitCanvasRoot = BuildMedkitCanvas();
    }

    private void DestroyMedkitCanvas()
    {
        if (medkitCanvasRoot == null) return;
        Destroy(medkitCanvasRoot);
        medkitCanvasRoot = null;
    }

    private void CancelMedkitCanvas()
    {
        if (scenarioManager != null
            && scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InMedkitView)
        {
            scenarioManager.ExitMedkitView();
        }

        DestroyMedkitCanvas();
        HideRealMedkitView();
    }

    private GameObject BuildMedkitCanvas()
    {
        GameObject root = new GameObject("VR Medkit Canvas");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 250;
        canvas.worldCamera = xrCamera;

        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(700f, 540f);
        rect.localScale = Vector3.one * 0.0018f;

        Image background = new GameObject("Background").AddComponent<Image>();
        background.transform.SetParent(root.transform, false);
        background.color = new Color(0f, 0f, 0f, 0.78f);
        StretchToParent(background.rectTransform);

        TextMeshProUGUI title = MakeText(root.transform, "Title", "Choose Medication",
            new Vector2(0f, 200f), new Vector2(640f, 80f), 44f, true);
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        string[] options = { "Epinephrine", "Antiemetic", "Inhaler" };
        for (int i = 0; i < options.Length; i++)
        {
            string medName = options[i];
            float yPos = 70f - i * 130f;
            CreateMedicationButton(root.transform, medName, yPos);
        }

        TextMeshProUGUI hint = MakeText(root.transform, "Hint",
            "Aim with right controller, press trigger to confirm.\nPress B to cancel.",
            new Vector2(0f, -240f), new Vector2(640f, 70f), 22f, false);
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(0.85f, 0.85f, 0.85f);

        return root;
    }

    private void CreateMedicationButton(Transform parent, string medicationName, float yPos)
    {
        GameObject buttonObject = new GameObject($"VR Medkit Button - {medicationName}");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, yPos);
        rect.sizeDelta = new Vector2(520f, 100f);

        Image img = buttonObject.AddComponent<Image>();
        img.color = new Color(0.18f, 0.36f, 0.78f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.35f, 0.6f, 1f, 1f);
        colors.selectedColor = new Color(0.45f, 0.75f, 1f, 1f);
        colors.pressedColor = new Color(0.1f, 0.25f, 0.6f, 1f);
        button.colors = colors;
        button.targetGraphic = img;

        TextMeshProUGUI label = MakeText(buttonObject.transform, "Label", medicationName,
            Vector2.zero, new Vector2(500f, 80f), 32f, true);
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        string captured = medicationName;
        button.onClick.AddListener(() => OnMedkitButtonClicked(captured));
    }

    private void OnMedkitButtonClicked(string medicationName)
    {
        if (scenarioManager == null) return;

        if (scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InMedkitView)
            scenarioManager.ExitMedkitView();

        scenarioManager.GiveMedication(medicationName);
        DestroyMedkitCanvas();
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string content,
        Vector2 position, Vector2 size, float fontSize, bool bold)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Canvas FindCanvasByName(string name)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas != null && canvas.name == name)
                return canvas;
        }

        return null;
    }

    private static RectTransform FindRectByName(string name)
    {
        foreach (RectTransform rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect != null && rect.name == name)
                return rect;
        }

        return null;
    }

    private static Transform FindExactTransform(string name)
    {
        foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == name)
                return transform;
        }

        return null;
    }

    private static GameObject FindAncestorNamed(Transform transform, string name)
    {
        while (transform != null)
        {
            if (transform.name == name)
                return transform.gameObject;

            transform = transform.parent;
        }

        return null;
    }
}

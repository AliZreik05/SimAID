using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

public class VRRuntimeCompatibility : MonoBehaviour
{
    private const string LobbySceneName = "Lobby Scene";
    private const float XRStartupWaitSeconds = 3f;
    private const float MenuCanvasDistance = 2.4f;
    private const float MenuCanvasScale = 0.00125f;

    private static VRRuntimeCompatibility instance;
    private static bool watcherInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (watcherInstalled)
            return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        watcherInstalled = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        watcherInstalled = false;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance().StartCoroutine(PrepareSceneForVR(scene));
    }

    private static VRRuntimeCompatibility EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject go = new GameObject("VR Runtime Compatibility");
        instance = go.AddComponent<VRRuntimeCompatibility>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private static IEnumerator PrepareSceneForVR(Scene scene)
    {
        if (scene.name == LobbySceneName)
        {
            yield return WaitForXRStartup();

            if (ShouldUseVRRuntime())
            {
                yield return null;
                PrepareVRMainMenu();
                yield break;
            }
        }

        yield return EnsureVRSceneCompatibility();
    }

    private static IEnumerator WaitForXRStartup()
    {
        float startTime = Time.realtimeSinceStartup;

        while (!ShouldUseVRRuntime() && Time.realtimeSinceStartup - startTime < XRStartupWaitSeconds)
            yield return null;
    }

    private static IEnumerator EnsureVRSceneCompatibility()
    {
        yield return null;
        yield return new WaitUntil(() => ShouldUseVRRuntime() || Time.timeSinceLevelLoad > 2f);

        if (!ShouldUseVRRuntime())
            yield break;

        // Install scene-specific adapters BEFORE resolving the runtime camera so their
        // Awake() can suppress auxiliary cameras (e.g. MedkitCamera in the Restaurant
        // scene starts active and tagged MainCamera) before ResolveRuntimeCamera() runs.
        InstallVRCPR();
        InstallVRRestaurant();

        Camera xrCamera = ResolveRuntimeCamera(false);
        HideDesktopInteractionOverlay();
        HideDesktopEndCanvases();
        ConvertScreenSpaceCanvasesForXR(xrCamera);
        ConfigureWorldSpaceCanvases(xrCamera);
        EnsureVRUIPointer(xrCamera);
        EnsureVRScenarioHUD(xrCamera);
    }

    private static bool ShouldUseVRRuntime()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return XRSettings.isDeviceActive;
#endif
    }

    private static void PrepareVRMainMenu()
    {
        Camera xrCamera = ResolveRuntimeCamera(true);
        if (xrCamera == null)
            return;

        EnsureEventSystem();

        Canvas menuCanvas = FindCanvasByName("Level Select Canvas");
        if (menuCanvas != null)
        {
            ConfigureMenuCanvasForVR(menuCanvas, xrCamera);
            SelectFirstButton(menuCanvas);
        }

        EnsureVRUIPointer(xrCamera);
    }

    private static Camera ResolveRuntimeCamera(bool forceMenuReady)
    {
        Camera camera = Camera.main != null ? Camera.main : FindBestAvailableCamera();

        if (camera == null && forceMenuReady)
        {
            GameObject cameraObject = new GameObject("VR Menu Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        if (camera == null)
            return null;

        if (forceMenuReady && camera.GetComponent<VRTrackedCameraPose>() == null)
            camera.gameObject.AddComponent<VRTrackedCameraPose>();

        camera.enabled = true;
        if (forceMenuReady)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }
        else if (RenderSettings.skybox != null && camera.clearFlags == CameraClearFlags.SolidColor)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        camera.cullingMask = ~0;
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, 0.03f);

        if (Camera.main == null)
        {
            try
            {
                camera.tag = "MainCamera";
            }
            catch (UnityException)
            {
                // The project may not expose the MainCamera tag in every build target.
            }
        }

        return camera;
    }

    private static Camera FindBestAvailableCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        }

        foreach (Camera camera in cameras)
        {
            if (camera != null)
                return camera;
        }

        return null;
    }

    private static void ConfigureMenuCanvasForVR(Canvas canvas, Camera xrCamera)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = xrCamera;
        canvas.sortingOrder = 100;

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.SetParent(xrCamera.transform, false);
        rect.localPosition = new Vector3(0f, 0f, MenuCanvasDistance);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * MenuCanvasScale;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1920f, 1080f);

        ConfigureCanvasRaycaster(canvas);
    }

    private static void ConvertScreenSpaceCanvasesForXR(Camera xrCamera)
    {
        if (xrCamera == null)
            return;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            if (IsDesktopInteractionCanvas(canvas) || IsDesktopOnlyEndCanvas(canvas))
                continue;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = xrCamera;
            canvas.planeDistance = 1.2f;

            ConfigureCanvasRaycaster(canvas);
        }
    }

    private static void ConfigureWorldSpaceCanvases(Camera xrCamera)
    {
        if (xrCamera == null)
            return;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                continue;

            if (canvas.worldCamera == null)
                canvas.worldCamera = xrCamera;

            ConfigureCanvasRaycaster(canvas);
        }
    }

    private static void HideDesktopInteractionOverlay()
    {
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null)
                continue;

            string textValue = text.text == null ? string.Empty : text.text.Replace("\n", " ");
            bool looksLikeDesktopPrompt =
                text.name.Contains("Press E") ||
                textValue.Contains("Press E");

            if (!looksLikeDesktopPrompt)
                continue;

            Canvas canvas = text.GetComponentInParent<Canvas>(true);
            if (canvas != null && IsDesktopInteractionCanvas(canvas))
            {
                canvas.gameObject.SetActive(false);
                continue;
            }

            text.gameObject.SetActive(false);
        }
    }

    private static void HideDesktopEndCanvases()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas != null && IsDesktopOnlyEndCanvas(canvas))
                canvas.gameObject.SetActive(false);
        }
    }

    private static bool IsDesktopInteractionCanvas(Canvas canvas)
    {
        if (canvas == null)
            return false;

        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            string textValue = text.text == null ? string.Empty : text.text.Replace("\n", " ");
            if (text.name.Contains("Press E") || textValue.Contains("Press E"))
                return true;
        }

        return false;
    }

    private static bool IsDesktopOnlyEndCanvas(Canvas canvas)
    {
        if (canvas == null)
            return false;

        string canvasName = canvas.name.ToLowerInvariant();
        bool isEndCanvas = canvasName.Contains("lose") || canvasName.Contains("fail");
        bool isVRCanvas = canvasName.Contains("vr");

        return isEndCanvas && !isVRCanvas;
    }

    private static void ConfigureCanvasRaycaster(Canvas canvas)
    {
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();

        raycaster.ignoreReversedGraphics = false;
    }

    private static Canvas FindCanvasByName(string canvasName)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas != null && canvas.name == canvasName)
                return canvas;
        }

        return null;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private static void SelectFirstButton(Canvas canvas)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || canvas == null)
            return;

        foreach (Button button in canvas.GetComponentsInChildren<Button>(false))
        {
            if (button != null && button.IsInteractable())
            {
                eventSystem.SetSelectedGameObject(button.gameObject);
                return;
            }
        }
    }

    private static void EnsureVRUIPointer(Camera xrCamera)
    {
        if (xrCamera == null)
            return;

        VRControllerUIPointer pointer = FindFirstObjectByType<VRControllerUIPointer>(FindObjectsInactive.Include);
        if (pointer == null)
        {
            GameObject pointerObject = new GameObject("VR Controller UI Pointer");
            pointer = pointerObject.AddComponent<VRControllerUIPointer>();
            DontDestroyOnLoad(pointerObject);
        }

        pointer.SetCamera(xrCamera);
    }

    private static void EnsureVRScenarioHUD(Camera xrCamera)
    {
        if (xrCamera == null || FindFirstObjectByType<ScenarioGameLoop>() == null)
            return;

        VRScenarioHUD hud = FindFirstObjectByType<VRScenarioHUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            GameObject hudObject = new GameObject("VR Scenario HUD");
            hud = hudObject.AddComponent<VRScenarioHUD>();
        }

        hud.SetCamera(xrCamera);
    }

    private static void InstallVRCPR()
    {
        GameObject target = GameObject.Find("CPR_Target");
        if (target == null || target.GetComponent<VRCPRInteraction>() != null)
            return;

        target.AddComponent<VRCPRInteraction>();
    }

    private static void InstallVRRestaurant()
    {
        // Only the Restaurant scene needs the restaurant adapter; the script
        // self-checks the active scene name, so calling this in other scenes is harmless.
        if (SceneManager.GetActiveScene().name != "Restaurant Scene")
            return;

        if (FindFirstObjectByType<VRRestaurantInteraction>(FindObjectsInactive.Include) != null)
            return;

        GameObject host = new GameObject("VR Restaurant Interaction");
        host.AddComponent<VRRestaurantInteraction>();
    }
}

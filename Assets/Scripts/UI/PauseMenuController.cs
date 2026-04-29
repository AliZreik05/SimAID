using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
public class PauseMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "Lobby Scene";

    [Header("Input")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    private static PauseMenuController instance;
    private static bool sceneWatcherInstalled;

    private GameObject pauseRoot;
    private Slider volumeSlider;
    private bool isPaused;
    private bool cursorWasVisible;
    private CursorLockMode previousCursorLockMode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForGameplayScene()
    {
        if (SceneManager.GetActiveScene().name == "Lobby Scene")
            return;

        EnsureInstance();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneWatcher()
    {
        if (sceneWatcherInstalled)
            return;

        SceneManager.sceneLoaded += HandleAnySceneLoaded;
        sceneWatcherInstalled = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        instance = null;
        sceneWatcherInstalled = false;
    }

    private static void HandleAnySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Lobby Scene")
            EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject controllerObject = new GameObject("Pause Menu Controller");
        instance = controllerObject.AddComponent<PauseMenuController>();
        DontDestroyOnLoad(controllerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        EnsureEventSystem();
        BuildPauseMenu();
        HidePauseMenuForScene();
    }

    private void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(pauseKey))
            return;

        if (EscapeInputGuard.WasHandledThisFrame)
            return;

        if (!isPaused && (ShouldLetScenarioUseEscape() || ShouldLetCprUseEscape()))
            return;

        EscapeInputGuard.MarkHandled();
        SetPaused(!isPaused);
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void ReturnToMainMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainMenuSceneName)
        {
            SetPaused(false);
            Destroy(gameObject);
            return;
        }

        EnsureEventSystem();
        BuildPauseMenu();
        HidePauseMenuForScene();
    }

    private void SetPaused(bool paused)
    {
        if (isPaused == paused)
            return;

        bool wasPaused = isPaused;
        isPaused = paused;

        if (paused)
        {
            cursorWasVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
        }

        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;

        if (pauseRoot != null)
            pauseRoot.SetActive(paused);

        if (paused)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else if (wasPaused)
        {
            Cursor.visible = cursorWasVisible;
            Cursor.lockState = previousCursorLockMode;
        }
    }

    private void HidePauseMenuForScene()
    {
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseRoot != null)
            pauseRoot.SetActive(false);
    }

    private bool ShouldLetScenarioUseEscape()
    {
        MedicalScenarioManager scenarioManager = FindObjectOfType<MedicalScenarioManager>();

        if (scenarioManager == null)
            return false;

        if (scenarioManager.HasSkippableDialogue)
            return true;

        return scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InspectionView ||
               scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.InMedkitView;
    }

    private bool ShouldLetCprUseEscape()
    {
        CPRMinigame cprMinigame = FindObjectOfType<CPRMinigame>();
        return cprMinigame != null && cprMinigame.IsRunning;
    }

    private void BuildPauseMenu()
    {
        if (pauseRoot != null)
            return;

        Canvas canvas = CreateCanvas();
        pauseRoot = canvas.gameObject;

        RectTransform overlay = CreatePanel("Pause Overlay", canvas.transform);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        Image overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.62f);

        RectTransform panel = CreatePanel("Pause Panel", overlay);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(560f, 430f);

        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.07f, 0.08f, 0.94f);

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.padding = new RectOffset(42, 42, 36, 36);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        CreateText("Title", panel, "Paused", 46, FontStyles.Bold, TextAlignmentOptions.Center, 58f);
        CreateText("Hint", panel, "Take a moment, adjust sound, or return to the lobby.", 20, FontStyles.Normal, TextAlignmentOptions.Center, 48f);

        CreateVolumeControl(panel);
        CreateButton(panel, "Resume", Resume);
        CreateButton(panel, "Quit to Main Menu", ReturnToMainMenu);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Pause Menu Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private void CreateVolumeControl(Transform parent)
    {
        RectTransform group = CreatePanel("Volume Control", parent);
        group.sizeDelta = new Vector2(0f, 82f);

        VerticalLayoutGroup layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        CreateText("Label", group, "Master Volume", 22, FontStyles.Bold, TextAlignmentOptions.Left, 28f);

        GameObject sliderObject = new GameObject("Volume Slider");
        sliderObject.transform.SetParent(group, false);

        volumeSlider = sliderObject.AddComponent<Slider>();
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = AudioListener.volume;
        volumeSlider.onValueChanged.AddListener(value => AudioListener.volume = value);

        RectTransform sliderRect = volumeSlider.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(0f, 32f);

        CreateSliderVisuals(volumeSlider);
    }

    private void CreateSliderVisuals(Slider slider)
    {
        RectTransform sliderRect = slider.GetComponent<RectTransform>();

        RectTransform background = CreatePanel("Background", sliderRect);
        background.anchorMin = new Vector2(0f, 0.35f);
        background.anchorMax = new Vector2(1f, 0.65f);
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;

        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.18f, 0.24f, 0.26f, 1f);

        RectTransform fillArea = CreatePanel("Fill Area", sliderRect);
        fillArea.anchorMin = new Vector2(0f, 0.35f);
        fillArea.anchorMax = new Vector2(1f, 0.65f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        RectTransform fill = CreatePanel("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.45f, 0.9f, 0.96f, 1f);

        RectTransform handle = CreatePanel("Handle", sliderRect);
        handle.sizeDelta = new Vector2(26f, 26f);

        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.targetGraphic = handleImage;
        slider.fillRect = fill;
        slider.handleRect = handle;
    }

    private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 60f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.1f, 0.18f, 0.2f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.16f, 0.28f, 0.31f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.05f, 0.11f, 0.13f, 1f);
        button.colors = colors;

        CreateText("Label", buttonObject.transform, label, 24, FontStyles.Bold, TextAlignmentOptions.Center, 44f);
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string text,
        int fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        float preferredHeight)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;

        return tmp;
    }

    private RectTransform CreatePanel(string objectName, Transform parent)
    {
        GameObject panel = new GameObject(objectName);
        panel.transform.SetParent(parent, false);
        return panel.AddComponent<RectTransform>();
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelectMenu : MonoBehaviour
{
    [Serializable]
    public class LevelOption
    {
        public string displayName;
        public string sceneName;

        [TextArea(2, 4)]
        public string description;

        public bool available = true;
    }

    [Header("Content")]
    [SerializeField] private string title = "SimAID";
    [SerializeField] private string subtitle = "Emergency Response Training";
    [SerializeField] private LevelOption[] levels;

    [Header("Background")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0.06f, 0.07f, 1f);
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.3f);

    [Header("Layout")]
    [SerializeField] private int titleFontSize = 92;
    [SerializeField] private int subtitleFontSize = 30;
    [SerializeField] private int buttonTitleFontSize = 32;
    [SerializeField] private int buttonDescriptionFontSize = 20;
    [SerializeField] private float menuWidth = 720f;

    private static readonly LevelOption[] DefaultLevels =
    {
        new LevelOption
        {
            displayName = "City Accident",
            sceneName = "City Scene",
            description = "Scene safety, emergency response, and first-aid intervention."
        },
        new LevelOption
        {
            displayName = "Restaurant Emergency",
            sceneName = "Restaurant Scene",
            description = "Patient assessment, clinical reasoning, inspection, and medication choice."
        }
    };

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureCamera();
        EnsureEventSystem();
        BuildMenu();
    }

    public void LoadLevel(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("LevelSelectMenu: Cannot load an empty scene name.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void BuildMenu()
    {
        LevelOption[] menuLevels = levels == null || levels.Length == 0
            ? DefaultLevels
            : levels;

        Canvas canvas = CreateCanvas();
        CreateBackground(canvas.transform);

        RectTransform content = CreatePanel("Menu Content", canvas.transform);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(menuWidth, 0f);

        Image panelImage = content.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.01f, 0.025f, 0.03f, 0.78f);

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 14f;
        layout.padding = new RectOffset(72, 54, 82, 68);
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateText("Title", content, title, titleFontSize, FontStyles.Bold, TextAlignmentOptions.Left, 104f);
        CreateText("Subtitle", content, subtitle, subtitleFontSize, FontStyles.Normal, TextAlignmentOptions.Left, 42f);
        CreateText("Mode Label", content, "Select a scenario", 22, FontStyles.Bold, TextAlignmentOptions.Left, 34f);
        CreateSpacer(content, 22f);

        foreach (LevelOption level in menuLevels)
            CreateLevelButton(content, level);

        CreateSpacer(content, 24f);
        CreateSmallButton(content, "Quit", QuitGame);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Level Select Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    private void CreateBackground(Transform parent)
    {
        RectTransform background = CreatePanel("Background", parent);
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;

        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = backgroundSprite == null ? backgroundColor : Color.white;
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;

        RectTransform overlay = CreatePanel("Background Overlay", parent);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        Image overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = overlayColor;

        RectTransform vignette = CreatePanel("Left Vignette", parent);
        vignette.anchorMin = Vector2.zero;
        vignette.anchorMax = Vector2.one;
        vignette.offsetMin = Vector2.zero;
        vignette.offsetMax = Vector2.zero;

        Image vignetteImage = vignette.gameObject.AddComponent<Image>();
        vignetteImage.color = new Color(0f, 0f, 0f, 0.18f);
    }

    private void CreateLevelButton(Transform parent, LevelOption level)
    {
        Button button = CreateButtonBase(parent, level.displayName, level.available);
        button.onClick.AddListener(() => LoadLevel(level.sceneName));

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 132f);

        RectTransform accent = CreatePanel("Accent", button.transform);
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(0f, 1f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(7f, 0f);

        Image accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = level.available
            ? new Color(0.42f, 0.88f, 0.95f, 0.95f)
            : new Color(0.45f, 0.48f, 0.5f, 0.9f);

        VerticalLayoutGroup layout = button.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.padding = new RectOffset(32, 30, 18, 16);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        CreateText("Level Name", button.transform, level.displayName, buttonTitleFontSize, FontStyles.Bold, TextAlignmentOptions.Left, 40f);
        CreateText("Level Description", button.transform, level.description, buttonDescriptionFontSize, FontStyles.Normal, TextAlignmentOptions.Left, 54f);
    }

    private void CreateSmallButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButtonBase(parent, label, true);
        button.onClick.AddListener(action);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 56f);

        CreateText("Label", button.transform, label, 22, FontStyles.Bold, TextAlignmentOptions.Center, 40f);
    }

    private Button CreateButtonBase(Transform parent, string name, bool interactable)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = interactable
            ? new Color(0.07f, 0.12f, 0.14f, 0.84f)
            : new Color(0.12f, 0.14f, 0.15f, 0.64f);

        Button button = buttonObject.AddComponent<Button>();
        button.interactable = interactable;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.12f, 0.2f, 0.23f, 0.94f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.03f, 0.08f, 0.1f, 1f);
        colors.disabledColor = image.color;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        return button;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string text,
        int fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        float preferredHeight = 0f)
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

        if (parent.GetComponent<Button>() != null)
        {
            tmp.color = objectName == "Level Description"
                ? new Color(0.78f, 0.86f, 0.88f, 1f)
                : Color.white;
        }
        else if (objectName == "Mode Label")
            tmp.color = new Color(0.65f, 0.85f, 0.9f, 1f);

        if (objectName == "Title")
            tmp.characterSpacing = 3f;

        if (preferredHeight > 0f)
        {
            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
        }

        return tmp;
    }

    private void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(parent, false);

        LayoutElement layout = spacer.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
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

    private static void EnsureCamera()
    {
        if (Camera.main != null || FindObjectOfType<Camera>() != null)
            return;

        GameObject cameraObject = new GameObject("Menu Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 0;
    }
}

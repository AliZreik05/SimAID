using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScenarioUIPolisher : MonoBehaviour
{
    private static ScenarioUIPolisher instance;
    private static bool sceneWatcherInstalled;

    private static readonly Color PanelColor = new Color(0.015f, 0.035f, 0.04f, 0.82f);
    private static readonly Color PanelStrongColor = new Color(0.015f, 0.025f, 0.03f, 0.92f);
    private static readonly Color QuestionButtonColor = new Color(0.82f, 0.94f, 0.97f, 0.96f);
    private static readonly Color QuestionButtonHoverColor = new Color(0.95f, 0.99f, 1f, 1f);
    private static readonly Color QuestionButtonPressedColor = new Color(0.56f, 0.79f, 0.86f, 1f);
    private static readonly Color DarkTextColor = new Color(0.03f, 0.07f, 0.08f, 1f);
    private static readonly Color AccentColor = new Color(0.46f, 0.9f, 0.96f, 1f);
    private static readonly Color BackplateColor = new Color(0f, 0f, 0f, 0.56f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
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

        GameObject go = new GameObject("Scenario UI Polisher");
        instance = go.AddComponent<ScenarioUIPolisher>();
        DontDestroyOnLoad(go);
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
        StartCoroutine(ApplyRepeatedly());
    }

    private void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Lobby Scene")
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(ApplyRepeatedly());
    }

    private IEnumerator ApplyRepeatedly()
    {
        ApplyToActiveScene();
        yield return null;
        ApplyToActiveScene();
        yield return new WaitForSecondsRealtime(0.25f);
        ApplyToActiveScene();
        yield return new WaitForSecondsRealtime(0.75f);
        ApplyToActiveScene();
    }

    private void ApplyToActiveScene()
    {
        StylePanels();
        StyleInspectionUi();
        StyleResultUi();
        StyleHudAndPromptText();
    }

    private void StylePanels()
    {
        foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (!IsRuntimeSceneObject(image.gameObject))
                continue;

            string name = image.gameObject.name;

            if (name == "QuestionPanel")
                continue;

            if (IsResultPanelName(name))
                image.color = PanelStrongColor;
            else if (name == "ObjectiveBanner")
                image.color = new Color(0.02f, 0.05f, 0.055f, 0.74f);
            else if (IsGenericPanelName(name))
                image.color = PanelColor;
        }
    }

    private void StyleInspectionUi()
    {
        TMP_Text title = FindTextByName("InspectionTitleText");
        TMP_Text body = FindTextByName("InspectionBodyText");
        TMP_Text exitHint = FindTextByName("InspectionExitHintText");

        if (title != null)
        {
            StyleReadableOverlayText(title, 26f, 32f, TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(560f, 52f));
            EnsureBackplate(title, new Vector2(48f, 18f), new Color(0f, 0f, 0f, 0.5f));
        }

        if (body != null)
        {
            StyleReadableOverlayText(body, 22f, 28f, TextAlignmentOptions.Center);
            body.enableWordWrapping = true;
            SetRect(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(980f, 96f));
            EnsureBackplate(body, new Vector2(56f, 28f), new Color(0f, 0f, 0f, 0.58f));
        }

        if (exitHint != null)
        {
            StyleReadableOverlayText(exitHint, 18f, 22f, TextAlignmentOptions.Left);
            exitHint.color = AccentColor;
            SetRect(exitHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 28f), new Vector2(260f, 38f));
            EnsureBackplate(exitHint, new Vector2(28f, 12f), new Color(0f, 0f, 0f, 0.45f));
        }
    }

    private void StyleResultUi()
    {
        TMP_Text body = FindTextByName("ResultBodyText");

        if (body != null)
        {
            body.color = Color.white;
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Overflow;
            body.enableAutoSizing = true;
            body.fontSizeMin = 14f;
            body.fontSizeMax = 24f;
        }

        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (!IsRuntimeSceneObject(text.gameObject))
                continue;

            string lowerName = text.gameObject.name.ToLowerInvariant();

            if (!lowerName.Contains("resulttitle") && !lowerName.Contains("win") && !lowerName.Contains("fail"))
                continue;

            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 1f;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void StyleHudAndPromptText()
    {
        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (!IsRuntimeSceneObject(text.gameObject))
                continue;

            if (IsQuestionOrInspectionText(text))
                continue;

            string value = text.text;
            string name = text.gameObject.name.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(value))
                continue;

            bool looksLikeHud =
                ContainsAny(value, "Bleed timer", "Cones:", "Bandages:", "CPR:", "Pick up", "Apply bandage", "Start CPR", "Need bandage", "Treated") ||
                ContainsAny(value, "You arrive at", "Approach the patient", "Ask up to", "Assessment complete", "Inspect", "Scenario");

            if (!looksLikeHud && !name.Contains("objective"))
                continue;

            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = Mathf.Max(text.fontSize, 24f);

            RectTransform rect = text.rectTransform;

            if (ContainsAny(value, "Bleed timer"))
            {
                SetRect(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -26f), new Vector2(430f, 118f));
                text.alignment = TextAlignmentOptions.TopRight;
                EnsureBackplate(text, new Vector2(34f, 22f), new Color(0f, 0f, 0f, 0.38f));
            }
            else if (ContainsAny(value, "Cones:", "Bandages:", "CPR:"))
            {
                SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(330f, 104f));
                text.alignment = TextAlignmentOptions.TopLeft;
                EnsureBackplate(text, new Vector2(34f, 22f), new Color(0f, 0f, 0f, 0.38f));
            }
            else if (ContainsAny(value, "Choose the correct item"))
            {
                rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 720f), Mathf.Max(rect.sizeDelta.y, 58f));
                text.alignment = TextAlignmentOptions.Center;
            }
            else if (ContainsAny(value, "You arrive at", "Approach the patient", "Ask up to", "Assessment complete", "Inspect"))
            {
                rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 410f), Mathf.Max(rect.sizeDelta.y, 118f));
                text.alignment = TextAlignmentOptions.Center;
                EnsureBackplate(text, new Vector2(44f, 18f), BackplateColor);
            }
            else
            {
                rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 620f), Mathf.Max(rect.sizeDelta.y, 58f));
                text.alignment = TextAlignmentOptions.Center;
                EnsureBackplate(text, new Vector2(44f, 18f), BackplateColor);
            }
        }
    }

    private static void StyleReadableOverlayText(TMP_Text text, float minSize, float maxSize, TextAlignmentOptions alignment)
    {
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
    }

    private static void EnsureBackplate(TMP_Text text, Vector2 padding, Color color)
    {
        RectTransform textRect = text.rectTransform;
        Transform parent = text.transform.parent;

        if (parent == null)
            return;

        string plateName = text.gameObject.name + " Backplate";
        Transform existing = parent.Find(plateName);
        RectTransform plateRect;
        Image image;

        if (existing == null)
        {
            GameObject plate = new GameObject(plateName);
            plate.transform.SetParent(parent, false);
            plate.transform.SetSiblingIndex(text.transform.GetSiblingIndex());
            plateRect = plate.AddComponent<RectTransform>();
            image = plate.AddComponent<Image>();
        }
        else
        {
            plateRect = existing as RectTransform;
            image = existing.GetComponent<Image>();
        }

        if (plateRect == null || image == null)
            return;

        plateRect.transform.SetSiblingIndex(Mathf.Max(0, text.transform.GetSiblingIndex() - 1));
        plateRect.anchorMin = textRect.anchorMin;
        plateRect.anchorMax = textRect.anchorMax;
        plateRect.pivot = textRect.pivot;
        plateRect.anchoredPosition = textRect.anchoredPosition;
        plateRect.sizeDelta = textRect.sizeDelta + padding;
        plateRect.localScale = Vector3.one;
        image.color = color;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static TMP_Text FindTextByName(string name)
    {
        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (IsRuntimeSceneObject(text.gameObject) && text.gameObject.name == name)
                return text;
        }

        return null;
    }

    private static Transform FindSceneTransform(string name)
    {
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (IsRuntimeSceneObject(transform.gameObject) && transform.gameObject.name == name)
                return transform;
        }

        return null;
    }

    private static bool IsRuntimeSceneObject(GameObject go)
    {
        return go != null && go.scene.IsValid() && go.scene.isLoaded;
    }

    private static bool IsGenericPanelName(string name)
    {
        return name.Contains("Panel") ||
               name.Contains("Banner") ||
               name.Contains("CPRPanel") ||
               name.Contains("Answer");
    }

    private static bool IsResultPanelName(string name)
    {
        return name.Contains("Result") ||
               name.Contains("Win") ||
               name.Contains("Fail");
    }

    private static bool IsQuestionOrInspectionText(TMP_Text text)
    {
        Transform questionPanel = FindSceneTransform("QuestionPanel");

        if (questionPanel != null && text.transform.IsChildOf(questionPanel))
            return true;

        string name = text.gameObject.name;
        return name == "InspectionTitleText" ||
               name == "InspectionBodyText" ||
               name == "InspectionExitHintText" ||
               name == "ResultBodyText";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (string needle in needles)
        {
            if (value.Contains(needle))
                return true;
        }

        return false;
    }
}

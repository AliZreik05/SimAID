using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class VRScenarioHUD : MonoBehaviour
{
    private const float CanvasDistance = 1.75f;
    private const float CanvasScale = 0.0014f;

    private Camera targetCamera;
    private ScenarioGameLoop gameLoop;
    private TMP_Text objectivesText;
    private TMP_Text timerText;
    private RectTransform objectivesBackplate;
    private RectTransform timerBackplate;

    public void SetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    private void Awake()
    {
        gameLoop = FindFirstObjectByType<ScenarioGameLoop>();
        BuildHud();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        if (targetCamera != null && transform.parent != targetCamera.transform)
        {
            transform.SetParent(targetCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, CanvasDistance);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * CanvasScale;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
                canvas.worldCamera = targetCamera;
        }

        if (gameLoop == null)
            gameLoop = FindFirstObjectByType<ScenarioGameLoop>();

        RefreshObjectives();
        MirrorTimerText();
        ApplyHudLayout();
        HideDuplicateSceneHudText();
    }

    private void BuildHud()
    {
        RectTransform canvasRect = gameObject.GetComponent<RectTransform>();
        if (canvasRect == null)
            canvasRect = gameObject.AddComponent<RectTransform>();

        canvasRect.sizeDelta = new Vector2(1920f, 1080f);

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;
        canvas.worldCamera = targetCamera;

        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        objectivesText = CreateHudText("VR Objectives Text", new Vector2(0.5f, 1f), new Vector2(-360f, -120f), new Vector2(430f, 140f), TextAlignmentOptions.TopLeft, out objectivesBackplate);
        timerText = CreateHudText("VR Timer Text", new Vector2(0.5f, 1f), new Vector2(360f, -120f), new Vector2(520f, 150f), TextAlignmentOptions.TopRight, out timerBackplate);
    }

    private TMP_Text CreateHudText(string objectName, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions alignment, out RectTransform plateRect)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(transform, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = 34f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 38f;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        GameObject plateObject = new GameObject(objectName + " Backplate");
        plateObject.transform.SetParent(transform, false);
        plateObject.transform.SetSiblingIndex(textObject.transform.GetSiblingIndex());

        plateRect = plateObject.AddComponent<RectTransform>();
        plateRect.anchorMin = rect.anchorMin;
        plateRect.anchorMax = rect.anchorMax;
        plateRect.pivot = rect.pivot;
        plateRect.anchoredPosition = rect.anchoredPosition;
        plateRect.sizeDelta = rect.sizeDelta + new Vector2(34f, 22f);

        Image plate = plateObject.AddComponent<Image>();
        plate.color = new Color(0f, 0f, 0f, 0.38f);

        textObject.transform.SetAsLastSibling();
        return text;
    }

    private void ApplyHudLayout()
    {
        ApplyTextLayout(
            objectivesText,
            objectivesBackplate,
            new Vector2(0.5f, 1f),
            new Vector2(-360f, -120f),
            new Vector2(430f, 140f),
            TextAlignmentOptions.TopLeft);

        ApplyTextLayout(
            timerText,
            timerBackplate,
            new Vector2(0.5f, 1f),
            new Vector2(360f, -120f),
            new Vector2(520f, 150f),
            TextAlignmentOptions.TopRight);
    }

    private static void ApplyTextLayout(TMP_Text text, RectTransform backplate, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        text.alignment = alignment;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = 34f;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 38f;

        if (backplate == null)
            return;

        backplate.anchorMin = anchor;
        backplate.anchorMax = anchor;
        backplate.pivot = anchor;
        backplate.anchoredPosition = position;
        backplate.sizeDelta = size + new Vector2(34f, 22f);
    }

    private void RefreshObjectives()
    {
        if (gameLoop == null || objectivesText == null)
            return;

        objectivesText.text =
            $"Cones: {gameLoop.ConesPlaced}/{gameLoop.ConesRequired}\n" +
            $"Bandages: {gameLoop.BandagesApplied}/{gameLoop.BandagesRequired}\n" +
            $"CPR: {(gameLoop.CprDone ? "Done" : "Not done")}";
    }

    private void MirrorTimerText()
    {
        if (timerText == null)
            return;

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null || text == timerText || text == objectivesText)
                continue;

            if (!string.IsNullOrWhiteSpace(text.text) && text.text.Contains("Bleed timer"))
            {
                timerText.text = text.text;
                return;
            }
        }
    }

    private void HideDuplicateSceneHudText()
    {
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null || text == timerText || text == objectivesText || text.transform.IsChildOf(transform))
                continue;

            string value = text.text;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            bool isScenarioTimer = value.Contains("Bleed timer");
            bool isScenarioObjectives =
                value.Contains("Cones:") &&
                value.Contains("Bandages:") &&
                value.Contains("CPR:");

            if (!isScenarioTimer && !isScenarioObjectives)
                continue;

            text.gameObject.SetActive(false);

            Transform parent = text.transform.parent;
            if (parent == null)
                continue;

            Transform backplate = parent.Find(text.gameObject.name + " Backplate");
            if (backplate != null)
                backplate.gameObject.SetActive(false);
        }
    }
}

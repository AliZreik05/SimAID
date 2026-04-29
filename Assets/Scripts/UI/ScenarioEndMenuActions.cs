using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ScenarioEndMenuActions
{
    private const string ButtonObjectName = "Return To Main Menu Button";
    private const float FooterSpace = 105f;

    public static void AddReturnToMainMenuButton(GameObject parentPanel, string mainMenuSceneName = "Lobby Scene")
    {
        if (parentPanel == null)
            return;

        if (parentPanel.transform.Find(ButtonObjectName) != null)
            return;

        ReserveFooterSpace(parentPanel.transform);
        EnsureEventSystem();

        GameObject buttonObject = new GameObject(ButtonObjectName);
        buttonObject.transform.SetParent(parentPanel.transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 26f);
        rect.sizeDelta = new Vector2(360f, 56f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.16f, 0.18f, 0.96f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => ReturnToMainMenu(mainMenuSceneName));

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.14f, 0.28f, 0.32f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.04f, 0.1f, 0.12f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Return to Main Menu";
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
    }

    private static void ReserveFooterSpace(Transform parentPanel)
    {
        TMP_Text[] texts = parentPanel.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null || text.transform.parent == null)
                continue;

            if (text.transform.parent.name == ButtonObjectName)
                continue;

            RectTransform rect = text.GetComponent<RectTransform>();

            if (rect == null)
                continue;

            bool namedBodyText = text.name.ToLowerInvariant().Contains("body");
            bool stretchBodyText =
                rect.anchorMin.y <= 0.05f &&
                rect.anchorMax.y >= 0.85f &&
                rect.offsetMin.y < FooterSpace;
            bool centeredLargeBodyText =
                Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x) &&
                Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y) &&
                rect.sizeDelta.y > 220f;

            if (!namedBodyText && !stretchBodyText && !centeredLargeBodyText)
                continue;

            if (stretchBodyText)
            {
                rect.offsetMin = new Vector2(rect.offsetMin.x, FooterSpace);
            }
            else
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(260f, rect.sizeDelta.y - FooterSpace));
                rect.anchoredPosition += new Vector2(0f, FooterSpace * 0.5f);
            }
        }
    }

    private static void ReturnToMainMenu(string mainMenuSceneName)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }
}

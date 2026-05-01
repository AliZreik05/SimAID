using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class VRControllerUIPointer : MonoBehaviour
{
    [SerializeField] private Camera uiCamera;
    [SerializeField] private float navigationRepeatDelay = 0.28f;

    private readonly List<Button> activeButtons = new List<Button>();
    private bool wasTriggerPressed;
    private float lastNavigateTime;
    private int selectedIndex;
    private string lastSceneName;

    public void SetCamera(Camera camera)
    {
        uiCamera = camera;
    }

    private void LateUpdate()
    {
        if (uiCamera == null)
            uiCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        HidePointerLines();

        if (EventSystem.current == null)
        {
            wasTriggerPressed = IsTriggerPressed();
            return;
        }

        ResetSelectionWhenSceneChanges();
        RefreshActiveButtons();
        EnsureSelection();
        HandleRightStickNavigation();
        HandleTriggerSubmit();
    }

    private void ResetSelectionWhenSceneChanges()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == lastSceneName)
            return;

        lastSceneName = sceneName;
        selectedIndex = 0;
    }

    private void EnsureSelection()
    {
        Button selectedButton = GetIndexedButton();
        if (selectedButton != null && EventSystem.current.currentSelectedGameObject != selectedButton.gameObject)
            EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
    }

    private void HandleRightStickNavigation()
    {
        if (Time.unscaledTime - lastNavigateTime < navigationRepeatDelay)
            return;

        float y = GetRightStickY();
        if (Mathf.Abs(y) < 0.55f)
            return;

        if (activeButtons.Count == 0)
            return;

        if (y < 0f)
            selectedIndex = Mathf.Min(selectedIndex + 1, activeButtons.Count - 1);
        else
            selectedIndex = Mathf.Max(selectedIndex - 1, 0);

        EventSystem.current.SetSelectedGameObject(activeButtons[selectedIndex].gameObject);
        lastNavigateTime = Time.unscaledTime;
    }

    private void HandleTriggerSubmit()
    {
        bool triggerPressed = IsTriggerPressed();
        if (triggerPressed && !wasTriggerPressed)
        {
            Button selectedButton = GetIndexedButton();
            if (selectedButton != null)
                SubmitButton(selectedButton);
        }

        wasTriggerPressed = triggerPressed;
    }

    private Button GetIndexedButton()
    {
        if (activeButtons.Count == 0)
            return null;

        selectedIndex = Mathf.Clamp(selectedIndex, 0, activeButtons.Count - 1);
        return activeButtons[selectedIndex];
    }

    private void RefreshActiveButtons()
    {
        activeButtons.Clear();

        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (IsUsableButton(button))
                activeButtons.Add(button);
        }

        activeButtons.Sort(CompareButtonsForMenuNavigation);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, activeButtons.Count - 1));
    }

    private static int CompareButtonsForMenuNavigation(Button a, Button b)
    {
        if (SceneManager.GetActiveScene().name == "Lobby Scene")
        {
            int labelCompare = GetLobbyButtonOrder(a).CompareTo(GetLobbyButtonOrder(b));
            if (labelCompare != 0)
                return labelCompare;
        }

        int canvasCompare = GetCanvasPriority(a).CompareTo(GetCanvasPriority(b));
        if (canvasCompare != 0)
            return canvasCompare;

        int verticalCompare = GetVerticalRank(b).CompareTo(GetVerticalRank(a));
        if (verticalCompare != 0)
            return verticalCompare;

        return GetHierarchyIndex(a.transform).CompareTo(GetHierarchyIndex(b.transform));
    }

    private static bool IsUsableButton(Button button)
    {
        if (button == null || !button.isActiveAndEnabled || !button.IsInteractable())
            return false;

        Canvas canvas = button.GetComponentInParent<Canvas>();
        return canvas != null && canvas.isActiveAndEnabled;
    }

    private static int GetCanvasPriority(Button button)
    {
        Canvas canvas = button.GetComponentInParent<Canvas>();
        if (canvas == null)
            return 1000;

        string name = canvas.name.ToLowerInvariant();
        if (name.Contains("level select"))
            return 0;
        if (name.Contains("lose") || name.Contains("fail") || name.Contains("win"))
            return 1;

        return 10;
    }

    private static int GetLobbyButtonOrder(Button button)
    {
        string label = GetButtonLabel(button);
        if (label.Contains("City"))
            return 0;
        if (label.Contains("Restaurant"))
            return 1;
        if (label.Contains("Quit"))
            return 2;

        return 50;
    }

    private static float GetVerticalRank(Button button)
    {
        RectTransform rect = button.transform as RectTransform;
        return rect == null ? button.transform.position.y : rect.position.y;
    }

    private static int GetHierarchyIndex(Transform transform)
    {
        int index = transform.GetSiblingIndex();
        Transform parent = transform.parent;

        while (parent != null)
        {
            index += parent.GetSiblingIndex() * 100;
            parent = parent.parent;
        }

        return index;
    }

    private static void SubmitButton(Button button)
    {
        string label = GetButtonLabel(button);

        if (SceneManager.GetActiveScene().name == "Lobby Scene")
        {
            if (label.Contains("Restaurant"))
            {
                SceneManager.LoadScene("Restaurant Scene");
                return;
            }

            if (label.Contains("City"))
            {
                SceneManager.LoadScene("City Scene");
                return;
            }
        }

        ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }

    private static string GetButtonLabel(Button button)
    {
        if (button == null)
            return string.Empty;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null && !string.IsNullOrWhiteSpace(text.text))
            return text.text;

        return button.gameObject.name;
    }

    private static bool IsTriggerPressed()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        return device.isValid &&
               device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) &&
               pressed;
    }

    private static float GetRightStickY()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!device.isValid)
            return 0f;

        if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 primaryAxis) && Mathf.Abs(primaryAxis.y) > 0.2f)
            return primaryAxis.y;

        if (device.TryGetFeatureValue(CommonUsages.secondary2DAxis, out Vector2 secondaryAxis) && Mathf.Abs(secondaryAxis.y) > 0.2f)
            return secondaryAxis.y;

        return 0f;
    }

    private static void HidePointerLines()
    {
        foreach (LineRenderer line in FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (line != null && line.gameObject.name.Contains("VR Controller UI Pointer"))
                line.enabled = false;
        }
    }
}

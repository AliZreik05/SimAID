using TMPro;
using UnityEngine;

public class ObjectiveUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text objectivesText;

    [Header("References")]
    [SerializeField] private ScenarioGameLoop gameLoop;

    private void Awake()
    {
        if (!gameLoop) gameLoop = FindFirstObjectByType<ScenarioGameLoop>();
    }

    private void OnEnable()
    {
        ApplyObjectiveTextStyle();
        Refresh();
        // If you already expose events from ScenarioGameLoop, subscribe here.
        // Otherwise we can refresh on a timer or in Update (not ideal).
    }

    public void Refresh()
    {
        if (!gameLoop || !objectivesText) return;

        ApplyObjectiveTextStyle();
        objectivesText.text =
            $"Cones: {gameLoop.ConesPlaced}/{gameLoop.ConesRequired}\n" +
            $"Bandages: {gameLoop.BandagesApplied}/{gameLoop.BandagesRequired}\n" +
            $"CPR: {(gameLoop.CprDone ? "Done" : "Not done")}";
    }

    private void ApplyObjectiveTextStyle()
    {
        if (objectivesText == null)
            return;

        objectivesText.enableWordWrapping = true;
        objectivesText.overflowMode = TextOverflowModes.Overflow;
        objectivesText.enableAutoSizing = true;
        objectivesText.fontSizeMin = 14f;
        objectivesText.fontSizeMax = Mathf.Max(objectivesText.fontSize, 24f);
    }
}

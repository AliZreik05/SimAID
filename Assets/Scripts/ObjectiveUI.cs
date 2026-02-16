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
        Refresh();
        // If you already expose events from ScenarioGameLoop, subscribe here.
        // Otherwise we can refresh on a timer or in Update (not ideal).
    }

    public void Refresh()
    {
        if (!gameLoop || !objectivesText) return;

        objectivesText.text =
            $"Cones: {gameLoop.ConesPlaced}/{gameLoop.ConesRequired}\n" +
            $"Bandages: {gameLoop.BandagesApplied}/{gameLoop.BandagesRequired}\n" +
            $"CPR: {(gameLoop.CprDone ? "Done" : "Not done")}";
    }
}

using UnityEngine;

public class InspectionOpener : Interactable
{
    [SerializeField] private InspectionManager inspectionManager;
    [SerializeField] private MedicalScenarioManager scenarioManager;
    [SerializeField] private InspectionPartType inspectionPart;

    private string enabledPrompt;

    private void Awake()
    {
        enabledPrompt = inspectionPart == InspectionPartType.Chest
            ? "Inspect chest (E)"
            : "Inspect hand (E)";

        promptMessage = enabledPrompt;
    }

    private void Update()
    {
        bool canShowPrompt =
            scenarioManager != null &&
            scenarioManager.CanInspect;

        promptMessage = canShowPrompt ? enabledPrompt : "";
    }

    protected override void Interact()
    {
        if (inspectionManager == null)
        {
            Debug.LogWarning("InspectionOpener: No InspectionManager assigned.");
            return;
        }

        if (scenarioManager == null || !scenarioManager.CanInspect)
            return;

        inspectionManager.OpenInspection(inspectionPart);
    }
}
using UnityEngine;

public class MedkitOpener : Interactable
{
    [SerializeField] private MedicalScenarioManager scenarioManager;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera medkitCamera;

    private void Awake()
    {
        promptMessage = "Open medkit (E)";
    }

    protected override void Interact()
    {
        if (!scenarioManager)
        {
            Debug.LogWarning("MedkitOpener: No MedicalScenarioManager assigned.");
            return;
        }

        if (!scenarioManager.CanOpenMedkit)
        {
            Debug.Log("Assessment not finished yet.");
            return;
        }

        if (!mainCamera || !medkitCamera)
        {
            Debug.LogWarning("MedkitOpener: Main camera or medkit camera is missing.");
            return;
        }

        scenarioManager.EnterMedkitView();

        mainCamera.enabled = false;
        medkitCamera.enabled = true;

        Debug.Log("Medkit opened.");
    }
}
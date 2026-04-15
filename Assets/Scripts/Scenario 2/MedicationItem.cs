using UnityEngine;

public class MedicationItem : Interactable
{
    [SerializeField] private string medicationName;
    [SerializeField] private MedicalScenarioManager scenarioManager;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera medkitCamera;

    private void Awake()
    {
        promptMessage = $"Use {medicationName} (E)";
    }

    protected override void Interact()
    {
        if (!scenarioManager)
        {
            Debug.LogWarning("MedicationItem: No MedicalScenarioManager assigned.");
            return;
        }

        if (medkitCamera != null)
            medkitCamera.enabled = false;

        if (mainCamera != null)
            mainCamera.enabled = true;



        scenarioManager.ExitMedkitView();
        scenarioManager.GiveMedication(medicationName);
    }
}
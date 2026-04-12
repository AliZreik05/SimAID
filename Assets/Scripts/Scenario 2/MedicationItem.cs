using UnityEngine;

public class MedicationItem : Interactable
{
    [SerializeField] private string medicationName = "Epinephrine";
    [SerializeField] private MedicalScenarioManager scenarioManager;

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

        Debug.Log($"Medication used: {medicationName}");
        scenarioManager.GiveMedication(medicationName);

        gameObject.SetActive(false);
    }
}
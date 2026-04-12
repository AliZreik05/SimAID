using UnityEngine;

public class PatientAssessmentStarter : Interactable
{
    [SerializeField] private MedicalScenarioManager scenarioManager;

    private void Awake()
    {
        promptMessage = "Assess patient (E)";
    }

    protected override void Interact()
    {
        if (!scenarioManager)
        {
            Debug.LogWarning("PatientAssessmentStarter: No MedicalScenarioManager assigned.");
            return;
        }

        scenarioManager.BeginPatientAssessment();
    }
}
using UnityEngine;

public class PatientAssessmentStarter : Interactable
{
    [SerializeField] private MedicalScenarioManager scenarioManager;
    [SerializeField] private PatientConversationController conversationController;

    public bool CanBeInteractedWith =>
        scenarioManager != null &&
        scenarioManager.CurrentState == MedicalScenarioManager.ScenarioState.WaitingForPatientInteraction;

    private void Awake()
    {
        promptMessage = "Assess patient (E)";
    }

    protected override void Interact()
    {
        if (!CanBeInteractedWith)
            return;

        if (conversationController != null)
            conversationController.StartConversation();

        scenarioManager.BeginPatientAssessment();
    }
}
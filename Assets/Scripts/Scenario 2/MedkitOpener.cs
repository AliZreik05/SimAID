using UnityEngine;

public class MedkitOpener : Interactable
{
    [SerializeField] private MedicalScenarioManager scenarioManager;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera medkitCamera;
    [SerializeField] private GameObject interactionPromptUI;

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
            Debug.LogWarning("MedkitOpener: Missing camera reference.");
            return;
        }

        scenarioManager.EnterMedkitView();

        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);

        medkitCamera.gameObject.SetActive(true);
        medkitCamera.enabled = true;
        mainCamera.enabled = false;
    }
}
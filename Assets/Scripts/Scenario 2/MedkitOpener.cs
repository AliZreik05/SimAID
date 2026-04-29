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

    private void Update()
    {
        if (!scenarioManager ||
            scenarioManager.CurrentState != MedicalScenarioManager.ScenarioState.InMedkitView ||
            !Input.GetKeyDown(KeyCode.Escape))
            return;

        EscapeInputGuard.MarkHandled();
        CloseMedkitView();
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

    private void CloseMedkitView()
    {
        if (medkitCamera != null)
        {
            medkitCamera.enabled = false;
            medkitCamera.gameObject.SetActive(false);
        }

        if (mainCamera != null)
            mainCamera.enabled = true;

        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(true);

        scenarioManager.ExitMedkitView();
    }
}

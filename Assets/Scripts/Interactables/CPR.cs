using UnityEngine;

public class CPR : Interactable
{
    [SerializeField] private CPRMinigame cprUI;   // drag your CPR UI object here

    private bool completed;

    private void Awake()
    {
        promptMessage = "Start CPR (E)";
    }

    private void Update()
    {
        // Optional: update prompt after completion
        if (completed) promptMessage = "CPR completed";
    }

    protected override void Interact()
{
    if (completed) return;

    if (!cprUI)
    {
        Debug.LogWarning("CPRInteractable: CPR UI not assigned.");
        return;
    }

    var loop = FindFirstObjectByType<ScenarioGameLoop>();
    if (loop) loop.PauseForCPR(true);  

    cprUI.StartCPR(OnCPRCompleted);
}


    private void OnCPRCompleted()
    {
        completed = true;
        var loop = FindFirstObjectByType<ScenarioGameLoop>();
if (loop == null)
{
    Debug.LogWarning("No ScenarioGameLoop found!");
}
else
{
    loop.NotifyCPRDone();
}
Debug.Log("CPR objective complete!");
        gameObject.SetActive(false);
    }
}

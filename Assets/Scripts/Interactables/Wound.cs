using UnityEngine;

public class WoundInteractable : Interactable
{
    
    [Header("Cost")]
    [SerializeField] private int bandageCost = 1;
    private ScenarioGameLoop loop;


    [Header("Feedback (optional)")]
    [SerializeField] private Renderer woundRenderer;      // assign capsule renderer, or auto
    [SerializeField] private GameObject bandageVisual;    // optional (disabled by default)
    [SerializeField] private bool disableAfterTreat = true;

    private bool treated;

    private void Awake()
    {
        loop = FindFirstObjectByType<ScenarioGameLoop>();
        if (!woundRenderer) woundRenderer = GetComponentInChildren<Renderer>();
        if (bandageVisual) bandageVisual.SetActive(false);
        UpdatePrompt();
    }
    private void LateUpdate()
        {
            if (treated) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (!player) return;

            var inv = player.GetComponent<BandageInventory>();
            if (!inv) return;

            promptMessage = inv.HasBandage ? "Apply bandage (E)" : "Need bandage";
        }
    private void UpdatePrompt()
    {
        if (treated)
        {
            promptMessage = "Wound already treated";
            return;
        }

        // We can’t know player inventory here unless we look it up.
        // Keep it simple: generic prompt.
        promptMessage = "Apply bandage (E)";
    }

    protected override void Interact()
    {
        if (treated) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
        {
            Debug.LogWarning("WoundInteractable: No Player tag found.");
            return;
        }

        var inv = player.GetComponent<BandageInventory>();
        if (!inv)
        {
            Debug.LogWarning("WoundInteractable: Player has no BandageInventory.");
            return;
        }

        if (!inv.Consume(bandageCost))
        {
            // Dynamic feedback
            promptMessage = "Need bandage first";
            Debug.Log("Need bandage first.");
            return;
        }

        treated = true;
        promptMessage = "Treated";


        if (woundRenderer) woundRenderer.enabled = false;
        if (bandageVisual) bandageVisual.SetActive(true);

         if (loop != null)
        loop.NotifyBandageApplied();

        // TODO: objective tick here if you have ObjectiveManager
        // ObjectiveManager.Instance.Complete(ObjectiveId.ControlBleeding);

        if (disableAfterTreat) gameObject.SetActive(false);
    }
}

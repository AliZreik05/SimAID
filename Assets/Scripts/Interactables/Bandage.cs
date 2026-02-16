using UnityEngine;

public class Bandage : Interactable
{
    [SerializeField] private int amount = 1;

    private void Awake()
    {
        promptMessage = "Pick up bandage (E)";
    }

    protected override void Interact()
    {
        var player = GameObject.FindGameObjectWithTag("Player"); // fast deadline mode
        if (!player)
        {
            Debug.LogWarning("BandagePickup: No Player tag found.");
            return;
        }

        var inv = player.GetComponent<BandageInventory>();
        if (!inv)
        {
            Debug.LogWarning("BandagePickup: Player has no BandageInventory.");
            return;
        }

        inv.Add(amount);
        gameObject.SetActive(false);
    }
}

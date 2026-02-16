using UnityEngine;

public class BandageInventory : MonoBehaviour
{
    [SerializeField] private int bandageCount;

    public bool HasBandage => bandageCount > 0;
    public int Count => bandageCount;

    public void Add(int amount = 1)
    {
        bandageCount += Mathf.Max(0, amount);
        Debug.Log($"Bandages: {bandageCount}");
    }

    public bool Consume(int amount = 1)
    {
        amount = Mathf.Max(1, amount);
        if (bandageCount < amount) return false;
        bandageCount -= amount;
        Debug.Log($"Bandages: {bandageCount}");
        return true;
    }
}

using UnityEngine;

public class Traffic_Cone : Interactable
{
   private ConePlacer conePlacer;
private void Start()
{
    conePlacer = FindFirstObjectByType<ConePlacer>();
}

    protected override void Interact()
    {
        if (conePlacer == null)
            conePlacer = FindFirstObjectByType<ConePlacer>();

        if (conePlacer == null) return;

        conePlacer.AddCone(1);

        // "Pick up" effect
        gameObject.SetActive(false); // or Destroy(gameObject);
    }
}

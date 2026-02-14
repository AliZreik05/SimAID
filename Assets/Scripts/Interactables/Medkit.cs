using UnityEngine;

public class Medkit : Interactable
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    // this function is where we will design our interaction using code.
    protected override void Interact()
    {
        Debug.Log("Interacted with " + gameObject.name);
    }
}

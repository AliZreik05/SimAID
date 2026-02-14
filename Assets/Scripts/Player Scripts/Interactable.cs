using System;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    public String promptMessage;
    public void baseInteract()
    {
        Interact();
    }
   protected virtual void Interact()
    {
        
    }
}

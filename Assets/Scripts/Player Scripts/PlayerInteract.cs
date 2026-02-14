using System;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Camera cam;
    [SerializeField]
    private float distance = 3f;
    [SerializeField]
    private LayerMask mask;
    private PlayerUI playerUI;
    private InputManager inputManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cam = GetComponent<PlayerLook>().cam;
        playerUI = GetComponent<PlayerUI>();
        inputManager = GetComponent<InputManager>();
    }

    // Update is called once per frame
    void Update()
    {
        playerUI.updateText(String.Empty);
        Ray ray = new Ray(cam.transform.position,cam.transform.forward);
        Debug.DrawRay(ray.origin,ray.direction*distance);
        RaycastHit hitInfo;
        if(Physics.Raycast(ray,out hitInfo,distance,mask))
        {
            if(hitInfo.collider.GetComponent<Interactable>()!=null)
            {
                Interactable interactable = hitInfo.collider.GetComponent<Interactable>();
                playerUI.updateText(interactable.promptMessage);
                if(inputManager.OnFoot.Interact.triggered)
                {
                    interactable.baseInteract();
                }
            }
        }
    }
}

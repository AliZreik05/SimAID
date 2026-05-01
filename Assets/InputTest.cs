using UnityEngine;
using UnityEngine.InputSystem;

public class InputTest : MonoBehaviour
{
    public InputActionProperty testActionValue;
    public InputActionProperty testActionButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
{
    if (testActionValue.action == null || testActionButton.action == null)
        return;

    float value = testActionValue.action.ReadValue<float>();
    bool button = testActionButton.action.IsPressed();

    Debug.Log($"VALUE: {value} | BUTTON: {button}");
}
}

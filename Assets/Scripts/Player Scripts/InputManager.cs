using UnityEngine;
using UnityEngine.InputSystem;
public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;
    private PlayerMotor motor;
    private PlayerLook look;
    public PlayerInput.OnFootActions OnFoot => onFoot;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
{
    playerInput = new PlayerInput();
    onFoot = playerInput.OnFoot;

    motor = GetComponent<PlayerMotor>();
    if (motor == null) motor = GetComponentInParent<PlayerMotor>();

    look = GetComponent<PlayerLook>();
    if (look == null) look = GetComponentInParent<PlayerLook>();

    onFoot.Jump.performed   += _ => motor.Jump();
    onFoot.Crouch.performed += _ => motor.Crouch();
    onFoot.Sprint.performed += _ => motor.Sprint();
}

    // Update is called once per frame
    void FixedUpdate()
    {
        motor.ProcessMove(onFoot.Movement.ReadValue<Vector2>());
    }
    void LateUpdate()
    {
        look.ProcessLook(onFoot.Look.ReadValue<Vector2>());
    }

    private void OnEnable()
    {
        onFoot.Enable();
    }
    private void OnDisable()
    {
        onFoot.Disable();
    }
}

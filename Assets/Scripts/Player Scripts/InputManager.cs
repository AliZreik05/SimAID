using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;
    private PlayerMotor motor;
    private PlayerLook look;

    public PlayerInput.OnFootActions OnFoot => onFoot;

    [Header("Runtime Locks")]
    [SerializeField] private bool blockMovementLook = false;
    [SerializeField] private bool allowLookOnly = false;

    private void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;

        motor = GetComponent<PlayerMotor>();
        if (motor == null) motor = GetComponentInParent<PlayerMotor>();

        look = GetComponent<PlayerLook>();
        if (look == null) look = GetComponentInParent<PlayerLook>();

        if (motor != null)
        {
            onFoot.Jump.performed += _ =>
            {
                if (!blockMovementLook) motor.Jump();
            };

            onFoot.Crouch.performed += _ =>
            {
                if (!blockMovementLook) motor.Crouch();
            };

            onFoot.Sprint.performed += _ =>
            {
                if (!blockMovementLook) motor.Sprint();
            };
        }
    }

    private void Update()
    {
        if (motor != null)
        {
            if (blockMovementLook)
                motor.ProcessMove(Vector2.zero);
            else
                motor.ProcessMove(onFoot.Movement.ReadValue<Vector2>());
        }

        if (look != null)
        {
            if (blockMovementLook && !allowLookOnly)
                look.ProcessLook(Vector2.zero);
            else
                look.ProcessLook(onFoot.Look.ReadValue<Vector2>());
        }
    }

    public void SetGameplayLocked(bool locked, bool lookOnly = false)
    {
        blockMovementLook = locked;
        allowLookOnly = lookOnly;
    }

    public float GetLookDeltaX()
    {
        return onFoot.Look.ReadValue<Vector2>().x;
    }

    public float GetLookDeltaY()
    {
        return onFoot.Look.ReadValue<Vector2>().y;
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
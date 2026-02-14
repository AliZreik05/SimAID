using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;
    public float speed = 5;
    private bool IsGrounded;
    public float gravity = -9.8f;
    public float jumpHeight = 3;
    private bool crouching = false;
    private bool lerpCrouch = false;
    private float crouchTimer = 0f;

    private bool sprinting = false;
    public float sprintSpeed = 8f;
    private float walkSpeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller = GetComponent<CharacterController>();
        walkSpeed = speed; 
    }

    // Update is called once per frame
    void Update()
    {
        IsGrounded = controller.isGrounded;
        if (lerpCrouch)
        {
            crouchTimer += Time.deltaTime;
            float p = crouchTimer / 1f;
            p *= p;

            if (crouching)
                controller.height = Mathf.Lerp(controller.height, 1f, p);
            else
                controller.height = Mathf.Lerp(controller.height, 2f, p);

            if (p > 1f)
            {
                lerpCrouch = false;
                crouchTimer = 0f;
            }
        }
    }
    public void ProcessMove(Vector2 input)
    {
        Vector3 moveDirection = Vector3.zero;
        moveDirection.x = input.x;
        moveDirection.z = input.y;
        controller.Move(transform.TransformDirection(moveDirection)*speed*Time.deltaTime);
        playerVelocity.y += gravity * Time.deltaTime;
        if(IsGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = -2f;
        }
        controller.Move(playerVelocity * Time.deltaTime);
        Debug.Log(playerVelocity.y);
    }
    public void Jump()
    {
        if(IsGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -3 * gravity);
        }
    }
    public void Crouch()
    {
        crouching = !crouching;
        crouchTimer = 0f;
        lerpCrouch = true;
    }

    public void Sprint()
    {
        sprinting = !sprinting;
        if (sprinting)
            speed = sprintSpeed;
        else
            speed = walkSpeed;
    }
}

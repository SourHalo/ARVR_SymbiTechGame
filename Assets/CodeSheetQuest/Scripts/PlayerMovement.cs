using UnityEngine;
using UnityEngine.InputSystem;

// Moves the player around.
// WASD = walk, Left Shift = sprint, Space = jump
// I used a CharacterController so the player can't walk through walls.
public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float sprintSpeed = 6f;
    public float jumpHeight = 1.35f;
    public float gravity = -20f;
    public float airControl = 2f;   // how much you can steer in the air
    public bool canMove = true;     // GameUI turns this off when a window is open

    CharacterController controller;
    Vector3 moveVelocity;           // forward / sideways speed
    float upSpeed;                  // jumping and falling speed
    float lastTimeOnGround = -10f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // read the keys
        float x = 0f;
        float z = 0f;
        if (canMove)
        {
            if (keyboard.wKey.isPressed) z += 1f;
            if (keyboard.sKey.isPressed) z -= 1f;
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.aKey.isPressed) x -= 1f;
        }

        Vector3 move = transform.right * x + transform.forward * z;
        if (move.magnitude > 1f) move = move.normalized; // so going diagonal isn't faster

        float speed = walkSpeed;
        if (canMove && keyboard.leftShiftKey.isPressed) speed = sprintSpeed;

        if (controller.isGrounded)
        {
            lastTimeOnGround = Time.time;
            moveVelocity = move * speed;
            if (upSpeed < 0f) upSpeed = -2f; // keeps us stuck to the floor
        }
        else
        {
            // in the air you keep your speed but can only steer a little bit
            moveVelocity = Vector3.Lerp(moveVelocity, move * speed, airControl * Time.deltaTime);
        }

        // jump! you get a tiny bit of extra time after running off an edge, makes parkour less annoying
        bool closeToGround = Time.time - lastTimeOnGround < 0.12f;
        if (canMove && closeToGround && upSpeed <= 0f && keyboard.spaceKey.wasPressedThisFrame)
        {
            upSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastTimeOnGround = -10f; // no double jumps
        }

        upSpeed += gravity * Time.deltaTime;

        Vector3 velocity = moveVelocity;
        velocity.y = upSpeed;
        controller.Move(velocity * Time.deltaTime);

        // bonk your head on the ceiling
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && upSpeed > 0f)
            upSpeed = 0f;
    }

    // PlayerInteract uses this so you can't grab scripts in the middle of a jump
    public bool IsOnGround()
    {
        return controller.isGrounded;
    }
}

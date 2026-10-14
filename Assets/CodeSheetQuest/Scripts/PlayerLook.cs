using UnityEngine;
using UnityEngine.InputSystem;

// Mouse look. This goes on the camera (the camera is a child of the player).
// Mouse left/right turns the whole player, mouse up/down only tilts the camera.
public class PlayerLook : MonoBehaviour
{
    public Transform playerBody;
    public float mouseSensitivity = 0.1f;
    public bool canLook = true;

    float upDown = 0f;
    int skipFrames = 0;

    void Update()
    {
        if (!canLook || Mouse.current == null) return;

        // the mouse can jump a bit right after it gets locked again, so skip a couple of frames
        if (skipFrames > 0)
        {
            skipFrames--;
            return;
        }

        Vector2 mouse = Mouse.current.delta.ReadValue() * mouseSensitivity;

        upDown -= mouse.y;
        upDown = Mathf.Clamp(upDown, -85f, 85f); // don't let the camera flip over
        transform.localRotation = Quaternion.Euler(upDown, 0f, 0f);

        playerBody.Rotate(Vector3.up * mouse.x);
    }

    public void SetCanLook(bool value)
    {
        if (value && !canLook) skipFrames = 2;
        canLook = value;
    }
}

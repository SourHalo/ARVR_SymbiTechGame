using UnityEngine;
using UnityEngine.InputSystem;

// Checks what is in the middle of the screen.
// E = open a script, put the held script into the robot, or press Byte's button
// G = drop the script you're holding
public class PlayerInteract : MonoBehaviour
{
    public Camera playerCamera;
    public Transform holdPoint;   // where the script goes when you hold it
    public GameUI gameUI;
    public PlayerMovement playerMovement;
    public float reach = 3f;

    [Header("Held script wobble when you walk")]
    public float bobSpeed = 9f;
    public float bobAmount = 0.022f;

    ScriptSheet heldSheet;
    CharacterController controller;
    Vector3 holdStartPosition;
    Quaternion holdStartRotation;
    float bobTimer;

    void Start()
    {
        controller = playerMovement.GetComponent<CharacterController>();
        holdStartPosition = holdPoint.localPosition;
        holdStartRotation = holdPoint.localRotation;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        BobHeldSheet();

        // while the code window is open only Esc does anything
        if (gameUI.IsEditorOpen())
        {
            if (keyboard.escapeKey.wasPressedThisFrame) gameUI.CloseEditor();
            return;
        }
        if (!gameUI.IsPlaying())
        {
            gameUI.SetAiming(false);
            return;
        }

        if (heldSheet != null && keyboard.gKey.wasPressedThisFrame)
            DropHeldSheet();

        // shoot a ray out of the middle of the screen
        ScriptSheet sheet = null;
        RobotController robot = null;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            sheet = hit.collider.GetComponentInParent<ScriptSheet>();
            robot = hit.collider.GetComponentInParent<RobotController>();
        }

        // E only works when you're standing on something (no grabbing scripts mid-jump!)
        bool pressedE = keyboard.eKey.wasPressedThisFrame && playerMovement.IsOnGround();

        if (sheet != null)
        {
            gameUI.SetAiming(true);
            if (pressedE) gameUI.OpenEditor(sheet);
        }
        else if (robot != null && robot.IsWaitingForButton())
        {
            gameUI.SetAiming(true);
            if (pressedE) robot.PressButton();
        }
        else if (robot != null && robot.CanTakeScripts() && heldSheet != null)
        {
            gameUI.SetAiming(true);
            if (pressedE) InsertHeldSheet(robot);
        }
        else
        {
            gameUI.SetAiming(false);
        }

        gameUI.UpdateHeldText(heldSheet);
    }

    // makes the held script bounce when you walk (like holding a map in Minecraft)
    void BobHeldSheet()
    {
        Vector3 velocity = controller.velocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;

        if (speed > 0.5f && controller.isGrounded)
        {
            // faster walking = faster bobbing
            bobTimer += Time.deltaTime * bobSpeed * (speed / 4f);
            float sideways = Mathf.Sin(bobTimer) * bobAmount;
            float upDown = Mathf.Abs(Mathf.Cos(bobTimer)) * bobAmount * 1.3f;
            Vector3 target = holdStartPosition + new Vector3(sideways, upDown, 0f);
            Quaternion tilt = holdStartRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(bobTimer) * 2f);
            holdPoint.localPosition = Vector3.Lerp(holdPoint.localPosition, target, 15f * Time.deltaTime);
            holdPoint.localRotation = Quaternion.Slerp(holdPoint.localRotation, tilt, 15f * Time.deltaTime);
        }
        else
        {
            // standing still (or in the air): slowly go back to normal
            holdPoint.localPosition = Vector3.Lerp(holdPoint.localPosition, holdStartPosition, 8f * Time.deltaTime);
            holdPoint.localRotation = Quaternion.Slerp(holdPoint.localRotation, holdStartRotation, 8f * Time.deltaTime);
        }
    }

    public void HoldSheet(ScriptSheet sheet)
    {
        if (heldSheet == sheet) return;
        if (heldSheet != null) DropHeldSheet(); // you can only carry one at a time
        heldSheet = sheet;
        sheet.PickUp(holdPoint);
    }

    public void InsertHeldSheet(RobotController robot)
    {
        if (heldSheet == null || !robot.CanTakeScripts()) return;
        ScriptSheet sheetToInsert = heldSheet;
        heldSheet = null; // it's the robot's now
        robot.InsertScript(sheetToInsert);
    }

    public void DropHeldSheet()
    {
        if (heldSheet == null) return;

        // drop it in front of us, but don't put it inside a wall
        Vector3 cameraPos = playerCamera.transform.position;
        Vector3 toHand = holdPoint.position - cameraPos;
        Vector3 dropPosition = holdPoint.position;
        RaycastHit hit;
        if (Physics.Raycast(cameraPos, toHand.normalized, out hit, toHand.magnitude + 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            dropPosition = cameraPos + toHand.normalized * Mathf.Max(0f, hit.distance - 0.25f);

        heldSheet.Drop(dropPosition, playerCamera.transform.forward * 1.5f);
        heldSheet = null;
        gameUI.audioManager.PlayDrop();
    }
}

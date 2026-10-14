using UnityEngine;

// A glowing page of code.
// It floats in the air until you drop it or pick it up.
// Some scripts are bad (isBadScript) and blow the robot up. They look exactly the same as
// the good ones, so you have to actually read the code to find out!
public class ScriptSheet : MonoBehaviour
{
    public string fileName = "Script.cs";
    [TextArea(5, 25)]
    public string code = "";
    public bool isBadScript = false;

    public Transform visual;      // the paper and frame (this part floats up and down)
    public Renderer glowFrame;    // the glowing border
    public Light glowLight;
    public Collider playerCollider; // so the player can walk through the paper
    public Color glowColor = new Color(0.2f, 0.9f, 1f);

    Rigidbody body;
    Collider sheetCollider;
    Material glowMaterial;
    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 visualStart;

    bool floating = true;
    bool held = false;

    void Start()
    {
        body = GetComponent<Rigidbody>();
        sheetCollider = GetComponent<Collider>();
        glowMaterial = glowFrame.material; // makes a copy so every sheet can glow on its own
        startPosition = transform.position;
        startRotation = transform.rotation;
        visualStart = visual.localPosition;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.None; // only needed once it falls (see Drop)

        // the player shouldn't bump into (or stand on) the floating paper
        Physics.IgnoreCollision(sheetCollider, playerCollider);
    }

    void Update()
    {
        // pulse goes smoothly between 0 and 1
        float pulse = (Mathf.Sin(Time.time * 2f + startPosition.x) + 1f) / 2f;

        float glow = 1f + pulse * 1.5f;
        if (held) glow = 0.9f; // softer glow when it's right in front of your face
        glowMaterial.SetColor("_BaseColor", glowColor * glow); // brighter than 1 = it glows

        glowLight.intensity = 0.4f + pulse * 0.6f;
        if (held) glowLight.intensity = glowLight.intensity * 0.3f; // don't blind the player

        if (floating)
            visual.localPosition = visualStart + Vector3.up * pulse * 0.03f;
        else
            visual.localPosition = visualStart;

        // fell out of the level somehow? put it back where it started
        if (transform.position.y < -5f)
            PutBack();
    }

    public bool IsFloating()
    {
        return floating;
    }

    // put the sheet in the player's hand
    public void PickUp(Transform hand)
    {
        held = true;
        floating = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.None; // otherwise physics keeps pulling it back to where it was
        sheetCollider.enabled = false;
        transform.SetParent(hand);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one * 0.62f;
        SetLayer(LayerMask.NameToLayer("HeldItem")); // the hand camera draws it on top, so it never goes into walls
    }

    // let gravity take over
    public void Drop(Vector3 position, Vector3 push)
    {
        held = false;
        floating = false;
        transform.SetParent(null);
        transform.position = position;
        transform.localScale = Vector3.one;
        SetLayer(0); // back to the normal layer
        sheetCollider.enabled = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate; // smooth falling
        body.linearVelocity = push;
        body.angularVelocity = Random.insideUnitSphere * 3f;
    }

    // the robot calls this when it pulls the sheet in
    public void Freeze()
    {
        held = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.None;
        sheetCollider.enabled = false;
        transform.SetParent(null);
        SetLayer(0);
    }

    // puts the sheet and all its parts on a layer
    void SetLayer(int layer)
    {
        if (layer < 0) return; // layer doesn't exist, just leave it
        foreach (Transform part in GetComponentsInChildren<Transform>(true))
            part.gameObject.layer = layer;
    }

    void PutBack()
    {
        body.isKinematic = true;
        transform.position = startPosition;
        transform.rotation = startRotation;
        floating = true;
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Byte the robot.
// You put scripts into him. Good scripts fix him, bad scripts make him explode.
// When all 3 good scripts are in, a button pops up. Press it and he does a happy dance :)
public class RobotController : MonoBehaviour
{
    public int scriptsNeeded = 3;
    public int scriptsInserted = 0;

    [Header("Robot parts")]
    public Transform scriptSlot;     // the slot on top of his body
    public Transform leftArm;
    public Transform rightArm;
    public Transform head;
    public Renderer[] glowParts;     // eyes, chest light and antenna
    public Light eyeLight;
    public TMP_Text screenText;          // the little screen next to him
    public GameObject activateButton; // pops up when all the good scripts are in

    [Header("Other stuff")]
    public GameUI gameUI;
    public AudioManager audioManager;
    public Material fireMaterial;    // for the explosion

    public Color normalColor = new Color(0.2f, 0.85f, 1f);
    public Color happyColor = new Color(0.35f, 1f, 0.55f);

    Material[] glowMaterials;
    Material buttonMaterial;
    Quaternion leftArmStart;
    Quaternion rightArmStart;
    Quaternion headStart;
    bool busy = false;
    bool waitingForButton = false;
    bool finished = false;
    bool exploded = false;

    void Start()
    {
        leftArmStart = leftArm.localRotation;
        rightArmStart = rightArm.localRotation;
        headStart = head.localRotation;

        // copy the materials so we can change the glow colour
        glowMaterials = new Material[glowParts.Length];
        for (int i = 0; i < glowParts.Length; i++)
            glowMaterials[i] = glowParts[i].material;

        buttonMaterial = activateButton.GetComponent<Renderer>().material;
        activateButton.SetActive(false); // hidden until all 3 good scripts are in

        UpdateScreen();
    }

    void Update()
    {
        if (busy || exploded) return;

        // idle: eyes slowly glow brighter and darker, head looks around a bit
        float pulse = (Mathf.Sin(Time.time * 1.6f) + 1f) / 2f;
        Color color = normalColor;
        if (waitingForButton || finished) color = happyColor;
        SetGlow(color, 1.4f + pulse * 1.2f);
        head.localRotation = headStart * Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.6f) * 10f, 0f);

        // the button blinks so you notice it
        if (waitingForButton)
        {
            float blink = (Mathf.Sin(Time.time * 6f) + 1f) / 2f;
            buttonMaterial.SetColor("_BaseColor", happyColor * (1f + blink * 3f));
        }
    }

    public bool CanTakeScripts()
    {
        return !busy && !waitingForButton && !finished && !exploded;
    }

    public bool IsWaitingForButton()
    {
        return waitingForButton && !busy;
    }

    public void InsertScript(ScriptSheet sheet)
    {
        StartCoroutine(InsertRoutine(sheet));
    }

    IEnumerator InsertRoutine(ScriptSheet sheet)
    {
        busy = true;
        sheet.Freeze();
        audioManager.PlayInsert();

        // slide the paper into the slot and shrink it
        Vector3 startPos = sheet.transform.position;
        Quaternion startRot = sheet.transform.rotation;
        Vector3 startScale = sheet.transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.6f;
            sheet.transform.position = Vector3.Lerp(startPos, scriptSlot.position, t);
            sheet.transform.rotation = Quaternion.Slerp(startRot, scriptSlot.rotation, t);
            sheet.transform.localScale = Vector3.Lerp(startScale, startScale * 0.15f, t);
            yield return null;
        }

        string fileName = sheet.fileName;
        bool bad = sheet.isBadScript;
        Destroy(sheet.gameObject);

        // Byte reads the code (blinks for a second)
        for (int i = 0; i < 8; i++)
        {
            if (i % 2 == 0) SetGlow(Color.white, 3f);
            else SetGlow(normalColor, 1f);
            yield return new WaitForSeconds(0.12f);
        }

        if (bad)
        {
            Explode(fileName);
            yield break;
        }

        scriptsInserted++;
        UpdateScreen();
        audioManager.PlayAccept();
        SetGlow(happyColor, 4f);

        if (scriptsInserted >= scriptsNeeded)
        {
            // all good scripts are in! show the button, the player has to press it
            waitingForButton = true;
            yield return StartCoroutine(PopUpButton());
        }
        else
        {
            yield return new WaitForSeconds(0.6f);
        }
        busy = false;
    }

    IEnumerator PopUpButton()
    {
        activateButton.SetActive(true);
        Vector3 fullSize = activateButton.transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.4f;
            activateButton.transform.localScale = fullSize * Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        activateButton.transform.localScale = fullSize;
    }

    public void PressButton()
    {
        if (!IsWaitingForButton()) return;
        waitingForButton = false;
        audioManager.PlayClick();

        // push the button down a little
        activateButton.transform.localPosition += Vector3.down * 0.03f;
        buttonMaterial.SetColor("_BaseColor", happyColor * 0.6f);
        StartCoroutine(Celebrate());
    }

    IEnumerator Celebrate()
    {
        gameUI.SetPlayerControl(false); // player watches the dance
        yield return new WaitForSeconds(0.4f);
        audioManager.PlayCelebration();

        Color[] partyColors = { happyColor, new Color(1f, 0.85f, 0.25f), new Color(1f, 0.45f, 0.85f), normalColor };
        float time = 0f;
        while (time < 4.5f)
        {
            time += Time.deltaTime;
            // arms go up in the first 0.6 seconds and then wave
            float raise = Mathf.SmoothStep(0f, 1f, time / 0.6f);
            float wave = Mathf.Sin(time * 10f) * 20f * raise;
            leftArm.localRotation = leftArmStart * Quaternion.Euler(0f, 0f, -(140f * raise + wave));
            rightArm.localRotation = rightArmStart * Quaternion.Euler(0f, 0f, 140f * raise + wave);
            head.localRotation = headStart * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 6f) * 12f);

            Color color = partyColors[(int)(time * 6f) % partyColors.Length];
            SetGlow(color, 2f + Mathf.PingPong(time * 8f, 1f) * 4f);
            yield return null;
        }

        // put the arms back down
        Quaternion leftNow = leftArm.localRotation;
        Quaternion rightNow = rightArm.localRotation;
        Quaternion headNow = head.localRotation;
        float back = 0f;
        while (back < 1f)
        {
            back += Time.deltaTime / 0.5f;
            leftArm.localRotation = Quaternion.Slerp(leftNow, leftArmStart, back);
            rightArm.localRotation = Quaternion.Slerp(rightNow, rightArmStart, back);
            head.localRotation = Quaternion.Slerp(headNow, headStart, back);
            yield return null;
        }

        finished = true;
        gameUI.ShowFinalCode(scriptsInserted, scriptsNeeded); // the last code window, then the win screen
    }

    void Explode(string fileName)
    {
        exploded = true;
        busy = false;
        audioManager.PlayBlast();
        StartCoroutine(Fireball());
        StartCoroutine(ShakeCamera());

        // eyes go dark
        foreach (Material material in glowMaterials)
            material.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.1f));
        eyeLight.enabled = false;
        screenText.canvas.gameObject.SetActive(false);
        GetComponent<Collider>().enabled = false;

        // break Byte into pieces and throw them everywhere
        Vector3 center = transform.position + Vector3.up * 1.2f;
        Renderer[] parts = GetComponentsInChildren<Renderer>();
        foreach (Renderer part in parts)
        {
            if (part.name == "Submission Pad") continue; // the pad stays on the floor
            part.transform.SetParent(null, true);
            part.gameObject.AddComponent<BoxCollider>();
            Rigidbody partBody = part.gameObject.AddComponent<Rigidbody>();
            partBody.mass = 0.5f;
            partBody.AddExplosionForce(9f, center, 6f, 1.5f, ForceMode.Impulse);
            partBody.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse); // make them spin
        }

        gameUI.ShowLose(fileName);
    }

    IEnumerator Fireball()
    {
        Vector3 center = transform.position + Vector3.up * 1.2f;

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ball.GetComponent<Collider>());
        ball.transform.position = center;
        ball.GetComponent<Renderer>().sharedMaterial = fireMaterial;

        Light flash = new GameObject("Blast Light").AddComponent<Light>();
        flash.transform.position = center;
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.55f, 0.2f);
        flash.range = 15f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.7f;
            float size = 6f * Mathf.Sqrt(t) * (1f - t * t); // grows fast then shrinks
            ball.transform.localScale = Vector3.one * size;
            flash.intensity = Mathf.Lerp(12f, 0f, t);
            yield return null;
        }
        Destroy(ball);
        Destroy(flash.gameObject);
    }

    IEnumerator ShakeCamera()
    {
        Transform cam = Camera.main.transform;
        Vector3 start = cam.localPosition;
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            cam.localPosition = start + Random.insideUnitSphere * 0.15f * (1f - t / 0.6f);
            yield return null;
        }
        cam.localPosition = start;
    }

    void UpdateScreen()
    {
        screenText.text = "SCRIPTS\n" + scriptsInserted + " / " + scriptsNeeded;
        gameUI.UpdateRobotCount(scriptsInserted, scriptsNeeded);
    }

    void SetGlow(Color color, float strength)
    {
        foreach (Material material in glowMaterials)
            material.SetColor("_BaseColor", color * strength);
        eyeLight.color = color;
        eyeLight.intensity = strength * 0.6f;
    }
}

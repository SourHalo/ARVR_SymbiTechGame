using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runs everything on the screen:
// the start window, the code editor, the HUD, the crosshair and the end screen.
// The buttons call the public functions down below (they are hooked up in the Inspector under On Click).
public class GameUI : MonoBehaviour
{
    [Header("Player")]
    public PlayerMovement playerMovement;
    public PlayerLook playerLook;
    public PlayerInteract playerInteract;
    public AudioManager audioManager;

    [Header("Start window")]
    public GameObject tutorialWindow;

    [Header("Code editor window")]
    public GameObject editorWindow;
    public TMP_Text editorTabText;
    public TMP_Text lineNumbersText;
    public TMP_Text codeText;
    public GameObject[] editorBottomStuff; // DROP/HOLD buttons etc, hidden for the final code

    [Header("Final code (shown after Byte's dance)")]
    public string finalMessage = "NIMITA DIDI KA CODE";
    public float finalCodeSeconds = 10f;

    [Header("HUD")]
    public TMP_Text robotCountText;
    public TMP_Text holdingText;

    [Header("Crosshair")]
    public GameObject crosshair;
    public RectTransform crosshairDot;
    public RectTransform crosshairDotOutline;
    public Image crosshairDotImage;
    public RectTransform crosshairRing;
    public Image crosshairRingImage;
    public Color normalColor = new Color(1f, 1f, 1f, 0.9f);
    public Color aimColor = new Color(0.3f, 0.9f, 1f, 1f);

    [Header("End screen")]
    public GameObject endScreen;
    public Image endScreenBackground;
    public TMP_Text endTitleText;
    public TMP_Text endInfoText;

    // stays true after you press R, so the start window doesn't show again
    static bool skipTutorial = false;

    ScriptSheet openSheet;      // the script that is open in the editor
    bool tutorialOpen = true;
    bool editorOpen = false;
    bool showingFinalCode = false;
    bool cutscene = false;      // robot dance, the player can't move
    bool aiming = false;        // looking at something you can use

    // pressing Play in the editor starts fresh (needed if domain reload is turned off)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetWhenGameStarts()
    {
        skipTutorial = false;
    }

    void Start()
    {
        // code is easier to read in a monospace font (if the computer has one)
        string[] fonts = Font.GetOSInstalledFontNames();
        TMP_FontAsset codeFont = null;
        if (System.Array.IndexOf(fonts, "Consolas") >= 0) codeFont = TMP_FontAsset.CreateFontAsset("Consolas", "Regular");
        else if (System.Array.IndexOf(fonts, "Courier New") >= 0) codeFont = TMP_FontAsset.CreateFontAsset("Courier New", "Regular");
        if (codeFont != null)
        {
            codeText.font = codeFont;
            lineNumbersText.font = codeFont;
        }

        tutorialOpen = !skipTutorial;
        editorWindow.SetActive(false);
        endScreen.SetActive(false);
        UpdateHeldText(null);
        RefreshControls();
    }

    void Update()
    {
        // crosshair: a small round dot, it turns into a ring when you aim at something
        float speed = 14f * Time.deltaTime;
        float dotSize = 8f;
        float ringSize = 12f;
        if (aiming)
        {
            dotSize = 6f;
            ringSize = 28f;
        }
        crosshairDot.sizeDelta = Vector2.Lerp(crosshairDot.sizeDelta, new Vector2(dotSize, dotSize), speed);
        crosshairDotOutline.sizeDelta = crosshairDot.sizeDelta + new Vector2(4f, 4f);
        crosshairRing.sizeDelta = Vector2.Lerp(crosshairRing.sizeDelta, new Vector2(ringSize, ringSize), speed);

        Color dotColor = normalColor;
        if (aiming) dotColor = aimColor;
        crosshairDotImage.color = Color.Lerp(crosshairDotImage.color, dotColor, speed);

        Color ringColor = aimColor;
        float ringAlpha = 0f;
        if (aiming) ringAlpha = 1f;
        ringColor.a = Mathf.Lerp(crosshairRingImage.color.a, ringAlpha, speed);
        crosshairRingImage.color = ringColor;

        // R restarts the level any time
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            Restart();
    }

    public void Restart()
    {
        skipTutorial = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---------------- buttons ----------------

    public void StartButton()
    {
        tutorialOpen = false;
        audioManager.PlayClick();
        RefreshControls();
    }

    public void DropButton()
    {
        // make the floating script fall down
        if (openSheet != null && openSheet.IsFloating())
        {
            Vector3 towardsPlayer = playerMovement.transform.position - openSheet.transform.position;
            towardsPlayer.y = 0f;
            openSheet.Drop(openSheet.transform.position, towardsPlayer.normalized * 0.8f);
            audioManager.PlayDrop();
        }
        CloseEditor();
    }

    public void HoldButton()
    {
        if (openSheet != null)
        {
            playerInteract.HoldSheet(openSheet);
            audioManager.PlayPickup();
        }
        CloseEditor();
    }

    // ---------------- code editor ----------------

    public void OpenEditor(ScriptSheet sheet)
    {
        if (!IsPlaying()) return;
        openSheet = sheet;
        editorTabText.text = "<color=#B180D7>C#</color>  " + sheet.fileName;
        codeText.text = ColorCode(sheet.code);

        // line numbers down the side
        int lineCount = sheet.code.Split('\n').Length;
        string numbers = "";
        for (int i = 1; i <= lineCount; i++)
        {
            numbers += i;
            if (i < lineCount) numbers += "\n";
        }
        lineNumbersText.text = numbers;

        editorOpen = true;
        audioManager.PlayClick();
        RefreshControls();
    }

    public void CloseEditor()
    {
        editorOpen = false;
        openSheet = null;
        RefreshControls();
    }

    // very simple syntax colours, like a real code editor
    string ColorCode(string code)
    {
        string[] lines = code.Split('\n');
        string colored = "";
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string comment = "";

            // cut the comment off so it doesn't get coloured twice
            int commentStart = line.IndexOf("//");
            if (commentStart >= 0)
            {
                comment = "<color=#6A9955>" + line.Substring(commentStart) + "</color>";
                line = line.Substring(0, commentStart);
            }

            line = Regex.Replace(line, "\"[^\"]*\"", "<color=#CE9178>$0</color>");
            line = Regex.Replace(line, @"\b(using|public|private|class|void|float|int|bool|string|if|else|return|new|true|false|for|while)\b", "<color=#569CD6>$1</color>");
            line = Regex.Replace(line, @"\b(MonoBehaviour|Transform|Light|Vector3|Quaternion|Mathf|Time|Debug|GameObject|Color|UnityEngine)\b", "<color=#4EC9B0>$1</color>");

            colored += line + comment;
            if (i < lines.Length - 1) colored += "\n";
        }
        return colored;
    }

    // ---------------- the final code window ----------------

    // after Byte's dance: a code window types out the final message, stays for 10 seconds,
    // then the win screen shows up
    public void ShowFinalCode(int inserted, int needed)
    {
        StartCoroutine(FinalCodeRoutine(inserted, needed));
    }

    IEnumerator FinalCodeRoutine(int inserted, int needed)
    {
        showingFinalCode = true;
        foreach (GameObject thing in editorBottomStuff)
            thing.SetActive(false); // no DROP / HOLD buttons here

        editorTabText.text = "<color=#B180D7>C#</color>  FinalCode.cs";
        lineNumbersText.fontSize = 46;
        codeText.fontSize = 46;
        lineNumbersText.text = "1";
        codeText.text = "";
        RefreshControls();

        // type it out one letter at a time
        float timeUsed = 0f;
        for (int i = 1; i <= finalMessage.Length; i++)
        {
            codeText.text = "<color=#4EC9B0>" + finalMessage.Substring(0, i) + "</color>_";
            yield return new WaitForSeconds(0.08f);
            timeUsed += 0.08f;
        }
        codeText.text = "<color=#4EC9B0>" + finalMessage + "</color>";

        yield return new WaitForSeconds(Mathf.Max(0f, finalCodeSeconds - timeUsed));

        showingFinalCode = false;
        cutscene = false;
        RefreshControls();
        ShowWin(inserted, needed);
    }

    // ---------------- HUD ----------------

    // true when the crosshair is on something you can use (a script, or Byte)
    public void SetAiming(bool canUse)
    {
        aiming = canUse;
    }

    public void UpdateHeldText(ScriptSheet sheet)
    {
        string text = "Holding: nothing";
        if (sheet != null) text = "Holding: " + sheet.fileName;
        if (holdingText.text != text) holdingText.text = text;
    }

    public void UpdateRobotCount(int inserted, int needed)
    {
        robotCountText.text = "Byte: " + inserted + "/" + needed + " working scripts";
    }

    // ---------------- end of the game ----------------

    public void ShowWin(int inserted, int needed)
    {
        endScreen.SetActive(true);
        endScreenBackground.color = new Color(0.03f, 0.18f, 0.1f, 0.92f);
        endTitleText.color = new Color(1f, 0.88f, 0.35f);
        endTitleText.text = "MISSION COMPLETE!";
        endInfoText.text = inserted + "/" + needed + " working scripts installed. Byte is fixed!\nPress R to play again.";
    }

    public void ShowLose(string badFileName)
    {
        endScreen.SetActive(true);
        endScreenBackground.color = new Color(0.25f, 0.04f, 0.03f, 0.92f);
        endTitleText.color = new Color(1f, 0.45f, 0.2f);
        endTitleText.text = "BOOM! BYTE EXPLODED";
        endInfoText.text = badFileName + " had a BlastRobot() call in it. Always read the code first!\nPress R to try again.";
    }

    // ---------------- player controls + mouse ----------------

    // the robot turns this off while it dances
    public void SetPlayerControl(bool on)
    {
        cutscene = !on;
        RefreshControls();
    }

    public bool IsEditorOpen()
    {
        return editorOpen;
    }

    public bool IsPlaying()
    {
        return !tutorialOpen && !editorOpen && !cutscene && !showingFinalCode;
    }

    void RefreshControls()
    {
        tutorialWindow.SetActive(tutorialOpen);
        editorWindow.SetActive(editorOpen || showingFinalCode);

        bool playing = IsPlaying();
        playerMovement.canMove = playing;
        playerLook.SetCanLook(playing);
        crosshair.SetActive(playing);
        if (!playing) aiming = false;

        // windows you click on need the mouse, walking around needs it locked and hidden
        bool windowOpen = tutorialOpen || editorOpen;
        if (windowOpen) Cursor.lockState = CursorLockMode.None;
        else Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = windowOpen;

        // forget the last clicked button so Space doesn't click it again
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
}

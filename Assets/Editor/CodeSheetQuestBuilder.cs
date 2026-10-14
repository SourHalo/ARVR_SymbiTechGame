using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor tool that builds the whole Code Sheet Quest level out of cubes.
// Menu: Tools > Code Sheet Quest > Build Prototype Scene
// It always starts a fresh scene and saves over Assets/Scenes/CodeSheetQuest.unity,
// so running it again never makes duplicates.
public static class CodeSheetQuestBuilder
{
    const string ScenePath = "Assets/Scenes/CodeSheetQuest.unity";
    const string MaterialFolder = "Assets/CodeSheetQuest/Materials";
    const string SettingsFolder = "Assets/CodeSheetQuest/Settings";
    const string UIFolder = "Assets/CodeSheetQuest/UI";

    const float WallHeight = 7f;
    const float WallThickness = 0.2f;
    const float DoorWidth = 2.4f;
    const float DoorHeight = 3f;

    static readonly Vector3 PlayerStart = new Vector3(0f, 0.05f, -4.5f);
    static readonly Vector3 RobotPosition = new Vector3(0f, 0f, 3.5f);

    // UI colours
    static readonly Color PanelDark = new Color(0.06f, 0.09f, 0.14f, 1f);
    static readonly Color Cyan = new Color(0.3f, 0.88f, 1f);
    static readonly Color TextLight = new Color(0.92f, 0.95f, 0.98f);
    static readonly Color TextMuted = new Color(0.62f, 0.68f, 0.76f);
    static readonly Color Ink = new Color(0.1f, 0.15f, 0.25f);
    static readonly Color InkAccent = new Color(0.12f, 0.48f, 0.62f);
    static readonly Color ButtonCyan = new Color(0.25f, 0.8f, 0.95f);
    static readonly Color ButtonOrange = new Color(0.95f, 0.55f, 0.3f);
    static readonly Color EditorBg = new Color(0.118f, 0.118f, 0.125f, 1f);
    static readonly Color EditorTitleBar = new Color(0.2f, 0.2f, 0.21f, 1f);
    static readonly Color EditorTabStrip = new Color(0.15f, 0.15f, 0.16f, 1f);
    static readonly Color EditorGutter = new Color(0.105f, 0.105f, 0.11f, 1f);

    static TMP_FontAsset uiFont;
    static Sprite roundedSprite;
    static Sprite circleSprite;
    static Sprite ringSprite;
    static Material floorMat, sideFloorMat, wallMat, ceilingMat, lampMat, woodMat, metalMat, accentMat,
        paperMat, holoMat, robotBodyMat, robotDarkMat, robotGlowMat, platformMat, fireMat, buttonMat;

    class PlayerRig
    {
        public GameObject root;
        public Camera camera;
        public PlayerMovement movement;
        public PlayerLook look;
        public PlayerInteract interact;
        public Transform holdPoint;
    }

    // one parkour block: where it is, how big, and how high its top is
    struct Block
    {
        public float x, z, width, depth, top;
        public Block(float x, float z, float width, float depth, float top)
        {
            this.x = x; this.z = z; this.width = width; this.depth = depth; this.top = top;
        }
    }

    // ---------------- the 5 scripts (3 good, 2 bad) ----------------

    const string PowerCoreCode = @"using UnityEngine;

// PowerCore.cs
// turns Byte on when the game starts
public class PowerCore : MonoBehaviour
{
    public bool isOn = false;
    public float power = 0f;
    public float maxPower = 100f;

    void Start()
    {
        // charge up to full and switch on
        power = maxPower;
        isOn = true;
        Debug.Log(""power on! "" + power + ""%"");
    }
}";

    const string EyeLightsCode = @"using UnityEngine;

// EyeLights.cs
// makes the robot eyes glow (blue looks the coolest)
public class EyeLights : MonoBehaviour
{
    public Light leftEye;
    public Light rightEye;
    public float brightness = 2f;

    void Start()
    {
        leftEye.color = Color.cyan;
        rightEye.color = Color.cyan;
    }

    void Update()
    {
        // keep the eyes on
        leftEye.intensity = brightness;
        rightEye.intensity = brightness;
    }
}";

    const string ArmWaveCode = @"using UnityEngine;

// ArmWave.cs
// waves the arms so Byte looks happy :)
public class ArmWave : MonoBehaviour
{
    public Transform leftArm;
    public Transform rightArm;
    public float speed = 3f;
    public float size = 45f;

    void Update()
    {
        // sin goes up and down between -1 and 1
        float angle = Mathf.Sin(Time.time * speed) * size;
        leftArm.localRotation = Quaternion.Euler(0, 0, angle);
        rightArm.localRotation = Quaternion.Euler(0, 0, -angle);
    }
}";

    const string SpeedUpgradeCode = @"using UnityEngine;

// SpeedUpgrade.cs
// makes Byte move faster (way faster)
public class SpeedUpgrade : MonoBehaviour
{
    public float speed = 5f;
    public float maxSafeSpeed = 20f;

    void Start()
    {
        speed = speed * 100f; // more = better right??

        if (speed > maxSafeSpeed)
        {
            BlastRobot();
        }
    }

    void BlastRobot()
    {
        Debug.Log(""BOOM"");
        Destroy(gameObject);
    }
}";

    const string BatteryFixCode = @"using UnityEngine;

// BatteryFix.cs
// fixes the battery so it never runs out
public class BatteryFix : MonoBehaviour
{
    public int battery = 50;

    void Update()
    {
        // charge it every frame
        battery = battery + 10;

        // TODO: stop charging when it's full
        if (battery > 100)
        {
            BlastRobot();
        }
    }

    void BlastRobot()
    {
        // overcharged...
        Destroy(gameObject);
    }
}";

    // ---------------- parkour courses (last block = where the script is) ----------------

    // walking can clear about 3.2m (level) or 2.7m (jumping up), sprinting about 4.5m,
    // so the gaps marked SPRINT are too far to walk. Small pillars (0.8 - 1.0) need careful jumps.
    static readonly Block[] Room1Course =
    {
        new Block(-6.8f, 9.0f, 1.0f, 1.0f, 0.8f),
        new Block(-6.8f, 11.6f, 0.9f, 0.9f, 1.5f),
        new Block(-2.0f, 11.2f, 1.4f, 1.4f, 2.0f),   // SPRINT
        new Block(-1.0f, 14.0f, 0.9f, 0.9f, 2.6f),
        new Block(-5.75f, 15.0f, 1.4f, 1.4f, 3.0f),  // SPRINT
        new Block(-1.5f, 18.0f, 1.6f, 1.6f, 3.6f),   // SPRINT (diagonal)
    };

    static readonly Block[] Room2Course =
    {
        new Block(6.8f, 9.0f, 1.0f, 1.0f, 0.9f),
        new Block(6.8f, 11.6f, 0.8f, 0.8f, 1.5f),
        new Block(6.8f, 14.1f, 0.8f, 0.8f, 2.1f),
        new Block(2.2f, 13.6f, 1.4f, 1.4f, 2.6f),    // SPRINT
        new Block(1.3f, 17.6f, 1.2f, 1.2f, 3.1f),
        new Block(6.2f, 17.6f, 1.6f, 1.6f, 3.7f),    // SPRINT
    };

    static readonly Block[] Room3Course =
    {
        new Block(10.0f, -2.5f, 1.0f, 1.0f, 0.8f),
        new Block(12.2f, -3.6f, 0.9f, 0.9f, 1.5f),
        new Block(16.6f, -3.6f, 1.4f, 1.4f, 2.0f),   // SPRINT
        new Block(18.6f, -0.6f, 0.9f, 0.9f, 2.6f),
        new Block(15.0f, 2.9f, 1.4f, 1.4f, 3.1f),    // SPRINT (diagonal)
        new Block(10.0f, 3.2f, 1.6f, 1.6f, 3.6f),    // SPRINT
    };

    static readonly Block[] Room4Course =
    {
        new Block(-10.2f, 2.6f, 1.0f, 1.0f, 0.9f),
        new Block(-12.4f, 3.6f, 0.9f, 0.9f, 1.6f),
        new Block(-14.6f, 2.4f, 0.9f, 0.9f, 2.2f),
        new Block(-19.3f, 2.4f, 1.2f, 1.2f, 2.2f),   // SPRINT (long, level)
        new Block(-19.3f, -1.4f, 1.2f, 1.2f, 2.9f),
        new Block(-14.6f, -2.6f, 1.6f, 1.6f, 3.5f),  // SPRINT
    };

    static readonly Block[] Room5Course =
    {
        new Block(-3.2f, -9.2f, 1.0f, 1.0f, 0.9f),
        new Block(-3.2f, -11.8f, 0.9f, 0.9f, 1.6f),
        new Block(1.6f, -12.0f, 1.4f, 1.4f, 2.1f),   // SPRINT
        new Block(3.8f, -14.6f, 0.9f, 0.9f, 2.7f),
        new Block(1.0f, -18.0f, 1.4f, 1.4f, 3.1f),
        new Block(-3.9f, -17.4f, 1.6f, 1.6f, 3.6f),  // SPRINT
    };

    [MenuItem("Tools/Code Sheet Quest/Build Prototype Scene")]
    public static void BuildPrototypeScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Code Sheet Quest] Stop Play mode before building the scene.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets/Scenes");
        EnsureFolder(MaterialFolder);
        EnsureFolder(SettingsFolder);
        EnsureFolder(UIFolder);

        // a brand new empty scene every time
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadUIResources();
        CreateMaterials();
        SetupLighting();

        Transform environment = new GameObject("Environment").transform;
        BuildRooms(environment);
        BuildMainRoomFurniture(environment);

        AudioManager audioManager = new GameObject("Audio").AddComponent<AudioManager>();
        PlayerRig rig = BuildPlayer();
        GameUI ui = BuildUI(rig, audioManager);
        rig.interact.gameUI = ui;
        BuildRobot(ui, audioManager);

        Transform parkour = new GameObject("Parkour").transform;
        Transform sheets = new GameObject("Code Sheets").transform;
        BuildCourse(parkour, sheets, "Side Room 1", Room1Course, "PowerCore.cs", PowerCoreCode, false);
        BuildCourse(parkour, sheets, "Side Room 2", Room2Course, "SpeedUpgrade.cs", SpeedUpgradeCode, true);
        BuildCourse(parkour, sheets, "Side Room 3", Room3Course, "EyeLights.cs", EyeLightsCode, false);
        BuildCourse(parkour, sheets, "Side Room 4", Room4Course, "BatteryFix.cs", BatteryFixCode, true);
        BuildCourse(parkour, sheets, "Side Room 5", Room5Course, "ArmWave.cs", ArmWaveCode, false);

        // mixes up which script is in which room every time you play
        ScriptShuffle shuffle = new GameObject("Script Shuffle").AddComponent<ScriptShuffle>();
        shuffle.sheets = sheets.GetComponentsInChildren<ScriptSheet>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();

        int missing = ValidateReferences(scene);
        if (missing == 0)
            Debug.Log("[Code Sheet Quest] Scene built and saved to " + ScenePath + ". Press Play to start!");
        else
            Debug.LogWarning("[Code Sheet Quest] Scene built, but " + missing + " reference(s) are missing. See the warnings above.");
    }

    // =====================================================================
    // Resources, materials and lighting
    // =====================================================================

    static void LoadUIResources()
    {
        // TextMeshPro text stays sharp at any size (the old Text component gets blurry)
        uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (uiFont == null) uiFont = TMP_Settings.defaultFontAsset;
        roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite = MakeCircleSprite("Circle.png", 0f);
        ringSprite = MakeCircleSprite("Ring.png", 0.78f);
    }

    // draws a smooth (anti-aliased) circle or ring into a png and imports it as a sprite
    static Sprite MakeCircleSprite(string fileName, float holeSize)
    {
        string path = UIFolder + "/" + fileName;
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = size / 2f - 1f;
        float hole = radius * holeSize;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                if (holeSize > 0f) alpha *= Mathf.Clamp01(distance - hole + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = true;  // stays smooth when it's drawn small
        importer.filterMode = FilterMode.Trilinear;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void CreateMaterials()
    {
        floorMat = GetOrCreateMaterial("Floor", new Color(0.55f, 0.57f, 0.6f), Color.black, 0.3f, 0f);
        sideFloorMat = GetOrCreateMaterial("SideFloor", new Color(0.46f, 0.5f, 0.55f), Color.black, 0.3f, 0f);
        wallMat = GetOrCreateMaterial("Wall", new Color(0.86f, 0.87f, 0.88f), Color.black, 0.1f, 0f);
        ceilingMat = GetOrCreateMaterial("Ceiling", new Color(0.78f, 0.79f, 0.8f), Color.black, 0.05f, 0f);
        lampMat = GetOrCreateGlowMaterial("CeilingLamp", new Color(1f, 0.97f, 0.9f) * 1.4f);
        woodMat = GetOrCreateMaterial("Wood", new Color(0.6f, 0.46f, 0.33f), Color.black, 0.25f, 0f);
        metalMat = GetOrCreateMaterial("Metal", new Color(0.3f, 0.32f, 0.36f), Color.black, 0.5f, 0.6f);
        accentMat = GetOrCreateGlowMaterial("CyanAccent", new Color(0.1f, 0.7f, 0.9f) * 1.2f);
        paperMat = GetOrCreateMaterial("Paper", new Color(0.93f, 0.95f, 0.97f), Color.black, 0.1f, 0f);
        holoMat = GetOrCreateGlowMaterial("HoloGlow", new Color(0.2f, 0.9f, 1f) * 2f);
        robotBodyMat = GetOrCreateMaterial("RobotBody", new Color(0.85f, 0.88f, 0.92f), Color.black, 0.6f, 0.1f);
        robotDarkMat = GetOrCreateMaterial("RobotDark", new Color(0.18f, 0.2f, 0.25f), Color.black, 0.5f, 0.3f);
        robotGlowMat = GetOrCreateGlowMaterial("RobotGlow", new Color(0.2f, 0.85f, 1f) * 2f);
        platformMat = GetOrCreateMaterial("Platform", new Color(0.36f, 0.42f, 0.5f), Color.black, 0.35f, 0.2f);
        fireMat = GetOrCreateGlowMaterial("Fire", new Color(1f, 0.45f, 0.08f) * 6f);
        buttonMat = GetOrCreateGlowMaterial("ButtonGlow", new Color(0.35f, 1f, 0.55f) * 1.5f);
    }

    // glowing stuff uses the Unlit shader with a colour brighter than 1, so the bloom makes it glow.
    // (this is more reliable than the Emission checkbox, which Unity sometimes switches back off)
    static Material GetOrCreateGlowMaterial(string name, Color glowColor)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", glowColor);
        EditorUtility.SetDirty(material);
        return material;
    }

    // loads the material if it's already there (so running again is safe), otherwise makes it
    static Material GetOrCreateMaterial(string name, Color color, Color emission, float smoothness, float metallic)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

        if (emission.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetupLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.57f);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;

        Light sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.55f;
        sun.color = new Color(1f, 0.97f, 0.92f);
        sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        // soft bloom so the glowing stuff looks like a hologram
        string profilePath = SettingsFolder + "/CodeSheetQuestVolume.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        Bloom bloom;
        if (!profile.TryGet(out bloom))
        {
            bloom = profile.Add<Bloom>(true);
            bloom.name = "Bloom";
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }
        bloom.active = true;
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.6f);
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);

        Volume volume = new GameObject("Global Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    // =====================================================================
    // Rooms
    // =====================================================================

    static void BuildRooms(Transform parent)
    {
        // floor, ceiling and a lamp for every room: (xMin, xMax, zMin, zMax)
        CreateRoomShell(parent, "Main Room", -8f, 8f, -7f, 7f, floorMat);
        CreateRoomShell(parent, "Side Room 1", -8f, 0f, 7f, 19f, sideFloorMat);
        CreateRoomShell(parent, "Side Room 2", 0f, 8f, 7f, 19f, sideFloorMat);
        CreateRoomShell(parent, "Side Room 3", 8f, 20f, -5f, 5f, sideFloorMat);
        CreateRoomShell(parent, "Side Room 4", -20f, -8f, -5f, 5f, sideFloorMat);
        CreateRoomShell(parent, "Side Room 5", -5f, 5f, -19f, -7f, sideFloorMat);

        Transform walls = new GameObject("Walls").transform;
        walls.SetParent(parent, false);

        // main room walls, each one has a doorway into a side room
        WallAlongX(walls, 7f, -8f, 8f, -4f, 4f);   // north: rooms 1 and 2
        WallAlongZ(walls, 8f, -7f, 7f, 0f);        // east: room 3
        WallAlongZ(walls, -8f, -7f, 7f, 0f);       // west: room 4
        WallAlongX(walls, -7f, -8f, 8f, 0f);       // south: room 5

        // rooms 1 and 2 (north)
        WallAlongX(walls, 19f, -8f, 8f);
        WallAlongZ(walls, -8f, 7f, 19f);
        WallAlongZ(walls, 0f, 7f, 19f);
        WallAlongZ(walls, 8f, 7f, 19f);
        // room 3 (east)
        WallAlongX(walls, 5f, 8f, 20f);
        WallAlongX(walls, -5f, 8f, 20f);
        WallAlongZ(walls, 20f, -5f, 5f);
        // room 4 (west)
        WallAlongX(walls, 5f, -20f, -8f);
        WallAlongX(walls, -5f, -20f, -8f);
        WallAlongZ(walls, -20f, -5f, 5f);
        // room 5 (south)
        WallAlongX(walls, -19f, -5f, 5f);
        WallAlongZ(walls, -5f, -19f, -7f);
        WallAlongZ(walls, 5f, -19f, -7f);
    }

    static void CreateRoomShell(Transform parent, string name, float xMin, float xMax, float zMin, float zMax, Material floor)
    {
        Transform room = new GameObject(name).transform;
        room.SetParent(parent, false);
        float width = xMax - xMin;
        float depth = zMax - zMin;
        Vector3 center = new Vector3((xMin + xMax) / 2f, 0f, (zMin + zMax) / 2f);

        CreateBox("Floor", room, center + Vector3.down * 0.05f, new Vector3(width, 0.1f, depth), floor);
        CreateBox("Ceiling", room, center + Vector3.up * (WallHeight + 0.05f), new Vector3(width, 0.1f, depth), ceilingMat);
        CreateBox("Ceiling Lamp", room, center + Vector3.up * (WallHeight - 0.02f), new Vector3(1.6f, 0.04f, 1.6f), lampMat, false);

        Light lamp = new GameObject("Room Light").AddComponent<Light>();
        lamp.transform.SetParent(room, false);
        lamp.transform.localPosition = center + Vector3.up * (WallHeight - 0.6f);
        lamp.type = LightType.Point;
        lamp.range = Mathf.Max(width, depth) * 1.1f + 4f;
        lamp.intensity = 2.2f;
        lamp.color = new Color(1f, 0.96f, 0.9f);
        lamp.shadows = LightShadows.None;
    }

    // a wall along the X axis at z, with gaps for doorways
    static void WallAlongX(Transform parent, float z, float x0, float x1, params float[] doorCenters)
    {
        List<float> doors = new List<float>(doorCenters);
        doors.Sort();
        float cursor = x0 - WallThickness / 2f;
        float end = x1 + WallThickness / 2f;
        foreach (float door in doors)
        {
            float doorStart = door - DoorWidth / 2f;
            AddWallPiece(parent, new Vector3((cursor + doorStart) / 2f, WallHeight / 2f, z), new Vector3(doorStart - cursor, WallHeight, WallThickness));
            AddDoorTop(parent, new Vector3(door, 0f, z), new Vector3(DoorWidth, 0f, WallThickness));
            cursor = door + DoorWidth / 2f;
        }
        AddWallPiece(parent, new Vector3((cursor + end) / 2f, WallHeight / 2f, z), new Vector3(end - cursor, WallHeight, WallThickness));
    }

    // a wall along the Z axis at x, with gaps for doorways
    static void WallAlongZ(Transform parent, float x, float z0, float z1, params float[] doorCenters)
    {
        List<float> doors = new List<float>(doorCenters);
        doors.Sort();
        float cursor = z0 - WallThickness / 2f;
        float end = z1 + WallThickness / 2f;
        foreach (float door in doors)
        {
            float doorStart = door - DoorWidth / 2f;
            AddWallPiece(parent, new Vector3(x, WallHeight / 2f, (cursor + doorStart) / 2f), new Vector3(WallThickness, WallHeight, doorStart - cursor));
            AddDoorTop(parent, new Vector3(x, 0f, door), new Vector3(WallThickness, 0f, DoorWidth));
            cursor = door + DoorWidth / 2f;
        }
        AddWallPiece(parent, new Vector3(x, WallHeight / 2f, (cursor + end) / 2f), new Vector3(WallThickness, WallHeight, end - cursor));
    }

    static void AddWallPiece(Transform parent, Vector3 center, Vector3 size)
    {
        if (size.x <= 0.01f || size.z <= 0.01f) return;
        CreateBox("Wall", parent, center, size, wallMat);
    }

    static void AddDoorTop(Transform parent, Vector3 doorCenter, Vector3 footprint)
    {
        float lintelHeight = WallHeight - DoorHeight;
        CreateBox("Door Lintel", parent, new Vector3(doorCenter.x, DoorHeight + lintelHeight / 2f, doorCenter.z),
            new Vector3(footprint.x, lintelHeight, footprint.z), wallMat);
        CreateBox("Door Accent", parent, new Vector3(doorCenter.x, DoorHeight, doorCenter.z),
            new Vector3(footprint.x + 0.04f, 0.06f, footprint.z + 0.06f), accentMat, false);
    }

    static void BuildMainRoomFurniture(Transform parent)
    {
        Transform furniture = new GameObject("Furniture").transform;
        furniture.SetParent(parent, false);
        CreateDesk(furniture, "Desk", new Vector3(4.5f, 0f, -3.5f), 90f);
        CreateShelf(furniture, "Shelf", new Vector3(-7.7f, 0f, -4f), -90f);
        CreateDesk(furniture, "Side Desk", new Vector3(6.2f, 0f, -6.5f), 0f);
    }

    // =====================================================================
    // Parkour courses + code sheets
    // =====================================================================

    static void BuildCourse(Transform parkourParent, Transform sheetParent, string roomName, Block[] course,
        string fileName, string code, bool isBad)
    {
        Transform group = new GameObject(roomName + " Course").transform;
        group.SetParent(parkourParent, false);

        for (int i = 0; i < course.Length; i++)
        {
            Block b = course[i];
            bool isGoal = i == course.Length - 1;
            string name = isGoal ? "Goal" : "Step " + (i + 1);
            Material material = b.top < 1.2f ? woodMat : platformMat;
            GameObject block = CreateBox(name, group, new Vector3(b.x, b.top / 2f, b.z), new Vector3(b.width, b.top, b.depth), material);
            // thin glowing strips along the top edges so you can see where to jump
            float y = b.top - 0.02f;
            float halfW = b.width / 2f;
            float halfD = b.depth / 2f;
            Vector3[] stripPositions =
            {
                new Vector3(b.x, y, b.z + halfD), new Vector3(b.x, y, b.z - halfD),
                new Vector3(b.x + halfW, y, b.z), new Vector3(b.x - halfW, y, b.z)
            };
            Vector3[] stripSizes =
            {
                new Vector3(b.width + 0.05f, 0.05f, 0.05f), new Vector3(b.width + 0.05f, 0.05f, 0.05f),
                new Vector3(0.05f, 0.05f, b.depth + 0.05f), new Vector3(0.05f, 0.05f, b.depth + 0.05f)
            };
            for (int s = 0; s < 4; s++)
                CreateBox("Edge", group, stripPositions[s], stripSizes[s], accentMat, false).transform.SetParent(block.transform, true);
        }

        // the script floats on the goal block, a little away from where you land
        Block goal = course[course.Length - 1];
        Block before = course[course.Length - 2];
        Vector3 goalTop = new Vector3(goal.x, goal.top, goal.z);
        Vector3 from = new Vector3(before.x, goal.top, before.z);
        Vector3 away = (goalTop - from).normalized;
        float push = Mathf.Min(goal.width, goal.depth) * 0.1f;
        CreateSheet(sheetParent, fileName, code, isBad, goalTop + away * push, from);
    }

    static Collider playerCollider; // set in BuildPlayer, used by the sheets

    static ScriptSheet CreateSheet(Transform parent, string fileName, string code, bool isBad, Vector3 surfacePoint, Vector3 faceToward)
    {
        code = code.Replace("\r", "").Trim();
        GameObject root = new GameObject("Code Sheet - " + fileName);
        root.transform.SetParent(parent, false);

        // the printed side faces -Z, so +Z points away from whoever is looking at it
        Vector3 position = surfacePoint + Vector3.up * 0.55f;
        Vector3 awayFromViewer = position - faceToward;
        awayFromViewer.y = 0f;
        root.transform.position = position;
        root.transform.rotation = Quaternion.LookRotation(awayFromViewer.normalized, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);

        BoxCollider sheetCollider = root.AddComponent<BoxCollider>();
        sheetCollider.size = new Vector3(0.5f, 0.64f, 0.05f);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.mass = 0.3f;
        body.linearDamping = 0.4f;
        body.angularDamping = 0.6f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        CreateBox("Paper", visual, Vector3.zero, new Vector3(0.42f, 0.56f, 0.01f), paperMat, false);
        GameObject frame = CreateBox("Holo Frame", visual, new Vector3(0f, 0f, 0.007f), new Vector3(0.47f, 0.61f, 0.004f), holoMat, false);
        CreateBox("Header Bar", visual, new Vector3(0f, 0.245f, -0.0055f), new Vector3(0.42f, 0.07f, 0.001f), accentMat, false);

        // the printed text is a tiny canvas sitting just in front of the paper
        GameObject print = new GameObject("Print", typeof(RectTransform));
        print.transform.SetParent(visual, false);
        Canvas canvas = print.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform printRect = print.GetComponent<RectTransform>();
        printRect.sizeDelta = new Vector2(840f, 1120f);
        printRect.localScale = Vector3.one * 0.0005f;
        printRect.localPosition = new Vector3(0f, 0f, -0.007f);

        CreateText(print.transform, "File Name", fileName, 50, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 490f), new Vector2(800f, 120f), FontStyle.Bold);
        TMP_Text codeLines = CreateText(print.transform, "Code Lines", PrintedLines(code), 34,
            new Color(0.07f, 0.1f, 0.16f), TextAnchor.UpperLeft, new Vector2(0f, -40f), new Vector2(780f, 860f));
        codeLines.textWrappingMode = TextWrappingModes.NoWrap;
        codeLines.overflowMode = TextOverflowModes.Truncate;
        codeLines.richText = false;
        CreateText(print.transform, "Footer", "C# SCRIPT", 28, InkAccent, TextAnchor.MiddleCenter,
            new Vector2(0f, -510f), new Vector2(800f, 60f), FontStyle.Bold);

        Light glow = new GameObject("Glow Light").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0f, 0.25f);
        glow.type = LightType.Point;
        glow.color = new Color(0.3f, 0.9f, 1f);
        glow.range = 1.6f;
        glow.intensity = 0.8f;
        glow.shadows = LightShadows.None;

        ScriptSheet sheet = root.AddComponent<ScriptSheet>();
        sheet.fileName = fileName;
        sheet.code = code;
        sheet.isBadScript = isBad;
        sheet.visual = visual;
        sheet.glowFrame = frame.GetComponent<Renderer>();
        sheet.glowLight = glow;
        sheet.playerCollider = playerCollider;
        return sheet;
    }

    // the first lines of the code, cut short so they fit on the paper
    static string PrintedLines(string code)
    {
        string[] lines = code.Split('\n');
        List<string> printed = new List<string>();
        for (int i = 0; i < lines.Length && printed.Count < 16; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.Length > 40) line = line.Substring(0, 38) + "..";
            printed.Add(line);
        }
        return string.Join("\n", printed);
    }

    static Transform CreateDesk(Transform parent, string name, Vector3 position, float yRotation)
    {
        float width = 1.6f, depth = 0.8f, height = 0.76f;
        Transform desk = new GameObject(name).transform;
        desk.SetParent(parent, false);
        desk.localPosition = position;
        desk.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        CreateBox("Top", desk, new Vector3(0f, height - 0.03f, 0f), new Vector3(width, 0.06f, depth), woodMat);
        float legX = width / 2f - 0.06f;
        float legZ = depth / 2f - 0.06f;
        float legHeight = height - 0.06f;
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                CreateBox("Leg", desk, new Vector3(sx * legX, legHeight / 2f, sz * legZ), new Vector3(0.06f, legHeight, 0.06f), metalMat);
        return desk;
    }

    static Transform CreateShelf(Transform parent, string name, Vector3 position, float yRotation)
    {
        float width = 1.6f, depth = 0.4f, height = 2f;
        Transform shelf = new GameObject(name).transform;
        shelf.SetParent(parent, false);
        shelf.localPosition = position;
        shelf.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        CreateBox("Side Left", shelf, new Vector3(-width / 2f + 0.03f, height / 2f, 0f), new Vector3(0.06f, height, depth), woodMat);
        CreateBox("Side Right", shelf, new Vector3(width / 2f - 0.03f, height / 2f, 0f), new Vector3(0.06f, height, depth), woodMat);
        CreateBox("Back", shelf, new Vector3(0f, height / 2f, depth / 2f - 0.015f), new Vector3(width, height, 0.03f), woodMat);
        foreach (float boardY in new[] { 0.08f, 0.6f, 1.35f, 1.95f })
            CreateBox("Board", shelf, new Vector3(0f, boardY, 0f), new Vector3(width - 0.12f, 0.04f, depth), woodMat);
        CreateBox("Book", shelf, new Vector3(-0.5f, 1.52f, 0f), new Vector3(0.08f, 0.3f, 0.26f), accentMat, false);
        CreateBox("Book", shelf, new Vector3(-0.4f, 1.5f, 0f), new Vector3(0.07f, 0.26f, 0.24f), robotDarkMat, false);
        CreateBox("Book", shelf, new Vector3(-0.31f, 1.51f, 0f), new Vector3(0.08f, 0.28f, 0.25f), metalMat, false);
        return shelf;
    }

    // =====================================================================
    // Player
    // =====================================================================

    static PlayerRig BuildPlayer()
    {
        PlayerRig rig = new PlayerRig();
        rig.root = new GameObject("Player");
        rig.root.layer = 2; // Ignore Raycast, so the look ray never hits the player
        rig.root.transform.position = PlayerStart;

        CharacterController controller = rig.root.AddComponent<CharacterController>();
        playerCollider = controller;
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.04f;

        rig.movement = rig.root.AddComponent<PlayerMovement>();
        rig.interact = rig.root.AddComponent<PlayerInteract>();

        GameObject cameraObject = new GameObject("Player Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(rig.root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        rig.camera = cameraObject.AddComponent<Camera>();
        rig.camera.fieldOfView = 70f;
        rig.camera.nearClipPlane = 0.05f;
        rig.camera.clearFlags = CameraClearFlags.SolidColor;
        rig.camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
        cameraObject.AddComponent<AudioListener>();
        rig.camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        rig.look = cameraObject.AddComponent<PlayerLook>();
        rig.look.playerBody = rig.root.transform;

        // where a held script sits: low in the middle of the screen, tilted back like holding a map
        rig.holdPoint = new GameObject("Hold Point").transform;
        rig.holdPoint.SetParent(cameraObject.transform, false);
        rig.holdPoint.localPosition = new Vector3(0f, -0.2f, 0.62f);
        rig.holdPoint.localRotation = Quaternion.Euler(20f, 0f, 0f);

        // a second camera that only draws the script you're holding, on top of everything,
        // so it never sinks into walls (same trick games use for hands and held items)
        int heldLayer = EnsureLayer("HeldItem");
        if (heldLayer >= 0)
        {
            rig.camera.cullingMask = ~(1 << heldLayer);
            GameObject handCameraObject = new GameObject("Hand Camera");
            handCameraObject.transform.SetParent(cameraObject.transform, false);
            Camera handCamera = handCameraObject.AddComponent<Camera>();
            handCamera.fieldOfView = rig.camera.fieldOfView;
            handCamera.nearClipPlane = 0.01f;
            handCamera.farClipPlane = 5f;
            handCamera.cullingMask = 1 << heldLayer;
            handCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            rig.camera.GetUniversalAdditionalCameraData().cameraStack.Add(handCamera);
        }

        rig.interact.playerCamera = rig.camera;
        rig.interact.playerMovement = rig.movement;
        rig.interact.holdPoint = rig.holdPoint;
        return rig;
    }

    // =====================================================================
    // Robot
    // =====================================================================

    static RobotController BuildRobot(GameUI ui, AudioManager audioManager)
    {
        GameObject robotObject = new GameObject("Robot (Byte)");
        Transform robot = robotObject.transform;
        robot.position = RobotPosition;
        robot.rotation = Quaternion.Euler(0f, 180f, 0f); // front (+Z) faces the player start

        BoxCollider bodyCollider = robotObject.AddComponent<BoxCollider>();
        bodyCollider.center = new Vector3(0f, 1.15f, 0f);
        bodyCollider.size = new Vector3(1.5f, 2.3f, 0.9f);

        CreatePrimitive(PrimitiveType.Cylinder, "Submission Pad", robot, new Vector3(0f, 0.01f, 0.3f), new Vector3(2.6f, 0.01f, 2.6f), accentMat, false);
        CreateBox("Leg Left", robot, new Vector3(-0.24f, 0.3f, 0f), new Vector3(0.28f, 0.6f, 0.3f), robotDarkMat, false);
        CreateBox("Leg Right", robot, new Vector3(0.24f, 0.3f, 0f), new Vector3(0.28f, 0.6f, 0.3f), robotDarkMat, false);
        CreateBox("Body", robot, new Vector3(0f, 1.075f, 0f), new Vector3(1f, 0.95f, 0.7f), robotBodyMat, false);
        GameObject chest = CreateBox("Chest Light", robot, new Vector3(0f, 1.1f, 0.36f), new Vector3(0.5f, 0.28f, 0.03f), robotGlowMat, false);
        CreateBox("Script Slot", robot, new Vector3(0f, 1.552f, 0.12f), new Vector3(0.55f, 0.01f, 0.08f), robotDarkMat, false);
        CreateBox("Neck", robot, new Vector3(0f, 1.61f, -0.1f), new Vector3(0.22f, 0.14f, 0.22f), robotDarkMat, false);

        // the point the paper flies into (just above the slot, standing up)
        Transform slot = new GameObject("Slot Point").transform;
        slot.SetParent(robot, false);
        slot.localPosition = new Vector3(0f, 1.62f, 0.12f);
        slot.localRotation = Quaternion.Euler(0f, 180f, 0f);

        Transform head = new GameObject("Head").transform;
        head.SetParent(robot, false);
        head.localPosition = new Vector3(0f, 1.67f, -0.1f);
        CreateBox("Head Shell", head, new Vector3(0f, 0.25f, 0f), new Vector3(0.72f, 0.5f, 0.56f), robotBodyMat, false);
        CreateBox("Face Plate", head, new Vector3(0f, 0.27f, 0.285f), new Vector3(0.6f, 0.3f, 0.02f), robotDarkMat, false);
        GameObject eyeLeft = CreatePrimitive(PrimitiveType.Sphere, "Eye Left", head, new Vector3(-0.15f, 0.28f, 0.29f), Vector3.one * 0.13f, robotGlowMat, false);
        GameObject eyeRight = CreatePrimitive(PrimitiveType.Sphere, "Eye Right", head, new Vector3(0.15f, 0.28f, 0.29f), Vector3.one * 0.13f, robotGlowMat, false);
        CreateBox("Antenna", head, new Vector3(0f, 0.6f, 0f), new Vector3(0.04f, 0.22f, 0.04f), robotDarkMat, false);
        GameObject antennaTip = CreatePrimitive(PrimitiveType.Sphere, "Antenna Tip", head, new Vector3(0f, 0.74f, 0f), Vector3.one * 0.1f, robotGlowMat, false);

        Light eyeLight = new GameObject("Eye Light").AddComponent<Light>();
        eyeLight.transform.SetParent(head, false);
        eyeLight.transform.localPosition = new Vector3(0f, 0.28f, 0.7f);
        eyeLight.type = LightType.Point;
        eyeLight.color = new Color(0.2f, 0.85f, 1f);
        eyeLight.range = 3f;
        eyeLight.intensity = 1.5f;
        eyeLight.shadows = LightShadows.None;

        // arms hang from a pivot at the shoulder, so turning the pivot swings the whole arm
        Transform leftArm = CreateArm(robot, "Left Arm Pivot", new Vector3(-0.6f, 1.48f, 0f));
        Transform rightArm = CreateArm(robot, "Right Arm Pivot", new Vector3(0.6f, 1.48f, 0f));

        // little terminal with a screen that shows how many scripts are in
        Transform terminal = new GameObject("Terminal").transform;
        terminal.SetParent(robot, false);
        terminal.localPosition = new Vector3(1.4f, 0f, 0.35f);
        CreateBox("Stand", terminal, new Vector3(0f, 0.5f, 0f), new Vector3(0.35f, 1f, 0.35f), robotDarkMat);
        Transform screenPivot = new GameObject("Screen Pivot").transform;
        screenPivot.SetParent(terminal, false);
        screenPivot.localPosition = new Vector3(0f, 1.12f, 0.03f);
        screenPivot.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        CreateBox("Screen", screenPivot, Vector3.zero, new Vector3(0.6f, 0.38f, 0.05f), robotDarkMat);

        GameObject screenCanvasObject = new GameObject("Screen Canvas", typeof(RectTransform));
        screenCanvasObject.transform.SetParent(screenPivot, false);
        Canvas screenCanvas = screenCanvasObject.AddComponent<Canvas>();
        screenCanvas.renderMode = RenderMode.WorldSpace;
        RectTransform screenRect = screenCanvasObject.GetComponent<RectTransform>();
        screenRect.sizeDelta = new Vector2(560f, 340f);
        screenRect.localScale = Vector3.one * 0.001f;
        screenRect.localPosition = new Vector3(0f, 0f, 0.03f);
        screenRect.localRotation = Quaternion.Euler(0f, 180f, 0f);
        TMP_Text screenText = CreateText(screenCanvasObject.transform, "Screen Text", "SCRIPTS\n0 / 3", 80, Cyan,
            TextAnchor.MiddleCenter, Vector2.zero, new Vector2(540f, 320f), FontStyle.Bold);

        // a post with a big button on top. The button only shows up once all 3 good scripts are in.
        CreateBox("Button Post", robot, new Vector3(-1.4f, 0.45f, 0.35f), new Vector3(0.3f, 0.9f, 0.3f), robotDarkMat);
        GameObject activateButton = CreatePrimitive(PrimitiveType.Cylinder, "Activate Button", robot, new Vector3(-1.4f, 0.93f, 0.35f), new Vector3(0.28f, 0.04f, 0.28f), buttonMat, true);

        RobotController controller = robotObject.AddComponent<RobotController>();
        controller.activateButton = activateButton;
        controller.scriptSlot = slot;
        controller.leftArm = leftArm;
        controller.rightArm = rightArm;
        controller.head = head;
        controller.glowParts = new Renderer[]
        {
            eyeLeft.GetComponent<Renderer>(), eyeRight.GetComponent<Renderer>(),
            chest.GetComponent<Renderer>(), antennaTip.GetComponent<Renderer>()
        };
        controller.eyeLight = eyeLight;
        controller.screenText = screenText;
        controller.gameUI = ui;
        controller.audioManager = audioManager;
        controller.fireMaterial = fireMat;
        return controller;
    }

    static Transform CreateArm(Transform robot, string name, Vector3 shoulder)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(robot, false);
        pivot.localPosition = shoulder;
        CreatePrimitive(PrimitiveType.Sphere, "Shoulder", pivot, Vector3.zero, Vector3.one * 0.22f, robotDarkMat, false);
        CreateBox("Arm", pivot, new Vector3(0f, -0.31f, 0f), new Vector3(0.18f, 0.62f, 0.18f), robotBodyMat, false);
        CreatePrimitive(PrimitiveType.Sphere, "Hand", pivot, new Vector3(0f, -0.68f, 0f), Vector3.one * 0.2f, robotDarkMat, false);
        return pivot;
    }

    // =====================================================================
    // User interface
    // =====================================================================

    static GameUI BuildUI(PlayerRig rig, AudioManager audioManager)
    {
        // the event system lets you click buttons (this project uses the new Input System)
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();

        GameObject canvasObject = new GameObject("Game UI", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        Transform root = canvasObject.transform;

        GameUI ui = canvasObject.AddComponent<GameUI>();
        ui.playerMovement = rig.movement;
        ui.playerLook = rig.look;
        ui.playerInteract = rig.interact;
        ui.audioManager = audioManager;

        // ---------- HUD (top left) ----------
        Image hud = CreateBox2D(root, "HUD", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(480f, 92f), new Color(0f, 0f, 0f, 0.55f));
        ui.robotCountText = CreateText(hud.transform, "Robot Count", "Byte: 0/3 working scripts", 24, TextLight, TextAnchor.MiddleLeft,
            new Vector2(0f, 18f), new Vector2(440f, 34f), FontStyle.Bold);
        ui.holdingText = CreateText(hud.transform, "Holding", "Holding: nothing", 20, TextMuted, TextAnchor.MiddleLeft,
            new Vector2(0f, -19f), new Vector2(440f, 30f));

        // ---------- round crosshair ----------
        GameObject crosshair = new GameObject("Crosshair", typeof(RectTransform));
        crosshair.transform.SetParent(root, false);
        Place(crosshair.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
        Image ring = CreateImage(crosshair.transform, "Ring", new Color(0.3f, 0.9f, 1f, 0f));
        ring.sprite = ringSprite;
        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));
        Image dotOutline = CreateImage(crosshair.transform, "Dot Outline", new Color(0f, 0f, 0f, 0.45f));
        dotOutline.sprite = circleSprite;
        Place(dotOutline.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));
        Image dot = CreateImage(crosshair.transform, "Dot", new Color(1f, 1f, 1f, 0.9f));
        dot.sprite = circleSprite;
        Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
        ui.crosshair = crosshair;
        ui.crosshairDot = dot.rectTransform;
        ui.crosshairDotImage = dot;
        ui.crosshairDotOutline = dotOutline.rectTransform;
        ui.crosshairRing = ring.rectTransform;
        ui.crosshairRingImage = ring;

        // ---------- end screen ----------
        Image endScreen = CreateBox2D(root, "End Screen", new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1150f, 240f), new Color(0.03f, 0.18f, 0.1f, 0.92f));
        ui.endScreen = endScreen.gameObject;
        ui.endScreenBackground = endScreen;
        ui.endTitleText = CreateText(endScreen.transform, "Title", "MISSION COMPLETE!", 62, new Color(1f, 0.88f, 0.35f), TextAnchor.MiddleCenter,
            new Vector2(0f, 45f), new Vector2(1100f, 84f), FontStyle.Bold);
        ui.endInfoText = CreateText(endScreen.transform, "Info", "", 24, TextLight, TextAnchor.MiddleCenter, new Vector2(0f, -55f), new Vector2(1100f, 100f));

        // ---------- start window ----------
        BuildTutorial(root, ui);

        // ---------- code editor window ----------
        Image editor = CreatePanel(root, "Code Editor", new Vector2(1240f, 900f), EditorBg);
        Transform ed = editor.transform;
        ui.editorWindow = editor.gameObject;

        Image titleBar = CreateImage(ed, "Title Bar", EditorTitleBar);
        titleBar.sprite = roundedSprite;
        titleBar.type = Image.Type.Sliced;
        Place(titleBar.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1240f, 46f));
        Color[] dotColors = { new Color(1f, 0.37f, 0.34f), new Color(1f, 0.74f, 0.18f), new Color(0.16f, 0.79f, 0.25f) };
        for (int i = 0; i < 3; i++)
        {
            Image windowDot = CreateImage(titleBar.transform, "Window Dot", dotColors[i]);
            windowDot.sprite = circleSprite;
            Place(windowDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f + i * 24f, 0f), new Vector2(14f, 14f));
        }
        CreateText(titleBar.transform, "Title", "Byte's Code Viewer", 18, TextMuted, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(600f, 40f));

        Image tabStrip = CreateImage(ed, "Tab Strip", EditorTabStrip);
        Place(tabStrip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(1240f, 44f));
        Image tab = CreateImage(tabStrip.transform, "Tab", EditorBg);
        Place(tab.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(340f, 44f));
        Image tabAccent = CreateImage(tab.transform, "Tab Accent", Cyan);
        Place(tabAccent.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(340f, 3f));
        ui.editorTabText = CreateText(tab.transform, "File Name", "Script.cs", 20, TextLight, TextAnchor.MiddleLeft, new Vector2(-12f, 0f), new Vector2(280f, 40f));
        TMP_Text closeLabel;
        Button closeTab = CreateButton(tab.transform, "Close Tab", "×", new Vector2(146f, 0f), new Vector2(34f, 30f), EditorBg, 24, ui.CloseEditor, out closeLabel);
        closeLabel.color = TextMuted;

        Image gutter = CreateImage(ed, "Gutter", EditorGutter);
        gutter.rectTransform.anchorMin = new Vector2(0f, 0f);
        gutter.rectTransform.anchorMax = new Vector2(0f, 1f);
        gutter.rectTransform.pivot = new Vector2(0f, 1f);
        gutter.rectTransform.offsetMin = new Vector2(0f, 120f);
        gutter.rectTransform.offsetMax = new Vector2(70f, -90f);

        ui.lineNumbersText = CreateText(gutter.transform, "Line Numbers", "1", 21, new Color(0.43f, 0.47f, 0.52f), TextAnchor.UpperRight, Vector2.zero, Vector2.zero);
        ui.lineNumbersText.rectTransform.anchorMin = Vector2.zero;
        ui.lineNumbersText.rectTransform.anchorMax = Vector2.one;
        ui.lineNumbersText.rectTransform.offsetMin = new Vector2(0f, 0f);
        ui.lineNumbersText.rectTransform.offsetMax = new Vector2(-14f, -16f);

        ui.codeText = CreateText(ed, "Code", "", 21, new Color(0.83f, 0.83f, 0.83f), TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
        ui.codeText.rectTransform.anchorMin = Vector2.zero;
        ui.codeText.rectTransform.anchorMax = Vector2.one;
        ui.codeText.rectTransform.offsetMin = new Vector2(92f, 120f);
        ui.codeText.rectTransform.offsetMax = new Vector2(-24f, -106f);
        ui.codeText.textWrappingMode = TextWrappingModes.NoWrap;

        Image bottomLine = CreateImage(ed, "Bottom Line", new Color(1f, 1f, 1f, 0.08f));
        Place(bottomLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(1240f, 2f));
        TMP_Text warning = CreateText(ed, "Warning", "Read the code before you insert it.\nSome scripts are buggy!", 18, new Color(1f, 0.75f, 0.4f), TextAnchor.MiddleLeft, Vector2.zero, new Vector2(330f, 60f));
        Place(warning.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 30f), new Vector2(330f, 60f));
        TMP_Text escHint = CreateText(ed, "Esc Hint", "Esc to close", 18, TextMuted, TextAnchor.MiddleRight, Vector2.zero, new Vector2(200f, 30f));
        Place(escHint.rectTransform, new Vector2(1f, 0f), new Vector2(-28f, 45f), new Vector2(200f, 30f));

        TMP_Text dropLabel;
        Button dropButton = CreateButton(ed, "Drop Button", "DROP\n<size=15>let it fall down</size>", Vector2.zero, new Vector2(240f, 72f), ButtonOrange, 26, ui.DropButton, out dropLabel);
        Place(dropButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-135f, 24f), new Vector2(240f, 72f));
        TMP_Text holdLabel;
        Button holdButton = CreateButton(ed, "Hold Button", "HOLD\n<size=15>carry it  (G to drop)</size>", Vector2.zero, new Vector2(240f, 72f), ButtonCyan, 26, ui.HoldButton, out holdLabel);
        Place(holdButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(135f, 24f), new Vector2(240f, 72f));
        ui.editorBottomStuff = new GameObject[] { dropButton.gameObject, holdButton.gameObject, warning.gameObject, escHint.gameObject, closeTab.gameObject };

        ui.tutorialWindow.SetActive(true);
        editor.gameObject.SetActive(false);
        endScreen.gameObject.SetActive(false);
        return ui;
    }

    // =====================================================================
    // Small helpers
    // =====================================================================

    static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
        Vector3 scale, Material material, bool keepCollider)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localScale = scale;
        primitive.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(primitive.GetComponent<Collider>());
        return primitive;
    }

    static GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool keepCollider = true)
    {
        return CreatePrimitive(PrimitiveType.Cube, name, parent, localPosition, size, material, keepCollider);
    }


    // the window you see when the game starts: the goal in 4 steps, then the controls
    static void BuildTutorial(Transform root, GameUI ui)
    {
        Image tutorial = CreatePanel(root, "Tutorial Window", new Vector2(1100f, 880f), PanelDark);
        Transform t = tutorial.transform;
        ui.tutorialWindow = tutorial.gameObject;

        Image topAccent = CreateImage(t, "Top Accent", Cyan);
        Place(topAccent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1040f, 4f));

        CreateText(t, "Title", "CODE SHEET QUEST", 60, Cyan, TextAnchor.MiddleCenter, new Vector2(0f, 360f), new Vector2(1000f, 76f), FontStyle.Bold);
        CreateText(t, "Subtitle", "Byte the robot is broken. Find the right code and bring him back to life!", 24, TextMuted,
            TextAnchor.MiddleCenter, new Vector2(0f, 310f), new Vector2(1000f, 34f));
        Image divider = CreateImage(t, "Divider", new Color(1f, 1f, 1f, 0.08f));
        Place(divider.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 278f), new Vector2(980f, 2f));

        CreateText(t, "Mission Heading", "YOUR MISSION", 21, Cyan, TextAnchor.MiddleLeft, new Vector2(0f, 248f), new Vector2(980f, 30f), FontStyle.Bold);
        string[] steps =
        {
            "Sprint and jump through the side rooms to reach the <b>5 glowing scripts</b>. They hide in new spots every game!",
            "Press <color=#4DE0FF><b>E</b></color> on a script and <b>read the code</b>. Two of them have a bug that makes Byte explode.",
            "Choose <b>HOLD</b> on a working script, carry it back and press <color=#4DE0FF><b>E</b></color> on Byte to insert it.",
            "Once all <b>3 working scripts</b> are in, press Byte's <color=#5CFF8A><b>green button</b></color> to wake him up."
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Image row = CreateBox2D(t, "Step " + (i + 1), new Vector2(0.5f, 0.5f), new Vector2(0f, 196f - i * 70f), new Vector2(980f, 62f),
                new Color(1f, 1f, 1f, 0.045f));
            Image badge = CreateImage(row.transform, "Number", Cyan);
            badge.sprite = circleSprite;
            Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(38f, 38f));
            FillText(badge.transform, "Digit", (i + 1).ToString(), 21, PanelDark, FontStyle.Bold, 0f);
            TMP_Text stepText = CreateText(row.transform, "Text", steps[i], 21, TextLight, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(890f, 58f));
            Place(stepText.rectTransform, new Vector2(0f, 0.5f), new Vector2(68f, 0f), new Vector2(890f, 58f));
        }

        CreateText(t, "Controls Heading", "CONTROLS", 21, Cyan, TextAnchor.MiddleLeft, new Vector2(0f, -78f), new Vector2(980f, 30f), FontStyle.Bold);
        string[] keys = { "WASD", "Mouse", "Shift", "Space", "E", "G", "Esc", "R" };
        string[] actions = { "Move", "Look around", "Sprint (hold)", "Jump", "Open a script / insert / press", "Drop the script you hold", "Close the code window", "Restart any time" };
        for (int i = 0; i < keys.Length; i++)
        {
            int column = i / 4;
            int line = i % 4;
            Vector2 position = new Vector2(-490f + column * 500f, -124f - line * 50f);
            Image keyCap = CreateBox2D(t, "Key " + keys[i], new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 40f), new Color(0.15f, 0.22f, 0.31f));
            keyCap.rectTransform.pivot = new Vector2(0f, 0.5f);
            keyCap.rectTransform.anchoredPosition = position;
            FillText(keyCap.transform, "Label", keys[i], 20, Cyan, FontStyle.Bold, 0f);
            TMP_Text action = CreateText(t, "Action " + keys[i], actions[i], 22, TextLight, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(370f, 40f));
            action.rectTransform.pivot = new Vector2(0f, 0.5f);
            action.rectTransform.anchoredPosition = position + new Vector2(112f, 0f);
        }

        TMP_Text unused;
        CreateButton(t, "Start Button", "Start Exploring", new Vector2(0f, -368f), new Vector2(360f, 72f), ButtonCyan, 28, ui.StartButton, out unused);
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Image CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        Image panel = CreateImage(parent, name, color);
        panel.sprite = roundedSprite;
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true; // stops clicks going through the window
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return panel;
    }

    static Image CreateBox2D(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        Image box = CreateImage(parent, name, color);
        box.sprite = roundedSprite;
        box.type = Image.Type.Sliced;
        Place(box.rectTransform, anchor, position, size);
        return box;
    }

    static TMP_Text CreateText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment,
        Vector2 position, Vector2 size, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = uiFont;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = ToTMPAlignment(alignment);
        if (style == FontStyle.Bold) text.fontStyle = FontStyles.Bold;
        else if (style == FontStyle.Italic) text.fontStyle = FontStyles.Italic;
        else text.fontStyle = FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        Place(text.rectTransform, new Vector2(0.5f, 0.5f), position, size);
        return text;
    }

    static TextAlignmentOptions ToTMPAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    static TMP_Text FillText(Transform parent, string name, string content, int fontSize, Color color, FontStyle style, float padding)
    {
        TMP_Text text = CreateText(parent, name, content, fontSize, color, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, style);
        Stretch(text.rectTransform, padding);
        return text;
    }

    // makes a button and hooks its On Click up to a GameUI function (same as dragging it in the Inspector)
    static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color,
        int fontSize, UnityAction onClick, out TMP_Text labelText)
    {
        Image image = CreateImage(parent, name, color);
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;
        Place(image.rectTransform, new Vector2(0.5f, 0.5f), position, size);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Navigation noNavigation = new Navigation();
        noNavigation.mode = Navigation.Mode.None; // WASD should never move between buttons
        button.navigation = noNavigation;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.95f, 1f);
        colors.pressedColor = new Color(0.65f, 0.75f, 0.8f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f);
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, onClick);

        labelText = FillText(image.transform, "Label", label, fontSize, new Color(0.05f, 0.08f, 0.12f), FontStyle.Bold, 4f);
        return button;
    }

    // adds a layer to Project Settings > Tags and Layers if it isn't there yet
    static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }
        Debug.LogWarning("[Code Sheet Quest] No free layer for HeldItem, held scripts may go into walls.");
        return -1;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // warns about any empty slot on our own scripts so nothing is forgotten
    static int ValidateReferences(Scene scene)
    {
        int missing = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || !string.IsNullOrEmpty(component.GetType().Namespace)) continue;
                if (component is AudioManager) continue; // its sound slots are allowed to be empty

                SerializedProperty property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                    if (property.objectReferenceValue != null) continue;
                    Debug.LogWarning("[Code Sheet Quest] Missing reference: " + component.name + " > " + component.GetType().Name + "." + property.propertyPath, component);
                    missing++;
                }
            }
        }
        return missing;
    }
}

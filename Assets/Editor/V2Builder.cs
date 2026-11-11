#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds everything v2 added to the scene: Crew Quarters, Engineering, the
// new interactables, crew logs, signage, the reworked artifact and the UI
// tweaks. Idempotent: all new geometry lives under Station/V2, which is
// deleted and rebuilt each run, and edits to v1 objects are absolute.
// Menu: Day 181 > Rebuild v2 content.
public static class V2Builder
{
    const string MatDir = "Assets/Materials/V2";
    const string ArtDir = "Assets/Art/V2";
    const string AudioDir = "Assets/Resources/V2Audio/";

    static Transform root;
    static Material wallSrc, floorSrc, ceilSrc;
    static Material mMetal, mInteract, mHazard, mCell, mCellDead, mScreen, mGlow, mDoor, mLamp, mRing, mPad;
    static readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

    [MenuItem("Day 181/Rebuild v2 content")]
    public static string Build()
    {
        log.Clear();
        var station = GameObject.Find("Station");
        if (station == null) return "Station not found";

        Directory(MatDir);
        Directory(ArtDir);
        LoadMaterials();

        var old = station.transform.Find("V2");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        root = new GameObject("V2").transform;
        root.SetParent(station.transform, false);

        var systems = GameObject.Find("Systems");
        var oldDirector = systems.transform.Find("StoryDirector");
        if (oldDirector != null) Object.DestroyImmediate(oldDirector.gameObject);
        var directorGO = new GameObject("StoryDirector");
        directorGO.transform.SetParent(systems.transform, false);
        var director = directorGO.AddComponent<StoryDirector>();

        FixV1Materials(station.transform);
        BuildQuarters(station.transform, director);
        BuildEngineering(station.transform, director);
        BuildLab(station.transform, director);
        BuildLogs();
        BuildAtmosphere(station.transform);
        BuildSoundPoints(director);
        BuildArtifact(station.transform);
        BuildUI();
        BuildVignette();
        Cleanup(station.transform);

        EditorUtility.SetDirty(director);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(station.scene);
        AssetDatabase.SaveAssets();
        log.AppendLine("renderers under Station: " + station.GetComponentsInChildren<Renderer>(true).Length);
        return log.ToString();
    }

    // ---------------------------------------------------------------- helpers

    static void Directory(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static Material Mat(string name, Color color, Color? emission = null, float metallic = 0.2f, float smoothness = 0.35f)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue) EnableEmission(m, emission.Value);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Emission has to be compiled into the material for the runtime glow
    // (a MaterialPropertyBlock on _EmissionColor) to be visible at all.
    static void EnableEmission(Material m, Color c)
    {
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        // Never exactly black, or Unity's material validation strips the keyword.
        if (c.maxColorComponent < 0.002f) c = new Color(0.002f, 0.002f, 0.002f);
        m.SetColor("_EmissionColor", c);
        EditorUtility.SetDirty(m);
    }

    static void LoadMaterials()
    {
        wallSrc = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Wall.mat");
        floorSrc = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Floor.mat");
        ceilSrc = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Ceiling.mat");
        mMetal = Mat("Mat_V2_Metal", new Color(0.13f, 0.15f, 0.18f), null, 0.6f, 0.4f);
        mInteract = Mat("Mat_V2_Interact", new Color(0.2f, 0.22f, 0.26f), Color.black, 0.5f, 0.45f);
        mHazard = Mat("Mat_V2_Hazard", new Color(0.85f, 0.62f, 0.08f), null, 0.1f, 0.3f);
        mCell = Mat("Mat_V2_Cell", new Color(0.05f, 0.25f, 0.24f), new Color(0.1f, 0.9f, 0.8f) * 0.8f, 0.3f, 0.6f);
        mCellDead = Mat("Mat_V2_CellDead", new Color(0.07f, 0.08f, 0.09f), null, 0.3f, 0.5f);
        mScreen = Mat("Mat_V2_Screen", new Color(0.02f, 0.05f, 0.07f), Color.black, 0f, 0.8f);
        mGlow = Mat("Mat_V2_Glow", new Color(0.06f, 0.08f, 0.09f), Color.black, 0.2f, 0.5f);
        mDoor = Mat("Mat_V2_Door", new Color(0.16f, 0.19f, 0.22f), null, 0.5f, 0.35f);
        mLamp = Mat("Mat_V2_Lamp", new Color(0.05f, 0.05f, 0.05f), Color.black, 0f, 0.5f);
        mRing = Mat("Mat_V2_Ring", new Color(0.05f, 0.06f, 0.1f), new Color(0.35f, 0.55f, 1f) * 0.6f, 0.8f, 0.7f);
        mPad = Mat("Mat_V2_Datapad", new Color(0.05f, 0.06f, 0.07f), null, 0.4f, 0.5f);
    }

    static Material Tiled(Material src, float x, float y)
    {
        var m = new Material(src) { name = src.name + " (V2)" };
        m.mainTextureScale = new Vector2(Mathf.Max(1f, x), Mathf.Max(1f, y));
        return m;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (type != PrimitiveType.Cube || !collider)
        {
            Object.DestroyImmediate(col);
            if (collider) go.AddComponent<BoxCollider>();
        }
        return go;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = true)
        => Prim(PrimitiveType.Cube, name, parent, pos, scale, mat, collider);

    // A wall slab whose panel texture repeats once per metre, like v1's walls.
    static GameObject Wall(string name, Transform parent, Vector3 pos, Vector3 scale)
    {
        float length = Mathf.Max(scale.x, scale.z);
        return Box(name, parent, pos, scale, Tiled(wallSrc, length, scale.y));
    }

    static Transform Group(string name, Transform parent, Vector3 pos, Vector3? euler = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(euler ?? Vector3.zero);
        return go.transform;
    }

    // `lookDir` = the direction the reader is looking when they read it.
    static TextMeshPro Sign(string name, Transform parent, Vector3 pos, Vector3 lookDir, string text, float size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        go.transform.localScale = Vector3.one * 0.02f;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.color = color;
        t.rectTransform.sizeDelta = new Vector2(120f, 30f);
        return t;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static AudioSource Source(GameObject go, string clip, float maxDistance)
    {
        var s = go.AddComponent<AudioSource>();
        s.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + clip + ".wav");
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1f;
        s.maxDistance = maxDistance;
        return s;
    }

    // Empty object carrying the collider + XR interactable for a hold interaction.
    static T Grab<T>(string name, Transform parent, Vector3 pos, Vector3 colliderSize) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider>();
        col.size = colliderSize;
        go.AddComponent<XRSimpleInteractable>();
        return go.AddComponent<T>();
    }

    static GameObject Clone(Transform source, string name, Transform parent, Vector3 pos)
    {
        var go = Object.Instantiate(source.gameObject, parent);
        go.name = name;
        go.transform.position = pos;
        return go;
    }

    // --------------------------------------------------------------- v1 fixes

    static void FixV1Materials(Transform station)
    {
        // v1 bug: these materials had emission compiled out, so the gold
        // "interact with this" pulse written to _EmissionColor never showed
        // on the artifact, the recorder or the terminal. Only the doors glowed.
        foreach (var n in new[] { "Mat_Artifact", "Mat_Recorder" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + n + ".mat");
            if (m != null) EnableEmission(m, Color.black);
        }
        var strip = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_StripLight.mat");
        if (strip != null) EnableEmission(strip, Color.white * 1.4f);

        // The console and monitor use materials embedded in their FBX, which
        // cannot be edited: give them emissive copies.
        var console = station.Find("ControlRoom/Console");
        if (console != null) SwapSlot(console.GetComponent<Renderer>(), "metalDark", "Mat_V2_ConsoleBody", Color.black);
        var monitor = station.Find("ControlRoom/Monitor");
        if (monitor != null) SwapSlot(monitor.GetComponent<Renderer>(), "dark", "Mat_V2_MonitorScreen", Color.black);
    }

    static void SwapSlot(Renderer r, string slotName, string newName, Color emission)
    {
        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            if (mats[i].name != slotName && mats[i].name != newName) continue;
            var copy = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + newName + ".mat");
            if (copy == null)
            {
                copy = new Material(mats[i]);
                AssetDatabase.CreateAsset(copy, MatDir + "/" + newName + ".mat");
            }
            if (newName.Contains("Screen")) copy.color = new Color(0.03f, 0.05f, 0.07f);
            EnableEmission(copy, emission);
            mats[i] = copy;
        }
        r.sharedMaterials = mats;
        EditorUtility.SetDirty(r);
    }

    // ---------------------------------------------------------- Crew Quarters

    static void BuildQuarters(Transform station, StoryDirector director)
    {
        var q = Group("CrewQuarters", root, Vector3.zero);

        // Cut a doorway into the corridor's left wall.
        var oldWall = station.Find("Corridor/Wall_Left");
        if (oldWall != null) oldWall.gameObject.SetActive(false);
        Wall("CorridorWall_A", q, new Vector3(-1.5f, 2f, 11.15f), new Vector3(0.2f, 4f, 2.3f));
        Wall("CorridorWall_B", q, new Vector3(-1.5f, 2f, 14.85f), new Vector3(0.2f, 4f, 2.3f));
        Wall("CorridorWall_Lintel", q, new Vector3(-1.5f, 3.25f, 13f), new Vector3(0.2f, 1.5f, 1.4f));

        Box("Floor", q, new Vector3(-4.55f, -0.1f, 13f), new Vector3(6.1f, 0.2f, 6f), Tiled(floorSrc, 6f, 6f));
        Box("Ceiling", q, new Vector3(-4.55f, 3.1f, 13f), new Vector3(6.1f, 0.2f, 6f), Tiled(ceilSrc, 6f, 6f));
        Wall("Wall_West", q, new Vector3(-7.6f, 1.5f, 13f), new Vector3(0.2f, 3f, 6f));
        Wall("Wall_South", q, new Vector3(-4.55f, 1.5f, 10.2f), new Vector3(6.1f, 3f, 0.2f));
        Wall("Wall_North", q, new Vector3(-4.55f, 1.5f, 15.8f), new Vector3(6.1f, 3f, 0.2f));

        // Powered sliding door: shut until the breaker is thrown.
        var door = Group("QuartersDoor", q, new Vector3(-1.66f, 1.25f, 13f));
        Box("Panel", door, new Vector3(-1.66f, 1.25f, 13f), new Vector3(0.08f, 2.5f, 1.5f), mDoor);
        Box("Stripe", door, new Vector3(-1.615f, 1.25f, 13f), new Vector3(0.01f, 0.12f, 1.5f), mHazard, false);
        var lamp = Box("QuartersDoorLamp", q, new Vector3(-1.38f, 2.62f, 13f), new Vector3(0.04f, 0.08f, 0.5f), mLamp, false);
        Sign("Sign_Quarters", q, new Vector3(-1.39f, 2.9f, 13f), Vector3.left, "CREW QUARTERS", 44f, new Color(0.75f, 0.85f, 0.95f));
        director.quartersDoor = door;
        director.quartersDoorLamp = lamp.GetComponent<Renderer>();

        PointLight("Light_Quarters", q, new Vector3(-4.5f, 2.55f, 13f), new Color(1f, 0.86f, 0.7f), 0.9f, 8f);
        Box("StripLight_Quarters", q, new Vector3(-4.5f, 2.97f, 13f), new Vector3(0.3f, 0.06f, 1.6f),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_StripLight.mat"), false);

        // Furniture: the bunk that used to sit oddly in the control room
        // moves here, plus copies of v1's own kit pieces to keep one look.
        var bunk = station.Find("ControlRoom/BunkBed_ControlRoom");
        if (bunk != null)
        {
            bunk.position = new Vector3(-7.41f, 0f, 10.4f);
            Clone(bunk, "BunkBed_2", q, new Vector3(-7.41f, 0f, 13.6f));
        }
        var desk = station.Find("ControlRoom/Desk");
        if (desk != null) Clone(desk, "Desk_Quarters", q, new Vector3(-2.92f, 0f, 15.75f));
        var shelf = station.Find("Lab/Shelf1");
        if (shelf != null) Clone(shelf, "Shelf_Quarters", q, new Vector3(-2.21f, 0f, 11.54f));
        var poster = station.Find("ControlRoom/Poster_Patch");
        if (poster != null)
        {
            var p = Clone(poster, "Poster_Quarters", q, new Vector3(-4.6f, 1.75f, 15.68f));
            p.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        // Crew-band relay: wall panel with a big tuning dial, straight ahead
        // of the door.
        var relay = Group("CrewBandRelay", q, new Vector3(-7.45f, 1.35f, 13f));
        Box("Backing", relay, new Vector3(-7.46f, 1.35f, 13f), new Vector3(0.08f, 0.8f, 1.0f), mMetal);
        Box("Frame", relay, new Vector3(-7.47f, 1.35f, 13f), new Vector3(0.06f, 0.9f, 1.1f), mHazard, false);
        Box("Speaker", relay, new Vector3(-7.41f, 1.3f, 13.28f), new Vector3(0.02f, 0.3f, 0.3f), mDoor, false);
        var readout = Sign("Readout", relay, new Vector3(-7.405f, 1.62f, 13f), Vector3.left, "140.0 kHz\nSCANNING", 30f, new Color(0.5f, 1f, 0.75f));
        Sign("Sign_Relay", relay, new Vector3(-7.49f, 2.0f, 13f), Vector3.left, "CREW BAND RELAY", 44f, new Color(0.75f, 0.85f, 0.95f));

        var dial = Grab<TuneDial>("RelayDial", relay, new Vector3(-7.36f, 1.25f, 12.82f), new Vector3(0.2f, 0.45f, 0.45f));
        var knob = Prim(PrimitiveType.Cylinder, "Knob", dial.transform, new Vector3(-7.38f, 1.25f, 12.82f), new Vector3(0.32f, 0.035f, 0.32f), mInteract, false, new Vector3(0f, 0f, 90f));
        var notch = Box("Notch", knob.transform, Vector3.zero, Vector3.one, mHazard, false);
        notch.transform.localPosition = new Vector3(0f, 1.05f, 0.3f);
        notch.transform.localRotation = Quaternion.identity;
        notch.transform.localScale = new Vector3(0.12f, 1f, 0.36f);
        dial.stepId = "radio";
        dial.knob = knob.transform;
        dial.localSpinAxis = Vector3.right;
        dial.readout = readout;
        dial.glowRenderers = new[] { knob.GetComponent<Renderer>() };
        dial.hoverHint = "GRAB, THEN MOVE IN A CIRCLE";
        dial.holdHint = "KEEP CIRCLING TO SCAN";
        dial.hintHeight = 0.55f;
        dial.staticSource = Source(dial.gameObject, "RadioStatic", 9f);
        dial.voiceSource = Source(dial.gameObject, "RadioVoices", 9f);
        UnityEventTools.AddPersistentListener(dial.onCompleted, director.OnRadioTuned);
    }

    // ------------------------------------------------------------ Engineering

    static void BuildEngineering(Transform station, StoryDirector director)
    {
        var e = Group("Engineering", root, Vector3.zero);

        var oldWall = station.Find("ControlRoom/Wall_Right");
        if (oldWall != null) oldWall.gameObject.SetActive(false);
        Wall("ControlWall_A", e, new Vector3(4f, 2f, 16.5f), new Vector3(0.2f, 4f, 1f));
        Wall("ControlWall_B", e, new Vector3(4f, 2f, 21.3f), new Vector3(0.2f, 4f, 5.4f));
        Wall("ControlWall_Lintel", e, new Vector3(4f, 3.25f, 17.8f), new Vector3(0.2f, 1.5f, 1.6f));

        Box("Floor", e, new Vector3(7.3f, -0.1f, 20f), new Vector3(6.6f, 0.2f, 7.4f), Tiled(floorSrc, 7f, 7f));
        Box("Ceiling", e, new Vector3(7.3f, 4.1f, 20f), new Vector3(6.6f, 0.2f, 7.4f), Tiled(ceilSrc, 7f, 7f));
        Wall("Wall_East", e, new Vector3(10.6f, 2f, 20f), new Vector3(0.2f, 4f, 7.4f));
        Wall("Wall_South", e, new Vector3(7.3f, 2f, 16.3f), new Vector3(6.6f, 4f, 0.2f));
        Wall("Wall_North", e, new Vector3(7.3f, 2f, 23.7f), new Vector3(6.6f, 4f, 0.2f));
        Sign("Sign_Engineering", e, new Vector3(3.89f, 2.9f, 17.8f), Vector3.right, "ENGINEERING", 44f, new Color(0.75f, 0.85f, 0.95f));

        PointLight("Light_Engineering", e, new Vector3(7.2f, 3.3f, 20f), new Color(0.85f, 0.92f, 1f), 1.15f, 10f);
        Box("StripLight_Engineering", e, new Vector3(7.2f, 3.97f, 20f), new Vector3(0.3f, 0.06f, 2f),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_StripLight.mat"), false);

        // Manual sliding hatch.
        var hatch = Group("EngineeringHatch", e, new Vector3(4.17f, 1.25f, 17.8f));
        Box("Panel", hatch, new Vector3(4.17f, 1.25f, 17.8f), new Vector3(0.1f, 2.5f, 1.7f), mDoor);
        Box("Stripe", hatch, new Vector3(4.115f, 0.45f, 17.8f), new Vector3(0.01f, 0.12f, 1.7f), mHazard, false);
        var slide = Grab<SlideHatch>("HatchHandle", hatch, new Vector3(4.03f, 1.2f, 17.3f), new Vector3(0.25f, 0.8f, 0.35f));
        var handle = Box("HandleBar", slide.transform, new Vector3(4.05f, 1.2f, 17.3f), new Vector3(0.07f, 0.55f, 0.08f), mInteract, false);
        slide.stepId = "enghatch";
        slide.door = hatch;
        slide.slideLocalDir = Vector3.forward;
        slide.distance = 1.25f;
        slide.glowRenderers = new[] { handle.GetComponent<Renderer>() };
        slide.hoverHint = "GRAB, THEN DRAG SIDEWAYS";
        slide.holdHint = "KEEP DRAGGING";
        slide.hintHeight = 0.6f;
        slide.completeClip = "HatchSlide";
        UnityEventTools.AddPersistentListener(slide.onCompleted, director.OnHatchOpened);

        // Reactor.
        var reactor = Group("Reactor", e, new Vector3(9f, 0f, 20f));
        Prim(PrimitiveType.Cylinder, "Base", reactor, new Vector3(9f, 0.1f, 20f), new Vector3(2.1f, 0.1f, 2.1f), mMetal, false);
        var body = Prim(PrimitiveType.Cylinder, "Body", reactor, new Vector3(9f, 1.4f, 20f), new Vector3(1.6f, 1.3f, 1.6f), mMetal, true);
        body.GetComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
        Prim(PrimitiveType.Cylinder, "Stack", reactor, new Vector3(9f, 3.35f, 20f), new Vector3(0.45f, 0.75f, 0.45f), mMetal, false);
        var glow = new List<Renderer>();
        foreach (float y in new[] { 0.75f, 1.4f, 2.05f })
            glow.Add(Prim(PrimitiveType.Cylinder, "GlowBand", reactor, new Vector3(9f, y, 20f), new Vector3(1.66f, 0.035f, 1.66f), mGlow, false).GetComponent<Renderer>());
        director.reactorGlow = glow.ToArray();
        director.reactorHum = Source(reactor.gameObject, "ReactorHum", 14f);

        Box("SocketHousing", reactor, new Vector3(8.12f, 1.1f, 20f), new Vector3(0.3f, 0.55f, 0.55f), mMetal);
        var socketFrame = Box("SocketFrame", reactor, new Vector3(7.96f, 1.1f, 20f), new Vector3(0.03f, 0.42f, 0.42f), mGlow, false);
        var socket = Group("SocketPoint", reactor, new Vector3(7.86f, 1.1f, 20f), new Vector3(0f, 0f, 90f));
        Sign("Sign_Socket", reactor, new Vector3(7.94f, 1.62f, 20f), Vector3.right, "CELL SOCKET", 36f, new Color(1f, 0.82f, 0.3f));

        // Cell rack, just inside the door.
        var rack = Group("CellRack", e, new Vector3(6.6f, 1.2f, 16.45f));
        Box("Board", rack, new Vector3(6.6f, 1.2f, 16.45f), new Vector3(1.4f, 0.9f, 0.1f), mMetal);
        Box("Shelf", rack, new Vector3(6.6f, 0.93f, 16.6f), new Vector3(1.4f, 0.05f, 0.3f), mHazard);
        Sign("Sign_Rack", rack, new Vector3(6.6f, 1.85f, 16.41f), Vector3.back, "POWER CELLS", 44f, new Color(0.75f, 0.85f, 0.95f));
        Prim(PrimitiveType.Cylinder, "DeadCell_1", rack, new Vector3(6.15f, 1.18f, 16.62f), new Vector3(0.17f, 0.22f, 0.17f), mCellDead, false);
        Prim(PrimitiveType.Cylinder, "DeadCell_2", rack, new Vector3(7.05f, 1.18f, 16.62f), new Vector3(0.17f, 0.22f, 0.17f), mCellDead, false);
        var cell = Grab<CarryCell>("PowerCell", rack, new Vector3(6.6f, 1.18f, 16.62f), new Vector3(0.3f, 0.55f, 0.3f));
        var cellBody = Prim(PrimitiveType.Cylinder, "CellBody", cell.transform, new Vector3(6.6f, 1.18f, 16.62f), new Vector3(0.17f, 0.22f, 0.17f), mCell, false);
        Prim(PrimitiveType.Cylinder, "CellCap", cell.transform, new Vector3(6.6f, 1.41f, 16.62f), new Vector3(0.19f, 0.02f, 0.19f), mMetal, false);
        cell.stepId = "powercell";
        cell.socket = socket;
        cell.socketGlow = socketFrame.GetComponent<Renderer>();
        cell.glowRenderers = new[] { cellBody.GetComponent<Renderer>() };
        cell.hoverHint = "GRAB AND HOLD";
        cell.holdHint = "CARRY IT TO THE REACTOR";
        cell.hintHeight = 0.4f;
        cell.completeClip = "CellSeat";
        UnityEventTools.AddPersistentListener(cell.onCompleted, director.OnCellSeated);

        // Main bus breaker on the far wall.
        var breaker = Group("Breaker", e, new Vector3(7f, 1.4f, 23.55f));
        Box("Frame", breaker, new Vector3(7f, 1.4f, 23.57f), new Vector3(0.8f, 1.1f, 0.06f), mHazard, false);
        Box("Panel", breaker, new Vector3(7f, 1.4f, 23.54f), new Vector3(0.6f, 0.9f, 0.08f), mMetal);
        Sign("Sign_Breaker", breaker, new Vector3(7f, 2.15f, 23.58f), Vector3.forward, "MAIN BUS BREAKER", 44f, new Color(1f, 0.82f, 0.3f));
        var lever = BuildLever(breaker, "BreakerLever", new Vector3(7f, 1.5f, 23.48f), Vector3.back);
        lever.stepId = "breaker";
        lever.hoverHint = "GRAB, THEN PULL DOWN";
        lever.holdHint = "PULL DOWN";
        lever.completeClip = "LeverClunk";
        UnityEventTools.AddPersistentListener(lever.onCompleted, director.OnBreakerPulled);

        // Dressing.
        Prim(PrimitiveType.Cylinder, "Pipe_1", e, new Vector3(10.35f, 3.2f, 20f), new Vector3(0.28f, 3.6f, 0.28f), mMetal, false, new Vector3(90f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, "Pipe_2", e, new Vector3(10.4f, 2.75f, 20f), new Vector3(0.14f, 3.6f, 0.14f), mHazard, false, new Vector3(90f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, "Pipe_3", e, new Vector3(9.7f, 3.35f, 20f), new Vector3(0.2f, 0.75f, 0.2f), mMetal, false, new Vector3(0f, 0f, 90f));
        Box("Crate_1", e, new Vector3(8.4f, 0.3f, 23.1f), new Vector3(0.6f, 0.6f, 0.6f), AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Crate.mat"));
        Box("Crate_2", e, new Vector3(9.15f, 0.25f, 23.2f), new Vector3(0.5f, 0.5f, 0.5f), AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Crate.mat"));
        var barrel = station.Find("DockingBay/Barrel1");
        if (barrel != null)
        {
            Clone(barrel, "Barrel_E1", e, new Vector3(10f, 0f, 17f));
            Clone(barrel, "Barrel_E2", e, new Vector3(9.4f, 0f, 16.9f));
            Clone(barrel, "Barrel_E3", e, new Vector3(10.05f, 0f, 17.65f));
        }
        var machine = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/SpaceKit/machine_wireless.fbx");
        if (machine != null)
        {
            var m = (GameObject)PrefabUtility.InstantiatePrefab(machine, e);
            m.name = "Machine_Wireless";
            m.transform.position = new Vector3(5.2f, 0f, 22.8f);
            m.transform.localScale = Vector3.one * 2.2f;
        }
    }

    // A wall lever. `outDir` points out of the wall, toward the player.
    static PullLever BuildLever(Transform parent, string name, Vector3 pivotPos, Vector3 outDir)
    {
        var pivot = Group(name + "Pivot", parent, pivotPos);
        pivot.rotation = Quaternion.LookRotation(outDir, Vector3.up) * Quaternion.Euler(-45f, 0f, 0f);
        var arm = Box("Arm", pivot, Vector3.zero, Vector3.one, mMetal, false);
        arm.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        arm.transform.localRotation = Quaternion.identity;
        arm.transform.localScale = new Vector3(0.05f, 0.05f, 0.4f);
        var knob = Prim(PrimitiveType.Sphere, "Knob", pivot, Vector3.zero, Vector3.one, mInteract, false);
        knob.transform.localPosition = new Vector3(0f, 0f, 0.42f);
        knob.transform.localScale = Vector3.one * 0.13f;
        var lever = Grab<PullLever>(name, parent, pivotPos + outDir * 0.22f + Vector3.up * 0.05f, new Vector3(0.4f, 0.7f, 0.45f));
        lever.pivot = pivot;
        lever.localAxis = Vector3.right;
        lever.maxAngle = 90f;
        lever.glowRenderers = new[] { knob.GetComponent<Renderer>(), arm.GetComponent<Renderer>() };
        lever.hintHeight = 0.55f;
        // The arm shares the metal material; give it the emissive one so both glow.
        arm.GetComponent<Renderer>().sharedMaterial = mInteract;
        return lever;
    }

    // -------------------------------------------------------------------- Lab

    static void BuildLab(Transform station, StoryDirector director)
    {
        var l = Group("LabAdditions", root, Vector3.zero);
        var artifact = station.GetComponentInChildren<ArtifactInteractable>(true);
        director.artifact = artifact;

        // The other ending: a containment lever just inside the lab door.
        var seal = Group("ContainmentSeal", l, new Vector3(2.4f, 1.4f, 24.14f));
        Box("Frame", seal, new Vector3(2.4f, 1.4f, 24.12f), new Vector3(0.8f, 1.1f, 0.06f), mHazard, false);
        Box("Panel", seal, new Vector3(2.4f, 1.4f, 24.15f), new Vector3(0.6f, 0.9f, 0.08f), mMetal);
        Sign("Sign_Seal", seal, new Vector3(2.4f, 2.15f, 24.11f), Vector3.back, "CONTAINMENT SEAL", 44f, new Color(1f, 0.35f, 0.25f));
        Sign("Sign_Seal2", seal, new Vector3(2.4f, 0.78f, 24.11f), Vector3.back, "CUTS LAB POWER", 30f, new Color(1f, 0.82f, 0.3f));
        var lever = BuildLever(seal, "ContainmentLever", new Vector3(2.4f, 1.5f, 24.21f), Vector3.forward);
        lever.stepId = "artifact";
        lever.hoverHint = "GRAB, THEN PULL DOWN TO SEAL THE LAB";
        lever.holdHint = "PULL DOWN";
        lever.completeClip = "LeverClunk";
        if (artifact != null) UnityEventTools.AddPersistentListener(lever.onCompleted, artifact.SealInstead);

        Box("StripLight_Lab", l, new Vector3(0f, 3.97f, 28f), new Vector3(0.3f, 0.06f, 2f),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_StripLight.mat"), false);
    }

    // ------------------------------------------------------------- Crew logs

    static void BuildLogs()
    {
        var g = Group("CrewLogs", root, Vector3.zero);
        Log(g, "tanaka", new Vector3(-1.05f, 0.66f, 5.2f), 25f, "TANAKA, PILOT. DAY 176",
            "\"Both shuttles are still clamped and they're staying that way. The Director has grounded every departure until after the test. Says nobody leaves before we hear it answer.\"");
        Log(g, "okafor", new Vector3(-1.38f, 0.78f, 19.42f), -15f, "DR. OKAFOR, PHYSICIST. DAY 179",
            "\"It isn't emitting. It's replying. Every pulse we send comes back changed, with our own voices folded into it. Words we haven't said yet.\"",
            "\"It responds to attention. The more of us in the room, the stronger it gets. He wants all five.\"");
        Log(g, "reyes", new Vector3(8.4f, 0.62f, 23.1f), 160f, "REYES, ENGINEER. DAY 180",
            "\"He had me wire the main bus straight into the lab. So I pulled the reactor cell and hid it on the rack with the dead ones. No cell, no test.\"",
            "\"If you're reading this and the lights are on, somebody put it back. Pull the breaker. Seal the lab. Don't let it finish.\"");
        Log(g, "lindqvist", new Vector3(-3.6f, 0.78f, 15.35f), 10f, "LINDQVIST, MEDIC. DAY 178",
            "\"All five of us are reporting the same dream. Standing in the dark, stars turning, someone breathing right beside you. Five people do not have the same dream.\"");
        Log(g, "halvorsen", new Vector3(0.95f, 0.03f, 30.1f), 30f, "DIRECTOR HALVORSEN. DAY 181",
            "\"Reyes tried to stop it. It didn't matter. It asked for all of us and we came. We are not gone. We are kept.\"",
            "\"If it is quiet when you find this, it is only waiting for someone to listen. It will ask for you by name.\"");
    }

    static void Log(Transform parent, string id, Vector3 pos, float yaw, string header, params string[] pages)
    {
        var go = new GameObject("CrewLog_" + id);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.08f, 0f);
        col.size = new Vector3(0.36f, 0.22f, 0.42f);
        go.AddComponent<XRSimpleInteractable>();
        var body = Box("Body", go.transform, pos, new Vector3(0.2f, 0.016f, 0.28f), mPad, false);
        body.transform.localRotation = Quaternion.identity;
        var screen = Box("Screen", go.transform, pos + Vector3.up * 0.01f, new Vector3(0.17f, 0.004f, 0.24f), mScreen, false);
        screen.transform.localRotation = Quaternion.identity;
        var logComp = go.AddComponent<CrewLog>();
        logComp.logId = id;
        logComp.header = header;
        logComp.pages = pages;
        logComp.screenRenderer = screen.GetComponent<Renderer>();
    }

    // ------------------------------------------------------------ Atmosphere

    static void BuildAtmosphere(Transform station)
    {
        var a = Group("Atmosphere", root, Vector3.zero);
        var stripMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_StripLight.mat");
        // A visible fixture under each v1 room light, so the light has a source.
        Box("StripLight_DockingBay", a, new Vector3(0f, 3.97f, 5f), new Vector3(0.3f, 0.06f, 2.4f), stripMat, false);
        Box("StripLight_ControlRoom", a, new Vector3(0f, 3.97f, 20f), new Vector3(0.3f, 0.06f, 2f), stripMat, false);

        // Dust in the rooms that had none.
        var dust = GameObject.Find("DustMotes_Corridor");
        if (dust != null)
        {
            Clone(dust.transform, "DustMotes_Quarters", a, new Vector3(-4.5f, 1.5f, 13f));
            Clone(dust.transform, "DustMotes_Engineering", a, new Vector3(7.2f, 1.7f, 20f));
            Clone(dust.transform, "DustMotes_ControlRoom", a, new Vector3(0f, 1.7f, 20f));
            Clone(dust.transform, "DustMotes_Lab", a, new Vector3(0f, 1.7f, 28f));
        }

        // Darker base, fog that thickens with progress (LightingDirector).
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.016f, 0.02f, 0.03f);
        RenderSettings.fogDensity = 0.02f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.07f);

        // The 0.05-intensity directional light was still paying for a soft
        // shadow pass on the headset.
        var sun = GameObject.Find("Directional Light");
        if (sun != null) sun.GetComponent<Light>().shadows = LightShadows.None;
    }

    static void BuildSoundPoints(StoryDirector director)
    {
        var s = Group("SoundPoints", root, Vector3.zero);
        director.dockingBayPoint = Group("Snd_DockingBay", s, new Vector3(-2f, 1.5f, 3f));
        director.corridorPoint = Group("Snd_Corridor", s, new Vector3(0f, 1.5f, 13f));
        director.controlRoomPoint = Group("Snd_ControlRoom", s, new Vector3(0f, 3.2f, 20f));
        director.labDoorPoint = Group("Snd_LabDoor", s, new Vector3(0f, 1.5f, 24f));
        var monitor = GameObject.Find("Station").transform.Find("ControlRoom/Monitor");
        if (monitor != null) director.monitorRenderer = monitor.GetComponent<Renderer>();
    }

    // --------------------------------------------------------------- Artifact

    static void BuildArtifact(Transform station)
    {
        var artifact = station.Find("Lab/Artifact");
        if (artifact == null) { log.AppendLine("Artifact not found"); return; }

        string meshPath = ArtDir + "/ArtifactShard.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null) { mesh = new Mesh { name = "ArtifactShard" }; AssetDatabase.CreateAsset(mesh, meshPath); }
        FillShardMesh(mesh);
        artifact.GetComponent<MeshFilter>().sharedMesh = mesh;

        string ringPath = ArtDir + "/ArtifactRing.asset";
        var ring = AssetDatabase.LoadAssetAtPath<Mesh>(ringPath);
        if (ring == null) { ring = new Mesh { name = "ArtifactRing" }; AssetDatabase.CreateAsset(ring, ringPath); }
        FillTorusMesh(ring, 1f, 0.022f, 40, 6);

        if (artifact.GetComponent<SlowSpin>() == null) artifact.gameObject.AddComponent<SlowSpin>();
        artifact.GetComponent<SlowSpin>().degreesPerSecond = new Vector3(0f, 14f, 5f);

        for (int i = artifact.childCount - 1; i >= 0; i--)
            if (artifact.GetChild(i).name.StartsWith("ArtifactRing")) Object.DestroyImmediate(artifact.GetChild(i).gameObject);
        MakeRing(artifact, ring, "ArtifactRing_1", 0.95f, new Vector3(70f, 0f, 0f), new Vector3(0f, 0f, 40f));
        MakeRing(artifact, ring, "ArtifactRing_2", 1.15f, new Vector3(20f, 0f, 60f), new Vector3(0f, 0f, -28f));
        EditorUtility.SetDirty(mesh);
        EditorUtility.SetDirty(ring);
    }

    static void MakeRing(Transform parent, Mesh mesh, string name, float scale, Vector3 euler, Vector3 spin)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mRing;
        go.AddComponent<SlowSpin>().degreesPerSecond = spin;
    }

    // Stellated icosahedron with uneven spikes, flat shaded: reads as a
    // grown crystal instead of the default sphere v1 used.
    static void FillShardMesh(Mesh mesh)
    {
        float p = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new[]
        {
            new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
            new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
            new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1)
        }.Select(x => x.normalized).ToArray();
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        var rng = new System.Random(181);
        var verts = new List<Vector3>();
        var tris = new List<int>();
        const float core = 0.26f;
        for (int i = 0; i < f.Length; i += 3)
        {
            Vector3 a = v[f[i]] * core, b = v[f[i + 1]] * core, c = v[f[i + 2]] * core;
            Vector3 centroid = (a + b + c) / 3f;
            float len = 0.36f + (float)rng.NextDouble() * 0.3f;
            Vector3 apex = centroid.normalized * len;
            AddTri(verts, tris, a, b, apex);
            AddTri(verts, tris, b, c, apex);
            AddTri(verts, tris, c, a, apex);
        }
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, (a + b + c) / 3f) < 0f) { var t = b; b = c; c = t; }
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    static void FillTorusMesh(Mesh mesh, float major, float minor, int segments, int sides)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int s = 0; s <= segments; s++)
        {
            float a = s / (float)segments * Mathf.PI * 2f;
            Vector3 centre = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * major;
            for (int t = 0; t <= sides; t++)
            {
                float b = t / (float)sides * Mathf.PI * 2f;
                verts.Add(centre + (centre.normalized * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b)) * minor);
            }
        }
        int ring = sides + 1;
        for (int s = 0; s < segments; s++)
            for (int t = 0; t < sides; t++)
            {
                int i = s * ring + t;
                tris.Add(i); tris.Add(i + 1); tris.Add(i + ring);
                tris.Add(i + 1); tris.Add(i + ring + 1); tris.Add(i + ring);
            }
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // --------------------------------------------------------------------- UI

    static RectTransform FindUI(string name)
    {
        foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
            if (rt.name == name && rt.gameObject.scene.IsValid()) return rt;
        return null;
    }

    static void Anchors(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        EditorUtility.SetDirty(rt);
    }

    static void AutoSize(RectTransform rt, float min, float max)
    {
        var t = rt.GetComponent<TMP_Text>();
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.overflowMode = TextOverflowModes.Truncate;
        EditorUtility.SetDirty(t);
    }

    static void BuildUI()
    {
        // Dialogue: the body used to overflow out of the bottom of its bar.
        var bar = FindUI("DialogueBar");
        if (bar != null)
        {
            Anchors(bar, 0.1f, 0.03f, 0.9f, 0.31f);
            Anchors(FindUI("SpeakerText"), 0.03f, 0.76f, 0.97f, 0.97f);
            var body = FindUI("BodyText");
            Anchors(body, 0.03f, 0.07f, 0.97f, 0.74f);
            AutoSize(body, 26f, 44f);
            var speaker = FindUI("SpeakerText").GetComponent<TMP_Text>();
            speaker.enableAutoSizing = true; speaker.fontSizeMin = 24f; speaker.fontSizeMax = 44f;
        }

        var info = FindUI("InfoPanel");
        if (info != null)
        {
            Anchors(info, 0.2f, 0.36f, 0.8f, 0.9f);
            AutoSize(FindUI("InfoBody"), 22f, 42f);
        }

        var endBody = FindUI("EndBody");
        if (endBody != null)
        {
            Anchors(endBody, 0.12f, 0.37f, 0.88f, 0.64f);
            AutoSize(endBody, 30f, 46f);
            endBody.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        }
        var endTitle = FindUI("EndTitle");
        if (endTitle != null) Anchors(endTitle, 0.1f, 0.65f, 0.9f, 0.78f);

        // Start screen: comfort toggles either side of BEGIN, and a controls line.
        var begin = FindUI("BeginButton");
        if (begin != null)
        {
            foreach (var n in new[] { "TurnToggle", "VignetteToggle", "ControlsHint" })
            {
                var existing = FindUI(n);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
            }
            MakeToggle(begin, "TurnToggle", ComfortToggleButton.Option.TurnMode, 0.05f, 0.28f);
            MakeToggle(begin, "VignetteToggle", ComfortToggleButton.Option.Vignette, 0.72f, 0.95f);

            var sub = FindUI("TerminalSub");
            var hintGO = Object.Instantiate(sub.gameObject, sub.parent);
            hintGO.name = "ControlsHint";
            var hint = hintGO.GetComponent<TMP_Text>();
            hint.text = "TRIGGER: select and grab    LEFT STICK: move    RIGHT STICK: turn\nGOLD GLOW = your next job    BLUE GLOW = optional crew log";
            hint.fontSize = 27f;
            hint.color = new Color(0.6f, 0.95f, 1f);
            Anchors(hintGO.GetComponent<RectTransform>(), 0.04f, 0.545f, 0.96f, 0.612f);
            var keyboard = FindUI("OnScreenKeyboard");
            if (keyboard != null) Anchors(keyboard, 0.06f, 0.03f, 0.94f, 0.535f);
        }
    }

    static void MakeToggle(RectTransform begin, string name, ComfortToggleButton.Option option, float x0, float x1)
    {
        var go = Object.Instantiate(begin.gameObject, begin.parent);
        go.name = name;
        Anchors(go.GetComponent<RectTransform>(), x0, begin.anchorMin.y, x1, begin.anchorMax.y);
        var label = go.GetComponentInChildren<TMP_Text>(true);
        label.fontSize = 26f;
        label.name = name + "_Label";
        var toggle = go.AddComponent<ComfortToggleButton>();
        toggle.option = option;
        toggle.label = label;
        var image = go.GetComponent<Image>();
        if (image != null) image.color = new Color(image.color.r * 0.6f, image.color.g * 0.6f, image.color.b * 0.6f, image.color.a);
    }

    // Optional comfort vignette during smooth movement and turning. Off by
    // default; toggled from the start screen.
    static void BuildVignette()
    {
        var rig = GameObject.Find("XR Origin (XR Rig)");
        if (rig == null) { log.AppendLine("XR rig not active/found: vignette skipped"); return; }
        var cam = rig.transform.Find("Camera Offset/Main Camera");
        var old = cam.Find("TunnelingVignette");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.6.0/Starter Assets/TunnelingVignette/TunnelingVignette.prefab");
        if (prefab == null) { log.AppendLine("vignette prefab missing"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cam);
        go.name = "TunnelingVignette";
        var controller = go.GetComponents<MonoBehaviour>().First(c => c.GetType().Name == "TunnelingVignetteController");
        var so = new SerializedObject(controller);
        var list = so.FindProperty("m_LocomotionVignetteProviders");
        var providers = new List<Object>();
        foreach (var mb in rig.transform.Find("Locomotion").GetComponentsInChildren<MonoBehaviour>(true))
        {
            string n = mb.GetType().Name;
            if (n == "DynamicMoveProvider" || n == "ContinuousTurnProvider") providers.Add(mb);
        }
        list.arraySize = providers.Count;
        for (int i = 0; i < providers.Count; i++)
        {
            var el = list.GetArrayElementAtIndex(i);
            var lp = el.FindPropertyRelative("m_LocomotionProvider");
            var en = el.FindPropertyRelative("m_Enabled");
            if (lp != null) lp.objectReferenceValue = providers[i];
            if (en != null) en.boolValue = true;
            log.AppendLine("vignette provider " + providers[i].GetType().Name + " lp=" + (lp != null) + " en=" + (en != null));
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(false);
    }

    // ---------------------------------------------------------------- Cleanup

    static void Cleanup(Transform station)
    {
        // Leftovers from the cut keycard/keypad puzzle were still visible
        // (and re-activated after the ending). The locker stays as dressing.
        var artifact = station.GetComponentInChildren<ArtifactInteractable>(true);
        var remove = new[] { "Keycard", "KeycardSocket", "KeypadLock" };
        foreach (var n in remove)
        {
            var go = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == n && g.scene.IsValid() && g.transform.parent == null);
            if (go != null) go.SetActive(false);
        }
        if (artifact != null && artifact.otherObjectsToHide != null)
        {
            artifact.otherObjectsToHide = artifact.otherObjectsToHide.Where(o => o != null && !remove.Contains(o.name)).ToArray();
            EditorUtility.SetDirty(artifact);
        }

        // These sat at the scene root, so they hung in open space on the start
        // screen while the station was hidden. Under Station they hide with it.
        var lab = station.Find("Lab");
        foreach (var n in new[] { "SupplyLocker", "LockerDoorHinge", "DustMotes_Corridor", "DustMotes_DockingBay" })
        {
            var go = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == n && g.scene.IsValid() && g.transform.parent == null);
            if (go != null && lab != null) go.transform.SetParent(n.StartsWith("Dust") ? station : lab, true);
        }

        var zone = station.GetComponentInChildren<RoomEntryZone>(true);
        if (zone != null)
        {
            zone.echoLine = "Nothing broken back here. Nothing burned either. It's like the whole place just stopped. Side door's dead without power. Keep going.";
            EditorUtility.SetDirty(zone);
        }
    }
}
#endif

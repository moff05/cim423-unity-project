using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Owns the station's light levels. v1 only ever changed ambience volume as the
// player progressed; this makes the ramp physical: each story phase has its
// own power state, the fog thickens and the lights sag and flicker the deeper
// the player goes.
public class LightingDirector : MonoBehaviour
{
    public static LightingDirector Instance { get; private set; }

    public enum Mode
    {
        Emergency,  // on arrival: reactor is cold, dim amber trickle
        Powered,    // breaker thrown: full light
        Blackout,   // after the relay: everything dies, suit lamp only
        LabOnly     // lab door open: only the lab is lit, and badly
    }

    public Mode mode = Mode.Emergency;
    // Set while the artifact ending drives the lab lights itself.
    public bool Suspended;

    class Entry
    {
        public Light light;
        public float baseIntensity;
        public Color baseColor;
        public float seed;
        public bool lab;
        public bool engineering;
        public float current;
        public float flashUntil;
    }

    class Strip
    {
        public Renderer renderer;
        public Entry owner;
        // The strip's own colour (each room has its own accent).
        public Color baseEmission;
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly List<Strip> strips = new List<Strip>();
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly Color Amber = new Color(1f, 0.66f, 0.38f);
    static readonly Color AlarmRed = new Color(1f, 0.25f, 0.18f);
    MaterialPropertyBlock mpb;
    Light lamp;
    float lampTarget;
    bool gathered;

    void Awake()
    {
        Instance = this;
        mpb = new MaterialPropertyBlock();
    }

    void Gather()
    {
        gathered = true;
        var station = GameObject.Find("Station");
        if (station == null) return;
        foreach (var l in station.GetComponentsInChildren<Light>(true))
        {
            entries.Add(new Entry
            {
                light = l,
                baseIntensity = l.intensity,
                baseColor = l.color,
                seed = Random.value * 100f,
                lab = l.name.StartsWith("Light_Lab"),
                engineering = l.name.Contains("Engineering"),
                current = l.intensity
            });
        }
        foreach (var r in station.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.StartsWith("StripLight")) continue;
            Entry best = null;
            float bestDist = float.MaxValue;
            foreach (var e in entries)
            {
                float d = (e.light.transform.position - r.transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            var mat = r.sharedMaterial;
            Color baseEmission = mat != null && mat.HasProperty(EmissionColor) ? mat.GetColor(EmissionColor) : Color.white;
            strips.Add(new Strip { renderer = r, owner = best, baseEmission = baseEmission });
        }
    }

    // Briefly force lights whose name contains `nameContains` to full, even
    // in a blackout. Used for the "something just flickered up ahead" beat.
    public void Flash(string nameContains, float seconds)
    {
        foreach (var e in entries)
            if (e.light != null && e.light.name.Contains(nameContains))
                e.flashUntil = Time.time + seconds;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gathered)
        {
            // Station is inactive until the intro ends; GameObject.Find only
            // sees active objects, so wait for it.
            if (gm == null || !gm.StoryStarted) return;
            Gather();
        }

        float progress = InteractionSequencer.Instance != null ? InteractionSequencer.Instance.Progress : 0f;

        UpdateLamp();
        if (Suspended) return;

        foreach (var e in entries)
        {
            if (e.light == null) continue;
            float factor;
            Color color = e.baseColor;
            float noise = Mathf.PerlinNoise(e.seed, Time.time * 2.2f);
            switch (mode)
            {
                case Mode.Emergency:
                    factor = (e.engineering ? 0.7f : 0.42f) * (0.85f + 0.3f * noise);
                    color = Color.Lerp(e.baseColor, e.engineering ? AlarmRed : Amber, e.engineering ? 0.85f : 0.55f);
                    break;
                case Mode.Powered:
                    // Mostly steady, with a rare sag that gets more common
                    // the further in the player is.
                    factor = 1f;
                    if (noise > Mathf.Lerp(0.93f, 0.78f, progress)) factor = 0.35f;
                    break;
                case Mode.Blackout:
                    factor = 0f;
                    break;
                default: // LabOnly
                    if (e.lab)
                    {
                        float fast = Mathf.PerlinNoise(e.seed, Time.time * 9f);
                        factor = fast > 0.72f ? 0.12f : 0.8f;
                    }
                    else factor = 0f;
                    break;
            }
            if (Time.time < e.flashUntil) { factor = 1.3f; color = e.baseColor; }

            float target = e.baseIntensity * factor;
            e.current = Mathf.Lerp(e.current, target, Time.deltaTime * 10f);
            e.light.intensity = e.current;
            e.light.color = Color.Lerp(e.light.color, color, Time.deltaTime * 4f);
        }

        foreach (var s in strips)
        {
            if (s.renderer == null || s.owner == null) continue;
            float k = s.owner.baseIntensity > 0.001f ? s.owner.current / s.owner.baseIntensity : 0f;
            mpb.SetColor(EmissionColor, s.baseEmission * k);
            s.renderer.SetPropertyBlock(mpb);
        }
    }

    void UpdateLamp()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (lamp == null)
        {
            var go = new GameObject("SuitLamp");
            lamp = go.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.spotAngle = 62f;
            lamp.innerSpotAngle = 30f;
            lamp.range = 11f;
            lamp.color = new Color(0.86f, 0.93f, 1f);
            lamp.shadows = LightShadows.None;
            lamp.intensity = 0f;
        }
        if (lamp.transform.parent != cam.transform)
        {
            lamp.transform.SetParent(cam.transform, false);
            lamp.transform.localPosition = new Vector3(0.12f, 0.08f, 0f);
            lamp.transform.localRotation = Quaternion.identity;
        }
        lampTarget = mode == Mode.Blackout ? 3.2f : mode == Mode.LabOnly ? 1.4f : 0f;
        if (Suspended) lampTarget = 0f;
        lamp.intensity = Mathf.Lerp(lamp.intensity, lampTarget, Time.deltaTime * 5f);
        lamp.enabled = lamp.intensity > 0.02f;
    }
}

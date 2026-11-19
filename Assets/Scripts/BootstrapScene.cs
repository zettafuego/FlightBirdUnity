// Flight Bird - Motion Control
// BootstrapScene.cs — Crea una escena jugable mínima en runtime si falta setup manual.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Arrastra este script a un GameObject vacío "Bootstrap" en una escena nueva.
/// Genera cámara, luz, pájaro, managers y UI básica para probar sin prefabs.
/// </summary>
public class BootstrapScene : MonoBehaviour
{
    [Tooltip("Si true, construye la escena al Start.")]
    public bool buildOnStart = true;

    void Start()
    {
        if (buildOnStart) Build();
    }

    [ContextMenu("Build Scene Now")]
    public void Build()
    {
        // Luz
        if (FindObjectOfType<Light>() == null)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.4f, 0.6f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.7f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.2f);
        }

        // Managers
        var managers = GameObject.Find("Managers") ?? new GameObject("Managers");
        Ensure<WebcamManager>(managers);
        Ensure<BodyPoseDetector>(managers);
        var gm = Ensure<GameManager>(managers);
        Ensure<FruitCollector>(managers);
        Ensure<ProceduralTerrain>(managers);

        // Pájaro
        var birdGo = GameObject.Find("Player_Bird");
        if (birdGo == null)
        {
            birdGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            birdGo.name = "Player_Bird";
            birdGo.tag = "Player";
            birdGo.transform.position = new Vector3(0, 20, 0);
            birdGo.transform.localScale = new Vector3(1.2f, 0.6f, 2f);
            var rb = birdGo.AddComponent<Rigidbody>();
            rb.useGravity = false;
            var bird = birdGo.AddComponent<BirdController>();
            var trail = birdGo.AddComponent<TrailRenderer>();
            trail.time = 0.6f;
            trail.startWidth = 0.4f;
            trail.endWidth = 0.05f;
            trail.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            trail.startColor = new Color(1f, 0.8f, 0.2f, 0.9f);
            trail.endColor = new Color(1f, 0.4f, 0f, 0f);
            var ps = birdGo.AddComponent<ParticleSystem>();
            bird.trail = trail;
            bird.wingParticles = ps;
            gm.bird = bird;
            gm.birdSpawn = birdGo.transform;
        }

        // Cámara
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
        }
        var follow = cam.GetComponent<CameraFollow>() ?? cam.gameObject.AddComponent<CameraFollow>();
        follow.target = birdGo.transform;
        follow.offset = new Vector3(0, 8, -18);
        cam.farClipPlane = 500f;
        cam.fieldOfView = 60f;

        // UI
        BuildUI(gm);

        // Tags útiles
        CreateTagIfEditor("Fruit");
        CreateTagIfEditor("Obstacle");
        CreateTagIfEditor("Terrain");

        Debug.Log("[BootstrapScene] Escena mínima lista. Asigna NNModel a BodyPoseDetector para ML.");
    }

    void BuildUI(GameManager gm)
    {
        if (FindObjectOfType<Canvas>() != null) return;

        var canvasGo = new GameObject("UI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // EventSystem
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        var ui = canvasGo.AddComponent<UIManager>();

        // Score
        ui.scoreText = CreateTmp(canvasGo.transform, "Score", new Vector2(20, -20), TextAnchor.UpperLeft, 36, "🍎 0/16");
        ui.timerText = CreateTmp(canvasGo.transform, "Timer", new Vector2(-20, -20), TextAnchor.UpperRight, 36, "REC 00:00");
        ui.statusText = CreateTmp(canvasGo.transform, "Status", new Vector2(0, 40), TextAnchor.LowerCenter, 28, "Listo");
        ui.statusText.alignment = TextAlignmentOptions.Center;
        ui.calibrationText = CreateTmp(canvasGo.transform, "Calib", new Vector2(0, 0), TextAnchor.MiddleCenter, 32, "Calibrando…");
        ui.calibrationText.alignment = TextAlignmentOptions.Center;

        // Webcam preview
        var camPanel = new GameObject("WebcamOverlay", typeof(RectTransform), typeof(RawImage));
        camPanel.transform.SetParent(canvasGo.transform, false);
        var rt = camPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -70);
        rt.sizeDelta = new Vector2(320, 180);
        ui.webcamRawImage = camPanel.GetComponent<RawImage>();
        ui.webcamRawImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Skeleton overlay on webcam
        var skel = new GameObject("SkeletonOverlay", typeof(RectTransform));
        skel.transform.SetParent(camPanel.transform, false);
        var skRt = skel.GetComponent<RectTransform>();
        skRt.anchorMin = Vector2.zero;
        skRt.anchorMax = Vector2.one;
        skRt.offsetMin = skRt.offsetMax = Vector2.zero;
        var vis = skel.AddComponent<SkeletonVisualizer>();
        vis.overlayRoot = skRt;

        ui.trackingLabel = CreateTmp(canvasGo.transform, "Tracking", new Vector2(20, -100), TextAnchor.UpperLeft, 18, "");

        // Victory panel
        var victory = new GameObject("VictoryPanel", typeof(RectTransform), typeof(Image));
        victory.transform.SetParent(canvasGo.transform, false);
        var vrt = victory.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        victory.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        ui.panelVictory = victory;
        ui.victoryText = CreateTmp(victory.transform, "VictoryText", Vector2.zero, TextAnchor.MiddleCenter, 42, "¡VICTORIA!");
        ui.victoryText.alignment = TextAlignmentOptions.Center;
        var btnGo = CreateButton(victory.transform, "Reiniciar", new Vector2(0, -120));
        ui.btnRestart = btnGo.GetComponent<Button>();
        victory.SetActive(false);

        ui.panelCalibrating = ui.calibrationText.gameObject;
        ui.panelPlaying = ui.scoreText.gameObject;
        ui.panelPaused = null;

        gm.ui = ui;
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, Vector2 pos, TextAnchor anchor, int size, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = AnchorToVec(anchor);
        rt.pivot = AnchorToVec(anchor);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600, 80);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        return tmp;
    }

    static Vector2 AnchorToVec(TextAnchor a)
    {
        switch (a)
        {
            case TextAnchor.UpperLeft: return new Vector2(0, 1);
            case TextAnchor.UpperRight: return new Vector2(1, 1);
            case TextAnchor.LowerCenter: return new Vector2(0.5f, 0);
            case TextAnchor.MiddleCenter: return new Vector2(0.5f, 0.5f);
            default: return new Vector2(0.5f, 0.5f);
        }
    }

    static GameObject CreateButton(Transform parent, string label, Vector2 pos)
    {
        var go = new GameObject(label + "Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(220, 56);
        go.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.4f);
        var t = CreateTmp(go.transform, "Label", Vector2.zero, TextAnchor.MiddleCenter, 28, label);
        t.alignment = TextAlignmentOptions.Center;
        var tr = t.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        return go;
    }

    static T Ensure<T>(GameObject host) where T : Component
    {
        var c = host.GetComponent<T>();
        if (c == null) c = host.AddComponent<T>();
        return c;
    }

    static void CreateTagIfEditor(string tag)
    {
#if UNITY_EDITOR
        // Tags se configuran en editor; aquí solo recordatorio en log
        Debug.Log($"[Bootstrap] Asegura el Tag '{tag}' en Project Settings > Tags and Layers.");
#endif
    }
}

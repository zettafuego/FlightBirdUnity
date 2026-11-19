// Flight Bird - Motion Control
// SkeletonVisualizer.cs — Dibuja esqueleto (líneas cian + puntos amarillos) sobre UI.
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visualiza los 17 keypoints y conexiones del esqueleto en un RectTransform (overlay).
/// </summary>
public class SkeletonVisualizer : MonoBehaviour
{
    [Header("Referencias")]
    public BodyPoseDetector poseDetector;
    public RectTransform overlayRoot;
    public RawImage webcamBackground;

    [Header("Estilo")]
    public Color boneColor = new Color(0.2f, 0.9f, 1f, 0.95f); // azul-cian
    public Color jointColor = new Color(1f, 0.92f, 0.2f, 1f);   // amarillo
    public float boneThickness = 4f;
    public float jointSize = 10f;

    static readonly int[][] Bones =
    {
        new[] { 5, 6 },   // shoulders
        new[] { 5, 7 }, new[] { 7, 9 },   // left arm
        new[] { 6, 8 }, new[] { 8, 10 },  // right arm
        new[] { 5, 11 }, new[] { 6, 12 }, // torso
        new[] { 11, 12 },
        new[] { 11, 13 }, new[] { 13, 15 }, // left leg
        new[] { 12, 14 }, new[] { 14, 16 }, // right leg
        new[] { 0, 5 }, new[] { 0, 6 }      // head-shoulders
    };

    Image[] _joints;
    Image[] _bones;
    bool _built;

    void Start()
    {
        if (poseDetector == null) poseDetector = BodyPoseDetector.Instance;
        if (overlayRoot == null) overlayRoot = transform as RectTransform;
        Build();
    }

    void Build()
    {
        if (overlayRoot == null) return;
        ClearChildren();

        _bones = new Image[Bones.Length];
        for (int i = 0; i < Bones.Length; i++)
        {
            var go = new GameObject($"Bone_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(overlayRoot, false);
            var img = go.GetComponent<Image>();
            img.color = boneColor;
            img.raycastTarget = false;
            _bones[i] = img;
        }

        _joints = new Image[BodyPoseDetector.KeypointCount];
        for (int i = 0; i < BodyPoseDetector.KeypointCount; i++)
        {
            var go = new GameObject($"Joint_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(overlayRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(jointSize, jointSize);
            var img = go.GetComponent<Image>();
            img.color = jointColor;
            img.raycastTarget = false;
            // círculo simple
            img.sprite = CreateCircleSprite();
            _joints[i] = img;
        }

        _built = true;
    }

    static Sprite _circle;
    static Sprite CreateCircleSprite()
    {
        if (_circle != null) return _circle;
        const int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
            float a = Mathf.Clamp01(c - d);
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        return _circle;
    }

    void ClearChildren()
    {
        for (int i = overlayRoot.childCount - 1; i >= 0; i--)
            Destroy(overlayRoot.GetChild(i).gameObject);
    }

    void LateUpdate()
    {
        if (!_built || poseDetector == null || overlayRoot == null) return;
        var rect = overlayRoot.rect;
        float w = rect.width;
        float h = rect.height;

        // Joints
        for (int i = 0; i < BodyPoseDetector.KeypointCount; i++)
        {
            var k = poseDetector.Keypoints[i];
            bool on = k.z >= poseDetector.minKeypointScore;
            _joints[i].enabled = on;
            if (!on) continue;
            var rt = _joints[i].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(k.x * w, k.y * h);
        }

        // Bones
        for (int b = 0; b < Bones.Length; b++)
        {
            int a = Bones[b][0];
            int c = Bones[b][1];
            var ka = poseDetector.Keypoints[a];
            var kc = poseDetector.Keypoints[c];
            bool on = ka.z >= poseDetector.minKeypointScore && kc.z >= poseDetector.minKeypointScore;
            _bones[b].enabled = on;
            if (!on) continue;

            Vector2 pa = new Vector2(ka.x * w, ka.y * h);
            Vector2 pb = new Vector2(kc.x * w, kc.y * h);
            Vector2 mid = (pa + pb) * 0.5f;
            float len = Vector2.Distance(pa, pb);
            float angle = Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;

            var rt = _bones[b].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(len, boneThickness);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
}

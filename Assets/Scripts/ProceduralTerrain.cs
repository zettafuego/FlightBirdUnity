// Flight Bird - Motion Control
// ProceduralTerrain.cs — Entorno low-poly infinito con object pooling.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera segmentos de terreno delante del jugador y recicla los que quedan atrás.
/// Montañas (5 variantes), lagos, árboles y flores con colores vibrantes URP.
/// </summary>
public class ProceduralTerrain : MonoBehaviour
{
    [Header("Seguimiento")]
    public Transform player;
    public float segmentLength = 80f;
    public int segmentsAhead = 6;
    public int segmentsBehind = 2;

    [Header("Colores URP")]
    public Color grassColor = new Color(0.25f, 0.85f, 0.35f);
    public Color mountainColor = new Color(0.45f, 0.55f, 0.7f);
    public Color waterColor = new Color(0.15f, 0.55f, 1f, 0.85f);
    public Color treeColor = new Color(0.1f, 0.65f, 0.2f);
    public Color flowerColor = new Color(1f, 0.4f, 0.7f);

    [Header("Densidad")]
    public int treesPerSegment = 12;
    public int flowersPerSegment = 20;
    public int mountainsPerSegment = 3;

    readonly Dictionary<int, GameObject> _segments = new Dictionary<int, GameObject>();
    readonly Queue<GameObject> _pool = new Queue<GameObject>();
    Material _matGrass, _matMountain, _matWater, _matTree, _matFlower;
    int _lastCenter = int.MinValue;

    void Start()
    {
        if (player == null)
        {
            var b = FindObjectOfType<BirdController>();
            if (b) player = b.transform;
        }
        CreateMaterials();
        // Cámara follow opcional
        if (Camera.main != null && Camera.main.GetComponent<CameraFollow>() == null)
            Camera.main.gameObject.AddComponent<CameraFollow>().target = player;
    }

    void CreateMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _matGrass = NewMat(lit, grassColor);
        _matMountain = NewMat(lit, mountainColor);
        _matWater = NewMat(lit, waterColor);
        if (_matWater.HasProperty("_Surface")) { /* URP transparent opcional */ }
        _matTree = NewMat(lit, treeColor);
        _matFlower = NewMat(lit, flowerColor);
    }

    static Material NewMat(Shader sh, Color c)
    {
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    void Update()
    {
        if (player == null) return;
        int center = Mathf.FloorToInt(player.position.z / segmentLength);
        if (center == _lastCenter) return;
        _lastCenter = center;

        int min = center - segmentsBehind;
        int max = center + segmentsAhead;

        // Spawn needed
        for (int i = min; i <= max; i++)
        {
            if (!_segments.ContainsKey(i))
                _segments[i] = GetOrCreateSegment(i);
        }

        // Recycle far
        var keys = new List<int>(_segments.Keys);
        foreach (int k in keys)
        {
            if (k < min || k > max)
            {
                Recycle(_segments[k]);
                _segments.Remove(k);
            }
        }
    }

    GameObject GetOrCreateSegment(int index)
    {
        GameObject go;
        if (_pool.Count > 0)
        {
            go = _pool.Dequeue();
            go.SetActive(true);
            // Limpiar hijos previos excepto suelo base
            ClearDecor(go);
        }
        else
        {
            go = new GameObject($"Segment_{index}");
            go.transform.SetParent(transform, false);
            BuildGround(go);
        }

        go.name = $"Segment_{index}";
        float z0 = index * segmentLength;
        go.transform.position = new Vector3(0, 0, z0);
        Populate(go, index, z0);
        return go;
    }

    void BuildGround(GameObject root)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.SetParent(root.transform, false);
        ground.transform.localScale = new Vector3(120f, 1f, segmentLength);
        ground.transform.localPosition = new Vector3(0, -0.5f, segmentLength * 0.5f);
        ground.GetComponent<Renderer>().sharedMaterial = _matGrass;
        ground.tag = "Terrain";
        ground.isStatic = true;
    }

    void ClearDecor(GameObject root)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var ch = root.transform.GetChild(i);
            if (ch.name == "Ground") continue;
            Destroy(ch.gameObject);
        }
    }

    void Populate(GameObject root, int seedIndex, float z0)
    {
        var rng = new System.Random(seedIndex * 9176 + 13);

        // Montañas (5 variantes de escala/forma)
        for (int m = 0; m < mountainsPerSegment; m++)
        {
            int variant = rng.Next(0, 5);
            float side = rng.NextDouble() > 0.5 ? 1f : -1f;
            float x = side * (35f + (float)rng.NextDouble() * 40f);
            float z = (float)rng.NextDouble() * segmentLength;
            float h = 15f + variant * 8f + (float)rng.NextDouble() * 12f;
            float w = 12f + variant * 3f;

            var mt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mt.name = $"Mountain_v{variant}";
            mt.transform.SetParent(root.transform, false);
            // Cono approx: scale y + mesh
            mt.transform.localScale = new Vector3(w, h * 0.5f, w);
            mt.transform.localPosition = new Vector3(x, h * 0.25f, z);
            mt.GetComponent<Renderer>().sharedMaterial = _matMountain;
            mt.tag = "Obstacle";
            // Simplificar colliders pesados
            var col = mt.GetComponent<CapsuleCollider>();
            if (col) col.height = 2f;
        }

        // Lagos
        if (rng.NextDouble() > 0.4)
        {
            var lake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lake.name = "Lake";
            lake.transform.SetParent(root.transform, false);
            float lw = 8f + (float)rng.NextDouble() * 14f;
            lake.transform.localScale = new Vector3(lw, 0.2f, lw * 0.7f);
            lake.transform.localPosition = new Vector3(
                (float)(rng.NextDouble() * 50 - 25),
                0.15f,
                (float)rng.NextDouble() * segmentLength);
            lake.GetComponent<Renderer>().sharedMaterial = _matWater;
            Object.Destroy(lake.GetComponent<Collider>());
        }

        // Árboles
        for (int t = 0; t < treesPerSegment; t++)
        {
            float x = (float)(rng.NextDouble() * 90 - 45);
            float z = (float)rng.NextDouble() * segmentLength;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localScale = new Vector3(0.6f, 2f, 0.6f);
            trunk.transform.localPosition = new Vector3(x, 2f, z);
            trunk.GetComponent<Renderer>().sharedMaterial = _matTree;
            Object.Destroy(trunk.GetComponent<Collider>());

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.transform.SetParent(trunk.transform, false);
            crown.transform.localScale = new Vector3(3f, 2.2f, 3f);
            crown.transform.localPosition = new Vector3(0, 1.2f, 0);
            crown.GetComponent<Renderer>().sharedMaterial = _matTree;
            Object.Destroy(crown.GetComponent<Collider>());
        }

        // Flores
        for (int f = 0; f < flowersPerSegment; f++)
        {
            var fl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fl.name = "Flower";
            fl.transform.SetParent(root.transform, false);
            fl.transform.localScale = Vector3.one * (0.3f + (float)rng.NextDouble() * 0.4f);
            fl.transform.localPosition = new Vector3(
                (float)(rng.NextDouble() * 80 - 40),
                0.4f,
                (float)rng.NextDouble() * segmentLength);
            fl.GetComponent<Renderer>().sharedMaterial = _matFlower;
            Object.Destroy(fl.GetComponent<Collider>());
        }
    }

    void Recycle(GameObject go)
    {
        if (go == null) return;
        ClearDecor(go);
        go.SetActive(false);
        _pool.Enqueue(go);
    }
}

/// <summary>Cámara tercera persona que sigue al pájaro.</summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 8f, -16f);
    public float followLerp = 6f;
    public float lookLerp = 8f;

    void LateUpdate()
    {
        if (target == null)
        {
            var b = FindObjectOfType<BirdController>();
            if (b) target = b.transform;
            if (target == null) return;
        }

        Vector3 desired = target.position + target.TransformDirection(offset);
        // Offset en mundo para estabilidad
        desired = target.position + offset;
        desired.x = Mathf.Lerp(transform.position.x, target.position.x + offset.x, followLerp * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);
        Quaternion look = Quaternion.LookRotation(target.position + Vector3.up * 2f - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, lookLerp * Time.deltaTime);
    }
}

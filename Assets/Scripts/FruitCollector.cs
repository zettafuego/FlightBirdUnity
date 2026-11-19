// Flight Bird - Motion Control
// FruitCollector.cs — Pool de manzanas + generación procedural.
using System.Collections.Generic;
using UnityEngine;

public class FruitCollector : MonoBehaviour
{
    [Header("Prefab y pool")]
    public FruitCollectible fruitPrefab;
    public int poolSize = 32;
    public int fruitsToWin = 16;

    [Header("Spawn")]
    public Transform player;
    public float spawnAheadMin = 30f;
    public float spawnAheadMax = 120f;
    public float lateralRange = 25f;
    public float heightMin = 8f;
    public float heightMax = 55f;
    public float spawnInterval = 1.2f;
    public int maxActive = 20;

    readonly Queue<FruitCollectible> _pool = new Queue<FruitCollectible>();
    readonly List<FruitCollectible> _active = new List<FruitCollectible>();
    float _timer;
    bool _spawning;
    int _spawnedTotal;

    void Start()
    {
        if (player == null)
        {
            var bird = FindObjectOfType<BirdController>();
            if (bird) player = bird.transform;
        }
        WarmPool();
    }

    void WarmPool()
    {
        if (fruitPrefab == null)
        {
            // Crear prefab runtime simple (esfera roja)
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FruitRuntime";
            go.tag = "Fruit";
            var col = go.GetComponent<Collider>();
            col.isTrigger = true;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var fc = go.AddComponent<FruitCollectible>();
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            r.material.color = Color.red;
            go.SetActive(false);
            fruitPrefab = fc;
        }

        for (int i = 0; i < poolSize; i++)
        {
            var f = Instantiate(fruitPrefab, transform);
            f.gameObject.SetActive(false);
            f.Init(this);
            _pool.Enqueue(f);
        }
    }

    public void ResetFruits()
    {
        _spawning = false;
        _spawnedTotal = 0;
        _timer = 0f;
        for (int i = _active.Count - 1; i >= 0; i--)
            Recycle(_active[i]);
        _active.Clear();
    }

    public void StartSpawning()
    {
        _spawning = true;
        _timer = 0f;
        // Pre-sembrar algunas
        for (int i = 0; i < 8; i++) SpawnOne();
    }

    public void StopSpawning() => _spawning = false;

    void Update()
    {
        if (!_spawning || player == null) return;
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval && _active.Count < maxActive && _spawnedTotal < fruitsToWin * 3)
        {
            _timer = 0f;
            SpawnOne();
        }

        // Reciclar frutas muy atrás del jugador
        float zMin = player.position.z - 20f;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i] != null && _active[i].transform.position.z < zMin)
                Recycle(_active[i]);
        }
    }

    void SpawnOne()
    {
        if (_pool.Count == 0) return;
        var f = _pool.Dequeue();
        float z = player.position.z + Random.Range(spawnAheadMin, spawnAheadMax);
        float x = Random.Range(-lateralRange, lateralRange);
        float y = Random.Range(heightMin, heightMax);
        f.transform.position = new Vector3(x, y, z);
        f.gameObject.SetActive(true);
        f.OnSpawned();
        _active.Add(f);
        _spawnedTotal++;
    }

    public void Recycle(FruitCollectible f)
    {
        if (f == null) return;
        f.gameObject.SetActive(false);
        _active.Remove(f);
        if (!_pool.Contains(f)) _pool.Enqueue(f);
    }
}

/// <summary>Manzana flotante recolectable.</summary>
public class FruitCollectible : MonoBehaviour
{
    public float rotateSpeed = 90f;
    public float bobAmp = 0.35f;
    public float bobFreq = 2f;
    public ParticleSystem collectFx;
    public AudioClip collectClip;

    FruitCollector _owner;
    Vector3 _base;
    bool _alive;
    float _phase;

    public void Init(FruitCollector owner) => _owner = owner;

    public void OnSpawned()
    {
        _base = transform.position;
        _alive = true;
        _phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        if (!_alive) return;
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float y = _base.y + Mathf.Sin(Time.time * bobFreq + _phase) * bobAmp;
        transform.position = new Vector3(_base.x, y, _base.z);
    }

    public void Collect()
    {
        if (!_alive) return;
        _alive = false;
        if (collectFx != null)
        {
            collectFx.transform.SetParent(null);
            collectFx.Play();
            Destroy(collectFx.gameObject, 2f);
        }
        GameManager.Instance?.OnFruitCollected();
        _owner?.Recycle(this);
    }
}

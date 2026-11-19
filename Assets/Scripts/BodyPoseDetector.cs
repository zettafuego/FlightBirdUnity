// Flight Bird - Motion Control
// BodyPoseDetector.cs — Inferencia MoveNet/PoseNet con Unity Barracuda.
using System.Collections;
using Unity.Barracuda;
using UnityEngine;

/// <summary>
/// Detecta 17 keypoints corporales desde la webcam usando un modelo ONNX/NN (MoveNet Lightning recomendado).
/// Publica altura de brazos, ancho entre muñecas e inclinación de hombros para BirdController.
/// </summary>
[DefaultExecutionOrder(-50)]
public class BodyPoseDetector : MonoBehaviour
{
    public static BodyPoseDetector Instance { get; private set; }

    public enum PoseKeypoint
    {
        Nose = 0,
        LeftEye = 1, RightEye = 2,
        LeftEar = 3, RightEar = 4,
        LeftShoulder = 5, RightShoulder = 6,
        LeftElbow = 7, RightElbow = 8,
        LeftWrist = 9, RightWrist = 10,
        LeftHip = 11, RightHip = 12,
        LeftKnee = 13, RightKnee = 14,
        LeftAnkle = 15, RightAnkle = 16
    }

    public const int KeypointCount = 17;

    [Header("Modelo ML (Barracuda)")]
    [Tooltip("Asigna el asset NNModel importado desde ONNX (MoveNet Lightning / Thunder).")]
    public NNModel modelAsset;
    public WorkerFactory.Type workerType = WorkerFactory.Type.ComputePrecompiled;

    [Header("Entrada del modelo")]
    public int inputSize = 192; // MoveNet Lightning suele ser 192x192
    public float inferenceInterval = 1f / 30f; // ~30 FPS inferencia

    [Header("Tracking")]
    [Range(0.05f, 0.5f)] public float minKeypointScore = 0.15f;
    public bool mirrorX = true;

    [Header("Calibración")]
    public float calibrationSeconds = 3f;

    // Salidas normalizadas 0..1 (x,y) y score
    public Vector3[] Keypoints { get; private set; } = new Vector3[KeypointCount];
    public bool IsTracking { get; private set; }
    public bool IsCalibrated { get; private set; }
    public float CalibrationProgress { get; private set; }

    /// <summary>0 = brazos abajo, 1 = brazos arriba (altura media muñecas, normalizada).</summary>
    public float ArmHeight01 { get; private set; } = 0.5f;
    /// <summary>-1 izquierda … +1 derecha (inclinación hombros / diferencia muñecas).</summary>
    public float ShoulderLean01 { get; private set; }
    /// <summary>0 = brazos juntos, 1 = brazos abiertos (distancia entre muñecas).</summary>
    public float ArmWidth01 { get; private set; } = 0.3f;
    public string Status { get; private set; } = "Esperando modelo…";

    Model _runtimeModel;
    IWorker _worker;
    Texture2D _frame;
    RenderTexture _rt;
    float _nextInference;
    float _calibTimer;
    float _armMin = 0.2f, _armMax = 0.8f;
    float _widthMin = 0.05f, _widthMax = 0.45f;
    bool _modelReady;

    // Suavizado
    float _smoothH = 0.5f, _smoothL, _smoothW = 0.3f;
    const float SmoothSpeed = 12f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        for (int i = 0; i < KeypointCount; i++)
            Keypoints[i] = new Vector3(0.5f, 0.5f, 0f);
    }

    void Start()
    {
        if (modelAsset == null)
        {
            Status = "Sin modelo NN — modo teclado/demo.";
            Debug.LogWarning("[BodyPoseDetector] Asigna un NNModel (MoveNet ONNX) en el inspector. " +
                             "Mientras tanto se usan valores demo si hay teclado.");
            _modelReady = false;
            return;
        }

        try
        {
            _runtimeModel = ModelLoader.Load(modelAsset);
            // Preferir GPUCompute / ComputePrecompiled
            var type = workerType;
            if (!SystemInfo.supportsComputeShaders)
                type = WorkerFactory.Type.CSharpBurst;
            _worker = WorkerFactory.CreateWorker(type, _runtimeModel);
            _modelReady = true;
            Status = "Modelo ML listo. Calibrando…";
            Debug.Log($"[BodyPoseDetector] Worker: {type}");
        }
        catch (System.Exception e)
        {
            _modelReady = false;
            Status = "Error cargando modelo: " + e.Message;
            Debug.LogError(Status);
        }
    }

    void Update()
    {
        // Fallback teclado: simula pose
        if (!_modelReady || WebcamManager.Instance == null ||
            WebcamManager.Instance.UsingKeyboardFallback ||
            !WebcamManager.Instance.IsWebcamActive)
        {
            SimulateFromKeyboard();
            return;
        }

        if (Time.unscaledTime < _nextInference) return;
        _nextInference = Time.unscaledTime + inferenceInterval;

        if (!WebcamManager.Instance.TryGetFrame(ref _frame))
            return;

        RunInference(_frame);
        UpdateDerivedSignals(Time.deltaTime);
        UpdateCalibration(Time.deltaTime);
    }

    void RunInference(Texture2D frame)
    {
        if (_worker == null || frame == null) return;

        // Redimensionar a inputSize x inputSize
        if (_rt == null || _rt.width != inputSize)
        {
            if (_rt != null) _rt.Release();
            _rt = new RenderTexture(inputSize, inputSize, 0, RenderTextureFormat.ARGB32);
        }

        Graphics.Blit(frame, _rt);

        using (var tensor = new Tensor(_rt, 3))
        {
            // Nombre de entrada típico MoveNet: "input" — ajustar si el ONNX usa otro
            string inputName = _runtimeModel.inputs.Count > 0
                ? _runtimeModel.inputs[0].name
                : "input";

            _worker.Execute(tensor);
            Tensor output = null;
            try
            {
                // Salida típica: [1,1,17,3] yx score o [1,17,3]
                output = _worker.PeekOutput();
                ParseOutput(output);
                IsTracking = CountValidKeypoints() >= 6;
                Status = IsTracking ? "Tracking OK" : "Pose parcial…";
            }
            catch (System.Exception e)
            {
                Status = "Inferencia: " + e.Message;
            }
            finally
            {
                output?.Dispose();
            }
        }
    }

    void ParseOutput(Tensor o)
    {
        // Soporta shapes comunes MoveNet: length >= 51 (17*3)
        int n = Mathf.Min(KeypointCount, o.length / 3);
        for (int i = 0; i < n; i++)
        {
            // Formato habitual: y, x, score (normalizado 0-1)
            float y = o[0, 0, i, 0];
            float x = o[0, 0, i, 1];
            float s = o[0, 0, i, 2];

            // Si el layout es distinto, intentar plano
            if (float.IsNaN(y) || float.IsNaN(x))
            {
                y = o[i * 3 + 0];
                x = o[i * 3 + 1];
                s = o[i * 3 + 2];
            }

            if (mirrorX) x = 1f - x;
            // Convertir a espacio pantalla-friendly: y hacia arriba
            y = 1f - Mathf.Clamp01(y);
            x = Mathf.Clamp01(x);
            Keypoints[i] = new Vector3(x, y, s);
        }
    }

    int CountValidKeypoints()
    {
        int c = 0;
        for (int i = 0; i < KeypointCount; i++)
            if (Keypoints[i].z >= minKeypointScore) c++;
        return c;
    }

    void UpdateDerivedSignals(float dt)
    {
        var lw = Keypoints[(int)PoseKeypoint.LeftWrist];
        var rw = Keypoints[(int)PoseKeypoint.RightWrist];
        var ls = Keypoints[(int)PoseKeypoint.LeftShoulder];
        var rs = Keypoints[(int)PoseKeypoint.RightShoulder];

        float rawHeight = 0.5f * (lw.y + rw.y);
        float rawWidth = Mathf.Abs(lw.x - rw.x);
        float rawLean = (rs.y - ls.y) * 2f; // hombro derecho más alto = lean
        // Alternativa lean: diferencia horizontal muñecas respecto al centro
        float midX = 0.5f * (ls.x + rs.x);
        float wristMid = 0.5f * (lw.x + rw.x);
        rawLean = Mathf.Clamp((wristMid - midX) * 4f, -1f, 1f);

        // Normalizar con calibración
        float h01 = Mathf.InverseLerp(_armMin, _armMax, rawHeight);
        float w01 = Mathf.InverseLerp(_widthMin, _widthMax, rawWidth);

        float k = 1f - Mathf.Exp(-SmoothSpeed * dt);
        _smoothH = Mathf.Lerp(_smoothH, Mathf.Clamp01(h01), k);
        _smoothW = Mathf.Lerp(_smoothW, Mathf.Clamp01(w01), k);
        _smoothL = Mathf.Lerp(_smoothL, Mathf.Clamp(rawLean, -1f, 1f), k);

        ArmHeight01 = _smoothH;
        ArmWidth01 = _smoothW;
        ShoulderLean01 = _smoothL;
    }

    void UpdateCalibration(float dt)
    {
        if (IsCalibrated || !IsTracking) return;
        _calibTimer += dt;
        CalibrationProgress = Mathf.Clamp01(_calibTimer / calibrationSeconds);

        // Acumular min/max de brazos
        var lw = Keypoints[(int)PoseKeypoint.LeftWrist];
        var rw = Keypoints[(int)PoseKeypoint.RightWrist];
        float h = 0.5f * (lw.y + rw.y);
        float w = Mathf.Abs(lw.x - rw.x);
        _armMin = Mathf.Min(_armMin, h);
        _armMax = Mathf.Max(_armMax, h);
        _widthMin = Mathf.Min(_widthMin, w);
        _widthMax = Mathf.Max(_widthMax, w);

        if (_calibTimer >= calibrationSeconds)
        {
            if (_armMax - _armMin < 0.05f) { _armMin = 0.25f; _armMax = 0.85f; }
            if (_widthMax - _widthMin < 0.02f) { _widthMin = 0.08f; _widthMax = 0.5f; }
            IsCalibrated = true;
            Status = "Calibrado — ¡a volar!";
        }
        else
        {
            Status = $"Calibrando… {Mathf.CeilToInt((1f - CalibrationProgress) * calibrationSeconds)}s — mueve los brazos";
        }
    }

    void SimulateFromKeyboard()
    {
        IsTracking = true;
        if (!IsCalibrated)
        {
            CalibrationProgress = 1f;
            IsCalibrated = true;
            Status = "Modo teclado (WASD / flechas)";
        }

        float h = ArmHeight01;
        float l = ShoulderLean01;
        float w = ArmWidth01;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) h += Time.deltaTime * 0.8f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) h -= Time.deltaTime * 0.8f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) l -= Time.deltaTime * 1.2f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) l += Time.deltaTime * 1.2f;
        if (Input.GetKey(KeyCode.E)) w += Time.deltaTime * 0.8f;
        if (Input.GetKey(KeyCode.Q)) w -= Time.deltaTime * 0.8f;

        ArmHeight01 = Mathf.Clamp01(h);
        ShoulderLean01 = Mathf.Clamp(l, -1f, 1f);
        ArmWidth01 = Mathf.Clamp01(w);

        // Fake skeleton for visualizer
        float cy = Mathf.Lerp(0.25f, 0.85f, ArmHeight01);
        float cx = 0.5f + ShoulderLean01 * 0.1f;
        float half = Mathf.Lerp(0.08f, 0.28f, ArmWidth01);
        Keypoints[(int)PoseKeypoint.Nose] = new Vector3(cx, cy + 0.12f, 1f);
        Keypoints[(int)PoseKeypoint.LeftShoulder] = new Vector3(cx - 0.08f, cy + 0.05f, 1f);
        Keypoints[(int)PoseKeypoint.RightShoulder] = new Vector3(cx + 0.08f, cy + 0.05f, 1f);
        Keypoints[(int)PoseKeypoint.LeftWrist] = new Vector3(cx - half, cy, 1f);
        Keypoints[(int)PoseKeypoint.RightWrist] = new Vector3(cx + half, cy, 1f);
        Keypoints[(int)PoseKeypoint.LeftHip] = new Vector3(cx - 0.05f, cy - 0.15f, 1f);
        Keypoints[(int)PoseKeypoint.RightHip] = new Vector3(cx + 0.05f, cy - 0.15f, 1f);
        Keypoints[(int)PoseKeypoint.LeftAnkle] = new Vector3(cx - 0.05f, cy - 0.4f, 1f);
        Keypoints[(int)PoseKeypoint.RightAnkle] = new Vector3(cx + 0.05f, cy - 0.4f, 1f);
    }

    public Vector2 GetKeypointScreen(int index, RectTransform canvasRect)
    {
        if (index < 0 || index >= KeypointCount) return Vector2.zero;
        var k = Keypoints[index];
        // Espacio 0..1 → anclado en esquina inferior izquierda del overlay
        return new Vector2(k.x * canvasRect.rect.width, k.y * canvasRect.rect.height);
    }

    void OnDestroy()
    {
        _worker?.Dispose();
        if (_rt != null) _rt.Release();
        if (_frame != null) Destroy(_frame);
        if (Instance == this) Instance = null;
    }
}

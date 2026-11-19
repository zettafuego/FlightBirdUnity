// Flight Bird - Motion Control
// WebcamManager.cs — Captura de webcam con fallback a teclado.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestiona la webcam (WebCamTexture), resolución y fallback si no hay cámara.
/// </summary>
[DefaultExecutionOrder(-100)]
public class WebcamManager : MonoBehaviour
{
    public static WebcamManager Instance { get; private set; }

    [Header("Resolución preferida")]
    public int preferredWidth = 1280;
    public int preferredHeight = 720;
    public int preferredFps = 30;

    [Header("Fallback")]
    [Tooltip("Si no hay cámara, el juego usa teclado (WASD / flechas).")]
    public bool allowKeyboardFallback = true;

    [Header("UI opcional")]
    public RawImage webcamPreview;

    public WebCamTexture WebcamTexture { get; private set; }
    public bool IsWebcamActive { get; private set; }
    public bool UsingKeyboardFallback { get; private set; }
    public string StatusMessage { get; private set; } = "Inicializando cámara…";

    public Texture CurrentTexture =>
        IsWebcamActive && WebcamTexture != null ? (Texture)WebcamTexture : null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(InitWebcam());
    }

    IEnumerator InitWebcam()
    {
        // Permisos (Android / WebGL / iOS)
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            FailToKeyboard("Permiso de cámara denegado.");
            yield break;
        }

        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            FailToKeyboard("No se detectó ninguna webcam.");
            yield break;
        }

        // Preferir cámara frontal si existe
        string deviceName = devices[0].name;
        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i].isFrontFacing)
            {
                deviceName = devices[i].name;
                break;
            }
        }

        // Intentar 1280x720, luego 640x480
        if (!TryStart(deviceName, preferredWidth, preferredHeight, preferredFps))
        {
            if (!TryStart(deviceName, 640, 480, preferredFps))
            {
                FailToKeyboard("No se pudo iniciar la webcam.");
                yield break;
            }
        }

        // Esperar primer frame
        float t = 0f;
        while (WebcamTexture != null && !WebcamTexture.didUpdateThisFrame && t < 3f)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (WebcamTexture == null || WebcamTexture.width < 16)
        {
            FailToKeyboard("La webcam no entrega frames válidos.");
            yield break;
        }

        IsWebcamActive = true;
        UsingKeyboardFallback = false;
        StatusMessage = $"Cámara OK: {WebcamTexture.width}x{WebcamTexture.height} @ {deviceName}";
        ApplyPreview();
        Debug.Log($"[WebcamManager] {StatusMessage}");
    }

    bool TryStart(string deviceName, int w, int h, int fps)
    {
        try
        {
            if (WebcamTexture != null)
            {
                WebcamTexture.Stop();
                Destroy(WebcamTexture);
            }

            WebcamTexture = new WebCamTexture(deviceName, w, h, fps);
            WebcamTexture.Play();
            return WebcamTexture.isPlaying;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WebcamManager] TryStart falló {w}x{h}: {e.Message}");
            return false;
        }
    }

    void FailToKeyboard(string reason)
    {
        IsWebcamActive = false;
        UsingKeyboardFallback = allowKeyboardFallback;
        StatusMessage = allowKeyboardFallback
            ? $"Sin cámara — modo teclado. ({reason})"
            : $"Error cámara: {reason}";
        Debug.LogWarning($"[WebcamManager] {StatusMessage}");
        if (webcamPreview != null)
            webcamPreview.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
    }

    void ApplyPreview()
    {
        if (webcamPreview != null && WebcamTexture != null)
        {
            webcamPreview.texture = WebcamTexture;
            webcamPreview.color = Color.white;
            // Espejo horizontal (más natural para el jugador)
            var uv = webcamPreview.uvRect;
            webcamPreview.uvRect = new Rect(1, 0, -1, 1);
        }
    }

    /// <summary>Copia el frame actual a una Texture2D (para Barracuda). Reutiliza buffer si se pasa.</summary>
    public bool TryGetFrame(ref Texture2D dest, bool flipY = true)
    {
        if (!IsWebcamActive || WebcamTexture == null || !WebcamTexture.didUpdateThisFrame)
            return false;

        int w = WebcamTexture.width;
        int h = WebcamTexture.height;
        if (dest == null || dest.width != w || dest.height != h)
        {
            if (dest != null) Destroy(dest);
            dest = new Texture2D(w, h, TextureFormat.RGB24, false);
        }

        dest.SetPixels32(WebcamTexture.GetPixels32());
        dest.Apply(false, false);
        return true;
    }

    void OnDestroy()
    {
        if (WebcamTexture != null)
        {
            WebcamTexture.Stop();
            Destroy(WebcamTexture);
        }
        if (Instance == this) Instance = null;
    }

    void OnApplicationPause(bool pause)
    {
        if (WebcamTexture == null) return;
        if (pause) WebcamTexture.Pause();
        else if (IsWebcamActive) WebcamTexture.Play();
    }
}

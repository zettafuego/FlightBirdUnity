// Flight Bird - Motion Control
// UIManager.cs — Score, timer, status, paneles y webcam overlay.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Textos TMP")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI calibrationText;
    public TextMeshProUGUI victoryText;

    [Header("Paneles")]
    public GameObject panelPlaying;
    public GameObject panelCalibrating;
    public GameObject panelPaused;
    public GameObject panelVictory;

    [Header("Webcam")]
    public RawImage webcamRawImage;
    public TextMeshProUGUI trackingLabel;

    [Header("Botones")]
    public Button btnRestart;
    public Button btnQuit;
    public Button btnResume;

    void Start()
    {
        if (btnRestart) btnRestart.onClick.AddListener(() => GameManager.Instance?.RestartGame());
        if (btnQuit) btnQuit.onClick.AddListener(() => GameManager.Instance?.QuitGame());
        if (btnResume) btnResume.onClick.AddListener(() => GameManager.Instance?.TogglePause());

        // Auto-wire webcam
        if (webcamRawImage != null && WebcamManager.Instance != null)
            WebcamManager.Instance.webcamPreview = webcamRawImage;
    }

    void Update()
    {
        if (trackingLabel != null)
        {
            string cam = WebcamManager.Instance != null ? WebcamManager.Instance.StatusMessage : "";
            string pose = BodyPoseDetector.Instance != null ? BodyPoseDetector.Instance.Status : "";
            trackingLabel.text = $"{cam}\n{pose}";
        }
    }

    public void SetScore(int current, int total)
    {
        if (scoreText) scoreText.text = $"🍎 {current}/{total}";
    }

    public void SetTimer(float seconds)
    {
        if (!timerText) return;
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int mm = s / 60;
        int ss = s % 60;
        timerText.text = $"REC {mm:00}:{ss:00}";
    }

    public void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg ?? "";
    }

    public void SetCalibration(float t01)
    {
        if (calibrationText)
            calibrationText.text = $"Calibración {Mathf.RoundToInt(t01 * 100f)}%\nMueve brazos arriba/abajo y abre/cierra";
    }

    public void ShowPanel(GameState state)
    {
        if (panelPlaying) panelPlaying.SetActive(state == GameState.Playing || state == GameState.Calibrating);
        if (panelCalibrating) panelCalibrating.SetActive(state == GameState.Calibrating);
        if (panelPaused) panelPaused.SetActive(state == GameState.Paused);
        if (panelVictory) panelVictory.SetActive(state == GameState.Victory);
    }

    public void ShowVictory(int fruits, float seconds)
    {
        ShowPanel(GameState.Victory);
        int s = Mathf.FloorToInt(seconds);
        if (victoryText)
            victoryText.text = $"¡VICTORIA!\nManzanas: {fruits}/16\nTiempo: {s / 60:00}:{s % 60:00}\n\nPulsa REINICIAR";
        SetStatus("¡Has ganado!");
    }
}

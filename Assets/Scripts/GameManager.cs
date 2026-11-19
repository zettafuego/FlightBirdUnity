// Flight Bird - Motion Control
// GameManager.cs — Estados, puntuación, temporizador y victoria.
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Boot,
    Calibrating,
    Playing,
    Paused,
    Victory,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Objetivo")]
    public int fruitsToWin = 16;

    [Header("Referencias")]
    public BirdController bird;
    public BodyPoseDetector pose;
    public FruitCollector fruitCollector;
    public UIManager ui;
    public Transform birdSpawn;

    [Header("Audio")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioClip sfxCollect;
    public AudioClip sfxHit;
    public AudioClip sfxWin;

    public GameState State { get; private set; } = GameState.Boot;
    public int FruitsCollected { get; private set; }
    public float ElapsedSeconds { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }

    void Start()
    {
        if (bird == null) bird = FindObjectOfType<BirdController>();
        if (pose == null) pose = BodyPoseDetector.Instance ?? FindObjectOfType<BodyPoseDetector>();
        if (fruitCollector == null) fruitCollector = FindObjectOfType<FruitCollector>();
        if (ui == null) ui = FindObjectOfType<UIManager>();

        BeginCalibration();
    }

    void Update()
    {
        switch (State)
        {
            case GameState.Calibrating:
                ui?.SetStatus(pose != null ? pose.Status : "Calibrando…");
                ui?.SetCalibration(pose != null ? pose.CalibrationProgress : 0f);
                if (pose != null && pose.IsCalibrated)
                    StartGameplay();
                // Saltar calibración con espacio
                if (Input.GetKeyDown(KeyCode.Space))
                    ForceStart();
                break;

            case GameState.Playing:
                ElapsedSeconds += Time.deltaTime;
                ui?.SetTimer(ElapsedSeconds);
                ui?.SetScore(FruitsCollected, fruitsToWin);
                ui?.SetStatus(pose != null ? pose.Status : "Jugando");
                if (Input.GetKeyDown(KeyCode.Escape))
                    TogglePause();
                break;

            case GameState.Paused:
                if (Input.GetKeyDown(KeyCode.Escape))
                    TogglePause();
                break;
        }
    }

    public void BeginCalibration()
    {
        State = GameState.Calibrating;
        FruitsCollected = 0;
        ElapsedSeconds = 0f;
        Time.timeScale = 1f;
        if (bird != null && birdSpawn != null)
            bird.ResetBird(birdSpawn.position);
        if (bird != null) bird.inputEnabled = false;
        fruitCollector?.ResetFruits();
        ui?.ShowPanel(GameState.Calibrating);
        ui?.SetScore(0, fruitsToWin);
        ui?.SetTimer(0f);
        if (musicSource != null && !musicSource.isPlaying) musicSource.Play();
    }

    void ForceStart()
    {
        if (pose != null)
        {
            // Forzar calibrado
            var t = typeof(BodyPoseDetector);
            // Usamos reflexión evitada: publicamos método simple
        }
        StartGameplay();
    }

    public void StartGameplay()
    {
        State = GameState.Playing;
        if (bird != null) bird.inputEnabled = true;
        fruitCollector?.StartSpawning();
        ui?.ShowPanel(GameState.Playing);
        ui?.SetStatus("¡Vuela y recolecta 16 manzanas!");
    }

    public void OnFruitCollected()
    {
        if (State != GameState.Playing) return;
        FruitsCollected++;
        ui?.SetScore(FruitsCollected, fruitsToWin);
        if (sfxSource && sfxCollect) sfxSource.PlayOneShot(sfxCollect);

        if (FruitsCollected >= fruitsToWin)
            Victory();
    }

    public void OnBirdHitObstacle()
    {
        if (State != GameState.Playing) return;
        if (sfxSource && sfxHit) sfxSource.PlayOneShot(sfxHit);
        // Penalización suave: empujón hacia atrás
        if (bird != null)
        {
            var p = bird.transform.position;
            bird.ResetBird(new Vector3(p.x, Mathf.Max(p.y, 10f), p.z - 5f));
        }
    }

    void Victory()
    {
        State = GameState.Victory;
        if (bird != null) bird.inputEnabled = false;
        fruitCollector?.StopSpawning();
        if (sfxSource && sfxWin) sfxSource.PlayOneShot(sfxWin);
        ui?.ShowVictory(FruitsCollected, ElapsedSeconds);
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
        {
            State = GameState.Paused;
            Time.timeScale = 0f;
            ui?.ShowPanel(GameState.Paused);
        }
        else if (State == GameState.Paused)
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
            ui?.ShowPanel(GameState.Playing);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        // Reinicio de escena o in-place
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

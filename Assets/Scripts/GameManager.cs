using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Estado global del juego: dinero, vidas, velocidad, victoria y derrota.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Valores iniciales")]
    public int startMoney = 150;
    [Tooltip("Vida de la puerta. Cuando llega a 0 se pierde la partida.")]
    public int gateHealth = 100;

    public int Money { get; private set; }
    public int Lives { get; private set; }
    public int MaxLives { get; private set; }

    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }
    public float GameSpeed { get; private set; } = 1f;
    public bool IsPaused { get; private set; }

    // Ventanas modales (ej. la ruleta de beneficios): congelan el juego y bloquean la entrada
    int modalCount;
    int modalEndFrame = -1;
    // Se mantiene bloqueado también durante el frame en que se cierra la ventana (evita que el mismo clic/tecla actúe dos veces)
    public bool IsInputBlocked { get { return IsPaused || modalCount > 0 || Time.frameCount == modalEndFrame; } }

    int lastPauseFrame = -1;

    public event Action OnStatsChanged;
    public event Action<bool> OnGameEnded; // true = victoria
    public event Action<bool> OnPauseChanged; // true = en pausa

void Awake()
    {
        Instance = this;
        Money = startMoney;
        Lives = gateHealth;
        MaxLives = gateHealth;
        Time.timeScale = 1f;

        // Sistemas de progresión: se crean solos, no hace falta agregarlos a la escena
        if (GetComponent<BuffManager>() == null) gameObject.AddComponent<BuffManager>();
        if (GetComponent<ExperienceManager>() == null) gameObject.AddComponent<ExperienceManager>();
    }

    void Start()
    {
        OnStatsChanged?.Invoke();
    }

void Update()
    {
        if (InputHelper.KeyDown(GameKey.Pause)) TogglePause();
        if (IsPaused && InputHelper.KeyDown(GameKey.Cancel)) TogglePause(); // Esc también reanuda

        if (InputHelper.KeyDown(GameKey.Speed)) ToggleSpeed();

        if ((IsPaused || IsGameOver) && InputHelper.KeyDown(GameKey.Restart))
        {
            Restart();
            return;
        }

        if (IsGameOver) return;

        if (!IsInputBlocked && InputHelper.KeyDown(GameKey.NextWave) && WaveSpawner.Instance != null)
            WaveSpawner.Instance.StartNextWave();
    }

    public bool CanAfford(int amount) { return Money >= amount; }

    public bool Spend(int amount)
    {
        if (Money < amount) return false;
        Money -= amount;
        OnStatsChanged?.Invoke();
        return true;
    }

    public void AddMoney(int amount)
    {
        Money += amount;
        OnStatsChanged?.Invoke();
    }

    public void LoseLife(int amount)
    {
        if (IsGameOver) return;
        Lives = Mathf.Max(0, Lives - amount);
        OnStatsChanged?.Invoke();
        if (Lives <= 0) EndGame(false);
    }

    public void Victory()
    {
        if (IsGameOver) return;
        EndGame(true);
    }

    void EndGame(bool win)
    {
        IsGameOver = true;
        IsVictory = win;
        Time.timeScale = 0f;
        OnGameEnded?.Invoke(win);
    }

public void ToggleSpeed()
    {
        if (IsGameOver || IsInputBlocked) return;
        GameSpeed = GameSpeed > 1f ? 1f : 2f;
        Time.timeScale = GameSpeed;
        OnStatsChanged?.Invoke();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }


/// <summary>Pausa o reanuda. Ignora un segundo cambio en el mismo frame (Esc lo pueden leer dos scripts).</summary>
public void TogglePause()
    {
        if (IsGameOver || modalCount > 0 || lastPauseFrame == Time.frameCount) return;
        lastPauseFrame = Time.frameCount;

        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : GameSpeed;
        OnPauseChanged?.Invoke(IsPaused);
    }

    /// <summary>Abre una ventana modal: frena el tiempo y bloquea construir, pausar y lanzar oleadas.</summary>
    public void BeginModal()
    {
        modalCount++;
        Time.timeScale = 0f;
    }

public void EndModal()
    {
        modalCount = Mathf.Max(0, modalCount - 1);
        modalEndFrame = Time.frameCount;
        if (modalCount == 0 && !IsGameOver) Time.timeScale = IsPaused ? 0f : GameSpeed;
    }
}

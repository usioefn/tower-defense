using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class WaveGroup
{
    public Enemy enemyPrefab;
    public int count = 5;
    public float interval = 1f;
}

[Serializable]
public class Wave
{
    public string name = "Oleada";
    public WaveGroup[] groups;
    public int bonus = 25; // dinero extra al completar la oleada

    // Datos de dificultad (los completa WaveGenerator)
    public bool isBoss;
    public int tier;                       // jefes ya derrotados cuando empieza esta oleada
    public float hpMultiplier = -1f;       // -1 = fórmula lineal vieja (healthGrowthPerWave)
    public float speedMultiplier = 1f;
    public float rewardMultiplier = 1f;
    public float damageMultiplier = 1f;    // multiplica el daño que hacen los enemigos a la puerta y a los soldados

}

/// <summary>
/// Controla las oleadas: cuenta regresiva, spawn de grupos de enemigos y condición de victoria.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance { get; private set; }

    [Header("Oleadas")]
    public Wave[] waves;
    public float firstWaveDelay = 15f;
    public float timeBetweenWaves = 10f;
    [Tooltip("Cuánta vida extra (porcentaje) gana cada enemigo por oleada")]
    public float healthGrowthPerWave = 0.12f;

    [Header("Oleadas generadas (modo 100 rondas con jefes)")]
    [Tooltip("Si está activo, las oleadas se generan por código y 'waves' solo se usa como respaldo.")]
    public bool useGeneratedWaves = true;
    public int totalWaves = 100;
    [Tooltip("Cada cuántas oleadas aparece un jefe.")]
    public int bossInterval = 10;
    [Tooltip("0 = oleadas distintas en cada partida.")]
    public int waveSeed = 0;

    [Header("Enemigos que usa el generador")]
    public Enemy enemyBasic;
    public Enemy enemyFast;
    public Enemy enemyTank;
    public Enemy enemyBoss;
    [Tooltip("Arqueros chicos que atacan a distancia (salen de a 2). Si falta, el generador no los usa.")]
    public Enemy enemyArcher;

    [Header("Enemigos counter (uno por tipo de torre)")]
    public Enemy enemyFlyer;        // Murciélago: vuela, le hace counter al Castillo
    public Enemy enemyGolem;        // Golem: resiste el área, counter del Cañón
    public Enemy enemyArmored;      // Blindado: armadura, counter de la Básica
    public Enemy enemySalamander;   // Salamandra: inmune al frío, counter del Hielo
    public Enemy enemyInsulated;    // Aislante: inmune al aturdimiento y casi al rayo, counter de la Eléctrica
    public Enemy enemyAmphibian;    // Anfibio: inmune al fuego, counter del Fuego
    [Tooltip("El director de amenazas mira tus torres y reemplaza parte de cada oleada por counters.")]
    public bool useThreatDirector = true;

    [Header("Dificultad")]
    [Tooltip("Cuánto más rápido crece la vida por oleada por cada jefe derrotado (0.35 = +35% por jefe).")]
    public float tierHpBoost = 0.35f;
    [Tooltip("Velocidad extra de los enemigos por cada jefe derrotado (0.04 = +4%).")]
    public float tierSpeedBoost = 0.04f;
    [Tooltip("Cantidad extra de enemigos por cada jefe derrotado (0.08 = +8%).")]
    public float tierCountBoost = 0.08f;
    [Tooltip("Dinero extra por enemigo por cada oleada (0.04 = +4% por oleada).")]
    public float rewardGrowthPerWave = 0.04f;
    [Tooltip("Crecimiento EXPONENCIAL extra de la vida por oleada (0.015 = x1,5% compuesto por oleada, ~x4,4 en la oleada 100). Es lo que impide ganar con pocas torres.")]
    public float healthExpGrowth = 0.015f;
    [Tooltip("Daño extra de los enemigos por oleada (0.02 = +2% por oleada).")]
    public float damageGrowthPerWave = 0.02f;
    [Tooltip("Daño extra de los enemigos por cada jefe derrotado (0.25 = +25% por jefe).")]
    public float tierDamageBoost = 0.25f;


    public int CurrentWave { get; private set; }       // 1-based, 0 = todavía no empezó
    public int TotalWaves { get { return waves != null ? waves.Length : 0; } }
    public bool WaveInProgress { get; private set; }
    public float Countdown { get; private set; }

    public event System.Action<int, bool> OnWaveStarted;   // (número de oleada, es jefe)
    public event System.Action<int, bool> OnWaveCleared;   // (número de oleada, era jefe)

    public bool IsBossWave(int wave)
    {
        return waves != null && wave > 0 && wave <= waves.Length && waves[wave - 1].isBoss;
    }

    public bool NextWaveIsBoss { get { return waves != null && CurrentWave < waves.Length && waves[CurrentWave].isBoss; } }

    /// <summary>Nivel de amenaza actual: cuántos jefes se derrotaron.</summary>
    public int CurrentTier { get { return waves != null && CurrentWave > 0 && CurrentWave <= waves.Length ? waves[CurrentWave - 1].tier : 0; } }

    bool spawning;

void Awake()
    {
        Instance = this;
        Countdown = firstWaveDelay;
        if (useGeneratedWaves) GenerateWaves();
    }

    void GenerateWaves()
    {
        ResolveEnemyPrefabs();
        if (enemyBasic == null)
        {
            Debug.LogWarning("[WaveSpawner] Falta el prefab de enemigo básico: se usan las oleadas manuales. Ejecutá 'Tower Defense > Configurar 100 oleadas con jefes'.");
            return;
        }

        int seed = waveSeed != 0 ? waveSeed : UnityEngine.Random.Range(1, int.MaxValue);
        waves = WaveGenerator.Generate(this, new System.Random(seed)).ToArray();
    }

    /// <summary>Si faltan prefabs asignados, los busca en las oleadas manuales por nombre.</summary>
    void ResolveEnemyPrefabs()
    {
        if (waves == null) return;
        foreach (var w in waves)
        {
            if (w == null || w.groups == null) continue;
            foreach (var g in w.groups)
            {
                if (g == null || g.enemyPrefab == null) continue;
                string n = g.enemyPrefab.name;
                if (enemyBasic == null && n.Contains("Basico")) enemyBasic = g.enemyPrefab;
                else if (enemyFast == null && n.Contains("Rapido")) enemyFast = g.enemyPrefab;
                else if (enemyTank == null && n.Contains("Tanque")) enemyTank = g.enemyPrefab;
                else if (enemyBoss == null && n.Contains("Jefe")) enemyBoss = g.enemyPrefab;
                else if (enemyArcher == null && n.Contains("Arquero")) enemyArcher = g.enemyPrefab;
            }
        }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver || TotalWaves == 0) return;

        if (WaveInProgress)
        {
            if (!spawning && Enemy.All.Count == 0) FinishWave();
            return;
        }

        if (CurrentWave >= TotalWaves) return;

        Countdown -= Time.deltaTime;
        if (Countdown <= 0f) StartNextWave();
    }

public void StartNextWave()
    {
        if (WaveInProgress || spawning || CurrentWave >= TotalWaves) return;
        CurrentWave++;
        WaveInProgress = true;
        spawning = true;
        var wave = waves[CurrentWave - 1];
        if (useThreatDirector) wave = ThreatDirector.Adapt(this, wave, CurrentWave);
        if (OnWaveStarted != null) OnWaveStarted(CurrentWave, wave.isBoss);
        StartCoroutine(SpawnWave(wave));
    }

IEnumerator SpawnWave(Wave wave)
    {
        // Las oleadas generadas traen su multiplicador; las manuales usan la fórmula lineal
        float hpMultiplier = wave.hpMultiplier > 0f ? wave.hpMultiplier : 1f + healthGrowthPerWave * (CurrentWave - 1);

        foreach (var group in wave.groups)
        {
            // Algunos enemigos salen en escuadra: cada unidad de la oleada son 'squadSize' enemigos, uno detrás del otro
            int squad = group.enemyPrefab != null ? Mathf.Max(1, group.enemyPrefab.squadSize) : 1;
            float stagger = group.enemyPrefab != null ? group.enemyPrefab.squadStagger : 0f;

            for (int i = 0; i < group.count; i++)
            {
                for (int k = 0; k < squad; k++)
                {
                    Spawn(group.enemyPrefab, wave, hpMultiplier);
                    if (k < squad - 1) yield return new WaitForSeconds(stagger);
                }
                yield return new WaitForSeconds(group.interval);
            }
        }

        spawning = false;
    }

void Spawn(Enemy prefab, Wave wave, float hpMultiplier)
    {
        if (prefab == null || Waypoints.Instance == null) return;
        var start = Waypoints.Instance.points[0].position;
        var e = Instantiate(prefab, start, Quaternion.identity);
        e.Init(hpMultiplier, wave.speedMultiplier, wave.rewardMultiplier, wave.damageMultiplier);

        // Cada jefe tiene un color distinto para reconocerlos
        if (wave.isBoss && prefab == enemyBoss)
        {
            int bossNumber = bossInterval > 0 ? CurrentWave / bossInterval : 1;
            e.SetBaseColor(Color.HSVToRGB((bossNumber * 0.11f) % 1f, 0.8f, 0.85f));
        }
    }

void FinishWave()
    {
        WaveInProgress = false;
        var wave = waves[CurrentWave - 1];
        GameManager.Instance.AddMoney(wave.bonus);
        if (OnWaveCleared != null) OnWaveCleared(CurrentWave, wave.isBoss);

        if (CurrentWave >= TotalWaves)
            GameManager.Instance.Victory();
        else
            Countdown = wave.isBoss ? timeBetweenWaves * 1.5f : timeBetweenWaves; // un respiro extra tras un jefe
    }
}
//[counters]

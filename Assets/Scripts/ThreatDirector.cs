using System.Collections.Generic;
using UnityEngine;

/// <summary>Tipo de daño de un impacto, para que los enemigos counter puedan resistir solo algunos.</summary>
public enum DamageKind { Physical, Area, Electric, Fire }

/// <summary>
/// Director de amenazas: mira en qué torres invirtió el jugador y, al empezar cada oleada, reemplaza una parte
/// de ella por los enemigos que le hacen counter a esas torres. Cuantas más torres de un tipo, más counters de ese tipo.
/// Empieza suave y sube con la oleada. Si el jugador ya tiene la respuesta a un counter (una evolución), pesa menos.
/// Para agregar un counter nuevo basta una línea en Build().
/// </summary>
public static class ThreatDirector
{
    public class CounterInfo
    {
        public string name;                    // nombre en plural, para el aviso
        public string targetTower;             // id de la torre a la que le hace counter
        public string targetName;              // nombre visible de esa torre
        public float cost;                     // costo en el presupuesto de la oleada (básico = 1)
        public int minWave;                    // no aparece antes de esta oleada
        public string[] answers;               // evoluciones que lo neutralizan (pesa menos si las hay)
        public float interval;                 // segundos entre unidades
        public System.Func<WaveSpawner, Enemy> prefab;
    }

    /// <summary>Texto del último ajuste (para mostrar en pantalla o mirar en la consola).</summary>
    public static string LastReport = "";

    /// <summary>Máximo de la oleada que puede pasar a ser counters (0,6 = 60 %).</summary>
    public static float maxPressure = 0.6f;
    /// <summary>Oleada en la que empieza a subir la presión y oleada en la que llega al máximo.</summary>
    public static int pressureStartWave = 3;
    public static int pressureFullWave = 43;
    /// <summary>Inversión mínima (suma de costos de torres) para que el director actúe.</summary>
    public static float minInvestment = 100f;
    /// <summary>Cuánto pesa un counter si el jugador tiene la evolución que lo neutraliza.</summary>
    public static float answeredWeight = 0.4f;

    static List<CounterInfo> counters;

    public static IList<CounterInfo> Counters { get { if (counters == null) Build(); return counters; } }

    static void Build()
    {
        counters = new List<CounterInfo>
        {
            new CounterInfo { name = "Murciélagos", targetTower = BuffCatalog.Castle,   targetName = "Castillo",
                cost = 2.4f, minWave = 5, interval = 0.7f, answers = new[] { "castle_archers" }, prefab = s => s.enemyFlyer },
            new CounterInfo { name = "Golems",      targetTower = BuffCatalog.Cannon,   targetName = "Cañón",
                cost = 5f,   minWave = 8, interval = 1.4f, answers = new string[0],              prefab = s => s.enemyGolem },
            new CounterInfo { name = "Blindados",   targetTower = BuffCatalog.Basic,    targetName = "Básica",
                cost = 3f,   minWave = 4, interval = 0.9f, answers = new[] { "basic_sniper" },   prefab = s => s.enemyArmored },
            new CounterInfo { name = "Salamandras", targetTower = BuffCatalog.Frost,    targetName = "Hielo",
                cost = 2.5f, minWave = 6, interval = 0.8f, answers = new string[0],              prefab = s => s.enemySalamander },
            new CounterInfo { name = "Aislantes",   targetTower = BuffCatalog.Electric, targetName = "Eléctrica",
                cost = 2.5f, minWave = 7, interval = 0.8f, answers = new string[0],              prefab = s => s.enemyInsulated },
            new CounterInfo { name = "Anfibios",    targetTower = BuffCatalog.Fire,     targetName = "Fuego",
                cost = 2.5f, minWave = 7, interval = 0.8f, answers = new string[0],              prefab = s => s.enemyAmphibian },
        };
    }

    static float UnitCost(WaveSpawner s, Enemy prefab)
    {
        if (prefab == s.enemyFast) return 1.3f;
        if (prefab == s.enemyTank) return 4f;
        if (prefab == s.enemyArcher) return 2.2f;
        return 1f;
    }

    /// <summary>Devuelve una copia de la oleada con parte de ella reemplazada por counters a las torres del jugador.</summary>
    public static Wave Adapt(WaveSpawner s, Wave wave, int waveNumber)
    {
        LastReport = "";
        if (s == null || wave == null || wave.groups == null) return wave;

        // inversión total y por tipo de torre
        float total = 0f;
        foreach (var t in Tower.All) if (t != null) total += t.InvestedCost;
        if (total < minInvestment) return wave;

        float pressure = Mathf.Clamp01((waveNumber - pressureStartWave) / (float)Mathf.Max(1, pressureFullWave - pressureStartWave)) * maxPressure;
        if (wave.isBoss) pressure *= 0.8f;
        if (pressure <= 0f) return wave;

        var list = Counters;
        var shares = new float[list.Count];
        float sumShare = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (waveNumber < c.minWave || c.prefab(s) == null) continue;
            float inv = 0f;
            foreach (var t in Tower.All)
            {
                if (t == null || t.TowerId != c.targetTower) continue;
                bool answered = false;
                if (t.Evolution != null && c.answers != null)
                    foreach (var a in c.answers) if (a == t.Evolution.id) answered = true;
                inv += t.InvestedCost * (answered ? answeredWeight : 1f);
            }
            shares[i] = Mathf.Min(0.45f, inv / total * pressure);
            sumShare += shares[i];
        }
        if (sumShare < 0.02f) return wave;

        // presupuesto de los grupos normales (el jefe no se toca)
        float baseBudget = 0f;
        foreach (var g in wave.groups)
            if (g.enemyPrefab != null && g.enemyPrefab != s.enemyBoss) baseBudget += g.count * UnitCost(s, g.enemyPrefab);

        var groups = new List<WaveGroup>();
        foreach (var g in wave.groups)
        {
            if (g.enemyPrefab == null || g.enemyPrefab == s.enemyBoss) { groups.Add(g); continue; }
            groups.Add(new WaveGroup { enemyPrefab = g.enemyPrefab, count = Mathf.Max(1, Mathf.RoundToInt(g.count * (1f - sumShare))), interval = g.interval });
        }

        // los grupos counter se mezclan en posiciones al azar (siempre antes del jefe)
        var rng = new System.Random(waveNumber * 7919 + 13);
        float k = Mathf.Max(0.5f, Mathf.Pow(0.985f, waveNumber - 1));
        var report = new List<string>();
        for (int i = 0; i < list.Count; i++)
        {
            if (shares[i] <= 0f) continue;
            var c = list[i];
            int count = Mathf.RoundToInt(baseBudget * shares[i] / c.cost);
            if (count < 1 && shares[i] >= 0.05f) count = 1;
            if (count < 1) continue;
            int maxIndex = groups.Count;
            if (maxIndex > 0 && groups[maxIndex - 1].enemyPrefab == s.enemyBoss) maxIndex--;
            groups.Insert(rng.Next(0, maxIndex + 1), new WaveGroup { enemyPrefab = c.prefab(s), count = count, interval = c.interval * k });
            report.Add(c.name + " x" + count + " (por " + c.targetName + " " + Mathf.RoundToInt(shares[i] / sumShare * 100f) + "%)");
        }
        if (report.Count == 0) return wave;

        LastReport = "Oleada " + waveNumber + " adaptada a tus torres: " + string.Join(", ", report.ToArray());
        Debug.Log("[ThreatDirector] " + LastReport);

        return new Wave
        {
            name = wave.name,
            groups = groups.ToArray(),
            bonus = wave.bonus,
            isBoss = wave.isBoss,
            tier = wave.tier,
            hpMultiplier = wave.hpMultiplier,
            speedMultiplier = wave.speedMultiplier,
            rewardMultiplier = wave.rewardMultiplier,
            damageMultiplier = wave.damageMultiplier
        };
    }
}

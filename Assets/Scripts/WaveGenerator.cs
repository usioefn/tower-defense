using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera las oleadas del modo infinito/100 rondas.
///
/// Dificultad:
///  - Cada oleada suma vida a los enemigos (healthGrowthPerWave).
///  - Cada JEFE derrotado sube un "nivel de amenaza" (tier). A partir de ahí la vida crece más rápido
///    por oleada, los enemigos son más veloces y vienen en mayor cantidad.
///  - Cada 'bossInterval' oleadas hay un jefe con escolta. En los jefes más avanzados aparecen 2 y 3 a la vez.
/// </summary>
public static class WaveGenerator
{
    // Puntos de "amenaza" de cada tipo, para repartir el presupuesto de la oleada
    const float BasicCost = 1f;
    const float FastCost = 1.3f;
    const float TankCost = 4f;
    const float ArcherCost = 2.2f;   // una pareja de arqueros

    public static List<Wave> Generate(WaveSpawner s, System.Random rng)
    {
        var list = new List<Wave>();
        Enemy boss = s.enemyBoss != null ? s.enemyBoss : s.enemyTank; // si no hay prefab de jefe, usa el tanque
        float hp = 1f;

        for (int w = 1; w <= s.totalWaves; w++)
        {
            int tier = (w - 1) / s.bossInterval;                  // jefes ya derrotados al empezar esta oleada
            if (w > 1) hp += s.healthGrowthPerWave * (1f + s.tierHpBoost * tier);

            bool isBoss = w % s.bossInterval == 0;
            int bossNumber = w / s.bossInterval;

            // Presupuesto de enemigos: crece con la oleada y con el nivel de amenaza
            float budget = (6f + 1.5f * (w - 1)) * (1f + s.tierCountBoost * tier);

            var wave = new Wave
            {
                name = isBoss ? "Jefe " + bossNumber : "Oleada " + w,
                isBoss = isBoss,
                tier = tier,
                hpMultiplier = hp * Mathf.Pow(1f + s.healthExpGrowth, w - 1),   // lineal por oleada x exponencial
                speedMultiplier = 1f + s.tierSpeedBoost * tier,
                rewardMultiplier = 1f + s.rewardGrowthPerWave * (w - 1),
                damageMultiplier = 1f + s.damageGrowthPerWave * (w - 1) + s.tierDamageBoost * tier,
                bonus = 25 + 3 * w + (isBoss ? 150 : 0)
            };

            // Los enemigos salen cada vez más seguido
            float k = Mathf.Max(0.25f, Mathf.Pow(0.985f, w - 1));

            var groups = new List<WaveGroup>();
            if (isBoss)
            {
                // Escolta con la mitad del presupuesto y después el/los jefe(s)
                AddEscort(groups, s, rng, budget * 0.5f, w, tier, k);
                int bossCount = 1 + (bossNumber - 1) / 4;         // 1 hasta el 4º jefe, 2 hasta el 8º, 3 después
                groups.Add(new WaveGroup { enemyPrefab = boss, count = bossCount, interval = 7f });
            }
            else
            {
                AddEscort(groups, s, rng, budget, w, tier, k);
            }

            wave.groups = groups.ToArray();
            list.Add(wave);
        }
        return list;
    }

    static void AddEscort(List<WaveGroup> groups, WaveSpawner s, System.Random rng, float budget, int w, int tier, float k)
    {
        // Los rápidos aparecen desde la oleada 3 y los tanques desde la 5, como en las oleadas originales
        float fastShare = w < 3 ? 0f : 0.15f + (float)rng.NextDouble() * 0.25f;
        float tankShare = w < 5 ? 0f : Mathf.Min(0.5f, 0.08f + 0.02f * tier + (float)rng.NextDouble() * 0.15f);
        // Los arqueros (de a 2, atacan desde lejos) aparecen desde la oleada 6
        float archerShare = w < 6 ? 0f : 0.08f + (float)rng.NextDouble() * 0.14f;
        float basicShare = Mathf.Max(0.2f, 1f - fastShare - tankShare - archerShare);

        int nBasic = Mathf.RoundToInt(budget * basicShare / BasicCost);
        int nFast = Mathf.RoundToInt(budget * fastShare / FastCost);
        int nTank = Mathf.RoundToInt(budget * tankShare / TankCost);
        int nArcher = Mathf.RoundToInt(budget * archerShare / ArcherCost);

        // Si falta algún prefab, ese presupuesto se pasa a enemigos básicos
        if (s.enemyFast == null) { nBasic += nFast; nFast = 0; }
        if (s.enemyTank == null) { nBasic += nTank * 3; nTank = 0; }
        if (s.enemyArcher == null) { nBasic += nArcher * 2; nArcher = 0; }

        // Intervalos cortos para que vayan agrupados (~0,7-0,95 unidades entre sí) y el daño en área del Cañón alcance a varios
        var parts = new List<WaveGroup>();
        if (nBasic > 0) parts.Add(new WaveGroup { enemyPrefab = s.enemyBasic, count = nBasic, interval = Mathf.Max(0.12f, 0.42f * k) });
        if (nFast > 0) parts.Add(new WaveGroup { enemyPrefab = s.enemyFast, count = nFast, interval = Mathf.Max(0.10f, 0.22f * k) });
        if (nTank > 0) parts.Add(new WaveGroup { enemyPrefab = s.enemyTank, count = nTank, interval = Mathf.Max(0.45f, 0.95f * k) });
        if (nArcher > 0) parts.Add(new WaveGroup { enemyPrefab = s.enemyArcher, count = nArcher, interval = Mathf.Max(0.35f, 0.9f * k) });

        // Orden al azar de los grupos para que las oleadas no se sientan iguales
        for (int i = parts.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = parts[i]; parts[i] = parts[j]; parts[j] = tmp;
        }
        groups.AddRange(parts);
    }
}

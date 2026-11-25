using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una evolución: al llegar al nivel máximo, cada torre puede evolucionar a una de varias ramas.
/// Cambia los valores de la torre y/o activa una habilidad nueva (los campos "Evolución" de Tower y CastleTower).
/// </summary>
public class TowerEvolution
{
    public string id;
    public string towerId;
    public string name;
    public string description;
    [Tooltip("Costo de evolucionar, como múltiplo del costo de la torre.")]
    public float costMultiplier = 2f;
    public Color tint = Color.white;      // se mezcla con el color del cuerpo para distinguir la torre evolucionada
    public Action<Tower> apply;
}

/// <summary>
/// Catálogo de evoluciones. Para agregar una nueva basta una línea en Build():
///   Add(Tower_X, "id", "Nombre", "Descripción corta", color, t => { t.damage *= 2f; });
/// </summary>
public static class TowerEvolutions
{
    static List<TowerEvolution> all;
    static readonly List<TowerEvolution> none = new List<TowerEvolution>();

    /// <summary>Evoluciones disponibles para un tipo de torre (lista vacía si no tiene).</summary>
    public static List<TowerEvolution> For(string towerId)
    {
        if (all == null) Build();
        var result = new List<TowerEvolution>();
        foreach (var e in all) if (e.towerId == towerId) result.Add(e);
        return result.Count > 0 ? result : none;
    }

    static void Add(string towerId, string id, string name, string description, Color tint, Action<Tower> apply)
    {
        all.Add(new TowerEvolution
        {
            towerId = towerId, id = id, name = name, description = description, tint = tint, apply = apply
        });
    }

    static void Build()
    {
        all = new List<TowerEvolution>();

        // ---------------- Torre Básica ----------------
        Add(BuffCatalog.Basic, "basic_double", "Doble ballesta", "2 disparos a la vez, a objetivos distintos",
            new Color(0.45f, 0.75f, 1f),
            t => { t.volleyCount = 2; });

        Add(BuffCatalog.Basic, "basic_sniper", "Francotirador", "Daño enorme y alcance largo, dispara muy lento",
            new Color(0.15f, 0.25f, 0.6f),
            t =>
            {
                t.damage *= 6f;
                t.fireRate *= 0.2f;
                t.range += 2.5f;
                t.projectileSpeed *= 2.5f;
                t.targetMode = TargetMode.Strongest;   // va por los más peligrosos
            });

        // ---------------- Cañón ----------------
        Add(BuffCatalog.Cannon, "cannon_double", "Cañón doble", "Dos obuses seguidos en cada disparo",
            new Color(1f, 0.65f, 0.3f),
            t => { t.volleyCount = 2; t.volleyDelay = 0.18f; });

        Add(BuffCatalog.Cannon, "cannon_mortar", "Mortero", "Alcance enorme y explosión grande; no apunta cerca",
            new Color(0.55f, 0.4f, 0.25f),
            t =>
            {
                t.range += 3f;
                t.minRange = 2.2f;
                t.splashRadius *= 1.6f;
                t.damage *= 1.5f;
                t.fireRate *= 0.75f;
                t.projectileSpeed *= 0.8f;
            });

        Add(BuffCatalog.Cannon, "cannon_cluster", "Bomba de racimo", "El obús se parte en 3 explosiones chicas",
            new Color(0.9f, 0.3f, 0.2f),
            t => { t.clusterCount = 3; t.clusterSplashFactor = 0.6f; t.clusterDamageFactor = 0.65f; });

        // ---------------- Torre de Hielo ----------------
        Add(BuffCatalog.Frost, "frost_blizzard", "Ventisca", "Aura: ralentiza a todos los enemigos en su radio",
            new Color(0.7f, 0.95f, 1f),
            t =>
            {
                t.auraSlow = true;
                t.range += 0.7f;
                t.slowMultiplier = 0.6f;
            });

        Add(BuffCatalog.Frost, "frost_freeze", "Congelación", "Cada 4 disparos congela 1 s; los congelados reciben más daño",
            new Color(0.3f, 0.6f, 1f),
            t =>
            {
                t.freezeEvery = 4;
                t.freezeDuration = 1f;
                t.damage *= 1.5f;
            });

        // ---------------- Torre Eléctrica ----------------
        Add(BuffCatalog.Electric, "elec_storm", "Tormenta", "El rayo salta a 5 enemigos en vez de 2",
            new Color(1f, 0.95f, 0.4f),
            t =>
            {
                t.chainCount = 5;
                t.chainDamageMult = 0.6f;
                t.chainRange = 2.2f;
            });

        Add(BuffCatalog.Electric, "elec_tesla", "Tesla", "Rayo continuo al más fuerte, con daño creciente",
            new Color(0.75f, 0.5f, 1f),
            t =>
            {
                t.beamMode = true;
                t.range += 0.5f;
                t.targetMode = TargetMode.Strongest;
            });

        // ---------------- Torre de Fuego ----------------
        Add(BuffCatalog.Fire, "fire_flame", "Lanzallamas", "Más alcance; cada golpe suma daño a la quemadura",
            new Color(1f, 0.55f, 0.1f),
            t =>
            {
                t.range += 0.8f;
                t.burnStacking = true;
                t.fireRate *= 1.2f;
            });

        Add(BuffCatalog.Fire, "fire_volcano", "Volcán", "Al morir un enemigo quemado explota y prende a los de alrededor",
            new Color(0.8f, 0.15f, 0.1f),
            t =>
            {
                t.volcano = true;
                t.burnDuration *= 1.3f;
                t.damage *= 1.2f;
            });

        // ---------------- Castillo ----------------
        Add(BuffCatalog.Castle, "castle_fortress", "Fortaleza", "5 soldados muy resistentes y, a veces, un caballero",
            new Color(0.95f, 0.8f, 0.35f),
            t => { if (t.castle != null) t.castle.BecomeFortress(); });

        Add(BuffCatalog.Castle, "castle_archers", "Cuartel de arqueros", "Soldados arqueros: disparan de lejos y no bloquean",
            new Color(0.4f, 0.8f, 0.5f),
            t => { if (t.castle != null) t.castle.BecomeArchery(); });

        Add(BuffCatalog.Castle, "castle_crypt", "Cripta", "Cada soldado caído deja un esqueleto más fuerte",
            new Color(0.5f, 0.35f, 0.6f),
            t => { if (t.castle != null) t.castle.BecomeCrypt(); });
    }
}

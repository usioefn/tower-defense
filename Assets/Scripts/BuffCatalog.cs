using System;
using System.Collections.Generic;

public enum BuffScope { Global, Tower }

/// <summary>
/// Conjunto de modificadores acumulados. Las torres lo consultan al disparar,
/// así un beneficio se aplica también a las torres ya construidas.
/// </summary>
public class Modifiers
{
    public static readonly Modifiers Identity = new Modifiers();

    // Multiplicadores (1 = sin cambio)
    public float damageMult = 1f;
    public float fireRateMult = 1f;
    public float rangeMult = 1f;
    public float splashMult = 1f;
    public float burnDpsMult = 1f;
    public float projectileSpeedMult = 1f;

    // Sumas
    public int extraPierce;              // enemigos extra que atraviesa cada disparo
    public int extraChain;               // saltos extra del rayo (solo si la torre ya encadena)
    public float stunBonus;              // segundos extra de aturdimiento (solo si la torre ya aturde)
    public float burnDurationBonus;      // segundos extra de quemadura (solo si la torre ya quema)
    public float slowStrengthBonus;      // cuánto más fuerte frena (resta al multiplicador de velocidad)
    public float slowDurationBonus;      // segundos extra de ralentización
    public int extraSoldiers;            // soldados extra del Castillo
    public float skeletonChance;         // probabilidad (0 a 1) de que un soldado caído deje un esqueleto


    // Solo tienen efecto en los modificadores globales
    public float rewardMult = 1f;        // dinero por enemigo
    public float xpMult = 1f;            // experiencia por enemigo

    public static Modifiers Combine(Modifiers a, Modifiers b)
    {
        return new Modifiers
        {
            damageMult = a.damageMult * b.damageMult,
            fireRateMult = a.fireRateMult * b.fireRateMult,
            rangeMult = a.rangeMult * b.rangeMult,
            splashMult = a.splashMult * b.splashMult,
            burnDpsMult = a.burnDpsMult * b.burnDpsMult,
            projectileSpeedMult = a.projectileSpeedMult * b.projectileSpeedMult,
            extraPierce = a.extraPierce + b.extraPierce,
            extraChain = a.extraChain + b.extraChain,
            stunBonus = a.stunBonus + b.stunBonus,
            burnDurationBonus = a.burnDurationBonus + b.burnDurationBonus,
            slowStrengthBonus = a.slowStrengthBonus + b.slowStrengthBonus,
            slowDurationBonus = a.slowDurationBonus + b.slowDurationBonus,
            extraSoldiers = a.extraSoldiers + b.extraSoldiers,
            skeletonChance = a.skeletonChance + b.skeletonChance,
            rewardMult = a.rewardMult * b.rewardMult,
            xpMult = a.xpMult * b.xpMult
        };
    }
}

/// <summary>Un beneficio que se puede elegir en la ruleta.</summary>
public class Buff
{
    public string id;
    public string name;
    public string description;
    public BuffScope scope;
    public string towerId;      // solo si scope == Tower (nombre del prefab, ej. "Tower_Electrica")
    public string towerName;    // nombre para mostrar (ej. "Torre Eléctrica")
    public int maxStacks = 3;   // cuántas veces se puede elegir
    public Action<Modifiers> apply; // se ejecuta una vez por cada nivel del beneficio
}

/// <summary>
/// CATÁLOGO DE BENEFICIOS. Para agregar uno nuevo, sumá una línea acá:
///   Global(id, nombre, descripción, máximo, m => m.campo ...)
///   ForTower(towerId, towerName, id, nombre, descripción, máximo, m => m.campo ...)
/// Los campos disponibles están en la clase Modifiers.
/// </summary>
public static class BuffCatalog
{
    public const string Basic = "Tower_Basica";
    public const string Cannon = "Tower_Canon";
    public const string Frost = "Tower_Hielo";
    public const string Electric = "Tower_Electrica";
    public const string Fire = "Tower_Fuego";
    public const string Castle = "Tower_Castillo";


    public static void Register(List<Buff> list)
    {
        // ================= Para TODAS las torres =================
        list.Add(Global("pierce", "Balas perforantes", "Los disparos de todas las torres atraviesan 1 enemigo más.", 3,
            m => m.extraPierce += 1));
        list.Add(Global("damage", "Munición reforzada", "+15% de daño en todas las torres.", 5,
            m => m.damageMult *= 1.15f));
        list.Add(Global("firerate", "Gatillo veloz", "+12% de cadencia de disparo en todas las torres.", 5,
            m => m.fireRateMult *= 1.12f));
        list.Add(Global("range", "Mira telescópica", "+10% de alcance en todas las torres.", 4,
            m => m.rangeMult *= 1.10f));
        list.Add(Global("speed", "Proyectiles veloces", "+20% de velocidad de proyectil en todas las torres.", 3,
            m => m.projectileSpeedMult *= 1.20f));
        list.Add(Global("loot", "Saqueo", "+25% de dinero por cada enemigo derrotado.", 3,
            m => m.rewardMult *= 1.25f));
        list.Add(Global("xp", "Estudioso", "+25% de experiencia por cada enemigo derrotado.", 3,
            m => m.xpMult *= 1.25f));

        // ================= Torre Básica =================
        list.Add(ForTower(Basic, "Torre Básica", "basic_rate", "Tirador experto", "La Torre Básica dispara +35% más rápido.", 3,
            m => m.fireRateMult *= 1.35f));
        list.Add(ForTower(Basic, "Torre Básica", "basic_dmg", "Calibre pesado", "La Torre Básica hace +40% de daño.", 3,
            m => m.damageMult *= 1.40f));

        // ================= Cañón =================
        list.Add(ForTower(Cannon, "Cañón", "cannon_splash", "Metralla", "Las explosiones del Cañón tienen +25% de radio.", 3,
            m => m.splashMult *= 1.25f));
        list.Add(ForTower(Cannon, "Cañón", "cannon_dmg", "Carga explosiva", "El Cañón hace +35% de daño.", 3,
            m => m.damageMult *= 1.35f));

        // ================= Torre de Hielo =================
        list.Add(ForTower(Frost, "Torre de Hielo", "frost_strength", "Escarcha profunda", "La ralentización de hielo es más fuerte (-10% de velocidad extra).", 3,
            m => m.slowStrengthBonus += 0.10f));
        list.Add(ForTower(Frost, "Torre de Hielo", "frost_duration", "Congelación prolongada", "La ralentización de hielo dura +1 segundo.", 3,
            m => m.slowDurationBonus += 1f));

        // ================= Torre Eléctrica =================
        list.Add(ForTower(Electric, "Torre Eléctrica", "elec_stun", "Sobrecarga", "El aturdimiento de la Torre Eléctrica dura +0,25 s.", 4,
            m => m.stunBonus += 0.25f));
        list.Add(ForTower(Electric, "Torre Eléctrica", "elec_chain", "Rayo encadenado", "El rayo de la Torre Eléctrica salta a 1 enemigo más.", 3,
            m => m.extraChain += 1));

        // ================= Torre de Fuego =================
        list.Add(ForTower(Fire, "Torre de Fuego", "fire_burn", "Brasas ardientes", "La quemadura de la Torre de Fuego hace +40% de daño.", 3,
            m => m.burnDpsMult *= 1.40f));
        list.Add(ForTower(Fire, "Torre de Fuego", "fire_duration", "Incendio", "La quemadura de la Torre de Fuego dura +1,5 s.", 3,
            m => m.burnDurationBonus += 1.5f));

        // ================= Castillo =================
        list.Add(ForTower(Castle, "Castillo", "castle_soldiers", "Reclutas", "El Castillo suelta +1 soldado más.", 4,
            m => m.extraSoldiers += 1));
        list.Add(ForTower(Castle, "Castillo", "castle_skeleton", "Nigromancia", "+12.5% de probabilidad de que un soldado caído se levante como esqueleto.", 4,
            m => m.skeletonChance += 0.125f));

    }

    static Buff Global(string id, string name, string description, int maxStacks, Action<Modifiers> apply)
    {
        return new Buff { id = id, name = name, description = description, scope = BuffScope.Global, maxStacks = maxStacks, apply = apply };
    }

    static Buff ForTower(string towerId, string towerName, string id, string name, string description, int maxStacks, Action<Modifiers> apply)
    {
        return new Buff
        {
            id = id, name = name, description = description, scope = BuffScope.Tower,
            towerId = towerId, towerName = towerName, maxStacks = maxStacks, apply = apply
        };
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Comportamiento del Castillo (va junto al componente Tower en el mismo prefab).
/// Suelta soldados sobre el camino, cerca del castillo, para que los enemigos se entretengan peleando.
///
///  - Subir de nivel el castillo (Tower.Upgrade) aumenta la vida y el daño de los soldados.
///  - Beneficio "Reclutas": +1 soldado.
///  - Beneficio "Nigromancia": cuando un soldado muere, probabilidad de que aparezca un esqueleto
///    (con menos vida y daño) en su lugar.
///  - Los soldados caídos reaparecen en el castillo pasado 'respawnTime' segundos.
/// </summary>
[RequireComponent(typeof(Tower))]
public class CastleTower : MonoBehaviour
{
    [Header("Soldados")]
    public Soldier soldierPrefab;
    public int baseSoldiers = 3;
    [Tooltip("Vida de un soldado a nivel 1.")]
    public float soldierHealth = 100f;
    [Tooltip("Segundos hasta que un soldado caído reaparece.")]
    public float respawnTime = 10f;
    [Tooltip("Separación entre soldados sobre el camino.")]
    public float soldierSpacing = 0.5f;
    [Tooltip("Cuánto crece la vida de los soldados por cada nivel del castillo (el daño crece con Tower.Upgrade).")]
    public float healthPerLevel = 1.5f;

    [Header("Esqueletos")]
    [Range(0f, 1f)] public float skeletonHealthFactor = 0.4f;
    [Range(0f, 1f)] public float skeletonDamageFactor = 0.5f;
    [Tooltip("Tope de esqueletos vivos a la vez, para no llenar el camino.")]
    public int maxSkeletons = 12;

    [Header("Evolución (los completa TowerEvolutions)")]
    [Tooltip("Multiplica el daño de los soldados (Cuartel de arqueros).")]
    public float soldierDamageMult = 1f;
    [Tooltip("Fortaleza: probabilidad de que un soldado salga como caballero (grande y resistente).")]
    public float knightChance = 0f;
    public float knightHealthMult = 3f;
    public float knightDamageMult = 1.6f;
    [Tooltip("Cuartel de arqueros: los soldados disparan desde un costado del camino y no bloquean.")]
    public bool archers = false;
    [Tooltip("Cripta: cada soldado caído deja siempre un esqueleto (más fuerte).")]
    public bool crypt = false;

    Tower tower;
    Soldier[] slots = new Soldier[0];   // un puesto por soldado; null = muerto o por reaparecer
    float[] timers = new float[0];      // segundos para reaparecer cada puesto

    int lastLevel;
    int lastBuffVersion = -1;

    // Camino (polilínea) y punto del camino más cercano al castillo
    bool pathReady;
    Vector3[] pts;
    float[] cum;
    float total;
    float castleS;

    void Awake()
    {
        tower = GetComponent<Tower>();
    }

    // ---------- Estadísticas ----------

    public int TargetSoldiers { get { return baseSoldiers + tower.Mods.extraSoldiers; } }

    public float SkeletonChance { get { return Mathf.Clamp01(tower.Mods.skeletonChance); } }

    public float SoldierMaxHealth { get { return soldierHealth * Mathf.Pow(healthPerLevel, tower.Level - 1); } }

    public float SoldierDamage { get { return tower.EffectiveDamage * soldierDamageMult; } }

    /// <summary>Texto para el panel de la torre seleccionada.</summary>
    public string InfoText()
    {
        return "Soldados " + TargetSoldiers + "   Vida " + SoldierMaxHealth.ToString("0") + "   Daño " + SoldierDamage.ToString("0.#")
            + "\nEsqueleto " + Mathf.FloorToInt(SkeletonChance * 100f) + "%   Reaparecen en " + respawnTime.ToString("0") + "s";
    }

    // ---------- Evoluciones ----------

    /// <summary>Fortaleza: 5 soldados base, mucho más resistentes, y a veces sale un caballero grande.</summary>
    public void BecomeFortress()
    {
        baseSoldiers = 5;
        soldierHealth *= 2.5f;
        knightChance = 0.2f;
        lastLevel = -1;   // fuerza a actualizar las unidades que ya están en el camino
    }

    /// <summary>Cuartel de arqueros: los soldados disparan desde un costado del camino, hacen más daño y no frenan tanto a los enemigos.</summary>
    public void BecomeArchery()
    {
        archers = true;
        soldierDamageMult = 1.6f;
        lastLevel = -1;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null) slots[i].SetPost(PostFor(i, slots.Length));
    }

    /// <summary>Cripta: cada soldado caído deja un esqueleto (sin necesitar el beneficio), más fuerte. Nigromancia da chance de un segundo.</summary>
    public void BecomeCrypt()
    {
        crypt = true;
        skeletonHealthFactor = 0.8f;
        skeletonDamageFactor = 0.9f;
        maxSkeletons += 6;
        lastLevel = -1;
    }

    // ---------- Ciclo de vida ----------

    void Update()
    {
        if (soldierPrefab == null || !EnsurePath()) return;

        int target = TargetSoldiers;
        if (slots.Length != target) Resize(target);

        // Subió de nivel o se eligió un beneficio: se actualizan también las unidades que ya están en el camino
        var bm = BuffManager.Instance;
        int version = bm != null ? bm.Version : 0;
        if (tower.Level != lastLevel || version != lastBuffVersion)
        {
            lastLevel = tower.Level;
            lastBuffVersion = version;
            RefreshStats();
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) continue;
            timers[i] -= Time.deltaTime;
            if (timers[i] <= 0f) Spawn(i);
        }
    }

    void OnDestroy()
    {
        // Si se vende el castillo, sus tropas desaparecen con él
        var mine = new List<Soldier>();
        foreach (var s in Soldier.All) if (s != null && s.Owner == this) mine.Add(s);
        foreach (var s in mine) Destroy(s.gameObject);
    }

    void Resize(int count)
    {
        var newSlots = new Soldier[count];
        var newTimers = new float[count];
        for (int i = 0; i < count && i < slots.Length; i++)
        {
            newSlots[i] = slots[i];
            newTimers[i] = timers[i];
        }
        slots = newSlots;
        timers = newTimers; // los puestos nuevos arrancan en 0: el recluta sale enseguida

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null) slots[i].SetPost(PostFor(i, slots.Length));
    }

    void Spawn(int index)
    {
        bool knight = knightChance > 0f && Random.value < knightChance;
        var s = Instantiate(soldierPrefab, transform.position, Quaternion.identity);
        s.Init(this, PostFor(index, slots.Length), SoldierMaxHealth * (knight ? knightHealthMult : 1f), SoldierDamage * (knight ? knightDamageMult : 1f), false);
        s.SetRole(knight, archers && !knight);
        slots[index] = s;
    }

    void RefreshStats()
    {
        foreach (var s in Soldier.All)
        {
            if (s == null || s.Owner != this || s.IsDead) continue;
            float hp = SoldierMaxHealth * (s.IsSkeleton ? skeletonHealthFactor : 1f) * (s.IsKnight ? knightHealthMult : 1f);
            float dmg = SoldierDamage * (s.IsSkeleton ? skeletonDamageFactor : 1f) * (s.IsKnight ? knightDamageMult : 1f);
            s.SetStats(hp, dmg);
            if (!s.IsSkeleton) s.SetRole(s.IsKnight, archers && !s.IsKnight);   // los caballeros siguen peleando cuerpo a cuerpo
        }
    }

    // ---------- Muerte de soldados y esqueletos ----------

    /// <summary>Lo llama el soldado al morir.</summary>
    public void OnSoldierDied(Soldier s, Vector3 where)
    {
        if (s.IsSkeleton) return; // un esqueleto que cae no deja nada más

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == s)
            {
                slots[i] = null;
                timers[i] = respawnTime;
                break;
            }
        }

        if (soldierPrefab == null) return;
        // Cripta: siempre deja uno; con Nigromancia puede dejar un segundo
        int n = (crypt ? 1 : 0) + (Random.value < SkeletonChance ? 1 : 0);
        for (int k = 0; k < n && CountSkeletons() < maxSkeletons; k++)
            SpawnSkeleton(where + (Vector3)(Random.insideUnitCircle * 0.15f));
    }

    void SpawnSkeleton(Vector3 where)
    {
        var sk = Instantiate(soldierPrefab, where, Quaternion.identity);
        sk.Init(this, where, SoldierMaxHealth * skeletonHealthFactor, SoldierDamage * skeletonDamageFactor, true);
    }

    int CountSkeletons()
    {
        int n = 0;
        foreach (var s in Soldier.All) if (s != null && s.Owner == this && s.IsSkeleton && !s.IsDead) n++;
        return n;
    }

    // ---------- Camino ----------

    /// <summary>Puesto del soldado 'index' de 'count': en fila sobre el camino, centrada en el punto más cercano al castillo.</summary>
    Vector3 PostFor(int index, int count)
    {
        float s = castleS + (index - (count - 1) / 2f) * soldierSpacing;
        Vector3 p = PathPosition(s);
        if (archers)
        {
            // Los arqueros se paran al costado del camino, del lado del castillo: no frenan a los enemigos
            Vector3 toCastle = transform.position - p;
            toCastle.z = 0f;
            if (toCastle.sqrMagnitude > 0.0001f) p += toCastle.normalized * Mathf.Min(0.7f, toCastle.magnitude * 0.6f);
        }
        return p;
    }

    bool EnsurePath()
    {
        if (pathReady) return true;

        var wp = Waypoints.Instance;
        if (wp == null || wp.points == null || wp.points.Length < 2) return false;

        int n = wp.points.Length;
        pts = new Vector3[n];
        cum = new float[n];
        for (int i = 0; i < n; i++)
        {
            pts[i] = wp.points[i].position;
            pts[i].z = 0f;
            if (i > 0) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        }
        total = cum[n - 1];

        // Punto del camino más cercano al castillo
        Vector2 c = transform.position;
        float best = float.MaxValue;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 a = pts[i];
            Vector2 ab = (Vector2)pts[i + 1] - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / len2) : 0f;
            float d = (a + ab * t - c).sqrMagnitude;
            if (d < best)
            {
                best = d;
                castleS = cum[i] + t * Mathf.Sqrt(len2);
            }
        }

        pathReady = true;
        return true;
    }

    Vector3 PathPosition(float s)
    {
        s = Mathf.Clamp(s, 0f, total);
        for (int i = 1; i < pts.Length; i++)
        {
            if (s <= cum[i])
            {
                float seg = cum[i] - cum[i - 1];
                float t = seg > 0f ? (s - cum[i - 1]) / seg : 0f;
                return Vector3.Lerp(pts[i - 1], pts[i], t);
            }
        }
        return pts[pts.Length - 1];
    }
}

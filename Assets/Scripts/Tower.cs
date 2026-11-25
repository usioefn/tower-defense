using System.Collections.Generic;
using UnityEngine;

/// <summary>A qué enemigo dentro del alcance le dispara la torre.</summary>
public enum TargetMode { First, Last, Strongest }

/// <summary>
/// Torre: busca un enemigo dentro del alcance según su modo de objetivo, apunta y dispara.
/// </summary>
public class Tower : MonoBehaviour
{
    [Header("Info")]
    public string towerName = "Torre";
    public int cost = 50;

    [Header("Combate")]
    public float range = 2.6f;
    public float fireRate = 1.5f;   // disparos por segundo
    public float damage = 10f;
    [Tooltip("First = el más adelantado, Last = el más atrasado, Strongest = el que tiene más vida")]
    public TargetMode targetMode = TargetMode.First;
    [Tooltip("Si está activo, esta torre no puede apuntar ni dañar a enemigos voladores (artillería).")]
    public bool groundOnly = false;

    [Header("Efectos especiales")]
    public float splashRadius = 0f;     // > 0 = daño en área
    public float slowMultiplier = 1f;   // < 1 = ralentiza
    public float slowDuration = 0f;

    [Header("Cadena eléctrica")]
    public int chainCount = 0;        // cuántos enemigos extra golpea el rayo
    public float chainRange = 1.8f;   // distancia máxima de cada salto
    public float chainDamageMult = 1f; // fracción del daño que hace cada salto del rayo
    public float stunDuration = 0f;   // segundos de aturdimiento

    [Header("Fuego (perforación y quemadura)")]
    public int pierceCount = 0;       // cuántos enemigos atraviesa el disparo (0 = no perfora)
    public float burnDps = 0f;        // daño por segundo de la quemadura
    public float burnDuration = 0f;   // segundos que dura la quemadura

    [Header("Proyectil")]
    public Projectile projectilePrefab;
    public float projectileSpeed = 10f;
    public Color projectileColor = Color.yellow;

    [Header("Evolución (los completa TowerEvolutions al evolucionar)")]
    [Tooltip("Proyectiles por disparo. Con volleyDelay = 0 salen juntos a objetivos distintos; con retraso salen uno tras otro.")]
    public int volleyCount = 1;
    public float volleyDelay = 0f;
    [Tooltip("No puede apuntar a enemigos más cerca que esto (mortero).")]
    public float minRange = 0f;
    [Tooltip("El proyectil se divide en N explosiones chicas al caer (bomba de racimo).")]
    public int clusterCount = 0;
    public float clusterSplashFactor = 0.55f;
    public float clusterDamageFactor = 0.8f;
    [Tooltip("No dispara: ralentiza a todos los enemigos dentro del alcance (ventisca).")]
    public bool auraSlow = false;
    [Tooltip("Cada N disparos congela del todo al objetivo (0 = nunca).")]
    public int freezeEvery = 0;
    public float freezeDuration = 1f;
    [Tooltip("Rayo continuo sobre un enemigo, con daño que crece mientras lo mantiene (tesla).")]
    public bool beamMode = false;
    public float beamDpsFactor = 1.6f;
    public float beamRampTime = 4f;
    public float beamRampBonus = 2.5f;
    [Tooltip("Cada golpe suma daño a la quemadura (lanzallamas).")]
    public bool burnStacking = false;
    [Tooltip("Cuando muere un enemigo quemado, explota y prende a los de alrededor (volcán).")]
    public bool volcano = false;
    public float volcanoRadius = 1.4f;

    [Header("Referencias")]
    public Transform head;
    public Transform firePoint;
    public Transform rangeIndicator;

    [Header("Mejoras")]
    public int maxLevel = 3;

    [Tooltip("Identificador para los beneficios por tipo de torre. Si está vacío se usa el nombre del prefab.")]
    public string towerId;

    [HideInInspector] public BuildNode node;

    public int Level { get; private set; } = 1;
    public int InvestedCost { get; private set; }

    public bool CanUpgrade { get { return Level < maxLevel; } }

    /// <summary>Si esta torre es un Castillo, su componente (el castillo no dispara: suelta soldados).</summary>
    public CastleTower castle { get; private set; }


    // ---------- Registro de torres construidas y beneficios ----------

    public static readonly List<Tower> All = new List<Tower>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    int lastBuffVersion;

    /// <summary>Identifica el tipo de torre (también funciona sobre el prefab).</summary>
    public string TowerId
    {
        get { return string.IsNullOrEmpty(towerId) ? name.Replace("(Clone)", "").Trim() : towerId; }
    }

    public Modifiers Mods
    {
        get { return BuffManager.Instance != null ? BuffManager.Instance.Get(TowerId) : Modifiers.Identity; }
    }

    // Stats con los beneficios aplicados (los campos de arriba son los valores base)
    public float EffectiveRange { get { return range * Mods.rangeMult; } }
    public float EffectiveDamage { get { return damage * Mods.damageMult; } }
    public float EffectiveFireRate { get { return fireRate * Mods.fireRateMult; } }
    public int UpgradeCost { get { return CanUpgrade ? Mathf.RoundToInt(cost * 0.75f * Level) : 0; } }
    public int SellValue { get { return Mathf.RoundToInt(InvestedCost * 0.6f); } }

    Enemy target;
    float cooldown;

void Awake()
    {
        InvestedCost = cost;
        castle = GetComponent<CastleTower>();
        if (string.IsNullOrEmpty(towerId)) towerId = name.Replace("(Clone)", "").Trim();
        lastBuffVersion = BuffManager.Instance != null ? BuffManager.Instance.Version : 0;
        UpdateRangeVisual();
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

void Update()
    {
        // Si se eligió un beneficio, refrescar el círculo de alcance
        var buffs = BuffManager.Instance;
        if (buffs != null && buffs.Version != lastBuffVersion)
        {
            lastBuffVersion = buffs.Version;
            UpdateRangeVisual();
        }

        // Se reevalúa cada frame: con "Último" o "Más fuerte" el mejor objetivo cambia aunque el actual siga en rango
        if (castle != null) return; // el castillo no dispara: pelean sus soldados
        if (auraSlow) { UpdateAura(); return; }
        if (beamMode) { UpdateBeam(); return; }
        AcquireTarget();

        cooldown -= Time.deltaTime;
        if (target == null) return;

        AimAt(target.transform.position);

        if (cooldown <= 0f)
        {
            Shoot();
            cooldown = 1f / EffectiveFireRate;
        }
    }

bool InRange(Enemy e)
    {
        if (groundOnly && e.isFlying) return false;
        Vector2 d = e.transform.position - transform.position;
        float r = EffectiveRange;
        float sq = d.sqrMagnitude;
        return sq <= r * r && sq >= minRange * minRange;
    }

    void AcquireTarget()
    {
        Enemy best = null;
        float bestScore = float.MinValue;
        foreach (var e in Enemy.All)
        {
            if (e == null || e.IsDead || !InRange(e)) continue;
            float score = TargetScore(e);
            if (score > bestScore) { bestScore = score; best = e; }
        }
        target = best;
    }

    /// <summary>Puntaje del enemigo según el modo de objetivo: gana el más alto.</summary>
    float TargetScore(Enemy e)
    {
        switch (targetMode)
        {
            case TargetMode.Last: return e.DistanceToGoal;
            case TargetMode.Strongest: return e.Health;
            default: return -e.DistanceToGoal;
        }
    }

    public void CycleTargetMode()
    {
        targetMode = targetMode == TargetMode.Strongest ? TargetMode.First : targetMode + 1;
    }

    public static string TargetModeName(TargetMode mode)
    {
        switch (mode)
        {
            case TargetMode.Last: return "Último";
            case TargetMode.Strongest: return "Más fuerte";
            default: return "Primero";
        }
    }

    void AimAt(Vector3 worldPos)
    {
        if (head == null) return;
        Vector3 dir = worldPos - transform.position;
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion desired = Quaternion.LookRotation(Vector3.forward, dir);
        head.rotation = Quaternion.RotateTowards(head.rotation, desired, 900f * Time.deltaTime);
    }

void Shoot()
    {
        if (projectilePrefab == null) return;
        if (volleyCount <= 1) { ShootAt(target, 0f); return; }

        // Salva con retraso (cañón doble): uno tras otro al mismo objetivo
        if (volleyDelay > 0f) { StartCoroutine(StaggeredVolley()); return; }

        // Salva simultánea (doble ballesta): a objetivos distintos si hay
        var targets = TopTargets(volleyCount);
        for (int i = 0; i < volleyCount; i++)
        {
            Enemy t = targets.Count > 0 ? targets[Mathf.Min(i, targets.Count - 1)] : target;
            ShootAt(t, i % 2 == 0 ? -0.1f : 0.1f);
        }
    }

    System.Collections.IEnumerator StaggeredVolley()
    {
        for (int i = 0; i < volleyCount; i++)
        {
            if (target == null || target.IsDead) AcquireTarget();   // si murió, sigue con el próximo
            if (target != null) ShootAt(target, i % 2 == 0 ? -0.1f : 0.1f);
            yield return new WaitForSeconds(volleyDelay);
        }
    }

    /// <summary>Los N mejores objetivos dentro del alcance según el modo de objetivo.</summary>
    List<Enemy> TopTargets(int n)
    {
        var list = new List<Enemy>();
        foreach (var e in Enemy.All) if (e != null && !e.IsDead && InRange(e)) list.Add(e);
        list.Sort((a, b) => TargetScore(b).CompareTo(TargetScore(a)));
        if (list.Count > n) list.RemoveRange(n, list.Count - n);
        return list;
    }

    int shotCounter;

    void ShootAt(Enemy t, float lateral)
    {
        if (projectilePrefab == null || t == null) return;
        var m = Mods;

        Vector3 pos = firePoint != null ? firePoint.position : transform.position;
        if (lateral != 0f && head != null) pos += head.right * lateral;
        Quaternion rot = head != null ? head.rotation : Quaternion.identity;
        var p = Instantiate(projectilePrefab, pos, rot);

        // Efectos propios de la torre + beneficios. Los bonus de un efecto solo cuentan si la torre ya tiene ese efecto.
        float splash = splashRadius > 0f ? splashRadius * m.splashMult : 0f;
        float slowMul = slowDuration > 0f ? Mathf.Clamp(slowMultiplier - m.slowStrengthBonus, 0.15f, 1f) : slowMultiplier;
        float slowDur = slowDuration > 0f ? slowDuration + m.slowDurationBonus : 0f;
        int chain = chainCount > 0 ? chainCount + m.extraChain : 0;
        float stun = stunDuration > 0f ? stunDuration + m.stunBonus : 0f;
        float bDps = burnDps > 0f ? burnDps * m.burnDpsMult : 0f;
        float bDur = burnDps > 0f ? burnDuration + m.burnDurationBonus : 0f;

        // Perforación: cantidad máxima de enemigos que atraviesa (0 = no perfora)
        int pierce = pierceCount;
        if (m.extraPierce > 0) pierce = (pierceCount > 0 ? pierceCount : 1) + m.extraPierce;

        p.Init(t, EffectiveDamage, projectileSpeed * m.projectileSpeedMult, splash, slowMul, slowDur, projectileColor);
        p.SetChain(chain, chainRange, chainDamageMult, stun);
        p.SetBurn(bDps, bDur, burnStacking);
        p.SetPierce(pierce, EffectiveRange + 0.6f);
        p.SetGroundOnly(groundOnly);

        if (clusterCount > 0 && splash > 0f) p.SetCluster(clusterCount, clusterSplashFactor, clusterDamageFactor);
        if (freezeEvery > 0)
        {
            shotCounter++;
            if (shotCounter % freezeEvery == 0) p.SetFreeze(freezeDuration);
        }
    }

public bool Upgrade()
    {
        if (!CanUpgrade) return false;
        int c = UpgradeCost;
        if (GameManager.Instance == null || !GameManager.Instance.Spend(c)) return false;

        InvestedCost += c;
        Level++;
        damage *= 1.5f;
        burnDps *= 1.5f;
        range += 0.3f;
        fireRate *= 1.15f;
        UpdateRangeVisual();
        return true;
    }

    // ---------- Evolución ----------

    public TowerEvolution Evolution { get; private set; }

    /// <summary>¿Llegó al nivel máximo, todavía no evolucionó y tiene ramas disponibles?</summary>
    public bool CanEvolve { get { return Level >= maxLevel && Evolution == null && TowerEvolutions.For(TowerId).Count > 0; } }

    public int EvolutionCost(TowerEvolution e) { return Mathf.RoundToInt(cost * e.costMultiplier); }

    public bool Evolve(TowerEvolution e)
    {
        if (!CanEvolve || e == null || e.towerId != TowerId) return false;
        int c = EvolutionCost(e);
        if (GameManager.Instance == null || !GameManager.Instance.Spend(c)) return false;

        InvestedCost += c;
        Evolution = e;
        if (e.apply != null) e.apply(this);

        // El cuerpo toma el color de la rama para distinguir la torre evolucionada
        Transform bodyT = transform.Find("Body");
        var sr = bodyT != null ? bodyT.GetComponent<SpriteRenderer>() : null;
        if (sr != null) sr.color = Color.Lerp(sr.color, e.tint, 0.55f);

        UpdateRangeVisual();
        return true;
    }

    // Ventisca: ralentiza a todos los enemigos dentro del alcance, sin disparar
    float auraTimer;
    Transform auraFx;

    void UpdateAura()
    {
        EnsureAuraFx();
        auraTimer -= Time.deltaTime;
        if (auraTimer > 0f) return;
        auraTimer = 0.15f;

        var m = Mods;
        float mul = Mathf.Clamp(slowMultiplier - m.slowStrengthBonus, 0.15f, 1f);
        float dur = 0.4f + m.slowDurationBonus;
        foreach (var e in Enemy.All)
            if (e != null && !e.IsDead && InRange(e)) e.ApplySlow(mul, dur);
    }

    void EnsureAuraFx()
    {
        if (auraFx != null || rangeIndicator == null) return;
        var src = rangeIndicator.GetComponent<SpriteRenderer>();
        if (src == null) return;
        var go = new GameObject("AuraFx");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite;
        sr.color = new Color(0.55f, 0.9f, 1f, 0.12f);
        sr.sortingOrder = 1;
        auraFx = go.transform;
        UpdateRangeVisual();
    }

    // Tesla: rayo continuo sobre un enemigo; el daño crece mientras no cambie de objetivo
    Enemy beamTarget;
    float beamTime;
    Transform beamFx;

    void UpdateBeam()
    {
        AcquireTarget();
        if (target == null)
        {
            beamTarget = null;
            beamTime = 0f;
            if (beamFx != null) beamFx.gameObject.SetActive(false);
            return;
        }

        if (target != beamTarget) { beamTarget = target; beamTime = 0f; }   // al cambiar de enemigo el daño vuelve a empezar
        beamTime += Time.deltaTime;
        AimAt(target.transform.position);

        Vector3 from = firePoint != null ? firePoint.position : transform.position;
        Vector3 to = target.transform.position;
        float ramp = 1f + beamRampBonus * Mathf.Clamp01(beamTime / Mathf.Max(0.1f, beamRampTime));
        DrawBeam(from, to, ramp);

        float dps = EffectiveDamage * EffectiveFireRate * beamDpsFactor * ramp;
        target.TakeHit(dps * Time.deltaTime, DamageKind.Electric, true);
    }

    void DrawBeam(Vector3 from, Vector3 to, float ramp)
    {
        if (beamFx == null)
        {
            var sprite = BuildManager.Instance != null ? BuildManager.Instance.squareSprite : null;
            if (sprite == null) return;
            var go = new GameObject("BeamFx");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 7;
            beamFx = go.transform;
        }

        beamFx.gameObject.SetActive(true);
        from.z = 0f; to.z = 0f;
        Vector3 dir = to - from;
        float len = dir.magnitude;
        float k = Mathf.Clamp01((ramp - 1f) / Mathf.Max(0.01f, beamRampBonus));   // 0 al empezar, 1 con el daño al máximo
        beamFx.position = from + dir * 0.5f;
        beamFx.up = len > 0.001f ? dir / len : Vector3.up;
        beamFx.localScale = new Vector3(0.07f + 0.07f * k, len, 1f);
        beamFx.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.75f, 0.85f, 1f, 0.85f), new Color(0.9f, 0.55f, 1f, 1f), k);
    }

    // Volcán: cuando muere un enemigo quemado dentro del alcance de un volcán, explota
    static int volcanoDepth;

    public static void OnBurningEnemyDied(Vector3 pos, float victimMaxHealth, float victimBurnDps, Sprite sprite)
    {
        if (volcanoDepth >= 3) return;   // tope de reacciones en cadena
        Tower v = null;
        foreach (var t in All)
        {
            if (t == null || !t.volcano) continue;
            Vector2 d = pos - t.transform.position;
            float r = t.EffectiveRange;
            if (d.sqrMagnitude <= r * r) { v = t; break; }
        }
        if (v == null) return;

        volcanoDepth++;
        v.Erupt(pos, victimMaxHealth, victimBurnDps, sprite);
        volcanoDepth--;
    }

    void Erupt(Vector3 pos, float victimMaxHealth, float victimBurnDps, Sprite sprite)
    {
        var m = Mods;
        float radius = volcanoRadius * m.splashMult;
        float dmg = EffectiveDamage * 2f + victimMaxHealth * 0.12f;   // escala con la vida del que murió
        float dps = Mathf.Max(victimBurnDps, burnDps * m.burnDpsMult);
        float dur = burnDuration + m.burnDurationBonus;

        var snapshot = new List<Enemy>(Enemy.All);
        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;
            if (Vector2.Distance(e.transform.position, pos) > radius) continue;
            e.TakeHit(dmg, DamageKind.Area);
            if (!e.IsDead) e.ApplyBurn(dps, dur);
        }

        if (sprite == null) return;
        var go = new GameObject("VolcanoFx");
        go.transform.position = pos;
        var fx = go.AddComponent<SplashFx>();
        fx.Play(sprite, new Color(1f, 0.45f, 0.1f, 0.55f), radius * 2f);
    }

    public void ShowRange(bool show)
    {
        if (rangeIndicator != null) rangeIndicator.gameObject.SetActive(show);
    }

void UpdateRangeVisual()
    {
        if (rangeIndicator != null) rangeIndicator.localScale = Vector3.one * EffectiveRange * 2f;
        if (auraFx != null) auraFx.localScale = Vector3.one * EffectiveRange * 2f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
//[counters]

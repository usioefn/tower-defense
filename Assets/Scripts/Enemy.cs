using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemigo: sigue los waypoints, tiene vida, puede ser ralentizado y da dinero al morir.
/// </summary>
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    [Header("Estadísticas")]
    public float maxHealth = 30f;
    public float speed = 2f;
    public int reward = 5;
    public float damageToBase = 1f;

    [Header("Combate")]
    [Tooltip("Segundos entre golpe y golpe (a la puerta o a un soldado).")]
    public float attackInterval = 1f;
    [Tooltip("Cada golpe a un soldado hace damageToBase x este factor (a la puerta le hace damageToBase).")]
    public float soldierDamageFactor = 5f;
    [Tooltip("A qué distancia de un soldado se detiene a pelear.")]
    public float engageRange = 0.6f;

    [Header("Escuadra y ataque a distancia")]
    [Tooltip("Cuántos enemigos salen juntos por cada unidad de la oleada (los amarillos salen de a 3, los arqueros de a 2).")]
    public int squadSize = 1;
    [Tooltip("Segundos entre un integrante de la escuadra y el siguiente al salir.")]
    public float squadStagger = 0.15f;
    [Tooltip("Enemigos a distancia: a cuántas casillas de la puerta se detienen a golpearla (0 = llegan hasta ella). Con engageRange alto también atacan a los soldados desde lejos.")]
    public float gateRange = 0f;

    float attackDamage;   // daño por golpe: damageToBase escalado por la oleada
    float attackTimer;
    float gateDamageCarry;   // fracción de daño a la puerta que todavía no completó 1 punto entero
    bool atGate;          // llegó al final del camino y está golpeando la puerta

    [Tooltip("Experiencia que da al morir. -1 = igual al dinero que da.")]
    public int xpReward = -1;

    [Header("Referencias")]
    public Transform healthFill; // se escala en X según la vida restante

    public bool IsDead { get; private set; }

    [Header("Resistencias (enemigos counter)")]
    [Tooltip("Vuela: los soldados cuerpo a cuerpo no lo pueden atacar y no se frena a pelear con ellos. Las torres con groundOnly tampoco lo apuntan.")]
    public bool isFlying = false;
    [Tooltip("Armadura: se resta del daño de cada impacto (nunca baja de 20 % del daño). No afecta quemadura ni rayo continuo.")]
    public float armor = 0f;
    [Tooltip("Multiplicador del daño de área (1 = normal, 0,3 = recibe 30 %).")]
    public float areaTakenMult = 1f;
    [Tooltip("Multiplicador del daño eléctrico (cadena y rayo).")]
    public float electricTakenMult = 1f;
    [Tooltip("Multiplicador del daño directo de fuego.")]
    public float fireTakenMult = 1f;
    public bool immuneSlow, immuneStun, immuneFreeze, immuneBurn;

    /// <summary>Soldado con el que está peleando (null si no está frenado por ninguno).</summary>
    public Soldier Opponent { get; private set; }

    /// <summary>Radio del cuerpo (los soldados no se le pueden encimar).</summary>
    public float Radius { get; private set; }

    float health;
    public float Health { get { return health; } }
    int waypointIndex = 1;
    float slowMultiplier = 1f;
    float slowTimer;
    Transform[] points;

    // Aturdimiento (la torre eléctrica lo usa)
    float stunTimer;
    SpriteRenderer bodyRenderer;
    Color baseColor = Color.white;

    // Quemadura (la torre de fuego la usa): daño por segundo durante un tiempo
    float burnTimer;
    float burnDps;

    // Congelación (torre de hielo evolucionada): inmóvil y recibe más daño
    float frozenTimer;
    public const float FrozenDamageMult = 1.35f;

    // Velocidad actual (la usan los disparos perforantes para apuntar con anticipación)
    public Vector2 Velocity { get; private set; }
    Vector3 lastPosition;

    // Dinero original del prefab (antes de escalarlo por oleada): la XP se calcula con este valor
    int baseReward;

    void Awake()
    {
        health = maxHealth;
        attackDamage = damageToBase;
        if (Waypoints.Instance != null) points = Waypoints.Instance.points;

        bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer != null)
        {
            Radius = bodyRenderer.bounds.extents.x;
            baseColor = bodyRenderer.color;
        }
        baseReward = reward;
        lastPosition = transform.position;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Lo llama el spawner para escalar la vida según la oleada.</summary>
    public void Init(float healthMultiplier, float speedMultiplier = 1f, float rewardMultiplier = 1f, float damageMultiplier = 1f)
    {
        maxHealth *= healthMultiplier;
        health = maxHealth;
        speed *= speedMultiplier;
        reward = Mathf.Max(1, Mathf.RoundToInt(reward * rewardMultiplier));
        attackDamage = damageToBase * damageMultiplier;
        UpdateHealthBar();
    }

    public float DistanceToGoal
    {
        get
        {
            if (points == null || points.Length == 0) return float.MaxValue;
            int i = Mathf.Min(waypointIndex, points.Length - 1);
            return Vector2.Distance(transform.position, points[i].position) + Waypoints.Instance.RemainingFrom(i);
        }
    }

    void Update()
    {
        if (IsDead || points == null || points.Length < 2) return;

        if (Time.deltaTime > 0f) Velocity = ((Vector2)(transform.position - lastPosition)) / Time.deltaTime;
        lastPosition = transform.position;

        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f) slowMultiplier = 1f;
        }

        if (frozenTimer > 0f)
        {
            frozenTimer -= Time.deltaTime;
            if (frozenTimer <= 0f) UpdateTint();
        }

        // Quemadura: daño continuo, incluso si está aturdido
        if (burnTimer > 0f)
        {
            burnTimer -= Time.deltaTime;
            TakeDamage(burnDps * Time.deltaTime);
            if (IsDead) return;
            if (burnTimer <= 0f)
            {
                burnDps = 0f;
                UpdateTint();
            }
        }

        // Aturdido: no se mueve ni pelea hasta que termine el tiempo
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f) UpdateTint();
            return;
        }

        // Llegó al final: golpea la puerta hasta que lo maten (o la derribe)
        if (atGate)
        {
            AttackGate();
            return;
        }

        // Un soldado en el camino lo frena: se detiene y pelea con él
        Opponent = isFlying ? null : FindSoldier();
        if (Opponent != null)
        {
            attackTimer -= Time.deltaTime * slowMultiplier;
            if (attackTimer <= 0f)
            {
                if (IsRanged) Shoot(Opponent.transform.position);
                Opponent.TakeDamage(attackDamage * soldierDamageFactor);
                attackTimer = attackInterval;
            }
            return;
        }

        // A distancia: se detiene a 'gateRange' casillas de la puerta y la golpea desde ahí
        if (gateRange > 0f && DistanceToGoal <= gateRange)
        {
            atGate = true;
            attackTimer = 0.4f;
            return;
        }

        Vector3 target = points[waypointIndex].position;
        Vector3 toTarget = target - transform.position;
        float step = speed * slowMultiplier * Time.deltaTime;

        if (toTarget.magnitude <= step)
        {
            transform.position = target;
            waypointIndex++;
            if (waypointIndex >= points.Length) ReachEnd();
        }
        else
        {
            transform.position += toTarget.normalized * step;
        }
    }

    /// <summary>Daño de un impacto, aplicando armadura y resistencias según el tipo. ignoreArmor sirve para daño continuo.</summary>
    public void TakeHit(float amount, DamageKind kind = DamageKind.Physical, bool ignoreArmor = false)
    {
        switch (kind)
        {
            case DamageKind.Area: amount *= areaTakenMult; break;
            case DamageKind.Electric: amount *= electricTakenMult; break;
            case DamageKind.Fire: amount *= fireTakenMult; break;
        }
        if (!ignoreArmor && armor > 0f) amount = Mathf.Max(amount * 0.2f, amount - armor);
        TakeDamage(amount);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        if (frozenTimer > 0f) amount *= FrozenDamageMult;
        health -= amount;
        UpdateHealthBar();
        if (health <= 0f) Die();
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (immuneSlow) return;
        ApplySlowRaw(multiplier, duration);
    }

    void ApplySlowRaw(float multiplier, float duration)
    {
        slowMultiplier = slowTimer > 0f ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
        slowTimer = Mathf.Max(slowTimer, duration);
    }

    void Die()
    {
        bool wasBurning = burnTimer > 0f && burnDps > 0f;
        float dyingBurnDps = burnDps;

        IsDead = true;
        All.Remove(this);

        var buffs = BuffManager.Instance;
        float rewardMult = buffs != null ? buffs.Global.rewardMult : 1f;
        if (GameManager.Instance != null) GameManager.Instance.AddMoney(Mathf.RoundToInt(reward * rewardMult));

        // La experiencia puede disparar la ruleta de nivel, así que va al final
        if (ExperienceManager.Instance != null) ExperienceManager.Instance.AddXp(xpReward >= 0 ? xpReward : baseReward);

        // Volcán: si estaba quemado y hay un volcán cerca, explota y prende a los de alrededor
        if (wasBurning) Tower.OnBurningEnemyDied(transform.position, maxHealth, dyingBurnDps, bodyRenderer != null ? bodyRenderer.sprite : null);

        Destroy(gameObject);
    }

    void ReachEnd()
    {
        // Ya no desaparece: se queda en la puerta golpeándola (se acomoda un poco al azar para no apilarse)
        atGate = true;
        attackTimer = 0.4f;
        Vector2 jitter = Random.insideUnitCircle * 0.35f;
        transform.position = points[points.Length - 1].position + (Vector3)jitter;
    }

    void AttackGate()
    {
        attackTimer -= Time.deltaTime * slowMultiplier;
        if (attackTimer > 0f) return;
        attackTimer = attackInterval;
        if (IsRanged) Shoot(points[points.Length - 1].position);

        // El daño puede ser menor a 1 por golpe (los amarillos de a 3): se acumula y se descuenta cuando junta 1 entero
        gateDamageCarry += attackDamage;
        int whole = Mathf.FloorToInt(gateDamageCarry);
        if (whole <= 0) return;
        gateDamageCarry -= whole;
        if (GameManager.Instance != null) GameManager.Instance.LoseLife(whole);
    }

    /// <summary>Ataca desde lejos (arqueros): dispara un proyectil visual en vez de golpear cuerpo a cuerpo.</summary>
    public bool IsRanged { get { return engageRange > 1f || gateRange > 0f; } }

    void Shoot(Vector3 targetPos)
    {
        if (bodyRenderer != null) ShotFx.Spawn(transform.position, targetPos, baseColor, bodyRenderer.sprite);
    }

    /// <summary>
    /// Soldado vivo más cercano dentro del alcance de pelea (null si no hay).
    /// El alcance nunca es menor que tocarse: el soldado no lo deja encimarse, así que si no, nunca lo frenaría.
    /// </summary>
    Soldier FindSoldier()
    {
        Soldier best = null;
        float bestSqr = float.MaxValue;
        var list = Soldier.All;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (s == null || s.IsDead) continue;
            float reach = Mathf.Max(engageRange, Radius + s.Radius + 0.05f);
            float d = ((Vector2)(s.transform.position - transform.position)).sqrMagnitude;
            if (d <= reach * reach && d < bestSqr) { bestSqr = d; best = s; }
        }
        return best;
    }

    void UpdateHealthBar()
    {
        if (healthFill == null) return;
        float ratio = Mathf.Clamp01(health / maxHealth);
        Vector3 s = healthFill.localScale;
        s.x = ratio;
        healthFill.localScale = s;
    }


    public void ApplyStun(float duration)
    {
        if (immuneStun) return;
        ApplyStunRaw(duration);
    }

    void ApplyStunRaw(float duration)
    {
        if (IsDead) return;
        stunTimer = Mathf.Max(stunTimer, duration);
        UpdateTint();
    }

    public void ApplyBurn(float dps, float duration)
    {
        if (immuneBurn) return;
        ApplyBurnRaw(dps, duration);
    }

    void ApplyBurnRaw(float dps, float duration)
    {
        if (IsDead) return;
        burnDps = burnTimer > 0f ? Mathf.Max(burnDps, dps) : dps;
        burnTimer = Mathf.Max(burnTimer, duration);
        UpdateTint();
    }

    /// <summary>Congelación total: no se mueve ni pelea y recibe más daño mientras dura.</summary>
    public void ApplyFreeze(float duration)
    {
        if (immuneFreeze) return;
        ApplyFreezeRaw(duration);
    }

    void ApplyFreezeRaw(float duration)
    {
        if (IsDead) return;
        frozenTimer = Mathf.Max(frozenTimer, duration);
        stunTimer = Mathf.Max(stunTimer, duration);
        UpdateTint();
    }

    /// <summary>Quemadura acumulable (lanzallamas): cada golpe suma daño por segundo, hasta 'maxMult' veces el de un golpe.</summary>
    public void AddBurnStack(float dps, float duration, float maxMult)
    {
        if (immuneBurn) return;
        AddBurnStackRaw(dps, duration, maxMult);
    }

    void AddBurnStackRaw(float dps, float duration, float maxMult)
    {
        if (IsDead) return;
        burnDps = burnTimer > 0f ? Mathf.Min(burnDps + dps * 0.6f, dps * maxMult) : dps;
        burnTimer = Mathf.Max(burnTimer, duration);
        UpdateTint();
    }

    // El aturdimiento (amarillo) tiene prioridad sobre la quemadura (naranja)
    void UpdateTint()
    {
        if (bodyRenderer == null) return;
        Color c = baseColor;
        if (frozenTimer > 0f) c = Color.Lerp(baseColor, new Color(0.6f, 0.9f, 1f), 0.8f);
        else if (stunTimer > 0f) c = Color.Lerp(baseColor, new Color(1f, 0.95f, 0.3f), 0.7f);
        else if (burnTimer > 0f) c = Color.Lerp(baseColor, new Color(1f, 0.45f, 0.05f), 0.65f);
        bodyRenderer.color = c;
    }


    /// <summary>Cambia el color base del enemigo (el spawner lo usa para diferenciar a los jefes).</summary>
    public void SetBaseColor(Color c)
    {
        baseColor = c;
        UpdateTint();
    }
}
//[counters]

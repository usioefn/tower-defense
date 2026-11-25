using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Soldado (o esqueleto) que suelta el Castillo sobre el camino. Se queda en su puesto,
/// frena a los enemigos que se le acercan y pelea cuerpo a cuerpo con ellos.
/// </summary>
public class Soldier : MonoBehaviour
{
    public static readonly List<Soldier> All = new List<Soldier>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    [Header("Movimiento y combate")]
    public float moveSpeed = 3.2f;
    [Tooltip("A qué distancia golpea a un enemigo.")]
    public float meleeRange = 0.9f;
    public float attackInterval = 1f;
    [Tooltip("Hasta qué distancia de su puesto sale a ayudar a un compañero que está peleando.")]
    public float helpRange = 3f;
    [Tooltip("Alcance de disparo de los soldados arqueros (Cuartel de arqueros).")]
    public float archerRange = 2.4f;

    [Header("Referencias")]
    public Transform healthFill;   // se escala en X según la vida restante
    public SpriteRenderer body;

    public bool IsSkeleton { get; private set; }
    public bool IsDead { get; private set; }
    public CastleTower Owner { get; private set; }
    public bool IsKnight { get; private set; }
    public bool IsArcher { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public float Damage { get; private set; }

    // Cuántos lugares se prueban alrededor del enemigo al buscar dónde pararse a pelear
    const int SpotSamples = 24;

    Vector3 post;
    float attackTimer;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Lo llama el castillo al crear la unidad.</summary>
    public void Init(CastleTower owner, Vector3 postPosition, float maxHealth, float damage, bool skeleton)
    {
        Owner = owner;
        IsSkeleton = skeleton;
        postPosition.z = 0f;
        post = postPosition;
        MaxHealth = maxHealth;
        Health = maxHealth;
        Damage = damage;
        attackTimer = 0.3f;

        if (skeleton)
        {
            // Esqueleto: más chico y color hueso para distinguirlo
            transform.localScale = Vector3.one * 0.85f;
            if (body != null) body.color = new Color(0.92f, 0.9f, 0.8f);
        }
        UpdateHealthBar();
    }

    /// <summary>Cambia el puesto (el castillo reparte los puestos cuando cambia la cantidad de soldados).</summary>
    public void SetPost(Vector3 postPosition)
    {
        postPosition.z = 0f;
        post = postPosition;
    }

    /// <summary>Actualiza las estadísticas (subida de nivel o beneficio) conservando el porcentaje de vida.</summary>
    public void SetStats(float maxHealth, float damage)
    {
        float ratio = MaxHealth > 0f ? Health / MaxHealth : 1f;
        MaxHealth = maxHealth;
        Health = maxHealth * ratio;
        Damage = damage;
        UpdateHealthBar();
    }

    void Update()
    {
        if (IsDead) return;
        attackTimer -= Time.deltaTime;

        if (IsArcher) { UpdateArcher(); return; }

        var foe = TargetEnemy();
        if (foe == null)
        {
            // Sin enemigos cerca: vuelve a su puesto
            Move((Vector2)(post - transform.position));
        }
        else
        {
            float dist = ((Vector2)(foe.transform.position - transform.position)).magnitude;
            if (dist <= meleeRange && attackTimer <= 0f)
            {
                foe.TakeDamage(Damage);
                attackTimer = attackInterval;
            }

            // Va a un lugar libre pegado al enemigo; si lo tienen rodeado, espera donde está
            Vector2 spot;
            if (FindSpot(foe, out spot)) Move(spot - (Vector2)transform.position);
        }

        ResolveOverlaps();
    }

    /// <summary>Caballero (grande y resistente) o arquero (dispara de lejos desde un costado). Los esqueletos nunca son arqueros.</summary>
    public void SetRole(bool knight, bool archer)
    {
        if (IsSkeleton) archer = false;
        IsKnight = knight;
        IsArcher = archer;
        if (knight)
        {
            transform.localScale = Vector3.one * 1.5f;
            if (body != null) body.color = new Color(1f, 0.82f, 0.35f);
        }
        else if (archer && body != null)
        {
            body.color = new Color(0.45f, 0.85f, 0.55f);
        }
    }

    /// <summary>Arquero: vuelve a su puesto y le dispara al enemigo más cercano que tenga al alcance.</summary>
    void UpdateArcher()
    {
        Move((Vector2)(post - transform.position));

        Enemy foe = null;
        float bestSqr = archerRange * archerRange;
        var list = Enemy.All;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.IsDead) continue;
            float d = ((Vector2)(e.transform.position - transform.position)).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; foe = e; }
        }
        if (foe == null || attackTimer > 0f) return;

        if (body != null) ShotFx.Spawn(transform.position, foe.transform.position, new Color(0.9f, 1f, 0.7f), body.sprite);
        foe.TakeDamage(Damage);
        attackTimer = attackInterval * 0.9f;
    }

    /// <summary>Se desplaza 'delta' sin pasarse de su velocidad.</summary>
    void Move(Vector2 delta)
    {
        float step = moveSpeed * Time.deltaTime;
        if (delta.sqrMagnitude > step * step) delta = delta.normalized * step;
        transform.position += (Vector3)delta;
    }

    /// <summary>
    /// Enemigo más cercano entre los que tiene al alcance de la espada o los que ya están peleando
    /// con un compañero cerca de su puesto. No sale a buscar enemigos libres: espera a que lleguen a la fila.
    /// </summary>
    Enemy TargetEnemy()
    {
        Enemy best = null;
        float bestSqr = float.MaxValue;
        float helpSqr = helpRange * helpRange;
        float meleeSqr = meleeRange * meleeRange;
        var list = Enemy.All;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.IsDead) continue;
            float d = ((Vector2)(e.transform.position - transform.position)).sqrMagnitude;
            if (e.isFlying) continue;   // los soldados cuerpo a cuerpo no alcanzan a los voladores
            if (d > meleeSqr && !IsTeammateFighting(e, helpSqr)) continue;
            if (d < bestSqr) { bestSqr = d; best = e; }
        }
        return best;
    }

    /// <summary>¿El enemigo está peleando con un soldado del mismo castillo, cerca de su puesto?</summary>
    bool IsTeammateFighting(Enemy e, float helpSqr)
    {
        Soldier mate = e.Opponent;
        if (mate == null || mate == this || mate.IsDead || mate.Owner != Owner) return false;
        return ((Vector2)(e.transform.position - post)).sqrMagnitude <= helpSqr;
    }

    /// <summary>
    /// Lugar pegado al enemigo, lo más cerca posible de donde está el soldado, que no ocupe un compañero
    /// más cercano a ese lugar ni otro enemigo. False si no queda ninguno libre.
    /// </summary>
    bool FindSpot(Enemy foe, out Vector2 spot)
    {
        Vector2 center = foe.transform.position;
        Vector2 from = (Vector2)transform.position - center;
        float baseAngle = from.sqrMagnitude > 0.0001f ? Mathf.Atan2(from.y, from.x) : 0f;
        float ring = foe.Radius + Radius;
        float stepAngle = 2f * Mathf.PI / SpotSamples;

        for (int k = 0; k < SpotSamples; k++)
        {
            // Se prueba primero de frente y después alternando a cada lado: 0, +1, -1, +2, -2...
            int turn = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
            float a = baseAngle + turn * stepAngle;
            spot = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
            if (IsSpotFree(spot, foe)) return true;
        }
        spot = transform.position;
        return false;
    }

    /// <summary>¿Puede pararse en 'spot'? Lo ocupa un soldado que esté más cerca de ahí, o cualquier otro enemigo.</summary>
    bool IsSpotFree(Vector2 spot, Enemy foe)
    {
        float mySqr = (spot - (Vector2)transform.position).sqrMagnitude;
        for (int i = 0; i < All.Count; i++)
        {
            var other = All[i];
            if (other == this || other == null || other.IsDead) continue;
            float minDist = Radius + other.Radius;
            float d = (spot - (Vector2)other.transform.position).sqrMagnitude;
            if (d < minDist * minDist && d < mySqr) return false;
        }
        var enemies = Enemy.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null || e == foe || e.IsDead || e.isFlying) continue;
            float minDist = Radius + e.Radius;
            if ((spot - (Vector2)e.transform.position).sqrMagnitude < minDist * minDist) return false;
        }
        return true;
    }

    /// <summary>
    /// Sale de encima de los soldados y enemigos que está tocando. Con otro soldado se reparten
    /// el empujón a medias (el otro hace lo mismo en su Update); a los enemigos los esquiva entero.
    /// </summary>
    void ResolveOverlaps()
    {
        Vector2 push = Vector2.zero;
        for (int i = 0; i < All.Count; i++)
        {
            var other = All[i];
            if (other == this || other == null || other.IsDead) continue;
            push += PushOut(other.transform.position, Radius + other.Radius) * 0.5f;
        }
        var enemies = Enemy.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null || e.IsDead) continue;
            if (!e.isFlying) push += PushOut(e.transform.position, Radius + e.Radius);
        }
        transform.position += (Vector3)push;
    }

    /// <summary>Lo que hay que moverse para quedar a 'minDist' de 'from' (cero si ya está lejos).</summary>
    Vector2 PushOut(Vector3 from, float minDist)
    {
        Vector2 away = transform.position - from;
        float d = away.magnitude;
        if (d >= minDist) return Vector2.zero;
        // Encimados del todo: se separan hacia un lado cualquiera
        Vector2 dir = d > 0.0001f ? away / d : Random.insideUnitCircle.normalized;
        return dir * (minDist - d);
    }

    /// <summary>Radio del cuerpo (se achica con el esqueleto).</summary>
    public float Radius { get { return body != null ? body.bounds.extents.x : 0.19f; } }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        Health -= amount;
        UpdateHealthBar();
        if (Health <= 0f) Die();
    }

    void Die()
    {
        IsDead = true;
        All.Remove(this);
        if (Owner != null) Owner.OnSoldierDied(this, transform.position);
        Destroy(gameObject);
    }

    void UpdateHealthBar()
    {
        if (healthFill == null) return;
        Vector3 s = healthFill.localScale;
        s.x = MaxHealth > 0f ? Mathf.Clamp01(Health / MaxHealth) : 0f;
        healthFill.localScale = s;
    }
}
//[counters]

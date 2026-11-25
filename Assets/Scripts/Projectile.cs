using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proyectil teledirigido. Soporta daño en área (splash) y ralentización.
/// </summary>
public class Projectile : MonoBehaviour
{
    Enemy target;
    Vector3 lastPos;
    float damage, speed, splash, slowMultiplier, slowDuration;
    Color color = Color.white;

    // Cadena eléctrica: el impacto salta a los enemigos más cercanos y los aturde
    int chainCount;
    float chainRange;
    float chainDamageMult = 1f;
    float stunDuration;

    [Tooltip("Sprite (cuadrado) para dibujar el rayo. Si está vacío usa el sprite del proyectil.")]
    public Sprite lineSprite;

    // Perforación y quemadura (torre de fuego)
    int pierceCount;              // máximo de enemigos que atraviesa (0 = no perfora)
    float pierceMaxDistance;
    Vector3 pierceDir;
    float pierceTraveled;
    int pierceHits;
    float trailTimer;
    readonly HashSet<Enemy> pierceHitSet = new HashSet<Enemy>();
    float burnDps;
    float burnDuration;
    const float PierceHitRadius = 0.4f;

    // Evolución: bomba de racimo, congelación y quemadura acumulable
    int clusterCount;
    float clusterSplashFactor = 0.55f;
    float clusterDamageFactor = 0.8f;
    float freezeDuration;
    bool burnStack;
    bool groundOnly;

    public void Init(Enemy t, float dmg, float spd, float splashRadius, float slowMul, float slowDur, Color c)
    {
        target = t;
        damage = dmg;
        speed = spd;
        splash = splashRadius;
        slowMultiplier = slowMul;
        slowDuration = slowDur;
        color = c;
        lastPos = t != null ? t.transform.position : transform.position;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = c;
    }

void Update()
    {
        // Proyectil perforante: vuela recto y no depende del objetivo
        if (pierceCount > 0)
        {
            UpdatePierce();
            return;
        }

        if (target != null && !target.IsDead)
        {
            lastPos = target.transform.position;
        }
        else if (splash <= 0f)
        {
            // Sin objetivo y sin daño en área: no tiene sentido seguir volando
            Destroy(gameObject);
            return;
        }

        Vector3 toTarget = lastPos - transform.position;
        float step = speed * Time.deltaTime;

        if (toTarget.magnitude <= step)
        {
            Impact();
            return;
        }

        transform.position += toTarget.normalized * step;
        transform.up = toTarget;
    }

void Impact()
    {
        if (splash > 0f && clusterCount > 0)
        {
            ExplodeCluster();
        }
        else if (splash > 0f)
        {
            var snapshot = new List<Enemy>(Enemy.All);
            foreach (var e in snapshot)
            {
                if (e == null || e.IsDead) continue;
                if (Vector2.Distance(e.transform.position, transform.position) <= splash) Apply(e);
            }
            SpawnSplashFx();
        }
        else if (target != null && !target.IsDead)
        {
            Vector3 hitPos = target.transform.position;
            Apply(target);
            if (chainCount > 0) Chain(target, hitPos);
        }

        Destroy(gameObject);
    }

void Apply(Enemy e, float damageMult = 1f)
    {
        if (groundOnly && e.isFlying) return;
        e.TakeHit(damage * damageMult, HitKind());
        if (e.IsDead) return;
        if (slowDuration > 0f) e.ApplySlow(slowMultiplier, slowDuration);
        if (stunDuration > 0f) e.ApplyStun(stunDuration);
        if (freezeDuration > 0f) e.ApplyFreeze(freezeDuration);
        if (burnDps > 0f)
        {
            if (burnStack) e.AddBurnStack(burnDps, burnDuration, 4f);
            else e.ApplyBurn(burnDps, burnDuration);
        }
    }

void SpawnSplashFx()
    {
        SpawnSplashAt(transform.position);
    }

    void SpawnSplashAt(Vector3 pos, float radius = -1f)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        var go = new GameObject("SplashFx");
        go.transform.position = pos;
        var fx = go.AddComponent<SplashFx>();
        fx.Play(sr.sprite, new Color(color.r, color.g, color.b, 0.5f), (radius > 0f ? radius : splash) * 2f);
    }


public void SetChain(int count, float range, float damageMult, float stun)
    {
        chainCount = count;
        chainRange = range;
        chainDamageMult = damageMult;
        stunDuration = stun;
    }

    /// <summary>Salta desde el primer objetivo al enemigo más cercano que no haya sido golpeado, hasta chainCount veces.</summary>
    void Chain(Enemy first, Vector3 startPos)
    {
        var alreadyHit = new HashSet<Enemy> { first };
        Vector3 from = startPos;

        for (int i = 0; i < chainCount; i++)
        {
            Enemy next = null;
            float bestSqr = chainRange * chainRange;
            foreach (var e in Enemy.All)
            {
                if (e == null || e.IsDead || alreadyHit.Contains(e)) continue;
                float d = ((Vector2)(e.transform.position - from)).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; next = e; }
            }
            if (next == null) break;

            alreadyHit.Add(next);
            Vector3 to = next.transform.position;
            SpawnBolt(from, to);
            Apply(next, chainDamageMult);
            from = to;
        }
    }

    void SpawnBolt(Vector3 from, Vector3 to)
    {
        Sprite sprite = lineSprite;
        if (sprite == null)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sprite = sr.sprite;
        }
        if (sprite == null) return;

        var go = new GameObject("BoltFx");
        var fx = go.AddComponent<BoltFx>();
        fx.Play(sprite, color, from, to);
    }


public void SetGroundOnly(bool v) { groundOnly = v; }

    /// <summary>Tipo de daño de este proyectil, para las resistencias de los enemigos counter.</summary>
    DamageKind HitKind()
    {
        if (chainCount > 0 || stunDuration > 0f) return DamageKind.Electric;
        if (burnDps > 0f) return DamageKind.Fire;
        if (splash > 0f) return DamageKind.Area;
        return DamageKind.Physical;
    }

    public void SetBurn(float dps, float duration, bool stack = false)
    {
        burnDps = dps;
        burnDuration = duration;
        burnStack = stack;
    }

    /// <summary>Bomba de racimo: al caer se divide en 'count' explosiones chicas alrededor del impacto.</summary>
    public void SetCluster(int count, float splashFactor, float damageFactor)
    {
        clusterCount = count;
        clusterSplashFactor = splashFactor;
        clusterDamageFactor = damageFactor;
    }

    /// <summary>Al impactar congela del todo al enemigo durante 'duration' segundos.</summary>
    public void SetFreeze(float duration)
    {
        freezeDuration = duration;
    }

    void ExplodeCluster()
    {
        float r = splash * clusterSplashFactor;
        float spread = r * 0.75f;   // las mini-explosiones se solapan sobre el objetivo: siempre lo alcanzan
        float baseAngle = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < clusterCount; i++)
        {
            float a = baseAngle + i * Mathf.PI * 2f / clusterCount;
            Vector3 c = transform.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * spread;
            var snapshot = new List<Enemy>(Enemy.All);
            foreach (var e in snapshot)
            {
                if (e == null || e.IsDead) continue;
                if (Vector2.Distance(e.transform.position, c) <= r) Apply(e, clusterDamageFactor);
            }
            SpawnSplashAt(c, r);
        }
    }

    /// <summary>Convierte el disparo en perforante: vuela recto hacia donde estaba el objetivo y atraviesa hasta 'count' enemigos.</summary>
public void SetPierce(int count, float maxDistance)
    {
        pierceCount = count;
        pierceMaxDistance = maxDistance;
        if (count <= 0) return;

        // Apuntar con anticipación: hacia donde va a estar el objetivo cuando llegue el disparo
        Vector3 aim = lastPos;
        if (target != null && !target.IsDead && speed > 0.01f)
        {
            Vector2 vel = target.Velocity;
            Vector2 origin = transform.position;
            Vector2 predicted = lastPos;
            for (int i = 0; i < 2; i++)
            {
                float t = Vector2.Distance(origin, predicted) / speed;
                predicted = (Vector2)lastPos + vel * t;
            }
            aim = predicted;
        }

        Vector3 dir = aim - transform.position;
        dir.z = 0f;
        pierceDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.up;
        transform.up = pierceDir;
        transform.localScale = Vector3.one * 0.35f; // proyectil perforante un poco más grande
    }

void UpdatePierce()
    {
        float step = speed * Time.deltaTime;
        Vector3 prev = transform.position;
        transform.position += pierceDir * step;
        pierceTraveled += step;

        // Estela
        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.05f;
            SpawnFxAt(transform.position, 0.35f, 0.45f);
        }

        // Golpea a todos los enemigos que cruzó en este frame (una sola vez cada uno)
        var snapshot = new List<Enemy>(Enemy.All);
        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead || pierceHitSet.Contains(e)) continue;
            if (DistToSegment(e.transform.position, prev, transform.position) > PierceHitRadius) continue;

            pierceHitSet.Add(e);
            PierceHit(e);

            pierceHits++;
            if (pierceHits >= pierceCount)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (pierceTraveled >= pierceMaxDistance) Destroy(gameObject);
    }

    /// <summary>Cada impacto de un disparo perforante aplica los efectos de la torre: área, rayo encadenado, estados.</summary>
    void PierceHit(Enemy e)
    {
        Vector3 pos = e.transform.position;

        if (splash > 0f)
        {
            var all = new List<Enemy>(Enemy.All);
            foreach (var other in all)
            {
                if (other == null || other.IsDead) continue;
                if (Vector2.Distance(other.transform.position, pos) <= splash) Apply(other);
            }
            SpawnSplashAt(pos);
        }
        else
        {
            Apply(e);
            SpawnFxAt(pos, 0.6f, 0.6f);
        }

        if (chainCount > 0) Chain(e, pos);
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.000001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }

    void SpawnFxAt(Vector3 pos, float size, float alpha)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        var go = new GameObject("FireFx");
        go.transform.position = pos;
        var fx = go.AddComponent<SplashFx>();
        fx.Play(sr.sprite, new Color(color.r, color.g, color.b, alpha), size);
    }
}
//[counters]

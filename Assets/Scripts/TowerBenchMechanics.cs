#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Prueba mecánica: una torre contra un solo enemigo lento y con vida enorme. Mide daño recibido y distancia recorrida en 8 s.</summary>
public partial class TowerBench
{
    struct Mech { public float damage, travelled; }

    IEnumerator MechanicsTest(StringBuilder sb)
    {
        var sp = WaveSpawner.Instance;
        Enemy[] prefabs = { sp.enemyArmored, sp.enemyGolem, sp.enemySalamander, sp.enemyInsulated, sp.enemyAmphibian, sp.enemyFlyer };
        string[] order = { "Tower_Basica", "Tower_Canon", "Tower_Hielo", "Tower_Electrica", "Tower_Fuego", "Tower_Castillo" };
        sb.AppendLine("== Mecanica: 1 torre nivel 1 vs 1 enemigo (vel 0,5, vida 1.000.000), 8 s ==");
        for (int i = 0; i < order.Length; i++)
        {
            Mech twin = default(Mech), real = default(Mech);
            yield return MechOne(order[i], prefabs[i], true, m => twin = m);
            yield return MechOne(order[i], prefabs[i], false, m => real = m);
            sb.AppendLine(prefabs[i].name + " vs " + order[i]
                + " | gemelo: dano " + twin.damage.ToString("0") + ", avanzo " + twin.travelled.ToString("0.00")
                + " | counter: dano " + real.damage.ToString("0") + ", avanzo " + real.travelled.ToString("0.00")
                + " | dano relativo " + (twin.damage > 0.01f ? (real.damage / twin.damage * 100f).ToString("0") + "%" : "n/a"));
            Progress = sb.ToString();
        }
    }

    IEnumerator MechOne(string towerName, Enemy prefab, bool stripResist, System.Action<Mech> done)
    {
        foreach (var e in new List<Enemy>(Enemy.All)) if (e != null) Destroy(e.gameObject);
        foreach (var t in new List<Tower>(Tower.All)) if (t != null) Destroy(t.gameObject);
        yield return null; yield return null;
        foreach (var s in new List<Soldier>(Soldier.All)) if (s != null) Destroy(s.gameObject);
        yield return null;

        var pts = Waypoints.Instance.points;
        Vector3 a = pts[0].position, b = pts[1].position;
        Vector3 dir = (b - a).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        var tower = Instantiate(Load(towerName), a + dir * 2.0f + perp * 1.0f, Quaternion.identity);

        prefab.maxHealth = prefab.maxHealth; // sin cambios en el prefab
        var en = Instantiate(prefab, a, Quaternion.identity);
        en.maxHealth = 1000000f;
        en.Init(1f, 1f, 1f, 0f);
        en.speed = 0.5f;
        if (stripResist)
        {
            en.isFlying = false; en.armor = 0f; en.areaTakenMult = 1f; en.electricTakenMult = 1f; en.fireTakenMult = 1f;
            en.immuneSlow = false; en.immuneStun = false; en.immuneFreeze = false; en.immuneBurn = false;
        }
        float hp0 = en.Health;
        float d0 = en.DistanceToGoal;

        Time.timeScale = 4f;
        float t0 = 0f;
        while (t0 < 8f) { yield return null; t0 += Time.deltaTime; }
        var m = new Mech { damage = hp0 - en.Health, travelled = d0 - en.DistanceToGoal };
        Destroy(en.gameObject);
        Time.timeScale = 1f;
        done(m);
    }
}
#endif

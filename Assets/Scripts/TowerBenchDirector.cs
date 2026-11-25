#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Prueba del director de amenazas: arma loadouts distintos, pide la oleada adaptada y cuenta qué enemigos trae. Luego suelta una oleada real.</summary>
public partial class TowerBench
{
    static string Describe(Wave w, WaveSpawner sp)
    {
        var sb = new StringBuilder();
        foreach (var g in w.groups)
        {
            if (g.enemyPrefab == null) continue;
            sb.Append(g.enemyPrefab.name.Replace("Enemy_", "")).Append(" x").Append(g.count).Append("  ");
        }
        return sb.ToString();
    }

    void Place(string towerName, int n, float startOffset)
    {
        var pts = Waypoints.Instance.points;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = pts[0].position, b = pts[1].position;
            Vector3 dir = (b - a).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            Instantiate(Load(towerName), a + dir * (startOffset + 1.2f * i) + perp * 1.0f, Quaternion.identity);
        }
    }

    IEnumerator Clear()
    {
        foreach (var e in new List<Enemy>(Enemy.All)) if (e != null) Destroy(e.gameObject);
        foreach (var t in new List<Tower>(Tower.All)) if (t != null) Destroy(t.gameObject);
        yield return null; yield return null;
        foreach (var s in new List<Soldier>(Soldier.All)) if (s != null) Destroy(s.gameObject);
        yield return null;
    }

    IEnumerator DirectorTest(StringBuilder sb)
    {
        var sp = WaveSpawner.Instance;
        var gen = WaveGenerator.Generate(sp, new System.Random(7));
        SetProp(GameManager.Instance, "Lives", 1000000);
        SetProp(GameManager.Instance, "IsGameOver", false);

        string[] labels = { "Solo Castillo x3", "Solo Cañón x4", "Mixto: 2 Básica + 2 Cañón + 2 Fuego + 1 Hielo + 1 Eléctrica + 1 Castillo", "Sin torres (no debe adaptar)" };
        for (int c = 0; c < labels.Length; c++)
        {
            yield return Clear();
            if (c == 0) Place("Tower_Castillo", 3, 2f);
            else if (c == 1) Place("Tower_Canon", 4, 2f);
            else if (c == 2)
            {
                Place("Tower_Basica", 2, 2f); Place("Tower_Canon", 2, 4.5f); Place("Tower_Fuego", 2, 7f);
                Place("Tower_Hielo", 1, 9.5f); Place("Tower_Electrica", 1, 11f); Place("Tower_Castillo", 1, 12.5f);
            }
            yield return null;
            sb.AppendLine("-- " + labels[c]);
            Progress = sb.ToString();
            foreach (int wn in new[] { 3, 10, 25, 45 })
            {
                var original = gen[wn - 1];
                var adapted = ThreatDirector.Adapt(sp, original, wn);
                sb.AppendLine("  oleada " + wn + (adapted == original ? " (sin cambios): " : " (adaptada): ") + Describe(adapted, sp));
            }
        }

        // Integración: oleada 20 real con solo Castillos, usando el flujo normal del spawner
        yield return Clear();
        Place("Tower_Castillo", 3, 2f);
        yield return null;
        var f = typeof(WaveSpawner).GetProperty("CurrentWave");
        var back = typeof(WaveSpawner).GetField("<CurrentWave>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (back != null) back.SetValue(sp, 19);
        sp.enabled = true;
        Time.timeScale = 6f;
        sp.StartNextWave();
        var seen = new Dictionary<int, string>();
        float t = 0f;
        while (t < 90f)
        {
            yield return null; t += Time.deltaTime;
            foreach (var e in Enemy.All) if (e != null && !seen.ContainsKey(e.GetInstanceID())) seen[e.GetInstanceID()] = e.name.Replace("(Clone)", "").Replace("Enemy_", "");
        }
        var count = new Dictionary<string, int>();
        foreach (var kv in seen) { int n; count.TryGetValue(kv.Value, out n); count[kv.Value] = n + 1; }
        sb.AppendLine("-- Integracion: oleada 20 real, 3 Castillos. Informe del director: " + ThreatDirector.LastReport);
        sb.Append("   enemigos que aparecieron: ");
        foreach (var kv in count) sb.Append(kv.Key).Append(" ").Append(kv.Value).Append("  ");
        sb.AppendLine();
        sp.enabled = false;
        Time.timeScale = 1f;
    }
}
#endif

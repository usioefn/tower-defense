#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Banco de pruebas (solo editor): enfrenta cada torre a oleadas reales que caminan por el camino y mide
/// cuánto daño llega a la puerta. Se arranca con TowerBench.Request() y se entra a Play Mode.
/// </summary>
public partial class TowerBench : MonoBehaviour
{
    public static string Report = "";
    public static string Progress = "";
    public static bool Done;
    const string Key = "TowerBenchRun";
    const string ModeKey = "TowerBenchMode";

    /// <summary>mode: "base" (torres actuales) o "director" (prueba del director de amenazas).</summary>
    public static void Request(string mode = "base") { EditorPrefs.SetString(Key, "1"); EditorPrefs.SetString(ModeKey, mode); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!EditorPrefs.HasKey(Key)) return;
        EditorPrefs.DeleteKey(Key);
        Done = false; Report = ""; Progress = "";
        new GameObject("TowerBench").AddComponent<TowerBench>();
    }

    static System.Action<Enemy> Tweak;

    struct Result { public string name; public float gateLoss, clearTime, avgProgress; public int spent, count; public bool timedOut; }

    static void SetProp(object o, string prop, object val)
    {
        var f = o.GetType().GetField("<" + prop + ">k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(o, val);
    }

    Tower Load(string n) { return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + n + ".prefab").GetComponent<Tower>(); }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return null;
        var gm = GameManager.Instance;
        var sp = WaveSpawner.Instance;
        sp.enabled = false;
        SetProp(ExperienceManager.Instance, "XpToNext", 1000000000);
        gm.AddMoney(100000);

        string[] names = { "Tower_Basica", "Tower_Canon", "Tower_Hielo", "Tower_Electrica", "Tower_Fuego", "Tower_Castillo" };
        var sb = new StringBuilder();
        var gen = WaveGenerator.Generate(sp, new System.Random(7));

        if (EditorPrefs.GetString(ModeKey, "base") == "director")
        {
            yield return DirectorTest(sb);
            Report = sb.ToString(); Done = true; Time.timeScale = 1f;
            yield break;
        }
        if (EditorPrefs.GetString(ModeKey, "base") == "mechanics")
        {
            yield return MechanicsTest(sb);
            Report = sb.ToString(); Done = true; Time.timeScale = 1f;
            yield break;
        }
        if (EditorPrefs.GetString(ModeKey, "base") == "counters")
        {
            yield return CounterTest(sb);
            Report = sb.ToString(); Done = true; Time.timeScale = 1f;
            yield break;
        }
        sb.AppendLine("== A) 450 de presupuesto, nivel 1, oleada 15 ==");
        foreach (var n in names)
        {
            Result r = default(Result);
            yield return Run(n, 450, 1, false, -1, gen[14], rr => r = rr);
            sb.AppendLine(Fmt(r));
            Progress = sb.ToString();
        }
        sb.AppendLine("== B) 1200 de presupuesto, nivel maximo, oleada 30 ==");
        foreach (var n in names)
        {
            Result r = default(Result);
            yield return Run(n, 1200, 99, false, -1, gen[29], rr => r = rr);
            sb.AppendLine(Fmt(r));
            Progress = sb.ToString();
        }
        sb.AppendLine("== C) 2 torres evolucionadas, oleada 40 ==");
        foreach (var n in names)
        {
            var p = Load(n);
            var evos = TowerEvolutions.For(p.TowerId);
            for (int i = 0; i < evos.Count; i++)
            {
                Result r = default(Result);
                yield return Run(n, 0, 99, true, i, gen[39], rr => r = rr);
                sb.AppendLine(Fmt(r));
                Progress = sb.ToString();
            }
        }
        Report = sb.ToString();
        Done = true;
        Time.timeScale = 1f;
    }

    string Fmt(Result r)
    {
        return r.name + " x" + r.count + " (gasto " + r.spent + "): puerta -" + r.gateLoss.ToString("0") + ", limpia en " + r.clearTime.ToString("0") + "s, progreso medio al morir " + (r.avgProgress * 100f).ToString("0") + "%" + (r.timedOut ? " [TIEMPO]" : "");
    }

    IEnumerator Run(string prefabName, int budget, int level, bool evolve, int evoIndex, Wave wave, System.Action<Result> done)
    {
        var gm = GameManager.Instance;
        var sp = WaveSpawner.Instance;

        foreach (var e in new List<Enemy>(Enemy.All)) if (e != null) Destroy(e.gameObject);
        foreach (var t in new List<Tower>(Tower.All)) if (t != null) Destroy(t.gameObject);
        yield return null; yield return null;
        foreach (var s in new List<Soldier>(Soldier.All)) if (s != null) Destroy(s.gameObject);
        yield return null;

        SetProp(gm, "Lives", 1000000);
        SetProp(gm, "IsGameOver", false);
        int livesStart = gm.Lives;

        var prefab = Load(prefabName);
        int count;
        if (evolve) count = 2;
        else
        {
            float perTower = prefab.cost * (level > 1 ? (prefab.maxLevel == 4 ? 5.5f : 3.25f) : 1f);
            count = Mathf.Max(1, Mathf.FloorToInt(budget / perTower));
        }

        var pts = Waypoints.Instance.points;
        var poly = new List<Vector3>();
        foreach (var p in pts) poly.Add(p.position);
        float total = 0f; var cum = new List<float> { 0f };
        for (int i = 1; i < poly.Count; i++) { total += Vector3.Distance(poly[i - 1], poly[i]); cum.Add(total); }

        var placed = new List<Vector3>();
        int spent = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = Vector3.zero; bool ok = false;
            for (int attempt = 0; attempt < 40 && !ok; attempt++)
            {
                float s = total * (0.2f + 0.6f * ((i + 0.5f) / count)) + (attempt / 2) * 0.8f * ((attempt % 2 == 0) ? 1 : -1);
                s = Mathf.Clamp(s, 0.5f, total - 0.5f);
                Vector3 a = poly[0], b = poly[1];
                for (int k = 1; k < poly.Count; k++)
                {
                    if (s <= cum[k])
                    {
                        float tt = (s - cum[k - 1]) / Mathf.Max(0.001f, cum[k] - cum[k - 1]);
                        a = Vector3.Lerp(poly[k - 1], poly[k], tt); b = poly[k];
                        break;
                    }
                }
                Vector3 dir = b - a; if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
                dir.Normalize();
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * (attempt % 2 == 0 ? 1f : -1f) * 1.0f;
                pos = a + perp;
                ok = true;
                foreach (var q in placed) if (Vector3.Distance(q, pos) < 1.0f) ok = false;
            }
            placed.Add(pos);
            var t = Instantiate(prefab, pos, Quaternion.identity);
            spent += prefab.cost;
            if (level > 1 || evolve)
            {
                int guard = 0;
                while (t.CanUpgrade && guard++ < 10) { int before = gm.Money; t.Upgrade(); spent += before - gm.Money; }
            }
            if (evolve)
            {
                var evos = TowerEvolutions.For(t.TowerId);
                int before = gm.Money;
                t.Evolve(evos[evoIndex]);
                spent += before - gm.Money;
            }
        }
        yield return null;

        string label = prefab.towerName;
        if (evolve) { var ev = TowerEvolutions.For(prefab.TowerId)[evoIndex]; label = prefab.towerName + " > " + ev.name; }

        Time.timeScale = 6f;
        float hpMul = wave.hpMultiplier > 0f ? wave.hpMultiplier : 1f;
        bool spawning = true;
        StartCoroutine(Spawn(wave, hpMul, () => spawning = false));

        var progress = new Dictionary<Enemy, float>();
        var finished = new List<float>();
        float simTime = 0f; bool timedOut = false;
        while (true)
        {
            yield return null;
            simTime += Time.deltaTime;
            var alive = new HashSet<Enemy>(Enemy.All);
            foreach (var e in alive) if (e != null) progress[e] = Mathf.Max(progress.ContainsKey(e) ? progress[e] : 0f, 1f - Mathf.Clamp01(e.DistanceToGoal / total));
            var gone = new List<Enemy>();
            foreach (var kv in progress) if (kv.Key == null || !alive.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var g in gone) { finished.Add(progress[g]); progress.Remove(g); }

            if (!spawning && Enemy.All.Count == 0) break;
            if (simTime > 220f) { timedOut = true; break; }
        }
        foreach (var kv in progress) finished.Add(kv.Value);

        float avg = 0f; foreach (var f in finished) avg += f; avg /= Mathf.Max(1, finished.Count);
        var res = new Result { name = label, gateLoss = livesStart - gm.Lives, clearTime = simTime, avgProgress = avg, spent = spent, count = count, timedOut = timedOut };
        foreach (var e in new List<Enemy>(Enemy.All)) if (e != null) Destroy(e.gameObject);
        Time.timeScale = 1f;
        done(res);
    }

    IEnumerator Spawn(Wave wave, float hpMul, System.Action finished)
    {
        var start = Waypoints.Instance.points[0].position;
        foreach (var g in wave.groups)
        {
            if (g.enemyPrefab == null) continue;
            int squad = Mathf.Max(1, g.enemyPrefab.squadSize);
            for (int i = 0; i < g.count; i++)
            {
                for (int k = 0; k < squad; k++)
                {
                    var e = Instantiate(g.enemyPrefab, start, Quaternion.identity);
                    e.Init(hpMul, wave.speedMultiplier, wave.rewardMultiplier, wave.damageMultiplier);
                    if (Tweak != null) Tweak(e);
                    if (k < squad - 1) yield return new WaitForSeconds(g.enemyPrefab.squadStagger);
                }
                yield return new WaitForSeconds(g.interval);
            }
        }
        finished();
    }
}
#endif

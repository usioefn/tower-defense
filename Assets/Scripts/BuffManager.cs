using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guarda los beneficios elegidos (con sus niveles) y calcula los modificadores efectivos
/// de cada tipo de torre. También arma la tirada de la ruleta.
/// </summary>
public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    readonly List<Buff> catalog = new List<Buff>();
    readonly Dictionary<string, int> stacks = new Dictionary<string, int>();
    readonly Dictionary<string, Modifiers> perTower = new Dictionary<string, Modifiers>();
    readonly Dictionary<string, Modifiers> cache = new Dictionary<string, Modifiers>();
    Modifiers global = new Modifiers();

    /// <summary>Sube cada vez que se elige un beneficio (las torres lo usan para refrescarse).</summary>
    public int Version { get; private set; }

    public Modifiers Global { get { return global; } }
    public IList<Buff> AllBuffs { get { return catalog; } }

    void Awake()
    {
        Instance = this;
        BuffCatalog.Register(catalog);
        Rebuild();
    }

    // ---------- Consulta ----------

    public int GetStacks(Buff b)
    {
        int n;
        return stacks.TryGetValue(b.id, out n) ? n : 0;
    }

    public bool IsAvailable(Buff b)
    {
        return GetStacks(b) < b.maxStacks;
    }

    /// <summary>Modificadores efectivos de un tipo de torre: globales + los específicos de ese tipo.</summary>
    public Modifiers Get(string towerId)
    {
        Modifiers result;
        if (cache.TryGetValue(towerId, out result)) return result;

        Modifiers specific;
        if (!perTower.TryGetValue(towerId, out specific)) specific = Modifiers.Identity;
        result = Modifiers.Combine(global, specific);
        cache[towerId] = result;
        return result;
    }

    // ---------- Elegir ----------

    public void Pick(Buff b)
    {
        if (b == null || !IsAvailable(b)) return;
        stacks[b.id] = GetStacks(b) + 1;
        Rebuild();
    }

    void Rebuild()
    {
        global = new Modifiers();
        perTower.Clear();
        cache.Clear();

        foreach (var b in catalog)
        {
            int n = GetStacks(b);
            if (n <= 0) continue;

            Modifiers target = global;
            if (b.scope == BuffScope.Tower)
            {
                if (!perTower.TryGetValue(b.towerId, out target))
                {
                    target = new Modifiers();
                    perTower[b.towerId] = target;
                }
            }
            for (int i = 0; i < n; i++) b.apply(target);
        }
        Version++;
    }

    // ---------- Tirada de la ruleta ----------

    /// <summary>
    /// Devuelve hasta 3 opciones distintas: las 2 primeras específicas de un tipo de torre (preferentemente de
    /// las que el jugador tenga construidas) y la última global (para todas las torres).
    /// </summary>
    public List<Buff> Roll()
    {
        var result = new List<Buff>();

        var globals = new List<Buff>();
        var towerBuffs = new List<Buff>();
        foreach (var b in catalog)
        {
            if (!IsAvailable(b)) continue;
            if (b.scope == BuffScope.Global) globals.Add(b); else towerBuffs.Add(b);
        }

        // 2 específicas de torre (primeras dos cartas): preferentemente de torres que ya estén en el mapa y de tipos distintos
        var owned = new HashSet<string>();
        foreach (var t in Tower.All) if (t != null) owned.Add(t.TowerId);

        var preferred = new List<Buff>();
        foreach (var b in towerBuffs) if (owned.Contains(b.towerId)) preferred.Add(b);
        var pool = preferred.Count > 0 ? preferred : new List<Buff>(towerBuffs);
        for (int i = 0; i < 2 && pool.Count > 0; i++)
        {
            // si hay opciones de otra torre distinta a la ya elegida, se usan esas primero
            var candidates = new List<Buff>();
            foreach (var b in pool) if (result.Count == 0 || b.towerId != result[0].towerId) candidates.Add(b);
            if (candidates.Count == 0) candidates = pool;
            var pick = candidates[Random.Range(0, candidates.Count)];
            result.Add(pick);
            pool.Remove(pick);
            towerBuffs.Remove(pick);
        }

        // 1 global (última carta)
        if (globals.Count > 0)
        {
            int gi = Random.Range(0, globals.Count);
            result.Add(globals[gi]);
            globals.RemoveAt(gi);
        }

        // Si faltan (por beneficios al máximo), completar con lo que quede
        var rest = new List<Buff>(globals);
        rest.AddRange(towerBuffs);
        while (result.Count < 3 && rest.Count > 0)
        {
            int idx = Random.Range(0, rest.Count);
            result.Add(rest[idx]);
            rest.RemoveAt(idx);
        }

        return result;
    }
}

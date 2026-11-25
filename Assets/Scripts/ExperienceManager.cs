using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Experiencia y nivel del jugador. Cada enemigo derrotado da XP; al subir de nivel
/// se encola un premio (la ruleta de beneficios) que la UI consume de a uno.
/// </summary>
public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance { get; private set; }

    [Header("Curva de experiencia")]
    [Tooltip("XP necesaria para pasar del nivel 1 al 2.")]
    public int baseXpToLevel = 40;
    [Tooltip("Cuánta XP más se necesita en cada nivel siguiente.")]
    public int xpGrowthPerLevel = 22;
    [Tooltip("Término cuadrático: los niveles altos cuestan bastante más (importa en partidas de 100 oleadas).")]
    public int xpGrowthQuadratic = 4;

    [Header("Pruebas")]
    [Tooltip("TEMPORAL: si está activo, los enemigos no dan XP (no hay subidas de nivel ni ruleta). Desactivalo para volver a jugar normal.")]
    public bool xpDisabled = false;

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public int XpToNext { get; private set; }
    public int PendingLevelUps { get { return pending.Count; } }

    public event Action<int> OnLevelUp;   // nivel alcanzado

    readonly Queue<int> pending = new Queue<int>();

    void Awake()
    {
        Instance = this;
        Level = 1;
        Xp = 0;
        XpToNext = NeededFor(Level);
    }

int NeededFor(int level)
    {
        int n = level - 1;
        return baseXpToLevel + xpGrowthPerLevel * n + xpGrowthQuadratic * n * n;
    }

    public void AddXp(int amount)
    {
        if (xpDisabled) return;
        if (amount <= 0) return;

        float mult = BuffManager.Instance != null ? BuffManager.Instance.Global.xpMult : 1f;
        Xp += Mathf.Max(1, Mathf.RoundToInt(amount * mult));

        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            XpToNext = NeededFor(Level);
            pending.Enqueue(Level);
            if (OnLevelUp != null) OnLevelUp(Level);
        }
    }

    /// <summary>Saca el siguiente nivel pendiente de premiar (0 si no hay).</summary>
    public int ConsumePendingLevelUp()
    {
        return pending.Count > 0 ? pending.Dequeue() : 0;
    }
}

#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Parte del banco de pruebas que enfrenta cada torre a su enemigo counter y a un "gemelo" sin resistencias.</summary>
public partial class TowerBench
{
    // 450 de presupuesto, nivel 1, vida x3. La diferencia entre el gemelo y el counter mide cuánto aporta la resistencia.
    IEnumerator CounterTest(StringBuilder sb)
    {
        var sp = WaveSpawner.Instance;
        Enemy[] prefabs = { sp.enemyArmored, sp.enemyGolem, sp.enemySalamander, sp.enemyInsulated, sp.enemyAmphibian, sp.enemyFlyer };
        int[] counts = { 14, 6, 14, 14, 14, 14 };
        string[] order = { "Tower_Basica", "Tower_Canon", "Tower_Hielo", "Tower_Electrica", "Tower_Fuego", "Tower_Castillo" };
        sb.AppendLine("== Counter vs gemelo sin resistencias (450 de presupuesto, nivel 1, vida x3) ==");
        for (int i = 0; i < order.Length; i++)
        {
            var w = new Wave
            {
                name = "test", hpMultiplier = 3f, speedMultiplier = 1f, rewardMultiplier = 1f, damageMultiplier = 1f,
                groups = new[] { new WaveGroup { enemyPrefab = prefabs[i], count = counts[i], interval = 0.7f } }
            };
            Result twin = default(Result), real = default(Result);
            Tweak = e =>
            {
                e.isFlying = false; e.armor = 0f; e.areaTakenMult = 1f; e.electricTakenMult = 1f; e.fireTakenMult = 1f;
                e.immuneSlow = false; e.immuneStun = false; e.immuneFreeze = false; e.immuneBurn = false;
            };
            yield return Run(order[i], 450, 1, false, -1, w, rr => twin = rr);
            Tweak = null;
            yield return Run(order[i], 450, 1, false, -1, w, rr => real = rr);
            sb.AppendLine(prefabs[i].name + " vs " + real.name
                + " | gemelo: puerta -" + twin.gateLoss.ToString("0") + " prog " + (twin.avgProgress * 100f).ToString("0") + "% t=" + twin.clearTime.ToString("0") + "s"
                + " | counter: puerta -" + real.gateLoss.ToString("0") + " prog " + (real.avgProgress * 100f).ToString("0") + "% t=" + real.clearTime.ToString("0") + "s"
                + (real.timedOut ? " [TIEMPO]" : ""));
            Progress = sb.ToString();
        }
        Tweak = null;
    }
}
#endif

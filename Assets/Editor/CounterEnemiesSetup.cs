#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Crea los 6 enemigos counter (uno por tipo de torre) y los conecta al WaveSpawner. No regenera nada más.</summary>
public static class CounterEnemiesSetup
{
    const string PrefabsDir = "Assets/Prefabs";
    const string SpritesDir = "Assets/Sprites";

    /// <summary>El Cañón (artillería) no puede apuntar a voladores. Poné false para que los vuelva a alcanzar.</summary>
    const bool CannonGroundOnly = true;

    static GameObject SpriteChild(string name, Transform parent, Sprite sprite, Color color, Vector2 localPos, Vector2 scale, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    /// <summary>Mismo molde que los otros enemigos: cuerpo circular con barra de vida. Devuelve el componente ya guardado como prefab.</summary>
    static Enemy Make(string name, Color color, float bodyScale, float hp, float speed, int reward, float damage, Sprite square, Sprite circle, System.Action<Enemy> configure)
    {
        var root = new GameObject(name);
        SpriteChild("Body", root.transform, circle, color, Vector2.zero, Vector2.one * bodyScale, 5);

        bool small = bodyScale < 0.5f;
        float barY = small ? 0.28f : 0.5f;
        float barW = small ? 0.4f : 0.8f;
        SpriteChild("HealthBack", root.transform, square, new Color(0f, 0f, 0f, 0.7f), new Vector2(0f, barY), new Vector2(barW, small ? 0.09f : 0.12f), 7);
        var holder = new GameObject("HealthFill");
        holder.transform.SetParent(root.transform, false);
        holder.transform.localPosition = new Vector3(-barW / 2f, barY, 0f);
        SpriteChild("Bar", holder.transform, square, new Color(0.2f, 0.9f, 0.3f), new Vector2(barW / 2f, 0f), new Vector2(barW, small ? 0.07f : 0.1f), 8);

        var e = root.AddComponent<Enemy>();
        e.maxHealth = hp;
        e.speed = speed;
        e.reward = reward;
        e.damageToBase = damage;
        e.healthFill = holder.transform;
        if (configure != null) configure(e);

        string path = PrefabsDir + "/" + name + ".prefab";
        var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return asset.GetComponent<Enemy>();
    }

    [MenuItem("Tower Defense/Aplicar enemigos counter y director de amenazas")]
    public static void Apply()
    {
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/Square.png");
        Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/Circle.png");
        if (square == null || circle == null)
        {
            Debug.LogError("[TowerDefense] No encontré Assets/Sprites/Square.png o Circle.png. Corré primero 'Construir escena base'.");
            return;
        }

        // Murciélago: vuela, sale de a 2, rápido y frágil. Los soldados cuerpo a cuerpo no lo alcanzan.
        Enemy flyer = Make("Enemy_Volador", new Color(0.6f, 0.3f, 0.9f), 0.34f, 26f, 2.6f, 6, 1.5f, square, circle, e =>
        {
            e.isFlying = true; e.squadSize = 2; e.squadStagger = 0.25f;
        });
        // Golem: lento y enorme, recibe solo 30 % del daño de área (counter del Cañón).
        Enemy golem = Make("Enemy_Golem", new Color(0.55f, 0.48f, 0.4f), 0.7f, 220f, 0.9f, 22, 3f, square, circle, e =>
        {
            e.areaTakenMult = 0.3f;
        });
        // Blindado: la armadura se come los impactos chicos (counter de la Básica).
        Enemy armored = Make("Enemy_Blindado", new Color(0.65f, 0.7f, 0.78f), 0.5f, 70f, 1.4f, 15, 2f, square, circle, e =>
        {
            e.armor = 6f;
        });
        // Salamandra: inmune a ralentización y congelación (counter del Hielo).
        Enemy salamander = Make("Enemy_Salamandra", new Color(1f, 0.45f, 0.15f), 0.45f, 60f, 2f, 12, 1.5f, square, circle, e =>
        {
            e.immuneSlow = true; e.immuneFreeze = true;
        });
        // Aislante: no se aturde y casi no siente la electricidad (counter de la Eléctrica).
        Enemy insulated = Make("Enemy_Aislante", new Color(0.2f, 0.5f, 0.45f), 0.45f, 65f, 1.7f, 12, 1.5f, square, circle, e =>
        {
            e.immuneStun = true; e.electricTakenMult = 0.25f;
        });
        // Anfibio: no se quema y el fuego directo le hace poco (counter del Fuego).
        Enemy amphibian = Make("Enemy_Anfibio", new Color(0.55f, 0.85f, 0.2f), 0.45f, 70f, 1.6f, 12, 1.5f, square, circle, e =>
        {
            e.immuneBurn = true; e.fireTakenMult = 0.4f;
        });
        AssetDatabase.SaveAssets();

        // Cañón = artillería terrestre
        var cannon = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/Tower_Canon.prefab");
        if (cannon != null)
        {
            var ct = cannon.GetComponent<Tower>();
            ct.groundOnly = CannonGroundOnly;
            EditorUtility.SetDirty(ct);
            AssetDatabase.SaveAssets();
        }

        var sys = GameObject.Find("GameSystems");
        var spawner = sys != null ? sys.GetComponent<WaveSpawner>() : null;
        if (spawner == null)
        {
            Debug.LogWarning("[TowerDefense] Prefabs creados, pero no encontré GameSystems/WaveSpawner en la escena abierta.");
            return;
        }
        spawner.enemyFlyer = flyer;
        spawner.enemyGolem = golem;
        spawner.enemyArmored = armored;
        spawner.enemySalamander = salamander;
        spawner.enemyInsulated = insulated;
        spawner.enemyAmphibian = amphibian;
        spawner.useThreatDirector = true;
        EditorUtility.SetDirty(spawner);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TowerDefense] Enemigos counter y director de amenazas aplicados.");
    }
}
#endif

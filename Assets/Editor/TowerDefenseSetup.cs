#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Genera sprites, prefabs y la escena base del Tower Defense.
/// Menú: Tower Defense > Construir escena base (se puede ejecutar varias veces).
/// </summary>
public static class TowerDefenseSetup
{
    const string SpritesDir = "Assets/Sprites";
    const string PrefabsDir = "Assets/Prefabs";

    // Waypoints en coordenadas de la grilla (cada casilla = 1 unidad)
    static readonly Vector2Int[] PathPoints =
    {
        new Vector2Int(-9, 3), new Vector2Int(-6, 3), new Vector2Int(-6, -2), new Vector2Int(-2, -2),
        new Vector2Int(-2, 2), new Vector2Int(3, 2), new Vector2Int(3, -3), new Vector2Int(7, -3),
        new Vector2Int(7, 1), new Vector2Int(8, 1)
    };

    const int GridMinX = -8, GridMaxX = 8, GridMinY = -4, GridMaxY = 4;

    [MenuItem("Tower Defense/Construir escena base")]
    public static void Build()
    {
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets", "Prefabs");

        Sprite square = CreateSprite("Square", false);
        Sprite circle = CreateSprite("Circle", true);

        // ----- Prefabs -----
        Projectile projectile = MakeProjectilePrefab(circle);

        Enemy basic = MakeEnemyPrefab("Enemy_Basico", new Color(0.9f, 0.25f, 0.25f), 0.6f, 30f, 1.8f, 6, 1, square, circle);
        Enemy fast = MakeFastPrefab(square, circle);
        MakeArcherPrefab(square, circle);
        Enemy tank = MakeEnemyPrefab("Enemy_Tanque", new Color(0.55f, 0.25f, 0.75f), 0.85f, 150f, 1.0f, 20, 3, square, circle);

        Tower basicTower = MakeTowerPrefab("Tower_Basica", "Torre Básica", new Color(0.25f, 0.5f, 0.9f), 50, 2.6f, 1.8f, 9f,
            0f, 1f, 0f, 12f, new Color(1f, 0.95f, 0.5f), projectile, square, circle);
        Tower cannonTower = MakeTowerPrefab("Tower_Canon", "Cañón", new Color(0.9f, 0.5f, 0.15f), 100, 2.4f, 0.7f, 28f,
            1.1f, 1f, 0f, 8f, new Color(1f, 0.55f, 0.2f), projectile, square, circle);
        Tower frostTower = MakeTowerPrefab("Tower_Hielo", "Torre de Hielo", new Color(0.35f, 0.85f, 0.95f), 75, 2.2f, 1.2f, 3f,
            0f, 0.5f, 2f, 10f, new Color(0.6f, 0.95f, 1f), projectile, square, circle);

        Enemy bossEnemy = MakeBossPrefab(square, circle);
        Tower electricTower = MakeElectricTowerPrefab(projectile, square, circle);
        Tower fireTower = MakeFireTowerPrefab(projectile, square, circle);
        Tower castleTower = MakeCastleTowerPrefab(square, circle);

        // ----- Escena -----
        DestroyIfExists("Map");
        DestroyIfExists("Waypoints");
        DestroyIfExists("GameSystems");
        DestroyIfExists("Base");

        SetupCamera();
        BuildMap(square);
        BuildSystems(square, circle, basic, fast, tank, bossEnemy, new[] { basicTower, cannonTower, frostTower, electricTower, fireTower, castleTower });

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[TowerDefense] Escena base construida y guardada.");
    }

    // ================= Escena =================

    static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        cam.orthographicSize = 5.75f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        EditorUtility.SetDirty(cam);
    }

static void BuildMap(Sprite square)
    {
        // El mapa (casillas, camino aleatorio, waypoints y base) se genera al iniciar cada partida
        var go = new GameObject("Map");
        var gen = go.AddComponent<MapGenerator>();
        gen.squareSprite = square;
        EditorUtility.SetDirty(gen);
    }

    static void BuildSystems(Sprite square, Sprite circle, Enemy basic, Enemy fast, Enemy tank, Enemy boss, Tower[] towers)
    {
        var sys = new GameObject("GameSystems");

        var gm = sys.AddComponent<GameManager>();
        gm.startMoney = 150;
        gm.gateHealth = 100;

        var spawner = sys.AddComponent<WaveSpawner>();
        spawner.useGeneratedWaves = true;
        spawner.totalWaves = 100;
        spawner.bossInterval = 10;
        spawner.enemyBasic = basic;
        spawner.enemyFast = fast;
        spawner.enemyTank = tank;
        spawner.enemyBoss = boss;
        spawner.enemyArcher = LoadEnemyPrefab("Enemy_Arquero");
        spawner.waves = new[]
        {
            W("Oleada 1", G(basic, 6, 1.0f)),
            W("Oleada 2", G(basic, 10, 0.8f)),
            W("Oleada 3", G(basic, 8, 0.8f), G(fast, 4, 0.6f)),
            W("Oleada 4", G(fast, 10, 0.5f)),
            W("Oleada 5", G(tank, 3, 2.0f), G(basic, 8, 0.7f)),
            W("Oleada 6", G(basic, 12, 0.6f), G(fast, 6, 0.5f)),
            W("Oleada 7", G(tank, 6, 1.5f)),
            W("Oleada 8", G(fast, 10, 0.4f), G(tank, 5, 1.5f)),
            W("Oleada 9", G(basic, 20, 0.4f), G(fast, 8, 0.4f)),
            W("Oleada 10", G(tank, 10, 1.2f), G(fast, 15, 0.4f)),
        };

        var bm = sys.AddComponent<BuildManager>();
        bm.towerPrefabs = towers;
        bm.squareSprite = square;
        bm.circleSprite = circle;

        sys.AddComponent<GameUI>();

        EditorUtility.SetDirty(gm);
        EditorUtility.SetDirty(spawner);
        EditorUtility.SetDirty(bm);
    }

    static WaveGroup G(Enemy prefab, int count, float interval)
    {
        return new WaveGroup { enemyPrefab = prefab, count = count, interval = interval };
    }

    static Wave W(string name, params WaveGroup[] groups)
    {
        return new Wave { name = name, groups = groups, bonus = 25 };
    }

    // ================= Prefabs =================

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

    static T SavePrefab<T>(GameObject root, string name) where T : Component
    {
        string path = PrefabsDir + "/" + name + ".prefab";
        var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return asset.GetComponent<T>();
    }

    static Projectile MakeProjectilePrefab(Sprite circle)
    {
        var go = new GameObject("Projectile");
        go.transform.localScale = Vector3.one * 0.22f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle;
        sr.sortingOrder = 6;
        go.AddComponent<Projectile>();
        return SavePrefab<Projectile>(go, "Projectile");
    }

    static Enemy MakeEnemyPrefab(string name, Color color, float bodyScale, float hp, float speed, int reward, float damage, Sprite square, Sprite circle)
    {
        var root = new GameObject(name);
        SpriteChild("Body", root.transform, circle, color, Vector2.zero, Vector2.one * bodyScale, 5);

        // Los enemigos chicos llevan una barra de vida más corta y más pegada al cuerpo
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

        return SavePrefab<Enemy>(root, name);
    }

    static Tower MakeTowerPrefab(string name, string display, Color color, int cost, float range, float fireRate, float damage,
                                 float splash, float slowMul, float slowDur, float projSpeed, Color projColor,
                                 Projectile projectile, Sprite square, Sprite circle)
    {
        var root = new GameObject(name);

        SpriteChild("Body", root.transform, circle, color, Vector2.zero, Vector2.one * 0.85f, 4);
        var rangeGo = SpriteChild("Range", root.transform, circle, new Color(1f, 1f, 1f, 0.15f), Vector2.zero, Vector2.one * range * 2f, 1);
        rangeGo.SetActive(false);

        var head = new GameObject("Head");
        head.transform.SetParent(root.transform, false);
        SpriteChild("Barrel", head.transform, square, new Color(0.15f, 0.15f, 0.18f), new Vector2(0f, 0.28f), new Vector2(0.22f, 0.6f), 5);
        var firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(head.transform, false);
        firePoint.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        var t = root.AddComponent<Tower>();
        t.towerName = display;
        t.towerId = name; // los beneficios por tipo de torre usan este id
        t.cost = cost;
        t.range = range;
        t.fireRate = fireRate;
        t.damage = damage;
        t.splashRadius = splash;
        t.slowMultiplier = slowMul;
        t.slowDuration = slowDur;
        t.projectileSpeed = projSpeed;
        t.projectileColor = projColor;
        t.projectilePrefab = projectile;
        t.head = head.transform;
        t.firePoint = firePoint.transform;
        t.rangeIndicator = rangeGo.transform;

        return SavePrefab<Tower>(root, name);
    }

    // ================= Utilidades =================

    static Sprite CreateSprite(string name, bool circle)
    {
        string path = SpritesDir + "/" + name + ".png";
        if (!File.Exists(path))
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    if (circle)
                    {
                        float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        a = Mathf.Clamp01(radius - dist);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static void DestroyIfExists(string objectName)
    {
        var go = GameObject.Find(objectName);
        while (go != null)
        {
            Object.DestroyImmediate(go);
            go = GameObject.Find(objectName);
        }
    }


[MenuItem("Tower Defense/Crear menú principal")]
    public static void BuildMainMenu()
    {
        const string menuPath = "Assets/Scenes/MainMenu.unity";
        const string gamePath = "Assets/Scenes/SampleScene.unity";

        // Guardar lo que haya abierto antes de cambiar de escena
        var current = EditorSceneManager.GetActiveScene();
        if (current.isDirty) EditorSceneManager.SaveScene(current);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.75f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        new GameObject("MainMenu").AddComponent<MainMenu>();

        EditorSceneManager.SaveScene(scene, menuPath);

        // Orden de escenas: 0 = menú, 1 = juego
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(menuPath, true),
            new EditorBuildSettingsScene(gamePath, true)
        };

        Debug.Log("[TowerDefense] Menú principal creado y escenas agregadas a Build Settings.");
    }


/// <summary>Crea el prefab de la torre eléctrica: el disparo salta a 2 enemigos más y los aturde 0,5 s.</summary>
    static Tower MakeElectricTowerPrefab(Projectile projectile, Sprite square, Sprite circle)
    {
        // El rayo se dibuja con segmentos cuadrados
        projectile.lineSprite = square;
        EditorUtility.SetDirty(projectile);

        Tower tower = MakeTowerPrefab("Tower_Electrica", "Torre Eléctrica", new Color(0.55f, 0.45f, 0.95f), 125, 2.3f, 0.8f, 6f,
            0f, 1f, 0f, 14f, new Color(0.75f, 0.85f, 1f), projectile, square, circle);

        tower.chainCount = 2;
        tower.chainRange = 1.8f;
        tower.chainDamageMult = 0.5f;
        tower.stunDuration = 0.5f;
        EditorUtility.SetDirty(tower);
        AssetDatabase.SaveAssets();
        return tower;
    }

    /// <summary>Agrega la torre eléctrica a un proyecto/escena ya construidos, sin regenerar todo.</summary>
    [MenuItem("Tower Defense/Agregar torre eléctrica")]
    public static void AddElectricTower()
    {
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets", "Prefabs");
        Sprite square = CreateSprite("Square", false);
        Sprite circle = CreateSprite("Circle", true);

        var projectileAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/Projectile.prefab");
        if (projectileAsset == null)
        {
            Debug.LogError("[TowerDefense] No existe Projectile.prefab. Ejecutá primero 'Tower Defense > Construir escena base'.");
            return;
        }

        Tower electric = MakeElectricTowerPrefab(projectileAsset.GetComponent<Projectile>(), square, circle);

        var sys = GameObject.Find("GameSystems");
        var bm = sys != null ? sys.GetComponent<BuildManager>() : null;
        if (bm == null)
        {
            Debug.LogWarning("[TowerDefense] Prefab creado, pero no encontré GameSystems/BuildManager en la escena abierta. Agregalo a mano a BuildManager.towerPrefabs.");
            return;
        }

        var list = new List<Tower>(bm.towerPrefabs);
        bool exists = false;
        foreach (var t in list) if (t != null && t.name == electric.name) exists = true;
        if (!exists) list.Add(electric);
        bm.towerPrefabs = list.ToArray();

        EditorUtility.SetDirty(bm);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TowerDefense] Torre eléctrica agregada (tecla 4).");
    }


/// <summary>Crea el prefab de la torre de fuego: alcance corto, disparo perforante y quemadura.</summary>
    static Tower MakeFireTowerPrefab(Projectile projectile, Sprite square, Sprite circle)
    {
        Tower tower = MakeTowerPrefab("Tower_Fuego", "Torre de Fuego", new Color(0.95f, 0.4f, 0.1f), 90, 1.6f, 1.4f, 5f,
            0f, 1f, 0f, 9f, new Color(1f, 0.55f, 0.1f), projectile, square, circle);

        tower.pierceCount = 5;      // atraviesa hasta 5 enemigos
        tower.burnDps = 6f;         // 6 de daño por segundo...
        tower.burnDuration = 3f;    // ...durante 3 segundos (18 en total)
        tower.targetMode = TargetMode.Last; // apunta al último para que el disparo atraviese a toda la fila
        EditorUtility.SetDirty(tower);
        AssetDatabase.SaveAssets();
        return tower;
    }

    /// <summary>Agrega la torre de fuego a un proyecto/escena ya construidos, sin regenerar todo.</summary>
    [MenuItem("Tower Defense/Agregar torre de fuego")]
    public static void AddFireTower()
    {
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets", "Prefabs");
        Sprite square = CreateSprite("Square", false);
        Sprite circle = CreateSprite("Circle", true);

        var projectileAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/Projectile.prefab");
        if (projectileAsset == null)
        {
            Debug.LogError("[TowerDefense] No existe Projectile.prefab. Ejecutá primero 'Tower Defense > Construir escena base'.");
            return;
        }

        Tower fire = MakeFireTowerPrefab(projectileAsset.GetComponent<Projectile>(), square, circle);

        var sys = GameObject.Find("GameSystems");
        var bm = sys != null ? sys.GetComponent<BuildManager>() : null;
        if (bm == null)
        {
            Debug.LogWarning("[TowerDefense] Prefab creado, pero no encontré GameSystems/BuildManager en la escena abierta. Agregalo a mano a BuildManager.towerPrefabs.");
            return;
        }

        var list = new List<Tower>(bm.towerPrefabs);
        bool exists = false;
        foreach (var t in list) if (t != null && t.name == fire.name) exists = true;
        if (!exists) list.Add(fire);
        bm.towerPrefabs = list.ToArray();

        EditorUtility.SetDirty(bm);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TowerDefense] Torre de fuego agregada (tecla 5).");
    }


/// <summary>Prefab del soldado del Castillo (el esqueleto es el mismo prefab con otro color y tamaño).</summary>
    static Soldier MakeSoldierPrefab(Sprite square, Sprite circle)
    {
        var root = new GameObject("Soldier");
        var body = SpriteChild("Body", root.transform, circle, new Color(0.3f, 0.55f, 0.95f), Vector2.zero, Vector2.one * 0.38f, 5);
        SpriteChild("Sword", root.transform, square, new Color(0.85f, 0.85f, 0.9f), new Vector2(0.2f, 0.05f), new Vector2(0.06f, 0.34f), 6);

        SpriteChild("HealthBack", root.transform, square, new Color(0f, 0f, 0f, 0.7f), new Vector2(0f, 0.34f), new Vector2(0.5f, 0.08f), 7);
        var holder = new GameObject("HealthFill");
        holder.transform.SetParent(root.transform, false);
        holder.transform.localPosition = new Vector3(-0.25f, 0.34f, 0f);
        SpriteChild("Bar", holder.transform, square, new Color(0.3f, 0.9f, 0.4f), new Vector2(0.25f, 0f), new Vector2(0.5f, 0.06f), 8);

        var s = root.AddComponent<Soldier>();
        s.healthFill = holder.transform;
        s.body = body.GetComponent<SpriteRenderer>();
        return SavePrefab<Soldier>(root, "Soldier");
    }

    /// <summary>
    /// Prefab del Castillo: no dispara, suelta 3 soldados sobre el camino. Cuesta 150, 4 niveles.
    /// El daño del Tower es el daño de cada soldado; el alcance es hasta dónde llega a poner el puesto.
    /// </summary>
    static Tower MakeCastleTowerPrefab(Sprite square, Sprite circle)
    {
        Soldier soldier = MakeSoldierPrefab(square, circle);

        var root = new GameObject("Tower_Castillo");
        SpriteChild("Wall", root.transform, square, new Color(0.62f, 0.62f, 0.68f), Vector2.zero, new Vector2(0.8f, 0.8f), 4);
        var turret = new Color(0.45f, 0.45f, 0.52f);
        const float c = 0.38f;
        SpriteChild("TurretNW", root.transform, square, turret, new Vector2(-c, c), Vector2.one * 0.3f, 5);
        SpriteChild("TurretNE", root.transform, square, turret, new Vector2(c, c), Vector2.one * 0.3f, 5);
        SpriteChild("TurretSW", root.transform, square, turret, new Vector2(-c, -c), Vector2.one * 0.3f, 5);
        SpriteChild("TurretSE", root.transform, square, turret, new Vector2(c, -c), Vector2.one * 0.3f, 5);
        SpriteChild("Gate", root.transform, square, new Color(0.25f, 0.15f, 0.1f), new Vector2(0f, -0.2f), new Vector2(0.22f, 0.3f), 6);

        const float range = 2.2f;
        var rangeGo = SpriteChild("Range", root.transform, circle, new Color(1f, 1f, 1f, 0.15f), Vector2.zero, Vector2.one * range * 2f, 1);
        rangeGo.SetActive(false);

        var t = root.AddComponent<Tower>();
        t.towerName = "Castillo";
        t.towerId = "Tower_Castillo"; // los beneficios por tipo de torre usan este id
        t.cost = 150;
        t.range = range;
        t.fireRate = 1f;
        t.damage = 8f;        // daño por golpe de cada soldado (sube x1,5 por nivel)
        t.maxLevel = 4;
        t.rangeIndicator = rangeGo.transform;

        var castle = root.AddComponent<CastleTower>();
        castle.soldierPrefab = soldier;

        return SavePrefab<Tower>(root, "Tower_Castillo");
    }

    /// <summary>Agrega el Castillo a un proyecto/escena ya construidos, sin regenerar todo.</summary>
    [MenuItem("Tower Defense/Agregar torre castillo")]
    public static void AddCastleTower()
    {
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets", "Prefabs");
        Sprite square = CreateSprite("Square", false);
        Sprite circle = CreateSprite("Circle", true);

        Tower castle = MakeCastleTowerPrefab(square, circle);

        var sys = GameObject.Find("GameSystems");
        var bm = sys != null ? sys.GetComponent<BuildManager>() : null;
        if (bm == null)
        {
            Debug.LogWarning("[TowerDefense] Prefab creado, pero no encontré GameSystems/BuildManager en la escena abierta. Agregalo a mano a BuildManager.towerPrefabs.");
            return;
        }

        var list = new List<Tower>(bm.towerPrefabs);
        bool exists = false;
        foreach (var t in list) if (t != null && t.name == castle.name) exists = true;
        if (!exists) list.Add(castle);
        bm.towerPrefabs = list.ToArray();

        EditorUtility.SetDirty(bm);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TowerDefense] Castillo agregado (tecla 6).");
    }


/// <summary>Convierte la escena abierta al mapa aleatorio, sin tocar torres, prefabs ni oleadas.</summary>
    [MenuItem("Tower Defense/Usar mapa aleatorio")]
    public static void UseRandomMap()
    {
        EnsureFolder("Assets", "Sprites");
        Sprite square = CreateSprite("Square", false);

        DestroyIfExists("Map");
        DestroyIfExists("Waypoints");
        DestroyIfExists("Base");
        BuildMap(square);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TowerDefense] La escena ahora usa mapa aleatorio (MapGenerator).");
    }


/// <summary>Prefab del jefe: grande, lento, con mucha vida y que le quita la mitad de las vidas a la base si llega.</summary>
    static Enemy MakeBossPrefab(Sprite square, Sprite circle)
    {
        var root = new GameObject("Enemy_Jefe");

        // El primer hijo con SpriteRenderer es el que Enemy tiñe (aturdimiento/quemadura/color del jefe)
        SpriteChild("Body", root.transform, circle, new Color(0.75f, 0.1f, 0.3f), Vector2.zero, Vector2.one * 1.25f, 5);
        SpriteChild("Aura", root.transform, circle, new Color(0.15f, 0.03f, 0.08f, 0.9f), Vector2.zero, Vector2.one * 1.5f, 4);
        SpriteChild("Core", root.transform, circle, new Color(1f, 0.85f, 0.3f), Vector2.zero, Vector2.one * 0.45f, 6);

        SpriteChild("HealthBack", root.transform, square, new Color(0f, 0f, 0f, 0.75f), new Vector2(0f, 1.0f), new Vector2(1.5f, 0.18f), 7);
        var holder = new GameObject("HealthFill");
        holder.transform.SetParent(root.transform, false);
        holder.transform.localPosition = new Vector3(-0.75f, 1.0f, 0f);
        SpriteChild("Bar", holder.transform, square, new Color(0.9f, 0.25f, 0.3f), new Vector2(0.75f, 0f), new Vector2(1.5f, 0.14f), 8);

        var e = root.AddComponent<Enemy>();
        e.maxHealth = 900f;
        e.speed = 0.9f;
        e.reward = 150;
        e.damageToBase = 10;
        e.healthFill = holder.transform;

        return SavePrefab<Enemy>(root, "Enemy_Jefe");
    }

    static Enemy LoadEnemyPrefab(string prefabName)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/" + prefabName + ".prefab");
        return go != null ? go.GetComponent<Enemy>() : null;
    }

    /// <summary>Configura una escena ya construida para 100 oleadas con un jefe cada 10, sin regenerar todo.</summary>
    [MenuItem("Tower Defense/Configurar 100 oleadas con jefes")]
    public static void ConfigureHundredWaves()
    {
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets", "Prefabs");
        Sprite square = CreateSprite("Square", false);
        Sprite circle = CreateSprite("Circle", true);

        Enemy boss = MakeBossPrefab(square, circle);

        var sys = GameObject.Find("GameSystems");
        var spawner = sys != null ? sys.GetComponent<WaveSpawner>() : null;
        if (spawner == null)
        {
            Debug.LogWarning("[TowerDefense] Prefab del jefe creado, pero no encontré GameSystems/WaveSpawner en la escena abierta.");
            return;
        }

        spawner.useGeneratedWaves = true;
        spawner.totalWaves = 100;
        spawner.bossInterval = 10;
        spawner.enemyBasic = LoadEnemyPrefab("Enemy_Basico");
        spawner.enemyFast = LoadEnemyPrefab("Enemy_Rapido");
        spawner.enemyTank = LoadEnemyPrefab("Enemy_Tanque");
        spawner.enemyBoss = boss;
        spawner.enemyArcher = LoadEnemyPrefab("Enemy_Arquero");

        EditorUtility.SetDirty(spawner);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TowerDefense] 100 oleadas con jefe cada 10 configuradas.");
    }

    /// <summary>
    /// Amarillo: sale en escuadras de 3, más chico. Sus estadísticas de antes (20 de vida, 1 de daño, 7 de dinero)
    /// quedan repartidas entre los 3: cada uno tiene 1/3 de vida y de daño.
    /// </summary>
    static Enemy MakeFastPrefab(Sprite square, Sprite circle)
    {
        Enemy e = MakeEnemyPrefab("Enemy_Rapido", new Color(0.95f, 0.85f, 0.2f), 0.3f, 20f / 3f, 3.2f, 2, 1f / 3f, square, circle);
        e.squadSize = 3;
        e.squadStagger = 0.12f;
        EditorUtility.SetDirty(e);
        AssetDatabase.SaveAssets();
        return e;
    }

    /// <summary>Arquero: chiquito, sale de a 2 y ataca desde 2 casillas (a los soldados y a la puerta).</summary>
    static Enemy MakeArcherPrefab(Sprite square, Sprite circle)
    {
        Enemy e = MakeEnemyPrefab("Enemy_Arquero", new Color(0.95f, 0.55f, 0.8f), 0.28f, 12f, 1.6f, 5, 1f, square, circle);
        e.attackInterval = 1.2f;
        e.engageRange = 2f;
        e.gateRange = 2f;
        e.squadSize = 2;
        e.squadStagger = 0.35f;
        EditorUtility.SetDirty(e);
        AssetDatabase.SaveAssets();
        return e;
    }

    /// <summary>Aplica a una escena ya construida los amarillos en escuadra y los arqueros, sin regenerar todo.</summary>
    [MenuItem("Tower Defense/Aplicar escuadras amarillas y arqueros")]
    public static void ApplySquadsAndArchers()
    {
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets", "Prefabs");
        Sprite square = CreateSprite("Square", false);
        Sprite circle = CreateSprite("Circle", true);

        Enemy fast = MakeFastPrefab(square, circle);
        Enemy archer = MakeArcherPrefab(square, circle);

        var sys = GameObject.Find("GameSystems");
        var spawner = sys != null ? sys.GetComponent<WaveSpawner>() : null;
        if (spawner == null)
        {
            Debug.LogWarning("[TowerDefense] Prefabs creados, pero no encontré GameSystems/WaveSpawner en la escena abierta.");
            return;
        }

        spawner.enemyFast = fast;
        spawner.enemyArcher = archer;
        EditorUtility.SetDirty(spawner);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TowerDefense] Amarillos en escuadras de 3 y arqueros de a 2 aplicados.");
    }
}
#endif

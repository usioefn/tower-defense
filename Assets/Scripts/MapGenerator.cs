using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera el mapa en cada partida: casillas, camino aleatorio, waypoints y base.
/// El camino nace en el borde izquierdo y termina en el borde derecho, serpenteando.
/// Los tramos del camino nunca se tocan entre sí, así siempre queda espacio para construir.
/// </summary>
public class MapGenerator : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite squareSprite;

    [Header("Mapa (en casillas)")]
    public int width = 17;
    public int height = 9;

    [Header("Camino")]
    [Tooltip("Largo mínimo del camino. El real es un valor al azar entre este y este + 8.")]
    public int minPathLength = 36;
    public int maxPathLength = 60;

    public enum PathStyle { Ordenado, Libre }

    [Tooltip("Ordenado: serpentina con carriles paralelos (el azar cambia los parámetros). Libre: caminata aleatoria caótica.")]
    public PathStyle pathStyle = PathStyle.Ordenado;

    [Header("Semilla (0 = distinta en cada partida)")]
    public int seed = 0;

    public int CurrentSeed { get; private set; }
    public List<Vector2Int> PathCells { get; private set; }

    static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.up, Vector2Int.down, Vector2Int.left };

    static readonly Color PathColor = new Color(0.76f, 0.63f, 0.42f);
    static readonly Color GrassA = new Color(0.35f, 0.62f, 0.30f);
    static readonly Color GrassB = new Color(0.31f, 0.57f, 0.27f);
    static readonly Color BaseColor = new Color(0.85f, 0.2f, 0.25f);

    void Awake()
    {
        Generate();
    }

    // ================= Generación =================

    void Generate()
    {
        if (squareSprite == null) squareSprite = MakeWhiteSprite();

        CurrentSeed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
        var rng = new System.Random(CurrentSeed);

        List<Vector2Int> path = null;
        for (int attempt = 0; attempt < 30 && path == null; attempt++)
            path = GeneratePath(rng);

        if (path == null)
        {
            Debug.LogWarning("[MapGenerator] No se pudo generar un camino aleatorio; se usa el camino fijo.");
            path = FallbackPath();
        }

        PathCells = path;
        BuildMap(path);
    }

    /// <summary>Busca un camino aleatorio con búsqueda en profundidad. Devuelve null si no encuentra.</summary>
public List<Vector2Int> GeneratePath(System.Random rng)
    {
        return pathStyle == PathStyle.Ordenado ? GenerateOrderedPath(rng) : GenerateFreePath(rng);
    }

    /// <summary>
    /// Camino ordenado: serpentina de carriles verticales paralelos, de izquierda a derecha.
    /// Lo aleatorio son los parámetros: fila de entrada, dirección inicial, cantidad de carriles,
    /// separación entre carriles, márgenes arriba/abajo y la fila donde termina (donde queda la base).
    /// </summary>
    List<Vector2Int> GenerateOrderedPath(System.Random rng)
    {
        int top = height - 1 - rng.Next(0, 2);   // fila superior de los giros (7 u 8)
        int bottom = rng.Next(0, 2);             // fila inferior de los giros (0 o 1)

        int spacing = rng.Next(2, 4);            // columnas entre carriles: 2 (1 de césped) o 3 (2 de césped)
        int x0 = rng.Next(2, 4);                 // columna del primer carril

        // Cantidad de carriles: todos tienen que caber dejando al menos 1 columna para el tramo final
        int maxLanes = Mathf.Min((width - 2 - x0) / spacing + 1, 7);
        const int minLanes = 3;
        if (maxLanes < minLanes) return null;
        int lanes = rng.Next(minLanes, maxLanes + 1);

        // Fila de entrada y dirección del primer carril (hacia el extremo más lejano, para que sea largo)
        int y = rng.Next(0, height);
        int distUp = top - y;
        int distDown = y - bottom;
        bool goingUp = distUp > distDown || (distUp == distDown && rng.Next(2) == 0);

        var path = new List<Vector2Int>();

        // Tramo de entrada, horizontal, hasta el primer carril
        for (int x = 0; x <= x0; x++) path.Add(new Vector2Int(x, y));

        for (int i = 0; i < lanes; i++)
        {
            int laneX = x0 + i * spacing;
            bool last = i == lanes - 1;

            int targetY;
            if (last)
            {
                // El último carril corta a media altura: ahí termina el camino y queda la base
                if (goingUp)
                {
                    int lo = y + 3, hi = Mathf.Min(top, y + 6);
                    if (lo > hi) return null;
                    targetY = rng.Next(lo, hi + 1);
                }
                else
                {
                    int lo = Mathf.Max(bottom, y - 6), hi = y - 3;
                    if (lo > hi) return null;
                    targetY = rng.Next(lo, hi + 1);
                }
            }
            else
            {
                // Los giros caen en el margen; a veces uno se corre una fila para dar variedad sin caos
                int jitter = rng.NextDouble() < 0.25 ? 1 : 0;
                targetY = goingUp ? top - jitter : bottom + jitter;
            }

            int step = targetY > y ? 1 : -1;
            while (y != targetY)
            {
                y += step;
                path.Add(new Vector2Int(laneX, y));
            }

            if (!last)
            {
                // Conector horizontal hacia el siguiente carril
                for (int k = 1; k <= spacing; k++) path.Add(new Vector2Int(laneX + k, y));
                goingUp = !goingUp;
            }
        }

        // Tramo final recto hasta el borde derecho
        int lastX = x0 + (lanes - 1) * spacing;
        for (int x = lastX + 1; x < width; x++) path.Add(new Vector2Int(x, y));

        return IsValidPath(path) ? path : null;
    }

    /// <summary>Camino libre: caminata aleatoria con búsqueda en profundidad (más caótico).</summary>
    List<Vector2Int> GenerateFreePath(System.Random rng)
    {
        int minLen = rng.Next(minPathLength, minPathLength + 9);

        var path = new List<Vector2Int>();
        var used = new HashSet<Vector2Int>();
        var start = new Vector2Int(0, rng.Next(0, height));
        path.Add(start);
        used.Add(start);

        int budget = 20000;
        return Extend(path, used, rng, minLen, ref budget) ? path : null;
    }

    /// <summary>Comprueba: dentro del mapa, casillas contiguas, sin repetir, sin tocarse entre tramos y con el largo permitido.</summary>
    bool IsValidPath(List<Vector2Int> path)
    {
        if (path == null || path.Count < minPathLength || path.Count > maxPathLength) return false;

        var index = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int c = path[i];
            if (c.x < 0 || c.x >= width || c.y < 0 || c.y >= height) return false;
            if (index.ContainsKey(c)) return false;
            index[c] = i;
            if (i > 0 && (c - path[i - 1]).sqrMagnitude != 1) return false;
        }

        if (path[0].x != 0 || path[path.Count - 1].x != width - 1) return false;

        for (int i = 0; i < path.Count; i++)
        {
            foreach (var d in Dirs)
            {
                int j;
                if (index.TryGetValue(path[i] + d, out j) && Mathf.Abs(j - i) != 1) return false;
            }
        }
        return true;
    }

    bool Extend(List<Vector2Int> path, HashSet<Vector2Int> used, System.Random rng, int minLen, ref int budget)
    {
        Vector2Int cur = path[path.Count - 1];
        if (cur.x == width - 1 && path.Count >= minLen) return true;
        if (path.Count >= maxPathLength || --budget <= 0) return false;

        Vector2Int prevDir = path.Count > 1 ? cur - path[path.Count - 2] : Vector2Int.right;

        foreach (var d in OrderedDirections(rng, prevDir))
        {
            Vector2Int n = cur + d;
            if (!CanUse(n, cur, used)) continue;

            path.Add(n);
            used.Add(n);
            if (Extend(path, used, rng, minLen, ref budget)) return true;
            path.RemoveAt(path.Count - 1);
            used.Remove(n);
        }
        return false;
    }

    /// <summary>La casilla debe estar dentro del mapa, libre y no tocar (en cruz) ningún otro tramo salvo del que viene.</summary>
    bool CanUse(Vector2Int n, Vector2Int from, HashSet<Vector2Int> used)
    {
        if (n.x < 0 || n.x >= width || n.y < 0 || n.y >= height) return false;
        if (used.Contains(n)) return false;
        foreach (var d in Dirs)
        {
            Vector2Int m = n + d;
            if (m != from && used.Contains(m)) return false;
        }
        return true;
    }

    /// <summary>Ordena las 4 direcciones al azar, con preferencia por avanzar a la derecha y por doblar.</summary>
    List<Vector2Int> OrderedDirections(System.Random rng, Vector2Int prevDir)
    {
        var keyed = new List<KeyValuePair<double, Vector2Int>>();
        foreach (var d in Dirs)
        {
            double w = d == Vector2Int.right ? 3.0 : (d == Vector2Int.left ? 0.6 : 2.0);
            if (d == prevDir) w *= 0.7;
            double key = System.Math.Pow(rng.NextDouble(), 1.0 / w);
            keyed.Add(new KeyValuePair<double, Vector2Int>(key, d));
        }
        keyed.Sort((a, b) => b.Key.CompareTo(a.Key));

        var result = new List<Vector2Int>(4);
        foreach (var kv in keyed) result.Add(kv.Value);
        return result;
    }

    /// <summary>Camino fijo (el original) por si la generación aleatoria fallara.</summary>
    List<Vector2Int> FallbackPath()
    {
        Vector2Int[] pts =
        {
            new Vector2Int(0, 7), new Vector2Int(2, 7), new Vector2Int(2, 2), new Vector2Int(6, 2),
            new Vector2Int(6, 6), new Vector2Int(11, 6), new Vector2Int(11, 1), new Vector2Int(15, 1),
            new Vector2Int(15, 5), new Vector2Int(16, 5)
        };

        var cells = new List<Vector2Int> { pts[0] };
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var d = new Vector2Int(Mathf.Clamp(pts[i + 1].x - pts[i].x, -1, 1), Mathf.Clamp(pts[i + 1].y - pts[i].y, -1, 1));
            var c = pts[i];
            while (c != pts[i + 1]) { c += d; cells.Add(c); }
        }
        return cells;
    }

    // ================= Construcción del mapa =================

    Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x - width / 2, cell.y - height / 2, 0f);
    }

    void BuildMap(List<Vector2Int> path)
    {
        var tilesRoot = new GameObject("Tiles");
        tilesRoot.transform.SetParent(transform, false);

        var pathSet = new HashSet<Vector2Int>(path);

        // Casilla de entrada, fuera de la grilla (de ahí salen los enemigos)
        var entry = new Vector2Int(-1, path[0].y);
        CreateTile(tilesRoot.transform, "Path_entry", CellToWorld(entry), PathColor, false);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = new Vector2Int(x, y);
                if (pathSet.Contains(cell))
                {
                    CreateTile(tilesRoot.transform, "Path_" + x + "_" + y, CellToWorld(cell), PathColor, false);
                }
                else
                {
                    Color grass = ((x + y) & 1) == 0 ? GrassA : GrassB;
                    CreateTile(tilesRoot.transform, "Node_" + x + "_" + y, CellToWorld(cell), grass, true);
                }
            }
        }

        BuildWaypoints(entry, path);
        BuildBase(path[path.Count - 1]);
    }

    void CreateTile(Transform parent, string tileName, Vector3 pos, Color color, bool buildable)
    {
        var go = new GameObject(tileName);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = color;
        sr.sortingOrder = 0;

        if (buildable)
        {
            go.transform.localScale = new Vector3(0.97f, 0.97f, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            go.AddComponent<BuildNode>();
        }
    }

    /// <summary>Crea los waypoints solo en las esquinas del camino (los hijos se detectan en Waypoints.Awake).</summary>
    void BuildWaypoints(Vector2Int entry, List<Vector2Int> path)
    {
        var all = new List<Vector2Int> { entry };
        all.AddRange(path);

        var corners = new List<Vector2Int> { all[0] };
        for (int i = 1; i < all.Count - 1; i++)
        {
            if (all[i] - all[i - 1] != all[i + 1] - all[i]) corners.Add(all[i]);
        }
        corners.Add(all[all.Count - 1]);

        var root = new GameObject("Waypoints");
        root.transform.SetParent(transform, false);
        for (int i = 0; i < corners.Count; i++)
        {
            var p = new GameObject("WP_" + i);
            p.transform.SetParent(root.transform, false);
            p.transform.position = CellToWorld(corners[i]);
        }

        // Los hijos ya existen: Awake los toma como puntos
        root.AddComponent<Waypoints>();
    }

    void BuildBase(Vector2Int cell)
    {
        var go = new GameObject("Base");
        go.transform.SetParent(transform, false);
        go.transform.position = CellToWorld(cell);
        go.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = BaseColor;
        sr.sortingOrder = 3;
    }

    static Sprite MakeWhiteSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}

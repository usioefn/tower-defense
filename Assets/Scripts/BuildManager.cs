using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Maneja la selección de torres, la construcción en casillas, mejorar y vender.
/// </summary>
public class BuildManager : MonoBehaviour
{
    public static BuildManager Instance { get; private set; }

    [Header("Torres disponibles (teclas 1, 2, 3...)")]
    public Tower[] towerPrefabs;

    [Header("Sprites del cursor de construcción")]
    public Sprite squareSprite;
    public Sprite circleSprite;

    public int SelectedIndex { get; private set; } = -1;   // tipo de torre a construir
    public Tower SelectedTower { get; private set; }       // torre ya construida seleccionada

    public event Action OnSelectionChanged;
    public event Action<string> OnMessage;

    Camera cam;
    SpriteRenderer hoverTile;
    Transform placementRange;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
        CreateHoverVisuals();
    }

    void CreateHoverVisuals()
    {
        var tileGo = new GameObject("HoverTile");
        hoverTile = tileGo.AddComponent<SpriteRenderer>();
        hoverTile.sprite = squareSprite;
        hoverTile.sortingOrder = 2;

        var rangeGo = new GameObject("PlacementRange");
        rangeGo.transform.SetParent(tileGo.transform, false);
        var rangeSr = rangeGo.AddComponent<SpriteRenderer>();
        rangeSr.sprite = circleSprite;
        rangeSr.color = new Color(1f, 1f, 1f, 0.18f);
        rangeSr.sortingOrder = 3;
        placementRange = rangeGo.transform;

        tileGo.SetActive(false);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.IsGameOver)
        {
            hoverTile.gameObject.SetActive(false);
            return;
        }

        if (gm != null && gm.IsInputBlocked)
        {
            hoverTile.gameObject.SetActive(false);
            return;
        }

        HandleKeys();

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        BuildNode node = null;

        if (!overUI && cam != null)
        {
            Vector2 screen = InputHelper.PointerPosition;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            var col = Physics2D.OverlapPoint(new Vector2(world.x, world.y));
            if (col != null) node = col.GetComponent<BuildNode>();
        }

        UpdateHover(node);

        if (InputHelper.SecondaryDown) ClearSelection();

        if (InputHelper.PrimaryDown && !overUI)
        {
            if (node == null)
            {
                SelectPlacedTower(null);
            }
            else if (node.tower != null)
            {
                SelectPlacedTower(node.tower);
            }
            else if (SelectedIndex >= 0)
            {
                TryBuild(node);
            }
            else
            {
                SelectPlacedTower(null);
            }
        }
    }

    void HandleKeys()
    {
        if (towerPrefabs != null)
        {
            if (towerPrefabs.Length > 0 && InputHelper.KeyDown(GameKey.Num1)) SelectTowerType(0);
            if (towerPrefabs.Length > 1 && InputHelper.KeyDown(GameKey.Num2)) SelectTowerType(1);
            if (towerPrefabs.Length > 2 && InputHelper.KeyDown(GameKey.Num3)) SelectTowerType(2);
            if (towerPrefabs.Length > 3 && InputHelper.KeyDown(GameKey.Num4)) SelectTowerType(3);
            if (towerPrefabs.Length > 4 && InputHelper.KeyDown(GameKey.Num5)) SelectTowerType(4);
            if (towerPrefabs.Length > 5 && InputHelper.KeyDown(GameKey.Num6)) SelectTowerType(5);

        }

        if (InputHelper.KeyDown(GameKey.Cancel))
        {
            // Esc: primero cancela la selección; si no hay nada seleccionado, pausa
            if (SelectedIndex >= 0 || SelectedTower != null) ClearSelection();
            else if (GameManager.Instance != null) GameManager.Instance.TogglePause();
        }
        if (InputHelper.KeyDown(GameKey.Upgrade)) UpgradeSelected();
        if (InputHelper.KeyDown(GameKey.Sell)) SellSelected();
        if (InputHelper.KeyDown(GameKey.Target)) CycleTargetSelected();
    }

    void UpdateHover(BuildNode node)
    {
        if (node == null)
        {
            hoverTile.gameObject.SetActive(false);
            return;
        }

        hoverTile.gameObject.SetActive(true);
        hoverTile.transform.position = node.transform.position;

        if (node.IsFree && SelectedIndex >= 0)
        {
            var prefab = towerPrefabs[SelectedIndex];
            bool canAfford = GameManager.Instance.CanAfford(prefab.cost);
            hoverTile.color = canAfford ? new Color(0.3f, 1f, 0.4f, 0.45f) : new Color(1f, 0.3f, 0.3f, 0.45f);
            placementRange.gameObject.SetActive(true);
            placementRange.localScale = Vector3.one * prefab.EffectiveRange * 2f;
        }
        else
        {
            hoverTile.color = new Color(1f, 1f, 1f, 0.15f);
            placementRange.gameObject.SetActive(false);
        }
    }

    public void SelectTowerType(int index)
    {
        if (towerPrefabs == null || index < 0 || index >= towerPrefabs.Length) return;
        SelectedIndex = (SelectedIndex == index) ? -1 : index;
        SetSelectedTower(null);
        OnSelectionChanged?.Invoke();
    }

    void SelectPlacedTower(Tower t)
    {
        SelectedIndex = -1;
        SetSelectedTower(t);
        OnSelectionChanged?.Invoke();
    }

    void SetSelectedTower(Tower t)
    {
        if (SelectedTower != null) SelectedTower.ShowRange(false);
        SelectedTower = t;
        if (SelectedTower != null) SelectedTower.ShowRange(true);
    }

    public void ClearSelection()
    {
        SelectedIndex = -1;
        SetSelectedTower(null);
        OnSelectionChanged?.Invoke();
    }

    void TryBuild(BuildNode node)
    {
        var prefab = towerPrefabs[SelectedIndex];
        if (!GameManager.Instance.Spend(prefab.cost))
        {
            OnMessage?.Invoke("No tienes suficiente dinero");
            return;
        }

        var t = Instantiate(prefab, node.transform.position, Quaternion.identity);
        t.node = node;
        node.tower = t;
    }

    public void UpgradeSelected()
    {
        if (SelectedTower == null) return;
        if (!SelectedTower.CanUpgrade) { OnMessage?.Invoke("La torre ya está al nivel máximo"); return; }
        if (!SelectedTower.Upgrade()) OnMessage?.Invoke("No tienes suficiente dinero");
        OnSelectionChanged?.Invoke();
    }

    /// <summary>Evoluciona la torre seleccionada (nivel máximo) a la rama 'index' de TowerEvolutions.</summary>
    public void EvolveSelected(int index)
    {
        if (SelectedTower == null || !SelectedTower.CanEvolve) return;
        var options = TowerEvolutions.For(SelectedTower.TowerId);
        if (index < 0 || index >= options.Count) return;

        var chosen = options[index];
        if (!SelectedTower.Evolve(chosen)) OnMessage?.Invoke("No tienes suficiente dinero");
        else OnMessage?.Invoke("¡Evolucionó a " + chosen.name + "!");
        OnSelectionChanged?.Invoke();
    }

    public void CycleTargetSelected()
    {
        if (SelectedTower == null) return;
        SelectedTower.CycleTargetMode();
        OnSelectionChanged?.Invoke();
    }

    public void SellSelected()
    {
        if (SelectedTower == null) return;
        var t = SelectedTower;
        GameManager.Instance.AddMoney(t.SellValue);
        if (t.node != null) t.node.tower = null;
        SetSelectedTower(null);
        Destroy(t.gameObject);
        OnSelectionChanged?.Invoke();
    }
}

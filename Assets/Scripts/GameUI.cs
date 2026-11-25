using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Construye toda la interfaz (HUD, botones de torres, panel de mejora/venta, fin de partida) por código.
/// </summary>
public class GameUI : MonoBehaviour
{
    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);
    static readonly Color ButtonBase = new Color(0.25f, 0.27f, 0.33f, 1f);
    static readonly Color ButtonSelected = new Color(0.25f, 0.55f, 0.95f, 1f);

    Font font;

    Text moneyText, livesText, waveText, speedLabel, nextWaveLabel, messageText;
    Text selTitle, selInfo, upgradeLabel, sellLabel, targetLabel, endTitle;
    Button speedBtn, nextWaveBtn, upgradeBtn, sellBtn;
    GameObject evolutionPanel;
    RectTransform evolutionRect;
    Text evolutionTitle;
    readonly Button[] evoBtns = new Button[3];
    readonly Text[] evoLabels = new Text[3];
    GameObject selectionPanel, endPanel;
    readonly List<Image> towerBtnImages = new List<Image>();
    readonly List<Text> towerBtnTexts = new List<Text>();
    Coroutine messageRoutine;

    // ---------- Panel de mejoras (roguelite) ----------
    const float UpgradesPanelWidth = 290f;
    const float UpgradesTabWidth = 40f;

    class UpgradeCard
    {
        public int cost;
        public bool bought;
        public Button button;
        public Text label;
        public UnityAction onBuy;
    }

    RectTransform upgradesRect;
    RectTransform upgradesContent;
    Text upgradesTabLabel;
    bool upgradesOpen;

    // ---------- Experiencia y ruleta de beneficios ----------
    static readonly Color CardSpinA = new Color(0.20f, 0.21f, 0.28f);
    static readonly Color CardSpinB = new Color(0.28f, 0.24f, 0.34f);
    static readonly Color CardGlobal = new Color(0.14f, 0.22f, 0.36f);
    static readonly Color CardTower = new Color(0.38f, 0.22f, 0.12f);

    class RouletteCard
    {
        public RectTransform rect;
        public Button button;
        public Image bg;
        public Text tag, title, desc, stacks;
        public Buff buff;
        public bool revealed;
    }

    RectTransform xpFill;
    Text xpLabel;
    GameObject roulettePanel;
    Text rouletteTitle, rouletteSub;
    readonly RouletteCard[] rouletteCards = new RouletteCard[3];
    bool rouletteActive, rouletteReady;

    // Botón de volver al menú (pide confirmación con un segundo clic)
    Text pauseLabel;
    GameObject pausePanel;
    readonly List<UpgradeCard> upgradeCards = new List<UpgradeCard>();

    void Start()
    {
        font = LoadFont();
        EnsureEventSystem();
        BuildUI();

        GameManager.Instance.OnStatsChanged += Refresh;
        GameManager.Instance.OnGameEnded += ShowEnd;
        ExperienceManager.Instance.OnLevelUp += OnLevelUp;
        WaveSpawner.Instance.OnWaveStarted += OnWaveStarted;
        WaveSpawner.Instance.OnWaveCleared += OnWaveCleared;
        GameManager.Instance.OnPauseChanged += SetPausePanel;
        BuildManager.Instance.OnSelectionChanged += Refresh;
        BuildManager.Instance.OnMessage += ShowMessage;

        Refresh();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStatsChanged -= Refresh;
            GameManager.Instance.OnGameEnded -= ShowEnd;
            if (ExperienceManager.Instance != null) ExperienceManager.Instance.OnLevelUp -= OnLevelUp;
            if (WaveSpawner.Instance != null)
            {
                WaveSpawner.Instance.OnWaveStarted -= OnWaveStarted;
                WaveSpawner.Instance.OnWaveCleared -= OnWaveCleared;
            }
            GameManager.Instance.OnPauseChanged -= SetPausePanel;
        }
        if (BuildManager.Instance != null)
        {
            BuildManager.Instance.OnSelectionChanged -= Refresh;
            BuildManager.Instance.OnMessage -= ShowMessage;
        }
    }

void Update()
    {
        AnimateUpgradesPanel();
        RefreshXp();
        HandleRouletteKeys();

        var ws = WaveSpawner.Instance;
        if (ws == null || waveText == null) return;

        if (waveText.fontSize != 16) waveText.fontSize = 16;

        // Dos líneas: arriba el estado de la oleada, abajo el progreso y la amenaza
        string threat = "Amenaza: " + ws.CurrentTier;
        if (ws.WaveInProgress)
        {
            bool boss = ws.IsBossWave(ws.CurrentWave);
            waveText.text = (boss ? "¡JEFE!  " : "") + "Oleada " + ws.CurrentWave + "/" + ws.TotalWaves
                + "\nEnemigos: " + Enemy.All.Count + "   " + threat;
            waveText.color = boss ? new Color(1f, 0.5f, 0.45f) : Color.white;
        }
        else if (ws.CurrentWave >= ws.TotalWaves)
        {
            waveText.text = "Oleada " + ws.CurrentWave + "/" + ws.TotalWaves + "\n" + threat;
            waveText.color = Color.white;
        }
        else
        {
            bool nextBoss = ws.NextWaveIsBoss;
            waveText.text = "Próxima oleada en " + Mathf.CeilToInt(ws.Countdown) + "s" + (nextBoss ? "  ¡JEFE!" : "")
                + "\n" + ws.CurrentWave + "/" + ws.TotalWaves + "   " + threat;
            waveText.color = nextBoss ? new Color(1f, 0.5f, 0.45f) : Color.white;
        }

        nextWaveBtn.interactable = !ws.WaveInProgress && ws.CurrentWave < ws.TotalWaves;
    }

    // ---------- Refresco ----------

void Refresh()
    {
        var gm = GameManager.Instance;
        var bm = BuildManager.Instance;
        if (gm == null || bm == null || moneyText == null) return;

        moneyText.text = "Dinero: $" + gm.Money;
        livesText.text = "Puerta: " + gm.Lives + "/" + gm.MaxLives;
        livesText.color = gm.Lives <= gm.MaxLives * 0.3f ? new Color(1f, 0.25f, 0.25f) : new Color(1f, 0.5f, 0.5f);
        speedLabel.text = "Velocidad x" + (gm.GameSpeed > 1.5f ? "2" : "1");

        for (int i = 0; i < towerBtnImages.Count; i++)
        {
            var t = bm.towerPrefabs[i];
            towerBtnImages[i].color = (i == bm.SelectedIndex) ? ButtonSelected : ButtonBase;
            towerBtnTexts[i].color = gm.CanAfford(t.cost) ? Color.white : new Color(1f, 0.55f, 0.55f);
        }

        foreach (var card in upgradeCards)
            card.button.interactable = !card.bought && gm.CanAfford(card.cost);

        var sel = bm.SelectedTower;
        selectionPanel.SetActive(sel != null);
        UpdateEvolutionPanel(sel, gm);
        if (sel != null)
        {
            selTitle.text = (sel.Evolution != null ? sel.Evolution.name : sel.towerName) + "  Nv. " + sel.Level + "/" + sel.maxLevel;
            selInfo.text = sel.castle != null ? sel.castle.InfoText() : "Daño " + sel.EffectiveDamage.ToString("0.#") + "   Alcance " + sel.EffectiveRange.ToString("0.#") + "   Cadencia " + sel.EffectiveFireRate.ToString("0.#") + "/s";
            if (sel.CanUpgrade)
            {
                upgradeLabel.text = "Mejorar ($" + sel.UpgradeCost + ")";
                upgradeBtn.interactable = gm.CanAfford(sel.UpgradeCost);
            }
            else
            {
                upgradeLabel.text = sel.Evolution != null ? "Evolucionada" : "Nivel máximo";
                upgradeBtn.interactable = false;
            }
            sellLabel.text = "Vender (+$" + sel.SellValue + ")";
            targetLabel.text = "Objetivo: " + Tower.TargetModeName(sel.targetMode) + "  [T]";
            targetLabel.transform.parent.gameObject.SetActive(sel.castle == null); // el castillo no apunta: sus soldados pelean solos

        }
    }

    void ShowEnd(bool win)
    {
        endPanel.SetActive(true);
        endTitle.text = win ? "¡Victoria!" : "Game Over";
        endTitle.color = win ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
    }

    void ShowMessage(string msg)
    {
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(msg));
    }

    IEnumerator MessageRoutine(string msg)
    {
        messageText.text = msg;
        yield return new WaitForSecondsRealtime(2f);
        messageText.text = "";
    }

    // ---------- Construcción de la UI ----------

void BuildUI()
    {
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Transform root = canvasGo.transform;

        BuildTopBar(root);
        BuildXpBar(root);
        BuildBottomBar(root);
        BuildSelectionPanel(root);
        BuildEvolutionPanel(root);
        BuildUpgradesPanel(root);

        messageText = Label(root, "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f),
            new Vector2(0.2f, 1f), new Vector2(0.8f, 1f), new Vector2(0f, -112f), new Vector2(0f, -76f));

        BuildRoulettePanel(root);
        BuildPausePanel(root);
        BuildEndPanel(root);
    }

void BuildTopBar(Transform root)
    {
        var bar = Panel(root, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), Vector2.zero, PanelColor).transform;

        moneyText = Label(bar, "", 22, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.3f),
            new Vector2(0f, 0f), new Vector2(0.18f, 1f), new Vector2(16f, 0f), Vector2.zero);
        livesText = Label(bar, "", 22, TextAnchor.MiddleLeft, new Color(1f, 0.5f, 0.5f),
            new Vector2(0.18f, 0f), new Vector2(0.34f, 1f), Vector2.zero, Vector2.zero);
        waveText = Label(bar, "", 20, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.34f, 0f), new Vector2(0.60f, 1f), Vector2.zero, Vector2.zero);

        MakeButton(bar, "Pausa", 16, new Vector2(0.61f, 0f), new Vector2(0.70f, 1f),
            new Vector2(4f, 8f), new Vector2(-4f, -8f), new Color(0.3f, 0.4f, 0.6f), () => GameManager.Instance.TogglePause(), out pauseLabel);
        speedBtn = MakeButton(bar, "Velocidad x1", 16, new Vector2(0.71f, 0f), new Vector2(0.84f, 1f),
            new Vector2(4f, 8f), new Vector2(-4f, -8f), ButtonBase, () => GameManager.Instance.ToggleSpeed(), out speedLabel);
        nextWaveBtn = MakeButton(bar, "Siguiente oleada", 16, new Vector2(0.85f, 0f), new Vector2(0.995f, 1f),
            new Vector2(4f, 8f), new Vector2(-4f, -8f), new Color(0.2f, 0.6f, 0.3f), () => WaveSpawner.Instance.StartNextWave(), out nextWaveLabel);
    }

void BuildBottomBar(Transform root)
    {
        var bar = Panel(root, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 70f), PanelColor).transform;

        var bm = BuildManager.Instance;
        int n = bm.towerPrefabs.Length;
        float spacing = n > 5 ? 128f : 150f;
        float half = n > 5 ? 60f : 70f;
        for (int i = 0; i < n; i++)
        {
            int index = i;
            var tower = bm.towerPrefabs[i];
            float cx = (i - (n - 1) / 2f) * spacing;
            Text label;
            var btn = MakeButton(bar, tower.towerName + "\n$" + tower.cost + "  [" + (i + 1) + "]", 16,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx - half, -28f), new Vector2(cx + half, 28f),
                ButtonBase, () => BuildManager.Instance.SelectTowerType(index), out label);
            towerBtnImages.Add(btn.GetComponent<Image>());
            towerBtnTexts.Add(label);
        }

        var hint = Label(bar, "1-" + n + ": elegir torre   Clic der.: cancelar\nEspacio: oleada   F: velocidad\nU/X: mejorar/vender   T: objetivo\nP/Esc: pausa",
            11, TextAnchor.MiddleLeft, new Color(0.7f, 0.72f, 0.78f),
            new Vector2(0f, 0f), new Vector2(0.2f, 1f), new Vector2(12f, 0f), Vector2.zero);
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

void BuildSelectionPanel(Transform root)
    {
        // Abajo a la izquierda, para no chocar con el panel de mejoras de la derecha
        var panel = Panel(root, "SelectionPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 82f), new Vector2(292f, 262f), PanelColor);
        selectionPanel = panel.gameObject;
        Transform p = panel.transform;

        selTitle = Label(p, "", 20, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0f, 0.75f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        selInfo = Label(p, "", 13, TextAnchor.MiddleCenter, new Color(0.75f, 0.78f, 0.85f),
            new Vector2(0f, 0.58f), new Vector2(1f, 0.75f), Vector2.zero, Vector2.zero);

        MakeButton(p, "Objetivo", 15, new Vector2(0.04f, 0.33f), new Vector2(0.96f, 0.55f),
            Vector2.zero, Vector2.zero, ButtonBase, () => BuildManager.Instance.CycleTargetSelected(), out targetLabel);

        upgradeBtn = MakeButton(p, "Mejorar", 15, new Vector2(0.04f, 0.05f), new Vector2(0.50f, 0.29f),
            Vector2.zero, Vector2.zero, new Color(0.2f, 0.5f, 0.85f), () => BuildManager.Instance.UpgradeSelected(), out upgradeLabel);
        sellBtn = MakeButton(p, "Vender", 15, new Vector2(0.52f, 0.05f), new Vector2(0.96f, 0.29f),
            Vector2.zero, Vector2.zero, new Color(0.75f, 0.3f, 0.25f), () => BuildManager.Instance.SellSelected(), out sellLabel);

        selectionPanel.SetActive(false);
    }

void BuildEvolutionPanel(Transform root)
    {
        // Encima del panel de la torre seleccionada: aparece cuando la torre llegó al nivel máximo
        var panel = Panel(root, "EvolutionPanel", Vector2.zero, Vector2.zero, new Vector2(12f, 266f), new Vector2(372f, 480f), PanelColor);
        evolutionPanel = panel.gameObject;
        evolutionRect = panel.rectTransform;
        Transform p = panel.transform;

        evolutionTitle = Label(p, "Evolucionar", 18, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), Vector2.zero);

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float top = 32f + i * 54f;
            evoBtns[i] = MakeButton(p, "", 13, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(8f, -(top + 50f)), new Vector2(-8f, -top), new Color(0.35f, 0.3f, 0.65f),
                () => BuildManager.Instance.EvolveSelected(index), out evoLabels[i]);
            evoLabels[i].horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        evolutionPanel.SetActive(false);
    }

    void UpdateEvolutionPanel(Tower sel, GameManager gm)
    {
        if (evolutionPanel == null) return;
        bool show = sel != null && sel.CanEvolve;
        evolutionPanel.SetActive(show);
        if (!show) return;

        var options = TowerEvolutions.For(sel.TowerId);
        evolutionRect.offsetMax = new Vector2(372f, 266f + 32f + options.Count * 54f + 6f);
        evolutionTitle.text = "Evolucionar: " + sel.towerName;
        for (int i = 0; i < evoBtns.Length; i++)
        {
            bool has = i < options.Count;
            evoBtns[i].gameObject.SetActive(has);
            if (!has) continue;
            int cost = sel.EvolutionCost(options[i]);
            evoLabels[i].text = "<b>" + options[i].name + "</b>   $" + cost + "\n<size=11>" + options[i].description + "</size>";
            evoBtns[i].interactable = gm.CanAfford(cost);
        }
    }

void BuildEndPanel(Transform root)
    {
        var panel = Panel(root, "EndPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
        endPanel = panel.gameObject;

        endTitle = Label(panel.transform, "", 64, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.78f), Vector2.zero, Vector2.zero);

        Text restartLabel;
        MakeButton(panel.transform, "Reiniciar  [R]", 24, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-110f, -80f), new Vector2(110f, -20f), new Color(0.2f, 0.6f, 0.3f), () => GameManager.Instance.Restart(), out restartLabel);

        Text menuLabelEnd;
        MakeButton(panel.transform, "Menú principal", 24, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-110f, -150f), new Vector2(110f, -90f), ButtonBase, () => GameManager.Instance.GoToMainMenu(), out menuLabelEnd);

        endPanel.SetActive(false);
    }

    // ---------- Helpers de UI ----------

    RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
        return rt;
    }

    Image Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color color)
    {
        var rt = Rect(name, parent, aMin, aMax, oMin, oMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var rt = Rect("Text", parent, aMin, aMax, oMin, oMax);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    Button MakeButton(Transform parent, string label, int size, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax,
                      Color color, UnityAction onClick, out Text labelText)
    {
        var img = Panel(parent, "Button", aMin, aMax, oMin, oMax, color);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        labelText = Label(img.transform, label, size, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return btn;
    }

    Font LoadFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return f;
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        var module = es.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }


// ---------- Panel de mejoras ----------

    void BuildUpgradesPanel(Transform root)
    {
        // Contenedor anclado a la derecha, entre la barra superior y la inferior.
        // Se desliza cambiando anchoredPosition.x: 0 = abierto, ancho del panel = cerrado (queda la pestaña).
        var container = Rect("UpgradesRoot", root, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        container.pivot = new Vector2(1f, 0.5f);
        container.offsetMin = new Vector2(-(UpgradesPanelWidth + UpgradesTabWidth), 70f);
        container.offsetMax = new Vector2(0f, -56f);
        container.anchoredPosition = new Vector2(UpgradesPanelWidth, 0f);
        upgradesRect = container;

        // Pestaña para abrir/cerrar
        Text tabLabel;
        MakeButton(container, "", 18, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, -70f), new Vector2(UpgradesTabWidth, 70f), new Color(0.2f, 0.45f, 0.8f), ToggleUpgrades, out tabLabel);
        var tabRt = tabLabel.rectTransform;
        tabRt.anchorMin = tabRt.anchorMax = new Vector2(0.5f, 0.5f);
        tabRt.anchoredPosition = Vector2.zero;
        tabRt.sizeDelta = new Vector2(140f, 40f);
        tabRt.localRotation = Quaternion.Euler(0f, 0f, -90f);
        upgradesTabLabel = tabLabel;
        upgradesTabLabel.text = "Mejoras  <";

        // Cuerpo del panel
        var body = Panel(container, "Body", Vector2.zero, Vector2.one, new Vector2(UpgradesTabWidth, 0f), Vector2.zero, PanelColor).transform;
        Label(body, "Mejoras", 22, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -44f), new Vector2(0f, -6f));

        // Lista con scroll
        var scrollRt = Rect("Scroll", body, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -52f));
        var viewport = Rect("Viewport", scrollRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var viewportImg = viewport.gameObject.AddComponent<Image>();
        viewportImg.color = new Color(0f, 0f, 0f, 0.001f); // invisible, pero recibe el arrastre
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        upgradesContent = content;

        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25f;

        // Items de ejemplo: reemplazalos por tus mejoras reales
        AddUpgradeCard("Ejemplo: Daño +10%", "Item de prueba. Reemplazalo por una mejora real.", 50, () => Debug.Log("Mejora 1 comprada"));
        AddUpgradeCard("Ejemplo: Alcance +0.5", "Item de prueba. Reemplazalo por una mejora real.", 80, () => Debug.Log("Mejora 2 comprada"));
        AddUpgradeCard("Ejemplo: Más dinero", "Item de prueba. Reemplazalo por una mejora real.", 120, () => Debug.Log("Mejora 3 comprada"));
    }

    /// <summary>Agrega una tarjeta de mejora al panel. onBuy se ejecuta al comprarla (se cobra el costo antes).</summary>
    public void AddUpgradeCard(string title, string description, int cost, UnityAction onBuy)
    {
        var card = Panel(upgradesContent, "Card", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.16f, 0.18f, 0.24f, 1f));
        var le = card.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 104f;
        le.preferredHeight = 104f;

        Label(card.transform, title, 17, TextAnchor.UpperLeft, Color.white,
            new Vector2(0f, 0.68f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, -6f));
        var desc = Label(card.transform, description, 13, TextAnchor.UpperLeft, new Color(0.75f, 0.78f, 0.85f),
            new Vector2(0f, 0.36f), new Vector2(1f, 0.70f), new Vector2(10f, 0f), new Vector2(-10f, 0f));
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;

        var data = new UpgradeCard { cost = cost, onBuy = onBuy };
        Text buyLabel;
        data.button = MakeButton(card.transform, "Comprar ($" + cost + ")", 14, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.34f),
            Vector2.zero, Vector2.zero, new Color(0.2f, 0.6f, 0.3f), () => BuyUpgrade(data), out buyLabel);
        data.label = buyLabel;
        upgradeCards.Add(data);

        Refresh();
    }

    void BuyUpgrade(UpgradeCard card)
    {
        if (card.bought) return;
        if (!GameManager.Instance.Spend(card.cost))
        {
            ShowMessage("No tienes suficiente dinero");
            return;
        }

        card.bought = true;
        card.label.text = "Comprado";
        if (card.onBuy != null) card.onBuy.Invoke();
        Refresh();
    }

    void ToggleUpgrades()
    {
        upgradesOpen = !upgradesOpen;
        upgradesTabLabel.text = upgradesOpen ? "Mejoras  >" : "Mejoras  <";
    }

    void AnimateUpgradesPanel()
    {
        if (upgradesRect == null) return;
        float targetX = upgradesOpen ? 0f : UpgradesPanelWidth;
        Vector2 p = upgradesRect.anchoredPosition;
        p.x = Mathf.Lerp(p.x, targetX, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        upgradesRect.anchoredPosition = p;
    }







void BuildPausePanel(Transform root)
    {
        var panel = Panel(root, "PausePanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
        pausePanel = panel.gameObject;

        Label(panel.transform, "Pausa", 64, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.82f), Vector2.zero, Vector2.zero);

        Text l1, l2, l3;
        var c = new Vector2(0.5f, 0.5f);
        MakeButton(panel.transform, "Continuar  [Esc]", 24, c, c, new Vector2(-130f, 20f), new Vector2(130f, 80f),
            new Color(0.2f, 0.6f, 0.3f), () => GameManager.Instance.TogglePause(), out l1);
        MakeButton(panel.transform, "Reiniciar  [R]", 24, c, c, new Vector2(-130f, -60f), new Vector2(130f, 0f),
            new Color(0.85f, 0.55f, 0.15f), () => GameManager.Instance.Restart(), out l2);
        MakeButton(panel.transform, "Menú principal", 24, c, c, new Vector2(-130f, -140f), new Vector2(130f, -80f),
            ButtonBase, () => GameManager.Instance.GoToMainMenu(), out l3);

        Label(panel.transform, "Reiniciar empieza una partida nueva con otro camino", 14, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f),
            new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.5f), new Vector2(0f, -185f), new Vector2(0f, -160f));

        pausePanel.SetActive(false);
    }

    void SetPausePanel(bool paused)
    {
        if (pausePanel != null) pausePanel.SetActive(paused);
    }


// ---------- Barra de experiencia ----------

    void BuildXpBar(Transform root)
    {
        var bg = Panel(root, "XpBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), new Vector2(0f, -56f), new Color(0.05f, 0.05f, 0.08f, 0.95f));
        var fill = Panel(bg.transform, "Fill", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Color(0.55f, 0.4f, 0.95f, 1f));
        xpFill = fill.rectTransform;
        xpLabel = Label(bg.transform, "", 12, TextAnchor.MiddleLeft, Color.white,
            Vector2.zero, Vector2.one, new Vector2(10f, 0f), Vector2.zero);
    }

    void RefreshXp()
    {
        var xp = ExperienceManager.Instance;
        if (xp == null || xpFill == null) return;

        float ratio = xp.XpToNext > 0 ? (float)xp.Xp / xp.XpToNext : 0f;
        xpFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        xpLabel.text = "Nv. " + xp.Level + "    " + xp.Xp + " / " + xp.XpToNext + " XP";
    }

    // ---------- Ruleta de beneficios ----------

    void BuildRoulettePanel(Transform root)
    {
        var overlay = Panel(root, "RoulettePanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.82f));
        roulettePanel = overlay.gameObject;

        rouletteTitle = Label(overlay.transform, "", 48, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f),
            new Vector2(0.1f, 0.80f), new Vector2(0.9f, 0.94f), Vector2.zero, Vector2.zero);
        rouletteSub = Label(overlay.transform, "", 20, TextAnchor.MiddleCenter, new Color(0.75f, 0.78f, 0.85f),
            new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.81f), Vector2.zero, Vector2.zero);

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var bg = Panel(overlay.transform, "Card" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-145f, -170f), new Vector2(145f, 160f), CardSpinA);

            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.disabledColor = Color.white;
            btn.colors = colors;
            btn.onClick.AddListener(() => ChooseCard(index));

            var card = new RouletteCard { rect = bg.rectTransform, button = btn, bg = bg };
            card.tag = Label(bg.transform, "", 13, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0f, 0.90f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, -6f));
            card.title = Label(bg.transform, "", 24, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0f, 0.58f), new Vector2(1f, 0.90f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            card.title.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.desc = Label(bg.transform, "", 16, TextAnchor.UpperCenter, new Color(0.82f, 0.85f, 0.92f),
                new Vector2(0f, 0.20f), new Vector2(1f, 0.58f), new Vector2(18f, 0f), new Vector2(-18f, -4f));
            card.desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.stacks = Label(bg.transform, "", 14, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f),
                new Vector2(0f, 0.04f), new Vector2(1f, 0.20f), new Vector2(8f, 0f), new Vector2(-8f, 0f));

            rouletteCards[i] = card;
        }

        roulettePanel.SetActive(false);
    }

    void OnLevelUp(int level)
    {
        TryStartRoulette();
    }

    /// <summary>Abre la ruleta si hay un nivel pendiente de premiar y no hay otra abierta.</summary>
    void TryStartRoulette()
    {
        if (rouletteActive) return;
        var xp = ExperienceManager.Instance;
        var gm = GameManager.Instance;
        if (xp == null || gm == null || gm.IsGameOver) return;

        while (xp.PendingLevelUps > 0)
        {
            int level = xp.ConsumePendingLevelUp();
            var options = BuffManager.Instance.Roll();
            if (options.Count == 0) continue; // ya no quedan beneficios por elegir

            rouletteActive = true;
            rouletteReady = false;
            gm.BeginModal();
            roulettePanel.SetActive(true);
            StartCoroutine(RouletteRoutine(level, options));
            return;
        }
    }

    IEnumerator RouletteRoutine(int level, List<Buff> options)
    {
        int n = options.Count;
        rouletteTitle.text = "¡Nivel " + level + "!";
        rouletteSub.text = "Girando la ruleta...";

        for (int i = 0; i < 3; i++)
        {
            var c = rouletteCards[i];
            c.revealed = false;
            c.buff = null;
            c.button.interactable = false;
            c.rect.localScale = Vector3.one;
            c.rect.gameObject.SetActive(i < n);
            if (i < n)
            {
                float cx = (i - (n - 1) / 2f) * 310f;
                c.rect.offsetMin = new Vector2(cx - 145f, -170f);
                c.rect.offsetMax = new Vector2(cx + 145f, 160f);
            }
        }

        float[] stopAt = { 1.0f, 1.6f, 2.2f };
        var all = BuffManager.Instance.AllBuffs;
        float end = stopAt[Mathf.Max(0, n - 1)] + 0.35f;
        float t = 0f, nextFlip = 0f;

        // Se usa tiempo sin escala porque el juego está congelado mientras gira
        while (t < end)
        {
            bool flip = t >= nextFlip;
            if (flip) nextFlip = t + 0.07f;

            for (int i = 0; i < n; i++)
            {
                var c = rouletteCards[i];
                if (!c.revealed && t >= stopAt[i])
                {
                    RevealCard(c, options[i]);
                }
                else if (!c.revealed && flip)
                {
                    var r = all[Random.Range(0, all.Count)];
                    c.bg.color = Color.Lerp(CardSpinA, CardSpinB, Random.value);
                    c.tag.text = "???";
                    c.tag.color = new Color(0.7f, 0.72f, 0.8f);
                    c.title.text = r.name;
                    c.desc.text = "";
                    c.stacks.text = "";
                }

                if (c.revealed)
                {
                    float k = Mathf.Clamp01((t - stopAt[i]) / 0.25f);
                    c.rect.localScale = Vector3.one * (1f + 0.12f * (1f - k));
                }
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        for (int i = 0; i < n; i++)
        {
            if (!rouletteCards[i].revealed) RevealCard(rouletteCards[i], options[i]);
            rouletteCards[i].rect.localScale = Vector3.one;
            rouletteCards[i].button.interactable = true;
        }

        rouletteReady = true;
        rouletteSub.text = "Elegí una mejora (clic o teclas 1, 2, 3)";
    }

    void RevealCard(RouletteCard c, Buff buff)
    {
        bool isGlobal = buff.scope == BuffScope.Global;
        int owned = BuffManager.Instance.GetStacks(buff);

        c.revealed = true;
        c.buff = buff;
        c.bg.color = isGlobal ? CardGlobal : CardTower;
        c.tag.text = isGlobal ? "TODAS LAS TORRES" : buff.towerName.ToUpper();
        c.tag.color = isGlobal ? new Color(0.55f, 0.8f, 1f) : new Color(1f, 0.7f, 0.4f);
        c.title.text = buff.name;
        c.desc.text = buff.description;
        c.stacks.text = "Nivel " + (owned + 1) + " / " + buff.maxStacks;
    }

    void HandleRouletteKeys()
    {
        if (!rouletteReady) return;
        if (InputHelper.KeyDown(GameKey.Num1)) ChooseCard(0);
        else if (InputHelper.KeyDown(GameKey.Num2)) ChooseCard(1);
        else if (InputHelper.KeyDown(GameKey.Num3)) ChooseCard(2);
    }

    void ChooseCard(int index)
    {
        if (!rouletteReady || index < 0 || index >= rouletteCards.Length) return;
        var card = rouletteCards[index];
        if (card.buff == null) return;

        string picked = card.buff.name;
        BuffManager.Instance.Pick(card.buff);

        rouletteReady = false;
        rouletteActive = false;
        roulettePanel.SetActive(false);
        GameManager.Instance.EndModal();

        ShowMessage("Mejora obtenida: " + picked);
        Refresh();
        TryStartRoulette(); // si subió más de un nivel, sigue la siguiente ruleta
    }


// ---------- Avisos de jefe ----------

    void OnWaveStarted(int wave, bool isBoss)
    {
        if (isBoss) ShowMessage("¡JEFE! Oleada " + wave);
    }

    void OnWaveCleared(int wave, bool wasBoss)
    {
        if (wasBoss) ShowMessage("¡Jefe derrotado! Las próximas oleadas serán más difíciles");
    }
}

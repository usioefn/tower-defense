using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Menú principal: construye su propia interfaz por código (Jugar, Cómo jugar, Salir).
/// </summary>
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "SampleScene";

    static readonly Color ButtonBase = new Color(0.25f, 0.27f, 0.33f, 1f);
    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 1f);

    Font font;
    GameObject howToPanel;

    void Start()
    {
        Time.timeScale = 1f;
        font = LoadFont();
        EnsureEventSystem();
        BuildUI();
    }

    void Update()
    {
        // Esc cierra el panel de ayuda si está abierto
        if (howToPanel != null && howToPanel.activeSelf && InputHelper.KeyDown(GameKey.Cancel))
            howToPanel.SetActive(false);
    }

    // ---------- Acciones ----------

    void Play()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Interfaz ----------

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

        // Título
        Label(root, "TOWER DEFENSE", 78, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.88f), Vector2.zero, Vector2.zero);
        Label(root, "Defendé tu base de las oleadas enemigas", 24, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f),
            new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.63f), Vector2.zero, Vector2.zero);

        // Botones
        MakeButton(root, "Jugar", 30, -20f, new Color(0.2f, 0.6f, 0.3f), Play);
        MakeButton(root, "Cómo jugar", 26, -90f, ButtonBase, () => howToPanel.SetActive(true));
        MakeButton(root, "Salir", 26, -160f, new Color(0.7f, 0.3f, 0.28f), Quit);

        Label(root, "v0.1", 14, TextAnchor.LowerRight, new Color(0.5f, 0.53f, 0.6f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(-12f, 34f));

        BuildHowToPanel(root);
    }

    void BuildHowToPanel(Transform root)
    {
        var overlay = Panel(root, "HowToPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.7f));
        howToPanel = overlay.gameObject;

        var box = Panel(overlay.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-340f, -265f), new Vector2(340f, 265f), PanelColor);

        Label(box.transform, "Cómo jugar", 36, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(0f, -10f));

        var body = Label(box.transform,
            "• Elegí una torre abajo (teclas 1, 2, 3) y hacé clic en una casilla verde para construirla.\n\n" +
            "• Clic en una torre para mejorarla (U) o venderla (X).\n\n" +
            "• Los enemigos siguen el camino: si llegan a la base, perdés vidas.\n\n" +
            "• Abrí el panel \"Mejoras\" de la derecha para comprar mejoras.\n\n" +
            "• Espacio: siguiente oleada   F: velocidad   Clic derecho: cancelar\n\n• P o Esc: pausa (ahí podés reiniciar la partida)\n\n• El camino de los enemigos es distinto en cada partida\n\n• 100 oleadas: cada 10 hay un jefe. Al derrotarlo, las siguientes oleadas son más difíciles",
            16, TextAnchor.UpperLeft, new Color(0.85f, 0.87f, 0.92f),
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 80f), new Vector2(-30f, -80f));
        body.horizontalOverflow = HorizontalWrapMode.Wrap;

        var back = Panel(box.transform, "Back", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-90f, 18f), new Vector2(90f, 66f), ButtonBase);
        SetupButton(back, "Volver", 22, () => howToPanel.SetActive(false));

        howToPanel.SetActive(false);
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

    // Botón centrado de 280x56, a una altura (y) relativa al centro de la pantalla
    void MakeButton(Transform parent, string label, int size, float y, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var img = Panel(parent, "Button_" + label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-140f, y - 28f), new Vector2(140f, y + 28f), color);
        SetupButton(img, label, size, onClick);
    }

    void SetupButton(Image img, string label, int size, UnityEngine.Events.UnityAction onClick)
    {
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        Label(img.transform, label, size, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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
}

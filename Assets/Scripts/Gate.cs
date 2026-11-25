using UnityEngine;

/// <summary>
/// La puerta (el final del camino). Los enemigos que llegan se quedan acá y la golpean;
/// su vida es GameManager.Lives y cuando llega a 0 se pierde la partida.
/// Para ponerle una puerta de verdad más adelante alcanza con cambiar el sprite de este objeto.
/// </summary>
public class Gate : MonoBehaviour
{
    public static Gate Instance { get; private set; }

    SpriteRenderer body;
    Color baseColor = Color.white;
    float flashTimer;

    Transform barRoot;
    Transform fill;
    SpriteRenderer fillRenderer;

    void Awake()
    {
        Instance = this;
        body = GetComponent<SpriteRenderer>();
        if (body != null) baseColor = body.color;
    }

    void Start()
    {
        BuildHealthBar();
        if (GameManager.Instance != null) GameManager.Instance.OnStatsChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStatsChanged -= Refresh;
        if (Instance == this) Instance = null;
        if (barRoot != null) Destroy(barRoot.gameObject);
    }

    void Update()
    {
        if (body == null) return;
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            body.color = Color.Lerp(baseColor, Color.white, 0.65f);
        }
        else
        {
            body.color = baseColor;
        }
    }

    /// <summary>Parpadeo cuando un enemigo la golpea.</summary>
    public void Flash()
    {
        flashTimer = 0.12f;
    }

    // Barra de vida sobre la puerta (en un objeto aparte para que no la afecte la escala de la base)
    void BuildHealthBar()
    {
        if (body == null) return;

        var rootGo = new GameObject("GateHealthBar");
        rootGo.transform.position = transform.position + new Vector3(0f, 0.75f, 0f);
        barRoot = rootGo.transform;

        var back = new GameObject("Back");
        back.transform.SetParent(barRoot, false);
        back.transform.localScale = new Vector3(1.3f, 0.16f, 1f);
        var backSr = back.AddComponent<SpriteRenderer>();
        backSr.sprite = body.sprite;
        backSr.color = new Color(0f, 0f, 0f, 0.75f);
        backSr.sortingOrder = 9;

        var holder = new GameObject("Fill");
        holder.transform.SetParent(barRoot, false);
        holder.transform.localPosition = new Vector3(-0.65f, 0f, 0f);
        fill = holder.transform;

        var bar = new GameObject("Bar");
        bar.transform.SetParent(holder.transform, false);
        bar.transform.localPosition = new Vector3(0.65f, 0f, 0f);
        bar.transform.localScale = new Vector3(1.3f, 0.12f, 1f);
        fillRenderer = bar.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = body.sprite;
        fillRenderer.sortingOrder = 10;
    }

    void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || fill == null) return;

        float ratio = gm.MaxLives > 0 ? Mathf.Clamp01((float)gm.Lives / gm.MaxLives) : 0f;
        Vector3 s = fill.localScale;
        s.x = ratio;
        fill.localScale = s;

        if (fillRenderer != null)
            fillRenderer.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.9f, 0.4f), ratio);
    }
}

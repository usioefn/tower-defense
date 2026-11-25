using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rayo eléctrico visual: segmentos en zigzag entre dos puntos que se desvanecen rápido.
/// </summary>
public class BoltFx : MonoBehaviour
{
    const float Duration = 0.2f;
    const int Segments = 4;

    readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    Color baseColor;
    float time;

    public void Play(Sprite sprite, Color color, Vector3 from, Vector3 to)
    {
        baseColor = color;

        Vector3 dir = to - from;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;

        Vector3 prev = from;
        for (int i = 1; i <= Segments; i++)
        {
            Vector3 p = (i == Segments)
                ? to
                : Vector3.Lerp(from, to, i / (float)Segments) + perp * Random.Range(-0.18f, 0.18f);
            AddSegment(sprite, prev, p);
            prev = p;
        }
    }

    void AddSegment(Sprite sprite, Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        if (d.sqrMagnitude < 0.0001f) return;

        var go = new GameObject("Segment");
        go.transform.SetParent(transform, false);
        go.transform.position = (a + b) * 0.5f;
        go.transform.up = d;
        go.transform.localScale = new Vector3(0.09f, d.magnitude, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = baseColor;
        sr.sortingOrder = 9;
        renderers.Add(sr);
    }

    void Update()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / Duration);
        var c = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t);
        foreach (var sr in renderers) if (sr != null) sr.color = c;
        if (t >= 1f) Destroy(gameObject);
    }
}

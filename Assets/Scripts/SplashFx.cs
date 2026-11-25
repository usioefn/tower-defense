using UnityEngine;

/// <summary>
/// Efecto visual simple: un círculo que se expande y se desvanece.
/// </summary>
public class SplashFx : MonoBehaviour
{
    SpriteRenderer sr;
    Color startColor;
    float targetSize;
    float time;
    const float Duration = 0.25f;

    public void Play(Sprite sprite, Color color, float size)
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 6;
        startColor = color;
        targetSize = size;
        transform.localScale = Vector3.one * 0.1f;
    }

    void Update()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / Duration);
        transform.localScale = Vector3.one * Mathf.Lerp(0.1f, targetSize, t);
        if (sr != null) sr.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
        if (t >= 1f) Destroy(gameObject);
    }
}

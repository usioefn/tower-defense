using UnityEngine;

/// <summary>
/// Disparo visual de los enemigos que atacan a distancia (los arqueros): una bolita que viaja
/// hasta el objetivo. El daño lo aplica el enemigo en el momento de disparar; esto es solo estética.
/// </summary>
public class ShotFx : MonoBehaviour
{
    const float Duration = 0.14f;

    Vector3 from;
    Vector3 to;
    float t;

    public static void Spawn(Vector3 from, Vector3 to, Color color, Sprite sprite)
    {
        from.z = 0f;
        to.z = 0f;

        var go = new GameObject("EnemyShot");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 0.14f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 9;

        var fx = go.AddComponent<ShotFx>();
        fx.from = from;
        fx.to = to;
    }

    void Update()
    {
        t += Time.deltaTime / Duration;
        transform.position = Vector3.Lerp(from, to, t);
        if (t >= 1f) Destroy(gameObject);
    }
}

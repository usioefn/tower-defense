using UnityEngine;

/// <summary>
/// Camino que siguen los enemigos. Los hijos de este objeto son los waypoints, en orden.
/// </summary>
public class Waypoints : MonoBehaviour
{
    public static Waypoints Instance { get; private set; }

    public Transform[] points;
    float[] remaining; // distancia restante hasta el final desde cada waypoint

    void Awake()
    {
        Instance = this;

        if (points == null || points.Length == 0)
        {
            points = new Transform[transform.childCount];
            for (int i = 0; i < points.Length; i++) points[i] = transform.GetChild(i);
        }

        remaining = new float[points.Length];
        for (int i = points.Length - 2; i >= 0; i--)
            remaining[i] = remaining[i + 1] + Vector2.Distance(points[i].position, points[i + 1].position);
    }

    public float RemainingFrom(int index)
    {
        if (remaining == null || index < 0 || index >= remaining.Length) return 0f;
        return remaining[index];
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        for (int i = 0; i < transform.childCount; i++)
        {
            Gizmos.DrawWireSphere(transform.GetChild(i).position, 0.2f);
            if (i < transform.childCount - 1)
                Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild(i + 1).position);
        }
    }
}

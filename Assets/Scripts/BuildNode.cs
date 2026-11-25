using UnityEngine;

/// <summary>
/// Casilla donde se puede construir una torre. Necesita un Collider2D para detectar clics.
/// </summary>
public class BuildNode : MonoBehaviour
{
    [HideInInspector] public Tower tower;

    public bool IsFree { get { return tower == null; } }
}

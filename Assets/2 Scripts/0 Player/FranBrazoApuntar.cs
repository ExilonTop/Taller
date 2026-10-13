using UnityEngine;

public class FranBrazoApuntar : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public Transform brazo;
    public Transform puntaBrazo;

    [Header("Órbita")]
    public float distancia = 1.5f;

    [Header("Cámara")]
    [Tooltip("Si se deja vacío, se utilizará Camera.main.")]
    public Camera camaraObjetivo;

    private void Awake()
    {
        if (camaraObjetivo == null)
            camaraObjetivo = Camera.main;

        if (player == null)
            Debug.LogError("[FranBrazoApuntar] Falta la referencia 'player'.");

        if (brazo == null)
            Debug.LogError("[FranBrazoApuntar] Falta la referencia 'brazo'.");
    }

    private void Update()
    {
        if (player == null || brazo == null || camaraObjetivo == null)
            return;

        if (!MouseWorld2D.TryGetPointOnPlane(
                camaraObjetivo,
                Input.mousePosition,
                player.position.z,
                out Vector2 mouseWorld))
        {
            return;
        }

        Vector2 direccionMundo = mouseWorld - (Vector2)player.position;

        if (direccionMundo.sqrMagnitude < 0.0001f)
            return;

        direccionMundo.Normalize();

        // Convertimos la dirección mundo al espacio local del padre del brazo,
        // para que también funcione si el personaje/pivote tiene rotación.
        Transform padre = brazo.parent;
        Vector3 direccionLocal = padre != null
            ? padre.InverseTransformDirection(direccionMundo)
            : (Vector3)direccionMundo;

        if (direccionLocal.sqrMagnitude < 0.0001f)
            return;

        direccionLocal.Normalize();
        brazo.localPosition = direccionLocal * distancia;

        float anguloLocal = Mathf.Atan2(
            direccionLocal.y,
            direccionLocal.x
        ) * Mathf.Rad2Deg;

        brazo.localRotation = Quaternion.Euler(0f, 0f, anguloLocal);
    }

    /// <summary>Punto real desde el que se lanza el gancho.</summary>
    public Vector2 ObtenerPuntoLanzamiento()
    {
        if (puntaBrazo != null)
            return puntaBrazo.position;

        if (brazo != null)
            return brazo.position;

        return transform.position;
    }
}

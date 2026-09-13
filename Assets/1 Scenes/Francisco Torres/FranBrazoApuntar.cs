using UnityEngine;

public class FranBrazoApuntar : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public Transform brazo;
    public Transform puntaBrazo;   // Punto de lanzamiento del gancho (hijo del brazo)

    [Header("Órbita")]
    public float distancia = 1.5f;

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
        if (player == null) Debug.LogError("[FranBrazoApuntar] Falta 'player'");
        if (brazo == null) Debug.LogError("[FranBrazoApuntar] Falta 'brazo'");
    }

    void Update()
    {
        if (player == null || brazo == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        Vector3 dir = (mouseWorld - player.position);
        if (dir.sqrMagnitude < 0.0001f) return; // Evita NaN si el mouse coincide con el player
        dir.Normalize();

        // Posición del brazo como offset LOCAL relativo al player
        brazo.localPosition = dir * distancia;

        // Rotación en espacio local para respetar la rotación del padre
        float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        brazo.localRotation = Quaternion.Euler(0f, 0f, angulo);
    }

    /// <summary>Punto de origen del gancho (la punta del brazo).</summary>
    public Vector2 ObtenerPuntoLanzamiento()
    {
        if (puntaBrazo != null) return puntaBrazo.position;
        return brazo != null ? brazo.position : (Vector2)transform.position;
    }
}
using UnityEngine;

public class FranBrazoApuntar : MonoBehaviour
{
    [Header("Configuración")]
    public Transform player;        // Referencia al player
    public Transform brazo;         // El brazo
    public float distancia = 1.5f;  // Distancia de órbita

    void Update()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // Dirección desde el player hacia el mouse
        Vector3 direccion = (mouseWorld - player.position).normalized;

        // Nueva posición del brazo (órbita alrededor del player)
        Vector3 nuevaPosicion = player.position + direccion * distancia;
        brazo.position = nuevaPosicion;

        // Rotación para que el brazo apunte en la dirección
        float angulo = Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg;
        brazo.rotation = Quaternion.Euler(0, 0, angulo);
    }
}

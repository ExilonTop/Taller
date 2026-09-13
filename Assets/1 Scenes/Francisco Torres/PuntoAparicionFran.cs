using UnityEngine;

public class PuntoAparicionFran : MonoBehaviour
{
    [Tooltip("ID único de este punto de aparición.")]
    public string id;

    private void Start()
    {
        if (string.IsNullOrEmpty(DatosTransicion.idSpawnDestino))
            return;

        if (DatosTransicion.idSpawnDestino != id)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("[PuntoAparicion] No se encontró un objeto con Tag 'Player'.");
            return;
        }

        player.transform.position = transform.position;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        DatosTransicion.idSpawnDestino = "";
    }
}

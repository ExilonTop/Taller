using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class TransicionEscenaFran : MonoBehaviour
{
    public enum Direccion
    {
        Arriba,
        Abajo,
        Izquierda,
        Derecha
    }

    [Header("Escena destino")]
    [Tooltip("Nombre exacto de la escena que se cargará.")]
    public string escenaDestino;

    [Tooltip("ID del PuntoAparicion de la escena destino.")]
    public string spawnDestino;

    [Header("Dirección del borde")]
    [Tooltip("Dirección desde la que debe entrar el Player.")]
    public Direccion direccion = Direccion.Arriba;

    [Header("Opcional")]
    [Tooltip("Actívalo para exigir que el Player se esté moviendo hacia este borde.")]
    public bool comprobarMovimiento = false;

    private bool usada;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (usada || !other.CompareTag("Player"))
            return;

        if (comprobarMovimiento && !EstaYendoHaciaElBorde(other))
            return;

        CambiarEscena();
    }

    private bool EstaYendoHaciaElBorde(Collider2D other)
    {
        Rigidbody2D rb = other.attachedRigidbody;
        if (rb == null)
            return true;

        const float umbral = 0.01f;

        switch (direccion)
        {
            case Direccion.Arriba:
                return rb.linearVelocity.y > umbral;
            case Direccion.Abajo:
                return rb.linearVelocity.y < -umbral;
            case Direccion.Izquierda:
                return rb.linearVelocity.x < -umbral;
            case Direccion.Derecha:
                return rb.linearVelocity.x > umbral;
        }

        return true;
    }

    private void CambiarEscena()
    {
        if (string.IsNullOrWhiteSpace(escenaDestino))
        {
            Debug.LogError("[TransicionEscena] Falta 'escenaDestino' en " + gameObject.name);
            return;
        }

        if (string.IsNullOrWhiteSpace(spawnDestino))
        {
            Debug.LogError("[TransicionEscena] Falta 'spawnDestino' en " + gameObject.name);
            return;
        }

        usada = true;
        DatosTransicion.idSpawnDestino = spawnDestino;

        SceneManager.LoadScene(escenaDestino, LoadSceneMode.Single);
    }
}

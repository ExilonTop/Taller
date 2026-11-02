using UnityEngine;
using Cinemachine;

/// <summary>
/// Zona de cámara (Collider2D con Is Trigger). Cuando el jugador entra, el administrador
/// cambia a camaraDestino; cuando sale, vuelve la cámara de la zona anterior que siga
/// ocupando. Cada zona debe cubrir la sala completa de su cámara (no una tira en la puerta).
///
/// Revisión: los cambios respecto de la versión anterior están marcados con [FIX n].
/// </summary>
public class TransicionCamaraFran : MonoBehaviour
{
    [Header("Cámara destino")]
    public CinemachineVirtualCamera camaraDestino;

    [Header("Administrador")]
    [Tooltip("Si queda vacío, se busca automáticamente el AdministradorCamarasFran de la escena.")]
    public AdministradorCamarasFran administrador;

    // [FIX 2] Campo nuevo.
    [Header("Jugador")]
    [Tooltip("Tag del jugador. Se acepta en el collider o en el objeto que tiene su Rigidbody2D.")]
    public string tagJugador = "Player";

    // [FIX 2] Colliders del jugador que están dentro (puede tener más de uno: cuerpo, pies...).
    private int collidersJugadorDentro;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        // [FIX 3] Ya no hace falta asignarlo a mano en cada zona.
        if (administrador == null)
        {
#if UNITY_2023_1_OR_NEWER
            administrador = FindAnyObjectByType<AdministradorCamarasFran>();
#else
            administrador = FindObjectOfType<AdministradorCamarasFran>();
#endif
        }

        // [FIX 3] La configuración se revisa una sola vez. Antes, con OnTriggerStay2D,
        // cada error se repetía en cada paso de física mientras el jugador estaba dentro.
        if (administrador == null)
            Debug.LogError("[TransicionCamaraFran] No hay AdministradorCamarasFran en la escena (zona " + gameObject.name + ").");

        if (camaraDestino == null)
            Debug.LogError("[TransicionCamaraFran] No se asignó cámara destino en " + gameObject.name);

        Collider2D col = GetComponent<Collider2D>();

        if (col == null)
            Debug.LogError("[TransicionCamaraFran] " + gameObject.name + " necesita un Collider2D con Is Trigger.");
        else if (!col.isTrigger)
            Debug.LogWarning("[TransicionCamaraFran] El Collider2D de " + gameObject.name + " no es trigger: actúa como pared y no cambia la cámara.");
    }

    // [FIX 1] Antes: OnTriggerStay2D, que se llama en cada paso de física. En el borde
    // entre dos zonas, ambas pedían su cámara en cada paso: el administrador cambiaba dos
    // veces por paso y la cámara que ganaba dependía del orden interno de la física.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!EsJugador(other))
            return;

        collidersJugadorDentro++;

        if (collidersJugadorDentro == 1 && administrador != null && camaraDestino != null)
            administrador.EntrarZona(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!EsJugador(other) || collidersJugadorDentro == 0)
            return;

        collidersJugadorDentro--;

        if (collidersJugadorDentro == 0 && administrador != null)
            administrador.SalirZona(this);
    }

    private void OnDisable()
    {
        // Si la zona se apaga con el jugador dentro, cuenta como salida.
        if (collidersJugadorDentro == 0)
            return;

        collidersJugadorDentro = 0;

        if (administrador != null)
            administrador.SalirZona(this);
    }

    // [FIX 2] Antes solo se miraba el tag del collider: si el collider de Mexy está en
    // un hijo sin tag, la zona no la detectaba. Además se ignoran los triggers del jugador
    // (radios de interacción, sensores) para que no cambien la cámara antes de tiempo.
    private bool EsJugador(Collider2D other)
    {
        if (other.isTrigger)
            return false;

        if (other.CompareTag(tagJugador))
            return true;

        Rigidbody2D cuerpo = other.attachedRigidbody;
        return cuerpo != null && cuerpo.CompareTag(tagJugador);
    }
}

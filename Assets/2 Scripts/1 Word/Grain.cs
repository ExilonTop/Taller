using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trigger de un solo uso.
/// Cuando el objeto con tag "Player" entra al trigger:
///   1. Apaga dos scripts del player (los eliges escribiendo su nombre).
///   2. Enciende un botón.
/// Al presionar el botón:
///   1. Vuelve a encender los dos scripts.
///   2. Apaga el botón.
/// Después de eso, el trigger no vuelve a funcionar.
///
/// Requisitos: este objeto necesita un Collider2D (o Collider) con "Is Trigger" activado.
/// </summary>
public class Grain : MonoBehaviour
{
    [Header("Scripts del player a apagar (nombre exacto de la clase)")]
    [SerializeField] private string nombreScript1;
    [SerializeField] private string nombreScript2;

    [Header("Botón que se enciende")]
    [SerializeField] private Button boton;

    [Header("Opciones")]
    [SerializeField] private string tagPlayer = "Player";

    private Behaviour script1;
    private Behaviour script2;
    private bool yaUsado = false;

    private void Start()
    {
        if (boton != null)
        {
            boton.gameObject.SetActive(false);
            boton.onClick.AddListener(AlPresionarBoton);
        }
    }

    // --- Juegos 2D ---
    private void OnTriggerEnter2D(Collider2D other)
    {
        Activar(other.gameObject);
    }

    // --- Juegos 3D (por si algún día lo necesitas) ---
    private void OnTriggerEnter(Collider other)
    {
        Activar(other.gameObject);
    }

    private void Activar(GameObject objeto)
    {
        if (yaUsado) return;
        if (!objeto.CompareTag(tagPlayer)) return;

        yaUsado = true;

        script1 = BuscarScript(objeto, nombreScript1);
        script2 = BuscarScript(objeto, nombreScript2);

        if (script1 != null) script1.enabled = false;
        if (script2 != null) script2.enabled = false;

        if (boton != null)
            boton.gameObject.SetActive(true);
    }

    private void AlPresionarBoton()
    {
        if (script1 != null) script1.enabled = true;
        if (script2 != null) script2.enabled = true;

        if (boton != null)
        {
            boton.onClick.RemoveListener(AlPresionarBoton);
            boton.gameObject.SetActive(false);
        }
    }

    private Behaviour BuscarScript(GameObject objeto, string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;

        Behaviour encontrado = objeto.GetComponent(nombre) as Behaviour;

        if (encontrado == null)
            Debug.LogWarning($"Grain: no se encontró el script '{nombre}' en '{objeto.name}'. Revisa que el nombre sea exacto.");

        return encontrado;
    }

    private void OnDestroy()
    {
        if (boton != null)
            boton.onClick.RemoveListener(AlPresionarBoton);
    }
}
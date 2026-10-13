using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SaltoAereoFran : MonoBehaviour
{
    [Header("Tecla")]
    public KeyCode teclaSalto = KeyCode.Space;

    [Header("Salto normal")]
    [Tooltip("Fuerza del impulso vertical del salto aéreo.")]
    public float fuerzaSalto = 4f;

    [Tooltip("Tiempo mínimo entre saltos.")]
    public float cooldownSalto = 0.15f;

    [Header("Uso del salto")]
    [Tooltip("Permite usar el salto mientras el gancho está enganchado.")]
    public bool permitirEnGancho = true;

    [Tooltip("Permite usar un salto mientras está cayendo o en el aire.")]
    public bool permitirEnAire = true;

    [Tooltip("Si está activo, el salto consume el gancho al utilizarse.")]
    public bool soltarGanchoAlSaltar = false;

    [Header("Reinicio")]
    [Tooltip("Detecta suelo para recuperar el salto aéreo.")]
    public Transform puntoSuelo;

    [Tooltip("Radio de detección del suelo.")]
    public float radioSuelo = 0.12f;

    [Tooltip("Capa de las plataformas/suelo.")]
    public LayerMask capaSuelo;

    private Rigidbody2D rb;

    private bool saltoDisponible = true;
    private bool estaEnSuelo;

    private float ultimoSalto = -999f;

    private DistanceJoint2D joint;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        joint = GetComponent<DistanceJoint2D>();
    }

    private void Update()
    {
        ComprobarSuelo();

        if (Input.GetKeyDown(teclaSalto))
        {
            IntentarSaltar();
        }
    }

    private void IntentarSaltar()
    {
        if (Time.time < ultimoSalto + cooldownSalto)
            return;

        // En el suelo no gastamos el salto aéreo.
        if (estaEnSuelo)
            return;

        if (!saltoDisponible)
            return;

        if (!permitirEnAire)
            return;

        bool estaEnganchado = joint != null && joint.enabled;

        if (estaEnganchado && !permitirEnGancho)
            return;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            0f
        );

        rb.AddForce(
            Vector2.up * fuerzaSalto,
            ForceMode2D.Impulse
        );

        ultimoSalto = Time.time;
        saltoDisponible = false;

        if (estaEnganchado && soltarGanchoAlSaltar)
        {
            joint.enabled = false;
        }

        Debug.Log(
            "[SaltoAereoFran] Salto realizado. Fuerza: "
            + fuerzaSalto
        );
    }

    private void ComprobarSuelo()
    {
        if (puntoSuelo == null)
        {
            estaEnSuelo = false;
            return;
        }

        bool estabaEnSuelo = estaEnSuelo;

        estaEnSuelo = Physics2D.OverlapCircle(
            puntoSuelo.position,
            radioSuelo,
            capaSuelo
        );

        // Al volver al suelo recuperamos el salto.
        if (!estabaEnSuelo && estaEnSuelo)
        {
            saltoDisponible = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (puntoSuelo == null)
            return;

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            puntoSuelo.position,
            radioSuelo
        );
    }
}
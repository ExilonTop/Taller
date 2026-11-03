using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Move : MonoBehaviour
{
    [Header("Movimiento terrestre")]
    [Tooltip("Velocidad máxima de arrastre de Mexy.")]
    public float velocidadArrastre = 0.8f;

    [Tooltip("Qué tan rápido alcanza la velocidad máxima.")]
    public float aceleracion = 2f;

    [Tooltip("Qué tan rápido se detiene al soltar A/D.")]
    public float desaceleracion = 3f;

    [Header("Detección del suelo")]
    [Tooltip("Punto desde donde se comprueba si Mexy está tocando el suelo.")]
    public Transform puntoSuelo;

    [Tooltip("Radio de detección del suelo.")]
    public float radioSuelo = 0.12f;

    [Tooltip("Capas consideradas como suelo.")]
    public LayerMask capaSuelo;

    [Header("Dirección del Player")]
    [Tooltip("Actívalo si el dibujo original del personaje mira a la izquierda.")]
    public bool invertirDireccion = false;

    private Rigidbody2D rb;
    private float escalaXOriginal;

    private bool estaEnSuelo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        escalaXOriginal = Mathf.Abs(transform.localScale.x);
    }

    private void Update()
    {
        ComprobarSuelo();
        GirarPlayer();
    }

    private void FixedUpdate()
    {
        // Si está en el aire, NO permitimos movimiento terrestre.
        if (!estaEnSuelo)
            return;

        // Si el gancho está activo, dejamos A/D para el balanceo.
        DistanceJoint2D joint = GetComponent<DistanceJoint2D>();

        if (joint != null && joint.enabled)
            return;

        float input = Input.GetAxisRaw("Horizontal");

        float objetivo = input * velocidadArrastre;

        float velocidadX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            objetivo,
            (Mathf.Abs(input) > 0.01f ? aceleracion : desaceleracion)
            * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector2(
            velocidadX,
            rb.linearVelocity.y
        );
    }

    private void ComprobarSuelo()
    {
        if (puntoSuelo == null)
        {
            estaEnSuelo = false;
            return;
        }

        estaEnSuelo = Physics2D.OverlapCircle(
            puntoSuelo.position,
            radioSuelo,
            capaSuelo
        );
    }

    private void GirarPlayer()
    {
        float input = Input.GetAxisRaw("Horizontal");

        // Al soltar A/D, conservamos la última dirección.
        if (Mathf.Abs(input) < 0.01f)
            return;

        // A: izquierda (-1). D: derecha (1).
        float direccion = input > 0f ? 1f : -1f;

        if (invertirDireccion)
            direccion *= -1f;

        // Invertimos el Player completo, incluidos sus objetos hijos.
        // Conservamos el tamaño original y las escalas Y/Z.
        Vector3 escala = transform.localScale;
        escala.x = escalaXOriginal * direccion;
        transform.localScale = escala;
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

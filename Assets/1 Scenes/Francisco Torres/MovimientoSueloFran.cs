using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovimientoSueloFran : MonoBehaviour
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

    [Header("Animación visual")]
    [Tooltip("Sprite de Mexy para poder cambiar la dirección.")]
    public SpriteRenderer sprite;

    private Rigidbody2D rb;

    private bool estaEnSuelo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (sprite == null)
            sprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        ComprobarSuelo();
        GirarSprite();
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

    private void GirarSprite()
    {
        if (sprite == null)
            return;

        float input = Input.GetAxisRaw("Horizontal");

        if (input > 0.01f)
            sprite.flipX = false;

        else if (input < -0.01f)
            sprite.flipX = true;
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
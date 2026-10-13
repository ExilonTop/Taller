using UnityEngine;

/// <summary>
/// Apunta el brazo hacia el cursor y acorta su órbita cuando una superficie
/// sólida se encuentra en la trayectoria. Los colliders de Mexy se excluyen
/// usando el Rigidbody2D raíz, aunque el campo Player apunte a un hijo.
/// </summary>
public class FranBrazoApuntar : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Asigna preferentemente Mexy_Player, el objeto que contiene Rigidbody2D.")]
    public Transform player;

    public Transform brazo;
    public Transform puntaBrazo;

    [Header("Órbita")]
    [Tooltip("Distancia normal del brazo desde su pivote.")]
    [Min(0f)]
    public float distancia = 1.5f;

    [Header("Acortamiento cerca de superficies")]
    public bool acortarCercaDeSuperficies = true;

    [Tooltip("Separación de seguridad entre la punta de lanzamiento y la superficie.")]
    [Min(0f)]
    public float margenSuperficie = 0.08f;

    [Tooltip("Radio del CircleCast que busca superficies cercanas.")]
    [Min(0f)]
    public float radioDeteccion = 0.04f;

    [Tooltip("Velocidad con la que el brazo vuelve a la distancia normal al quedar libre.")]
    [Min(0f)]
    public float velocidadRestauracion = 5f;

    [Tooltip("Transform visual externo que no debe detectar el cast; normalmente PuntaGancho.")]
    public Transform excluirDelRaycast;

    [Header("Cámara")]
    public Camera camaraObjetivo;

    [Header("Depuración")]
    public bool mostrarDiagnostico;

    private Rigidbody2D playerRb;
    private Transform playerRoot;
    private Transform centroOrbita;

    private float distanciaActual;
    private float ultimaDistanciaSegura;
    private Vector2 ultimaDireccionMundo = Vector2.right;
    private bool referenciasAvisadas;

    private void Awake()
    {
        ResolverReferencias();

        distanciaActual = Mathf.Max(0f, distancia);
        ultimaDistanciaSegura = distanciaActual;
    }

    private void OnValidate()
    {
        distancia = Mathf.Max(0f, distancia);
        margenSuperficie = Mathf.Max(0f, margenSuperficie);
        radioDeteccion = Mathf.Max(0f, radioDeteccion);
        velocidadRestauracion = Mathf.Max(0f, velocidadRestauracion);
    }

    private void ResolverReferencias()
    {
        if (camaraObjetivo == null)
            camaraObjetivo = Camera.main;

        // El script normalmente está en Mexy_Player; buscar primero aquí
        // evita fallar si Player se asignó por error a Cuerpo.
        playerRb = GetComponent<Rigidbody2D>();

        if (playerRb == null && player != null)
            playerRb = player.GetComponent<Rigidbody2D>();

        if (playerRb == null && player != null)
            playerRb = player.GetComponentInParent<Rigidbody2D>();

        if (playerRb == null && player != null)
            playerRb = player.GetComponentInChildren<Rigidbody2D>();

        playerRoot = playerRb != null
            ? playerRb.transform
            : player;

        if (playerRoot != null && player != playerRoot && playerRb != null)
        {
            Debug.LogWarning(
                "[FranBrazoApuntar] El campo Player apuntaba a un hijo. " +
                "Para mayor claridad, asigna Mexy_Player (el objeto con Rigidbody2D). " +
                "El script ya está utilizando el Rigidbody2D raíz para excluir sus colliders.",
                this
            );
        }

        // El brazo se mueve con localPosition respecto a su padre.
        // Por eso el punto de inicio del cast debe ser ese mismo pivote.
        if (brazo != null && brazo.parent != null)
            centroOrbita = brazo.parent;
        else
            centroOrbita = playerRoot;

        if (playerRoot == null)
            Debug.LogError("[FranBrazoApuntar] No se encontró el Transform raíz de Mexy.", this);

        if (playerRb == null)
            Debug.LogWarning(
                "[FranBrazoApuntar] No se encontró Rigidbody2D. La exclusión de colliders " +
                "dependerá del Transform Player asignado.",
                this
            );

        if (brazo == null)
            Debug.LogError("[FranBrazoApuntar] Falta la referencia 'brazo'.", this);

        if (centroOrbita == null)
            Debug.LogError("[FranBrazoApuntar] No se encontró el pivote de órbita.", this);

        referenciasAvisadas = true;
    }

    private void Update()
    {
        if (playerRoot == null || centroOrbita == null ||
            brazo == null || camaraObjetivo == null)
        {
            if (!referenciasAvisadas)
                ResolverReferencias();

            return;
        }

        if (!MouseWorld2D.TryGetPointOnPlane(
                camaraObjetivo,
                Input.mousePosition,
                centroOrbita.position.z,
                out Vector2 mouseWorld))
        {
            return;
        }

        Vector2 direccionMundo = mouseWorld - (Vector2)centroOrbita.position;

        if (direccionMundo.sqrMagnitude < 0.0001f)
            return;

        direccionMundo.Normalize();
        ultimaDireccionMundo = direccionMundo;

        float distanciaSegura = distancia;

        if (acortarCercaDeSuperficies)
            distanciaSegura = CalcularDistanciaSegura(direccionMundo);

        ultimaDistanciaSegura = distanciaSegura;

        // Si una superficie está cerca, acortar de inmediato para no atravesarla.
        if (distanciaSegura < distanciaActual)
        {
            distanciaActual = distanciaSegura;
        }
        else
        {
            // Cuando el camino queda libre, recuperar el radio con suavidad.
            distanciaActual = Mathf.MoveTowards(
                distanciaActual,
                distanciaSegura,
                velocidadRestauracion * Time.deltaTime
            );
        }

        Transform padre = brazo.parent;

        Vector3 direccionLocal = padre != null
            ? padre.InverseTransformDirection(direccionMundo)
            : (Vector3)direccionMundo;

        if (direccionLocal.sqrMagnitude < 0.0001f)
            return;

        direccionLocal.Normalize();
        brazo.localPosition = direccionLocal * Mathf.Max(0f, distanciaActual);

        float anguloLocal = Mathf.Atan2(
            direccionLocal.y,
            direccionLocal.x
        ) * Mathf.Rad2Deg;

        brazo.localRotation = Quaternion.Euler(0f, 0f, anguloLocal);
    }

    private float CalcularDistanciaSegura(Vector2 direccion)
    {
        if (centroOrbita == null)
            return distancia;

        float extensionPunta = 0f;

        if (puntaBrazo != null && brazo != null)
            extensionPunta = Vector2.Distance(brazo.position, puntaBrazo.position);

        float distanciaCast = distancia + extensionPunta + margenSuperficie;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            centroOrbita.position,
            radioDeteccion,
            direccion,
            distanciaCast
        );

        float menorDistancia = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || !EsObstaculoValido(hit.collider))
                continue;

            if (hit.distance < menorDistancia)
                menorDistancia = hit.distance;
        }

        // Si no hay obstáculo, la órbita normal debe poder restaurarse.
        if (menorDistancia == float.MaxValue)
            return distancia;

        // CircleCast mide desde el pivote; compensamos lo que sobresale
        // la punta del brazo y añadimos el margen de seguridad.
        float segura = menorDistancia - extensionPunta - margenSuperficie;

        return Mathf.Clamp(segura, 0f, distancia);
    }

    private bool EsObstaculoValido(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
            return false;

        // Esta es la corrección clave: ignora el collider raíz y todos los
        // colliders hijos conectados al Rigidbody2D de Mexy.
        if (playerRb != null && collider.attachedRigidbody == playerRb)
            return false;

        // También ignora colliders sin Rigidbody2D que pertenezcan a la
        // jerarquía del personaje.
        if (playerRoot != null &&
            (collider.transform == playerRoot ||
             collider.transform.IsChildOf(playerRoot)))
        {
            return false;
        }

        if (brazo != null &&
            (collider.transform == brazo ||
             collider.transform.IsChildOf(brazo)))
        {
            return false;
        }

        if (excluirDelRaycast != null &&
            (collider.transform == excluirDelRaycast ||
             collider.transform.IsChildOf(excluirDelRaycast)))
        {
            return false;
        }

        return true;
    }

    /// <summary>Punto real desde el que se lanza el gancho.</summary>
    public Vector2 ObtenerPuntoLanzamiento()
    {
        if (puntaBrazo != null)
            return puntaBrazo.position;

        if (brazo != null)
            return brazo.position;

        return centroOrbita != null ? centroOrbita.position : transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Transform centro =
            brazo != null && brazo.parent != null
                ? brazo.parent
                : player;

        if (centro == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(centro.position, radioDeteccion);

        // Muestra la órbita normal y el radio seguro en Scene View.
        Vector3 direccion = new Vector3(
            ultimaDireccionMundo.x,
            ultimaDireccionMundo.y,
            0f
        );

        if (direccion.sqrMagnitude < 0.0001f)
            direccion = Vector3.right;

        direccion.Normalize();

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            centro.position,
            centro.position + direccion * distancia
        );

        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            centro.position,
            centro.position + direccion * ultimaDistanciaSegura
        );
    }
}

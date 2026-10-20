using UnityEngine;

/// <summary>
/// Apunta el brazo hacia el cursor y acorta su órbita cuando una superficie
/// sólida se encuentra en la trayectoria. Los colliders de Mexy se excluyen
/// usando el Rigidbody2D raíz, aunque el campo Player apunte a un hijo.
/// </summary>
public class FranBrazoApuntarClaude : MonoBehaviour
{
    private const int MaxImpactos = 16;

    [Header("Referencias")]
    [Tooltip("Asigna preferentemente Mexy_Player, el objeto que contiene Rigidbody2D.")]
    public Transform player;

    [Tooltip("Objeto del brazo. Con rotación 0, su sprite debe apuntar hacia +X (derecha).")]
    public Transform brazo;

    [Tooltip("Hijo de Brazo, ubicado sobre su eje +X: el punto desde donde sale el gancho.")]
    public Transform puntaBrazo;

    [Tooltip("GanchoFran de Mexy. Si queda vacío, se busca en el objeto del Rigidbody2D.")]
    public GanchoFran gancho;

    [Tooltip("Mientras el gancho vuela, vuelve o está enganchado, el brazo apunta al gancho en vez de al mouse, para que la cuerda salga recta.")]
    public bool apuntarAlGancho = true;

    [Header("Órbita")]
    [Tooltip("Distancia normal del brazo desde su pivote, en unidades de mundo.")]
    [Min(0f)]
    public float distancia = 1.5f;

    [Header("Acortamiento cerca de superficies")]
    public bool acortarCercaDeSuperficies = true;

    [Tooltip("Capas que cuentan como superficie. Deben incluir todas las capas que bloquean " +
             "el gancho: GanchoFran trata como obstáculo cualquier collider sólido de cualquier capa.")]
    public LayerMask capasSuperficie = ~0;

    [Tooltip("Separación de seguridad entre la punta y la superficie. " +
             "La separación final es este valor más Radio Deteccion.")]
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
    [Tooltip("Dibuja el cast (rojo si choca) y registra en consola qué collider acorta el brazo.")]
    public bool mostrarDiagnostico;

    // Buffer reutilizable: CircleCastAll crea un arreglo nuevo en cada llamada.
    private readonly RaycastHit2D[] impactos = new RaycastHit2D[MaxImpactos];

    private Rigidbody2D playerRb;
    private Transform playerRoot;
    private Transform centroOrbita;

    private float distanciaActual;
    private float ultimaDistanciaSegura;
    private Vector2 ultimaDireccionMundo = Vector2.right;
    private bool faltantesReportados;
    private Collider2D ultimoObstaculoReportado;

    private void Awake()
    {
        ResolverReferencias();
        ReportarConfiguracion();

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

        // Prioridad: lo asignado en el Inspector. GetComponentInParent incluye
        // el propio objeto, así que también resuelve Player asignado a Cuerpo.
        playerRb = null;

        if (player != null)
            playerRb = player.GetComponentInParent<Rigidbody2D>();

        // Respaldo: la jerarquía del objeto que tiene este script.
        if (playerRb == null)
            playerRb = GetComponentInParent<Rigidbody2D>();

        if (playerRb == null && player != null)
            playerRb = player.GetComponentInChildren<Rigidbody2D>();

        playerRoot = playerRb != null ? playerRb.transform : player;

        if (gancho == null && playerRoot != null)
            gancho = playerRoot.GetComponent<GanchoFran>();

        // El brazo orbita alrededor de su padre; el cast sale de ese mismo pivote.
        centroOrbita = brazo != null && brazo.parent != null ? brazo.parent : playerRoot;
    }

    private void ReportarConfiguracion()
    {
        if (player != null && playerRb != null && player != playerRb.transform)
        {
            Debug.LogWarning(
                $"[FranBrazoApuntar] Player apunta a '{player.name}', pero el Rigidbody2D está en " +
                $"'{playerRb.name}'. Se usa '{playerRb.name}' como raíz para excluir sus colliders.",
                this);
        }

        if (playerRb == null)
        {
            Debug.LogWarning(
                "[FranBrazoApuntar] No se encontró Rigidbody2D. La exclusión de colliders " +
                "dependerá solo de la jerarquía de Player.",
                this);
        }
    }

    private bool ReferenciasListas()
    {
        if (playerRoot != null && centroOrbita != null && brazo != null && camaraObjetivo != null)
            return true;

        // Reintenta en vez de rendirse: la cámara, por ejemplo, puede crearse después de Awake.
        ResolverReferencias();

        bool listas = playerRoot != null && centroOrbita != null &&
                      brazo != null && camaraObjetivo != null;

        if (!listas && !faltantesReportados)
        {
            faltantesReportados = true;

            Debug.LogError(
                "[FranBrazoApuntar] Script detenido. Falta:" +
                (brazo == null ? " brazo;" : "") +
                (playerRoot == null ? " player;" : "") +
                (camaraObjetivo == null
                    ? " cámara (asigna Camara Objetivo o etiqueta la cámara como MainCamera);"
                    : ""),
                this);
        }

        return listas;
    }

    private void Update()
    {
        if (!ReferenciasListas())
            return;

        Vector2 centro = centroOrbita.position;

        if (!TryObtenerObjetivo(out Vector2 objetivo))
            return;

        Vector2 direccionMundo = objetivo - centro;

        if (direccionMundo.sqrMagnitude < 0.0001f)
            return;

        direccionMundo.Normalize();
        ultimaDireccionMundo = direccionMundo;

        float distanciaSegura = acortarCercaDeSuperficies
            ? CalcularDistanciaSegura(centro, direccionMundo)
            : distancia;

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
                velocidadRestauracion * Time.deltaTime);
        }

        AplicarPose(centro, direccionMundo);
    }

    /// <summary>
    /// Enganchado: el punto de enganche. Gancho en vuelo o volviendo: el punto
    /// donde la cuerda toca la punta. En reposo: el mouse.
    /// </summary>
    private bool TryObtenerObjetivo(out Vector2 objetivo)
    {
        if (apuntarAlGancho && gancho != null)
        {
            if (gancho.EstaEnganchado)
            {
                objetivo = gancho.PuntoEnganche;
                return true;
            }

            Transform visual = gancho.puntoUnionCuerda != null
                ? gancho.puntoUnionCuerda
                : gancho.puntaGancho;

            if (visual != null && visual.gameObject.activeInHierarchy)
            {
                objetivo = visual.position;
                return true;
            }
        }

        return MouseWorld2D.TryGetPointOnPlane(
            camaraObjetivo,
            Input.mousePosition,
            centroOrbita.position.z,
            out objetivo);
    }

    private void AplicarPose(Vector2 centro, Vector2 direccionMundo)
    {
        // Posición en coordenadas de mundo: la órbita mide lo mismo que el
        // CircleCast aunque el padre tenga escala distinta de 1 o negativa.
        Vector2 objetivo = centro + direccionMundo * Mathf.Max(0f, distanciaActual);
        brazo.position = new Vector3(objetivo.x, objetivo.y, centroOrbita.position.z);

        // InverseTransformVector sí considera la escala del padre (incluido el
        // volteo con localScale.x = -1); InverseTransformDirection la ignora.
        Transform padre = brazo.parent;

        Vector3 direccionLocal = padre != null
            ? padre.InverseTransformVector(direccionMundo)
            : (Vector3)direccionMundo;

        if (direccionLocal.sqrMagnitude < 1e-10f)
            return;

        float anguloLocal = Mathf.Atan2(direccionLocal.y, direccionLocal.x) * Mathf.Rad2Deg;
        brazo.localRotation = Quaternion.Euler(0f, 0f, anguloLocal);
    }

    private float CalcularDistanciaSegura(Vector2 centro, Vector2 direccion)
    {
        float extensionPunta = ExtensionPunta();
        float distanciaCast = distancia + extensionPunta + margenSuperficie;

        float distanciaImpacto = DistanciaAlObstaculo(
            centro, direccion, distanciaCast, out Collider2D obstaculo);

        if (mostrarDiagnostico)
            Diagnosticar(centro, direccion, distanciaCast, distanciaImpacto, obstaculo);

        // Si no hay obstáculo, la órbita normal debe poder restaurarse.
        if (obstaculo == null)
            return distancia;

        // El cast mide desde el pivote; descontamos lo que sobresale
        // la punta del brazo y el margen de seguridad.
        float segura = distanciaImpacto - extensionPunta - margenSuperficie;

        return Mathf.Clamp(segura, 0f, distancia);
    }

    private float ExtensionPunta()
    {
        if (puntaBrazo == null || brazo == null)
            return 0f;

        return Vector2.Distance(brazo.position, puntaBrazo.position);
    }

    /// <summary>
    /// Distancia desde el origen hasta la primera superficie válida en la
    /// dirección dada, o PositiveInfinity si no hay ninguna dentro del rango.
    /// </summary>
    private float DistanciaAlObstaculo(
        Vector2 origen,
        Vector2 direccion,
        float distanciaMax,
        out Collider2D obstaculo)
    {
        ContactFilter2D filtro = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = capasSuperficie,
            useTriggers = false
        };

        int cantidad = Physics2D.CircleCast(
            origen, radioDeteccion, direccion, filtro, impactos, distanciaMax);

        float menorDistancia = float.PositiveInfinity;
        obstaculo = null;

        for (int i = 0; i < cantidad; i++)
        {
            RaycastHit2D hit = impactos[i];

            // distance == 0: el collider ya envolvía el pivote al iniciar el cast
            // (p. ej. una plataforma one-way que Mexy está atravesando). No es una
            // pared delante; contarlo colapsaría el brazo hacia cualquier dirección.
            if (hit.distance <= 0f)
                continue;

            if (!EsObstaculoValido(hit.collider))
                continue;

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                obstaculo = hit.collider;
            }
        }

        return menorDistancia;
    }

    private bool EsObstaculoValido(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
            return false;

        // Ignora el collider raíz y todos los colliders hijos conectados
        // al Rigidbody2D de Mexy.
        if (playerRb != null && collider.attachedRigidbody == playerRb)
            return false;

        // También ignora colliders sin Rigidbody2D de la jerarquía del
        // personaje, del brazo y del objeto excluido.
        Transform t = collider.transform;

        // La punta del gancho vive fuera de Mexy; se excluye aunque
        // Excluir Del Raycast quede vacío.
        return !EsParteDe(t, playerRoot) &&
               !EsParteDe(t, brazo) &&
               !EsParteDe(t, excluirDelRaycast) &&
               !(gancho != null && EsParteDe(t, gancho.puntaGancho));
    }

    private static bool EsParteDe(Transform t, Transform raiz)
    {
        return raiz != null && (t == raiz || t.IsChildOf(raiz));
    }

    /// <summary>
    /// Punto real desde el que se lanza el gancho. Nunca queda dentro de una
    /// superficie: si la punta sobresale más allá de la pared (por ejemplo,
    /// con el brazo recogido contra el pivote), devuelve el último punto libre.
    /// </summary>
    public Vector2 ObtenerPuntoLanzamiento()
    {
        Vector2 centro = centroOrbita != null
            ? (Vector2)centroOrbita.position
            : (Vector2)transform.position;

        Vector2 punta = puntaBrazo != null ? (Vector2)puntaBrazo.position
                      : brazo != null ? (Vector2)brazo.position
                      : centro;

        Vector2 trayecto = punta - centro;
        float largo = trayecto.magnitude;

        if (largo < 0.0001f)
            return punta;

        Vector2 direccion = trayecto / largo;

        float impacto = DistanciaAlObstaculo(
            centro, direccion, largo + margenSuperficie, out Collider2D obstaculo);

        if (obstaculo == null)
            return punta;

        float libre = Mathf.Max(0f, impacto - margenSuperficie);
        return centro + direccion * Mathf.Min(largo, libre);
    }

    private void Diagnosticar(
        Vector2 origen,
        Vector2 direccion,
        float largoCast,
        float distanciaImpacto,
        Collider2D obstaculo)
    {
        float hasta = obstaculo != null ? distanciaImpacto : largoCast;

        Debug.DrawLine(
            origen,
            origen + direccion * hasta,
            obstaculo != null ? Color.red : Color.green);

        if (obstaculo == ultimoObstaculoReportado)
            return;

        ultimoObstaculoReportado = obstaculo;

        if (obstaculo != null)
        {
            Debug.Log(
                $"[FranBrazoApuntar] Brazo acortado por '{obstaculo.name}' " +
                $"(capa '{LayerMask.LayerToName(obstaculo.gameObject.layer)}', " +
                $"a {distanciaImpacto:0.00} u del pivote).",
                obstaculo);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform centro = centroOrbita != null
            ? centroOrbita
            : brazo != null && brazo.parent != null ? brazo.parent : player;

        if (centro == null)
            return;

        Vector3 origen = centro.position;
        Vector3 direccion = new Vector3(ultimaDireccionMundo.x, ultimaDireccionMundo.y, 0f);

        if (direccion.sqrMagnitude < 0.0001f)
            direccion = Vector3.right;

        direccion.Normalize();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origen, radioDeteccion);

        // Cian: órbita normal. Verde: radio seguro del último frame.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origen, origen + direccion * distancia);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origen, origen + direccion * ultimaDistanciaSegura);
    }
}

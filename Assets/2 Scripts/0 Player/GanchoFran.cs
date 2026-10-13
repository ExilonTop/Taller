using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Gancho de agarre 2D: se lanza con el click izquierdo hacia el cursor,
/// se engancha a superficies válidas mediante un DistanceJoint2D y permite
/// balancearse (A/D) y recoger/soltar cuerda (W/S).
///
/// Dependencias externas (se conservan tal cual):
///  - FranBrazoApuntar.ObtenerPuntoLanzamiento()
///  - MouseWorld2D.TryGetPointOnPlane(...)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class GanchoFran : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Estados
    // ------------------------------------------------------------------
    private enum Estado
    {
        Reposo,
        Lanzando,
        Enganchado,
        Recogiendo
    }

    // ------------------------------------------------------------------
    // Inspector
    // ------------------------------------------------------------------
    [Header("Alcance y velocidad")]
    public float distanciaMaxima = 10f;
    public float distanciaMinima = 1f;
    public float velocidadGancho = 20f;

    [Tooltip("Si está activo, el gancho no avanza más allá del punto del cursor ni supera distanciaMaxima.")]
    public bool limitarAlPuntoMouse = true;

    [Header("Superficies")]
    [Tooltip("Capas donde el gancho puede quedarse enganchado. Cualquier otro collider sólido detiene el gancho, pero no permite engancharse.")]
    public LayerMask capasGancho;

    [Tooltip("Si está activo, los colliders de Tilemap/Composite se consideran enganchables cuando su Layer (o la de algún padre) está en capasGancho.")]
    public bool detectarTilemaps = true;

    public bool ignorarTriggers = true;
    public bool devolverAlLlegarAlMaximo = true;

    [Header("Retorno visual")]
    public float velocidadRetornoGancho = 25f;
    public LineRenderer linea;

    [Tooltip("Objeto visual del gancho. Puede permanecer fuera de Mexy_Player.")]
    public Transform puntaGancho;

    [Tooltip("Punto exacto donde la cuerda debe tocar el gancho. Crea un Empty hijo de PuntaGancho y colócalo en la punta visible. Si queda vacío, se usa el Transform de PuntaGancho.")]
    public Transform puntoUnionCuerda;

    [Header("Referencias")]
    public FranBrazoApuntar brazo;
    public Camera camaraObjetivo;

    [Header("Balanceo A/D")]
    [Tooltip("Aceleración tangencial de balanceo.")]
    public float fuerzaBalanceo = 20f;

    public bool usarMovimientoTangencial = true;

    [Tooltip("Fuerza ascendente adicional. Déjala en 0 para probar la física natural.")]
    public float asistenciaAscenso = 0f;

    [Header("Recogido y soltado W/S")]
    [Tooltip("Velocidad a la que W reduce y S aumenta la longitud objetivo de la cuerda.")]
    public float velocidadRecogida = 1.6f;

    [Tooltip("Velocidad de ajuste de la longitud física de la cuerda, en unidades por segundo.")]
    public float suavidadRecogida = 6f;

    [Tooltip("Diferencia máxima entre la distancia real y la longitud de cuerda al recogerla. Evita acumular tensión si una pared bloquea al jugador.")]
    public float diferenciaMaximaTension = 0.18f;

    [Tooltip("Fuerza controlada que atrae a Mexy hacia el ancla mientras mantiene W.")]
    public float fuerzaRecogida = 12f;

    [Tooltip("Límite de velocidad hacia el punto de enganche mientras W recoge la cuerda.")]
    public float velocidadMaximaHaciaGancho = 3.5f;

    [Header("Joint")]
    [Tooltip("True = cuerda que limita la distancia máxima.")]
    public bool soloDistanciaMaxima = true;

    // ------------------------------------------------------------------
    // Constantes
    // ------------------------------------------------------------------
    private const float EpsilonSqr = 0.0001f;
    private const float UmbralEntrada = 0.01f;
    private const int TamanoBuffer = 32;

    // ------------------------------------------------------------------
    // Estado interno
    // ------------------------------------------------------------------
    private Rigidbody2D rb;
    private DistanceJoint2D joint;
    private Camera cam;

    private Estado estado = Estado.Reposo;

    private Vector2 puntoGancho;
    private Vector2 posicionGanchoVisual;

    private float longitudObjetivo;
    private float entradaRecogido;   // W = +1, S = -1
    private float entradaHorizontal; // A = -1, D = +1

    private Coroutine lanzamientoCoroutine;

    // Buffers reutilizables: evitan generar basura (GC) en cada raycast.
    private readonly RaycastHit2D[] bufferHits = new RaycastHit2D[TamanoBuffer];
    private readonly Collider2D[] bufferColliders = new Collider2D[TamanoBuffer];

    // ------------------------------------------------------------------
    // API pública de solo lectura (útil para animaciones, UI, etc.)
    // ------------------------------------------------------------------
    public bool EstaEnganchado => estado == Estado.Enganchado;
    public bool EstaLanzando => estado == Estado.Lanzando;
    public Vector2 PuntoEnganche => puntoGancho;

    // ------------------------------------------------------------------
    // Compatibilidad Unity 6 (linearVelocity) / versiones anteriores (velocity)
    // ------------------------------------------------------------------
    private Vector2 Velocidad
    {
#if UNITY_6000_0_OR_NEWER
        get => rb.linearVelocity;
        set => rb.linearVelocity = value;
#else
        get => rb.velocity;
        set => rb.velocity = value;
#endif
    }

    // ==================================================================
    // Ciclo de vida
    // ==================================================================
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = ObtenerCamara();

        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (linea == null)
            linea = GetComponent<LineRenderer>();

        if (linea != null)
        {
            // SetPosition trabaja en coordenadas del mundo, igual que los puntos del gancho.
            linea.useWorldSpace = true;
            linea.positionCount = 2;
            linea.enabled = false;
        }

        if (puntaGancho != null)
            puntaGancho.gameObject.SetActive(false);

        // Reutiliza un joint existente en lugar de crear duplicados.
        joint = GetComponent<DistanceJoint2D>();
        if (joint == null)
            joint = gameObject.AddComponent<DistanceJoint2D>();

        joint.autoConfigureDistance = false;
        joint.enableCollision = true;
        joint.maxDistanceOnly = soloDistanciaMaxima;
        joint.enabled = false;

        if (brazo == null)
            Debug.LogWarning($"[{nameof(GanchoFran)}] No hay 'brazo' asignado: el gancho saldrá del centro del Rigidbody.", this);
    }

    private void OnDisable()
    {
        // Si el componente se desactiva (muerte, cambio de escena, pausa de control...),
        // dejamos todo limpio para no quedar colgados de una cuerda fantasma.
        ReiniciarTodo();
    }

    private void OnValidate()
    {
        distanciaMaxima = Mathf.Max(0.1f, distanciaMaxima);
        distanciaMinima = Mathf.Clamp(distanciaMinima, 0f, distanciaMaxima);
        velocidadGancho = Mathf.Max(0f, velocidadGancho);
        velocidadRetornoGancho = Mathf.Max(0f, velocidadRetornoGancho);
        velocidadRecogida = Mathf.Max(0f, velocidadRecogida);
        suavidadRecogida = Mathf.Max(0f, suavidadRecogida);
        diferenciaMaximaTension = Mathf.Max(0f, diferenciaMaximaTension);
        fuerzaRecogida = Mathf.Max(0f, fuerzaRecogida);
        fuerzaBalanceo = Mathf.Max(0f, fuerzaBalanceo);
    }

    private void Update()
    {
        ProcesarClick();
        LeerEntradaDeCuerda();
    }

    private void FixedUpdate()
    {
        if (estado != Estado.Enganchado || joint == null || !joint.enabled)
            return;

        float dt = Time.fixedDeltaTime;
        Vector2 anclaJugador = transform.TransformPoint(joint.anchor);
        float distanciaReal = Vector2.Distance(anclaJugador, puntoGancho);

        ActualizarLongitudCuerda(distanciaReal, dt);
        AplicarRecogido(anclaJugador, distanciaReal);
        AplicarBalanceo(anclaJugador);

        if (asistenciaAscenso > 0f)
            rb.AddForce(Vector2.up * asistenciaAscenso, ForceMode2D.Force);
    }

    private void LateUpdate()
    {
        if (estado != Estado.Reposo)
            ActualizarLinea();
    }

    // ==================================================================
    // Entrada
    // ==================================================================
    private void ProcesarClick()
    {
        if (Input.GetMouseButtonDown(0) && estado == Estado.Reposo)
            IntentarLanzar();

        if (!Input.GetMouseButtonUp(0))
            return;

        switch (estado)
        {
            case Estado.Lanzando:
                // Soltar durante el vuelo cancela el lanzamiento.
                CancelarLanzamiento();
                break;

            case Estado.Enganchado:
                SoltarGancho();
                break;
        }
    }

    private void LeerEntradaDeCuerda()
    {
        entradaRecogido = 0f;
        entradaHorizontal = 0f;

        if (estado != Estado.Enganchado)
            return;

        // Teclas directas: evitan el retardo de suavizado de Input.GetAxis("Vertical").
        bool w = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool s = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        if (w != s)
            entradaRecogido = w ? 1f : -1f;

        entradaHorizontal = Input.GetAxisRaw("Horizontal");
    }

    private void IntentarLanzar()
    {
        cam = ObtenerCamara();
        if (cam == null)
            return;

        if (!MouseWorld2D.TryGetPointOnPlane(
                cam,
                Input.mousePosition,
                transform.position.z,
                out Vector2 mouseWorld))
            return;

        estado = Estado.Lanzando;
        lanzamientoCoroutine = StartCoroutine(LanzarGancho(mouseWorld));
    }

    private Camera ObtenerCamara()
    {
        if (cam != null)
            return cam;

        return camaraObjetivo != null ? camaraObjetivo : Camera.main;
    }

    // ==================================================================
    // Física mientras se está enganchado
    // ==================================================================
    private void ActualizarLongitudCuerda(float distanciaReal, float dt)
    {
        // W reduce la longitud deseada; S la aumenta.
        if (Mathf.Abs(entradaRecogido) > UmbralEntrada)
        {
            longitudObjetivo = Mathf.Clamp(
                longitudObjetivo - entradaRecogido * velocidadRecogida * dt,
                distanciaMinima,
                distanciaMaxima
            );
        }

        float longitudSiguiente = Mathf.MoveTowards(
            joint.distance,
            longitudObjetivo,
            suavidadRecogida * dt
        );

        // Protección contra tensión acumulada: el joint nunca queda muy por debajo
        // de la distancia real (por ejemplo si una pared bloquea al jugador).
        float longitudMinimaSegura = Mathf.Max(
            distanciaMinima,
            distanciaReal - Mathf.Max(0.02f, diferenciaMaximaTension)
        );

        longitudSiguiente = Mathf.Max(longitudSiguiente, longitudMinimaSegura);

        joint.distance = Mathf.Clamp(longitudSiguiente, distanciaMinima, distanciaMaxima);
    }

    private void AplicarRecogido(Vector2 anclaJugador, float distanciaReal)
    {
        // W atrae gradualmente a Mexy hacia el ancla, con velocidad radial limitada.
        if (entradaRecogido <= 0f ||
            fuerzaRecogida <= 0f ||
            distanciaReal <= distanciaMinima + 0.05f)
            return;

        Vector2 haciaGancho = puntoGancho - anclaJugador;
        if (haciaGancho.sqrMagnitude <= EpsilonSqr)
            return;

        haciaGancho.Normalize();

        LimitarVelocidadHaciaGancho(haciaGancho);
        rb.AddForce(haciaGancho * fuerzaRecogida, ForceMode2D.Force);
    }

    private void AplicarBalanceo(Vector2 anclaJugador)
    {
        if (Mathf.Abs(entradaHorizontal) <= UmbralEntrada)
            return;

        if (!usarMovimientoTangencial)
        {
            rb.AddForce(Vector2.right * entradaHorizontal * fuerzaBalanceo, ForceMode2D.Force);
            return;
        }

        Vector2 vectorAlGancho = puntoGancho - anclaJugador;
        if (vectorAlGancho.sqrMagnitude <= EpsilonSqr)
            return;

        Vector2 direccionCuerda = vectorAlGancho.normalized;
        Vector2 direccionTangente = new Vector2(-direccionCuerda.y, direccionCuerda.x);

        // El signo menos conserva los controles corregidos: A izquierda, D derecha.
        rb.AddForce(direccionTangente * -entradaHorizontal * fuerzaBalanceo, ForceMode2D.Force);
    }

    private void LimitarVelocidadHaciaGancho(Vector2 haciaGanchoNormalizado)
    {
        if (velocidadMaximaHaciaGancho <= 0f)
            return;

        Vector2 velocidad = Velocidad;
        float velocidadRadial = Vector2.Dot(velocidad, haciaGanchoNormalizado);

        // Limita solo la componente que acerca a Mexy al ancla; la inercia
        // tangencial y A/D siguen funcionando sin límite artificial.
        if (velocidadRadial > velocidadMaximaHaciaGancho)
        {
            Velocidad = velocidad -
                        haciaGanchoNormalizado * (velocidadRadial - velocidadMaximaHaciaGancho);
        }
    }

    // ==================================================================
    // Lanzamiento
    // ==================================================================
    private Vector2 ObtenerOrigen()
    {
        if (brazo != null)
            return brazo.ObtenerPuntoLanzamiento();

        return rb != null ? rb.position : (Vector2)transform.position;
    }

    private IEnumerator LanzarGancho(Vector2 destino)
    {
        Vector2 origen = ObtenerOrigen();
        Vector2 vectorDestino = destino - origen;
        float distanciaAlCursor = vectorDestino.magnitude;

        if (distanciaAlCursor < 0.0001f)
        {
            FinalizarLanzamiento();
            yield break;
        }

        Vector2 direccion = vectorDestino / distanciaAlCursor;

        // La punta solo llega hasta el cursor (si está activado) o hasta distanciaMaxima.
        float alcanceVuelo = limitarAlPuntoMouse
            ? Mathf.Min(distanciaAlCursor, distanciaMaxima)
            : distanciaMaxima;

        // Un raycast no detecta de forma fiable la superficie que contiene su origen.
        // Si el origen está dentro de geometría sólida, se cancela el lanzamiento.
        if (HayObstaculoEnOrigen(origen))
        {
            FinalizarLanzamiento();
            yield break;
        }

        posicionGanchoVisual = origen;
        float distanciaRecorrida = 0f;

        MostrarGanchoVisual(posicionGanchoVisual);

        while (distanciaRecorrida < alcanceVuelo)
        {
            float avance = Mathf.Min(
                velocidadGancho * Time.deltaTime,
                alcanceVuelo - distanciaRecorrida
            );

            if (avance > 0f)
            {
                RaycastHit2D hit = BuscarSuperficie(posicionGanchoVisual, direccion, avance);

                if (hit.collider != null)
                {
                    posicionGanchoVisual = hit.point;
                    MostrarGanchoVisual(posicionGanchoVisual);

                    if (EsEnganchable(hit.collider))
                    {
                        puntoGancho = hit.point;
                        ConfigurarJoint(origen);
                        lanzamientoCoroutine = null;
                        yield break;
                    }

                    // Superficie no enganchable: la punta vuelve al brazo.
                    yield return RecogerGancho();
                    FinalizarLanzamiento();
                    yield break;
                }
            }

            posicionGanchoVisual += direccion * avance;
            distanciaRecorrida += avance;

            MostrarGanchoVisual(posicionGanchoVisual);
            yield return null;
        }

        // No encontró nada hasta el máximo alcance.
        if (devolverAlLlegarAlMaximo)
        {
            yield return RecogerGancho();
        }
        else
        {
            OcultarGanchoVisual();
        }

        FinalizarLanzamiento();
    }

    private void FinalizarLanzamiento()
    {
        lanzamientoCoroutine = null;
        estado = Estado.Reposo;
    }

    private void CancelarLanzamiento()
    {
        if (lanzamientoCoroutine != null)
            StopCoroutine(lanzamientoCoroutine);

        lanzamientoCoroutine = null;
        estado = Estado.Reposo;
        OcultarGanchoVisual();
    }

    private IEnumerator RecogerGancho()
    {
        estado = Estado.Recogiendo;

        while (true)
        {
            Vector2 origenActual = ObtenerOrigen();

            posicionGanchoVisual = Vector2.MoveTowards(
                posicionGanchoVisual,
                origenActual,
                velocidadRetornoGancho * Time.deltaTime
            );

            MostrarGanchoVisual(posicionGanchoVisual);

            if (Vector2.Distance(posicionGanchoVisual, origenActual) <= 0.01f)
                break;

            yield return null;
        }

        posicionGanchoVisual = ObtenerOrigen();
        OcultarGanchoVisual();
        // El estado vuelve a Reposo en FinalizarLanzamiento().
    }

    // ==================================================================
    // Detección de superficies
    // ==================================================================
    private ContactFilter2D CrearFiltro()
    {
        ContactFilter2D filtro = new ContactFilter2D();
        filtro.NoFilter();
        filtro.useTriggers = !ignorarTriggers;
        return filtro;
    }

    private RaycastHit2D BuscarSuperficie(Vector2 origen, Vector2 direccion, float distancia)
    {
        // Se detectan TODOS los colliders sólidos del trayecto: el más cercano siempre
        // bloquea el gancho y luego EsEnganchable decide si puede adherirse.
        int cantidad = Physics2D.Raycast(
            origen,
            direccion,
            CrearFiltro(),
            bufferHits,
            distancia
        );

        RaycastHit2D mejorHit = default;
        float menorDistancia = float.MaxValue;

        for (int i = 0; i < cantidad; i++)
        {
            RaycastHit2D hit = bufferHits[i];

            if (!ColliderValido(hit.collider))
                continue;

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                mejorHit = hit;
            }
        }

        return mejorHit;
    }

    private bool HayObstaculoEnOrigen(Vector2 origen)
    {
        int cantidad = Physics2D.OverlapPoint(origen, CrearFiltro(), bufferColliders);

        for (int i = 0; i < cantidad; i++)
        {
            if (ColliderValido(bufferColliders[i]))
                return true;
        }

        return false;
    }

    private bool ColliderValido(Collider2D collider)
    {
        if (collider == null)
            return false;

        if (ignorarTriggers && collider.isTrigger)
            return false;

        if (collider.attachedRigidbody == rb)
            return false;

        if (EsParteDe(collider.transform, transform))
            return false;

        // La punta visual y su punto de unión no son superficies del escenario.
        if (puntaGancho != null && EsParteDe(collider.transform, puntaGancho))
            return false;

        if (puntoUnionCuerda != null && EsParteDe(collider.transform, puntoUnionCuerda))
            return false;

        return true;
    }

    private static bool EsParteDe(Transform objetivo, Transform raiz)
    {
        return objetivo == raiz || objetivo.IsChildOf(raiz);
    }

    private static bool EsColliderDeTilemap(Collider2D collider)
    {
        // GetComponentInParent también incluye al propio objeto.
        return collider.GetComponentInParent<TilemapCollider2D>() != null ||
               collider.GetComponentInParent<CompositeCollider2D>() != null;
    }

    private bool EsEnganchable(Collider2D collider)
    {
        if (collider == null)
            return false;

        if (CapaIncluida(capasGancho, collider.gameObject.layer))
            return true;

        if (!detectarTilemaps || !EsColliderDeTilemap(collider))
            return false;

        // En algunos Tilemap el collider está en un hijo y la Layer se asignó
        // al Tilemap o al Grid padre: se comprueba toda la jerarquía.
        for (Transform actual = collider.transform; actual != null; actual = actual.parent)
        {
            if (CapaIncluida(capasGancho, actual.gameObject.layer))
                return true;
        }

        return false;
    }

    private static bool CapaIncluida(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    // ==================================================================
    // Joint
    // ==================================================================
    private void ConfigurarJoint(Vector2 origen)
    {
        estado = Estado.Enganchado;

        joint.connectedBody = null;
        joint.connectedAnchor = puntoGancho;
        joint.anchor = transform.InverseTransformPoint(origen);

        float distanciaReal = Vector2.Distance(origen, puntoGancho);
        longitudObjetivo = Mathf.Clamp(distanciaReal, distanciaMinima, distanciaMaxima);

        joint.distance = longitudObjetivo;
        joint.maxDistanceOnly = soloDistanciaMaxima;

        // Se mantiene activado para conservar las colisiones del jugador.
        joint.enableCollision = true;
        joint.enabled = true;
    }

    private void SoltarGancho()
    {
        estado = Estado.Reposo;
        entradaRecogido = 0f;
        entradaHorizontal = 0f;

        if (joint != null)
            joint.enabled = false;

        OcultarGanchoVisual();
    }

    private void ReiniciarTodo()
    {
        StopAllCoroutines();
        lanzamientoCoroutine = null;
        SoltarGancho();
    }

    // ==================================================================
    // Visual
    // ==================================================================
    private void MostrarGanchoVisual(Vector2 posicion)
    {
        posicionGanchoVisual = posicion;

        if (puntaGancho != null)
        {
            puntaGancho.gameObject.SetActive(true);

            // Si el punto de unión está desplazado respecto al pivote del sprite,
            // se mueve el objeto padre para que el punto hijo coincida EXACTAMENTE
            // con la posición lógica del gancho.
            Vector3 offsetPivoteAUnion = Vector3.zero;
            if (puntoUnionCuerda != null)
                offsetPivoteAUnion = puntoUnionCuerda.position - puntaGancho.position;

            puntaGancho.position = new Vector3(
                posicion.x - offsetPivoteAUnion.x,
                posicion.y - offsetPivoteAUnion.y,
                puntaGancho.position.z
            );
        }

        ActualizarLinea();
    }

    private void OcultarGanchoVisual()
    {
        if (puntaGancho != null)
            puntaGancho.gameObject.SetActive(false);

        if (linea != null)
            linea.enabled = false;
    }

    private void ActualizarLinea()
    {
        if (linea == null)
            return;

        linea.enabled = true;
        linea.positionCount = 2;
        linea.SetPosition(0, ObtenerOrigen());

        // Termina exactamente en el punto de unión, no en el pivote del sprite.
        if (puntaGancho != null && puntaGancho.gameObject.activeSelf)
        {
            Vector3 extremo = puntoUnionCuerda != null
                ? puntoUnionCuerda.position
                : puntaGancho.position;

            linea.SetPosition(1, extremo);
        }
        else
        {
            linea.SetPosition(1, estado == Estado.Enganchado ? puntoGancho : posicionGanchoVisual);
        }
    }

    // ==================================================================
    // Gizmos
    // ==================================================================
    private void OnDrawGizmosSelected()
    {
        Vector3 centro = transform.position;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(centro, distanciaMaxima);

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(centro, distanciaMinima);

        if (Application.isPlaying && estado == Estado.Enganchado)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(centro, puntoGancho);
            Gizmos.DrawWireSphere(puntoGancho, 0.15f);
        }
    }
}
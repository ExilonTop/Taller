using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class GanchoFran : MonoBehaviour
{
    [Header("Alcance y velocidad")]
    public float distanciaMaxima = 10f;
    public float distanciaMinima = 1f;
    public float velocidadGancho = 20f;

    [Tooltip("Si está activo, el gancho no avanza más allá del punto del cursor ni supera distanciaMaxima.")]
    public bool limitarAlPuntoMouse = true;

    [Header("Superficies")]
    [Tooltip("Capas donde el gancho puede quedarse enganchado.")]
    public LayerMask capasGancho;

    [Tooltip("Capas que detienen el gancho pero no permiten engancharse.")]
    public LayerMask capasImpacto;

    public bool usarCapasGanchoComoImpacto = true;
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

    private Rigidbody2D rb;
    private DistanceJoint2D joint;
    private Camera cam;

    private Vector2 puntoGancho;
    private Vector2 posicionGanchoVisual;

    private bool enganchado;
    private bool lanzando;
    private bool recogiendo;

    private float longitudObjetivo;
    private float entradaRecogido; // W = +1, S = -1

    private Coroutine lanzamientoCoroutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        cam = camaraObjetivo != null ? camaraObjetivo : Camera.main;

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
    }

    private void Update()
    {
        if (cam == null)
            cam = camaraObjetivo != null ? camaraObjetivo : Camera.main;

        if (cam != null &&
            MouseWorld2D.TryGetPointOnPlane(
                cam,
                Input.mousePosition,
                transform.position.z,
                out Vector2 mouseWorld))
        {
            if (Input.GetMouseButtonDown(0) &&
                !enganchado &&
                !lanzando &&
                !recogiendo &&
                brazo != null)
            {
                lanzamientoCoroutine = StartCoroutine(LanzarGancho(mouseWorld));
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            // Soltar durante el vuelo cancela el lanzamiento.
            if (lanzando && !recogiendo)
            {
                if (lanzamientoCoroutine != null)
                    StopCoroutine(lanzamientoCoroutine);

                lanzamientoCoroutine = null;
                lanzando = false;
                OcultarGanchoVisual();
            }

            if (enganchado)
                SoltarGancho();
        }

        // Leer teclas directas evita el retardo de suavizado de Input.GetAxis("Vertical").
        entradaRecogido = 0f;

        if (enganchado)
        {
            bool w = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool s = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

            if (w != s)
                entradaRecogido = w ? 1f : -1f;
        }
    }

    private void FixedUpdate()
    {
        if (!enganchado || joint == null || !joint.enabled)
            return;

        float dt = Time.fixedDeltaTime;

        // W reduce la longitud deseada; S la aumenta.
        if (Mathf.Abs(entradaRecogido) > 0.01f)
        {
            longitudObjetivo = Mathf.Clamp(
                longitudObjetivo - entradaRecogido * velocidadRecogida * dt,
                distanciaMinima,
                distanciaMaxima
            );
        }

        Vector2 anclaJugador = rb.transform.TransformPoint(joint.anchor);
        float distanciaReal = Vector2.Distance(anclaJugador, puntoGancho);

        float longitudSiguiente = Mathf.MoveTowards(
            joint.distance,
            longitudObjetivo,
            suavidadRecogida * dt
        );

        // Protección permanente contra tensión acumulada:
        // nunca permitimos que el joint quede muy por debajo de la distancia real.
        // A diferencia de la versión anterior, ahora W aplica una fuerza radial
        // controlada para que Mexy sí pueda acercarse al punto de enganche.
        float longitudMinimaSegura = Mathf.Max(
            distanciaMinima,
            distanciaReal - Mathf.Max(0.02f, diferenciaMaximaTension)
        );

        longitudSiguiente = Mathf.Max(
            longitudSiguiente,
            longitudMinimaSegura
        );

        joint.distance = Mathf.Clamp(
            longitudSiguiente,
            distanciaMinima,
            distanciaMaxima
        );

        // Recogido activo: W atrae a Mexy hacia el ancla gradualmente.
        // La velocidad se limita para evitar que al desbloquearse de una pared
        // el personaje salga disparado por la tensión acumulada.
        if (entradaRecogido > 0f &&
            fuerzaRecogida > 0f &&
            distanciaReal > distanciaMinima + 0.05f)
        {
            Vector2 haciaGancho = puntoGancho - anclaJugador;

            if (haciaGancho.sqrMagnitude > 0.0001f)
            {
                haciaGancho.Normalize();

                LimitarVelocidadHaciaGancho(anclaJugador);

                rb.AddForce(
                    haciaGancho * fuerzaRecogida,
                    ForceMode2D.Force
                );
            }
        }

        // Balanceo A/D a lo largo de la tangente de la cuerda.
        float movHorizontal = Input.GetAxisRaw("Horizontal");

        if (usarMovimientoTangencial && Mathf.Abs(movHorizontal) > 0.01f)
        {
            Vector2 vectorAlGancho = puntoGancho - anclaJugador;

            if (vectorAlGancho.sqrMagnitude > 0.0001f)
            {
                Vector2 direccionCuerda = vectorAlGancho.normalized;
                Vector2 direccionTangente = new Vector2(
                    -direccionCuerda.y,
                    direccionCuerda.x
                );

                // El signo menos conserva los controles corregidos: A izquierda, D derecha.
                rb.AddForce(
                    direccionTangente * -movHorizontal * fuerzaBalanceo,
                    ForceMode2D.Force
                );
            }
        }
        else if (!usarMovimientoTangencial && Mathf.Abs(movHorizontal) > 0.01f)
        {
            rb.AddForce(
                Vector2.right * movHorizontal * fuerzaBalanceo,
                ForceMode2D.Force
            );
        }

        if (asistenciaAscenso > 0f)
        {
            rb.AddForce(
                Vector2.up * asistenciaAscenso,
                ForceMode2D.Force
            );
        }
    }

    private void LimitarVelocidadHaciaGancho(Vector2 anclaJugador)
    {
        if (velocidadMaximaHaciaGancho <= 0f)
            return;

        Vector2 haciaGancho = puntoGancho - anclaJugador;

        if (haciaGancho.sqrMagnitude < 0.0001f)
            return;

        haciaGancho.Normalize();

        float velocidadRadial = Vector2.Dot(rb.linearVelocity, haciaGancho);

        // Limitamos sólo la componente de velocidad que acerca a Mexy al ancla.
        // Se invoca durante el recogido para que A/D y la inercia tangencial
        // sigan funcionando sin un límite horizontal artificial.
        if (velocidadRadial > velocidadMaximaHaciaGancho)
        {
            rb.linearVelocity -= haciaGancho *
                (velocidadRadial - velocidadMaximaHaciaGancho);
        }
    }

    private void LateUpdate()
    {
        if (lanzando || enganchado || recogiendo)
            ActualizarLinea();
    }

    private Vector2 ObtenerOrigen()
    {
        if (brazo != null)
            return brazo.ObtenerPuntoLanzamiento();

        return rb.position;
    }

    private IEnumerator LanzarGancho(Vector2 destino)
    {
        lanzando = true;

        Vector2 origen = ObtenerOrigen();
        Vector2 vectorDestino = destino - origen;
        float distanciaAlCursor = vectorDestino.magnitude;

        if (distanciaAlCursor < 0.0001f)
        {
            lanzando = false;
            lanzamientoCoroutine = null;
            yield break;
        }

        Vector2 direccion = vectorDestino / distanciaAlCursor;

        // IMPORTANTE: en el código anterior la punta siempre viajaba hasta
        // distanciaMaxima, aunque el cursor estuviera sobre una superficie
        // cercana. Así podía alcanzar una pared situada más lejos del objetivo.
        float alcanceVuelo = limitarAlPuntoMouse
            ? Mathf.Min(distanciaAlCursor, distanciaMaxima)
            : distanciaMaxima;

        // El raycast no detecta de forma fiable una superficie que contiene
        // su propio origen. Si el origen queda dentro de geometría sólida,
        // no permitimos que el gancho atraviese esa superficie para agarrar otra.
        if (HayObstaculoEnOrigen(origen))
        {
            lanzando = false;
            lanzamientoCoroutine = null;
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

            Vector2 siguientePosicion =
                posicionGanchoVisual + direccion * avance;

            Vector2 tramo = siguientePosicion - posicionGanchoVisual;
            float longitudTramo = tramo.magnitude;

            if (longitudTramo > 0f)
            {
                RaycastHit2D hit = BuscarSuperficie(
                    posicionGanchoVisual,
                    tramo.normalized,
                    longitudTramo
                );

                if (hit.collider != null)
                {
                    posicionGanchoVisual = hit.point;
                    MostrarGanchoVisual(posicionGanchoVisual);

                    if (EsEnganchable(hit.collider))
                    {
                        puntoGancho = hit.point;
                        ConfigurarJoint(origen);

                        lanzando = false;
                        lanzamientoCoroutine = null;
                        yield break;
                    }

                    // Superficie no enganchable: la punta vuelve al brazo.
                    yield return StartCoroutine(RecogerGancho());

                    lanzando = false;
                    lanzamientoCoroutine = null;
                    yield break;
                }
            }

            posicionGanchoVisual = siguientePosicion;
            distanciaRecorrida += avance;

            MostrarGanchoVisual(posicionGanchoVisual);
            yield return null;
        }

        // Si no encontró nada hasta el máximo alcance, también vuelve al brazo.
        if (devolverAlLlegarAlMaximo)
            yield return StartCoroutine(RecogerGancho());
        else
            OcultarGanchoVisual();

        lanzando = false;
        lanzamientoCoroutine = null;
    }

    private RaycastHit2D BuscarSuperficie(
        Vector2 origen,
        Vector2 direccion,
        float distancia
    )
    {
        // Detectamos TODOS los colliders 2D sólidos de la trayectoria, no solo
        // los de capasGancho/capasImpacto. La superficie más cercana siempre
        // bloquea el gancho; luego EsEnganchable decide si puede adherirse.
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origen,
            direccion,
            distancia
        );

        RaycastHit2D mejorHit = default;
        float menorDistancia = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            if (!HitValido(hit))
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
        // Protección contra raycasts que empiezan dentro de un collider.
        // El brazo ya intenta quedarse antes de la superficie, pero esto
        // evita un enganche a través de paredes si la geometría llega a solaparse.
        Collider2D[] overlaps = Physics2D.OverlapPointAll(origen);

        foreach (Collider2D collider in overlaps)
        {
            if (ColliderValido(collider))
                return true;
        }

        return false;
    }

    private bool HitValido(RaycastHit2D hit)
    {
        return ColliderValido(hit.collider);
    }

    private bool ColliderValido(Collider2D collider)
    {
        if (collider == null)
            return false;

        if (ignorarTriggers && collider.isTrigger)
            return false;

        if (collider.attachedRigidbody == rb)
            return false;

        if (collider.transform == transform ||
            collider.transform.IsChildOf(transform))
            return false;

        // La punta visual y su punto de unión no son superficies del escenario.
        if (puntaGancho != null &&
            (collider.transform == puntaGancho ||
             collider.transform.IsChildOf(puntaGancho)))
            return false;

        if (puntoUnionCuerda != null &&
            (collider.transform == puntoUnionCuerda ||
             collider.transform.IsChildOf(puntoUnionCuerda)))
            return false;

        return true;
    }

    private bool EsColliderDeTilemap(Collider2D collider)
    {
        if (collider.GetComponent<TilemapCollider2D>() != null)
            return true;

        if (collider.GetComponentInParent<TilemapCollider2D>() != null)
            return true;

        if (collider.GetComponent<CompositeCollider2D>() != null)
            return true;

        if (collider.GetComponentInParent<CompositeCollider2D>() != null)
            return true;

        return false;
    }

    private bool EsEnganchable(Collider2D collider)
    {
        if (collider == null)
            return false;

        if (CapaIncluida(capasGancho, collider.gameObject.layer))
            return true;

        bool esTilemap =
            collider.GetComponent<TilemapCollider2D>() != null ||
            collider.GetComponentInParent<TilemapCollider2D>() != null ||
            collider.GetComponent<CompositeCollider2D>() != null ||
            collider.GetComponentInParent<CompositeCollider2D>() != null;

        if (!esTilemap)
            return false;

        // En algunos Tilemap, el collider está en un hijo y la Layer
        // se asignó al Tilemap o al Grid padre. Comprobamos toda esa jerarquía.
        for (Transform actual = collider.transform; actual != null; actual = actual.parent)
        {
            if (CapaIncluida(capasGancho, actual.gameObject.layer))
                return true;
        }

        return false;
    }

    private bool CapaIncluida(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private IEnumerator RecogerGancho()
    {
        recogiendo = true;

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
        recogiendo = false;
        OcultarGanchoVisual();
    }

    private void ConfigurarJoint(Vector2 origen)
    {
        enganchado = true;

        joint.connectedBody = null;
        joint.connectedAnchor = puntoGancho;
        joint.anchor = rb.transform.InverseTransformPoint(origen);

        float distanciaReal = Vector2.Distance(origen, puntoGancho);
        longitudObjetivo = Mathf.Clamp(
            distanciaReal,
            distanciaMinima,
            distanciaMaxima
        );

        joint.distance = longitudObjetivo;
        joint.maxDistanceOnly = soloDistanciaMaxima;

        // Mantener esta opción activada para conservar las colisiones del jugador.
        joint.enableCollision = true;
        joint.enabled = true;
    }

    private void SoltarGancho()
    {
        enganchado = false;
        entradaRecogido = 0f;

        if (joint != null)
            joint.enabled = false;

        OcultarGanchoVisual();
    }

    private void MostrarGanchoVisual(Vector2 posicion)
    {
        posicionGanchoVisual = posicion;

        if (puntaGancho != null)
        {
            puntaGancho.gameObject.SetActive(true);

            // Si el punto de unión está desplazado respecto al pivote del sprite,
            // movemos el objeto padre para que el punto hijo coincida EXACTAMENTE
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

        Vector2 origen = ObtenerOrigen();

        linea.enabled = true;
        linea.positionCount = 2;
        linea.SetPosition(0, origen);

        // Terminar exactamente en el punto de unión, no necesariamente en el pivote
        // central del sprite. Esta es la corrección principal del espacio visual.
        if (puntaGancho != null && puntaGancho.gameObject.activeSelf)
        {
            Vector3 extremo = puntoUnionCuerda != null
                ? puntoUnionCuerda.position
                : puntaGancho.position;

            linea.SetPosition(1, extremo);
        }
        else
        {
            linea.SetPosition(1, enganchado ? puntoGancho : posicionGanchoVisual);
        }
    }
}

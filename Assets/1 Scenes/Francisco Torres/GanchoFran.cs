using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class GanchoFran : MonoBehaviour
{
    [Header("Configuración del gancho")]
    public float distanciaMaxima = 10f;
    public float distanciaMinima = 1f;
    public float velocidadRecogida = 5f;
    public float velocidadGancho = 20f;

    [Header("Detección de superficies")]
    [Tooltip("Capas donde el gancho SÍ puede quedarse enganchado.")]
    public LayerMask capasGancho;

    [Tooltip("Capas que detienen al gancho, pero NO permiten engancharse.")]
    public LayerMask capasImpacto;

    [Tooltip("Si está activo, una capa configurada como 'Capas Gancho' también se usa para detectar impacto.")]
    public bool usarCapasGanchoComoImpacto = true;

    [Tooltip("Incluye TilemapCollider2D y CompositeCollider2D encontrados en el mapa.")]
    public bool detectarTilemaps = true;

    [Tooltip("No considera triggers como superficies que detienen el gancho.")]
    public bool ignorarTriggers = true;

    [Tooltip("Si el gancho no encuentra ninguna superficie, vuelve al brazo al alcanzar la distancia máxima.")]
    public bool devolverAlLlegarAlMaximo = true;

    [Header("Velocidades visuales")]
    [Tooltip("Velocidad con la que el gancho vuelve al brazo cuando no puede engancharse.")]
    public float velocidadRetornoGancho = 25f;

    public LineRenderer linea;

    [Header("Punta visual del gancho")]
    [Tooltip("Hijo visual que viaja desde el brazo hasta la superficie.")]
    public Transform puntaGancho;

    [Header("Referencias")]
    public FranBrazoApuntar brazo;

    [Header("Balanceo")]
    public float fuerzaBalanceo = 5f;

    [Header("Joint")]
    [Tooltip("True = cuerda: solo limita la distancia máxima. False = mantiene una distancia fija.")]
    public bool soloDistanciaMaxima = true;

    private Rigidbody2D rb;
    private DistanceJoint2D joint;
    private Camera cam;

    private Vector2 puntoGancho;
    private Vector2 posicionGanchoVisual;

    private bool enganchado;
    private bool lanzando;
    private bool recogiendo;

    private Coroutine lanzamientoCoroutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (linea == null)
            linea = GetComponent<LineRenderer>();

        if (linea != null)
        {
            linea.positionCount = 2;
            linea.enabled = false;
        }

        if (puntaGancho != null)
            puntaGancho.gameObject.SetActive(false);

        joint = gameObject.AddComponent<DistanceJoint2D>();
        joint.autoConfigureDistance = false;

        // Importante: el Player conserva sus colisiones con el escenario.
        joint.enableCollision = true;

        joint.maxDistanceOnly = soloDistanciaMaxima;
        joint.enabled = false;
    }

    private void Update()
    {
        if (cam == null || brazo == null)
            return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        if (Input.GetMouseButtonDown(0) &&
            !enganchado &&
            !lanzando &&
            !recogiendo)
        {
            lanzamientoCoroutine = StartCoroutine(LanzarGancho(mouseWorld));
        }

        if (Input.GetMouseButtonUp(0))
        {
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

        if (enganchado)
        {
            float ajuste =
                Input.GetAxis("Vertical") *
                velocidadRecogida *
                Time.deltaTime;

            if (Mathf.Abs(ajuste) > 0.01f)
            {
                joint.distance = Mathf.Clamp(
                    joint.distance - ajuste,
                    distanciaMinima,
                    distanciaMaxima
                );
            }
        }
    }

    private void FixedUpdate()
    {
        if (!enganchado)
            return;

        float movHorizontal = Input.GetAxis("Horizontal");

        if (Mathf.Abs(movHorizontal) > 0.1f)
        {
            rb.AddForce(
                new Vector2(
                    movHorizontal * fuerzaBalanceo,
                    0f
                ),
                ForceMode2D.Force
            );
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
        Vector2 direccion = destino - origen;

        if (direccion.sqrMagnitude < 0.0001f)
        {
            lanzando = false;
            yield break;
        }

        direccion.Normalize();

        posicionGanchoVisual = origen;
        float distanciaRecorrida = 0f;

        MostrarGanchoVisual(posicionGanchoVisual);

        while (distanciaRecorrida < distanciaMaxima)
        {
            float avance = velocidadGancho * Time.deltaTime;
            float distanciaRestante =
                distanciaMaxima - distanciaRecorrida;

            avance = Mathf.Min(avance, distanciaRestante);

            Vector2 siguientePosicion =
                posicionGanchoVisual + direccion * avance;

            Vector2 tramo =
                siguientePosicion - posicionGanchoVisual;

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

                    // Golpeó una superficie, pero no se puede agarrar.
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

        // Llegó al alcance máximo sin engancharse a nada.
        if (devolverAlLlegarAlMaximo)
        {
            yield return StartCoroutine(RecogerGancho());
        }
        else
        {
            OcultarGanchoVisual();
        }

        lanzando = false;
        lanzamientoCoroutine = null;
    }

    private RaycastHit2D BuscarSuperficie(
        Vector2 origen,
        Vector2 direccion,
        float distancia
    )
    {
        // La capa del gancho también se considera superficie de impacto.
        int mascaraImpacto = capasImpacto.value;

        if (usarCapasGanchoComoImpacto)
            mascaraImpacto |= capasGancho.value;

        // Si no hay ninguna capa configurada, no hacemos un Raycast vacío.
        if (mascaraImpacto == 0)
            return default;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origen,
            direccion,
            distancia,
            mascaraImpacto
        );

        RaycastHit2D mejorHit = default;
        float menorDistancia = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (ignorarTriggers && hit.collider.isTrigger)
                continue;

            // Evita que el gancho detecte el propio Player.
            if (hit.collider.attachedRigidbody == rb)
                continue;

            if (hit.collider.transform == transform ||
                hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                mejorHit = hit;
            }
        }

        // Fallback específico para TilemapCollider2D:
        // permite detectarlo aunque la configuración del Tilemap
        // no coincida con la de las capas de impacto.
        if (detectarTilemaps)
        {
            RaycastHit2D[] todosLosHits = Physics2D.RaycastAll(
                origen,
                direccion,
                distancia
            );

            foreach (RaycastHit2D hit in todosLosHits)
            {
                if (hit.collider == null)
                    continue;

                if (ignorarTriggers && hit.collider.isTrigger)
                    continue;

                if (hit.collider.attachedRigidbody == rb)
                    continue;

                if (!EsColliderDeTilemap(hit.collider))
                    continue;

                if (hit.distance < menorDistancia)
                {
                    menorDistancia = hit.distance;
                    mejorHit = hit;
                }
            }
        }

        return mejorHit;
    }

    private bool EsColliderDeTilemap(Collider2D collider)
    {
        if (collider.GetComponent<TilemapCollider2D>() != null)
            return true;

        if (collider.GetComponentInParent<TilemapCollider2D>() != null)
            return true;

        // Compatible con TilemapCollider2D + CompositeCollider2D.
        if (collider.GetComponent<CompositeCollider2D>() != null)
        {
            if (collider.GetComponent<TilemapCollider2D>() != null)
                return true;

            if (collider.GetComponentInParent<TilemapCollider2D>() != null)
                return true;
        }

        return false;
    }

    private bool EsEnganchable(Collider2D collider)
    {
        // Caso normal: collider directamente en una capa enganchable.
        if (CapaIncluida(capasGancho, collider.gameObject.layer))
            return true;

        // Si es un TilemapCollider2D, comprobamos también
        // la capa del GameObject que contiene el Tilemap.
        TilemapCollider2D tilemapCollider =
            collider.GetComponent<TilemapCollider2D>();

        if (tilemapCollider == null)
        {
            tilemapCollider =
                collider.GetComponentInParent<TilemapCollider2D>();
        }

        if (tilemapCollider != null &&
            CapaIncluida(
                capasGancho,
                tilemapCollider.gameObject.layer
            ))
        {
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

            if (Vector2.Distance(
                    posicionGanchoVisual,
                    origenActual
                ) <= 0.01f)
            {
                break;
            }

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

        joint.anchor =
            rb.transform.InverseTransformPoint(origen);

        float distReal =
            Vector2.Distance(origen, puntoGancho);

        joint.distance = Mathf.Clamp(
            distReal,
            distanciaMinima,
            distanciaMaxima
        );

        joint.maxDistanceOnly = soloDistanciaMaxima;

        // MUY IMPORTANTE para que el Player conserve las colisiones.
        joint.enableCollision = true;

        joint.enabled = true;
    }

    private void SoltarGancho()
    {
        enganchado = false;

        if (joint != null)
            joint.enabled = false;

        OcultarGanchoVisual();
    }

    private void MostrarGanchoVisual(Vector2 posicion)
    {
        if (puntaGancho != null)
        {
            puntaGancho.gameObject.SetActive(true);

            puntaGancho.position = new Vector3(
                posicion.x,
                posicion.y,
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

        // Origen de la cuerda: punta del brazo.
        linea.SetPosition(0, origen);

        // Extremo de la cuerda: posición REAL de PuntaGancho.
        // De esta forma la línea siempre termina exactamente
        // donde se encuentra visualmente la punta.
        if (puntaGancho != null && puntaGancho.gameObject.activeSelf)
        {
            linea.SetPosition(1, puntaGancho.position);
        }
        else
        {
            Vector2 extremo =
                enganchado
                    ? puntoGancho
                    : posicionGanchoVisual;

            linea.SetPosition(1, extremo);
        }
    }
}

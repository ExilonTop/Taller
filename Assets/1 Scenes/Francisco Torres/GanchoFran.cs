using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GanchoFran : MonoBehaviour
{
    [Header("Configuración del gancho")]
    public float distanciaMaxima = 10f;
    public float distanciaMinima = 1f;
    public float velocidadRecogida = 5f;
    public float velocidadGancho = 20f;
    public LayerMask capasGancho;
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
    private Coroutine lanzamientoCoroutine;

    void Awake()
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

        // IMPORTANTE: mantiene las colisiones del jugador.
        joint.enableCollision = true;

        joint.maxDistanceOnly = soloDistanciaMaxima;
        joint.enabled = false;
    }

    void Update()
    {
        if (cam == null || brazo == null)
            return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        if (Input.GetMouseButtonDown(0) && !enganchado && !lanzando)
            lanzamientoCoroutine = StartCoroutine(LanzarGancho(mouseWorld));

        if (Input.GetMouseButtonUp(0))
        {
            if (lanzando)
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
            float ajuste = Input.GetAxis("Vertical") * velocidadRecogida * Time.deltaTime;

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

    void FixedUpdate()
    {
        if (!enganchado)
            return;

        float movHorizontal = Input.GetAxis("Horizontal");

        if (Mathf.Abs(movHorizontal) > 0.1f)
        {
            rb.AddForce(
                new Vector2(movHorizontal * fuerzaBalanceo, 0f),
                ForceMode2D.Force
            );
        }
    }

    void LateUpdate()
    {
        if (lanzando || enganchado)
            ActualizarLinea();
    }

    Vector2 ObtenerOrigen()
    {
        if (brazo != null)
            return brazo.ObtenerPuntoLanzamiento();

        return rb.position;
    }

    IEnumerator LanzarGancho(Vector2 destino)
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
            float distanciaRestante = distanciaMaxima - distanciaRecorrida;

            if (avance > distanciaRestante)
                avance = distanciaRestante;

            Vector2 siguientePosicion = posicionGanchoVisual + direccion * avance;
            Vector2 tramo = siguientePosicion - posicionGanchoVisual;
            float longitudTramo = tramo.magnitude;

            if (longitudTramo > 0f)
            {
                RaycastHit2D hit = Physics2D.Raycast(
                    posicionGanchoVisual,
                    tramo.normalized,
                    longitudTramo,
                    capasGancho
                );

                if (hit.collider != null)
                {
                    puntoGancho = hit.point;
                    posicionGanchoVisual = puntoGancho;

                    MostrarGanchoVisual(posicionGanchoVisual);
                    ConfigurarJoint(origen);

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

        lanzando = false;
        lanzamientoCoroutine = null;
        OcultarGanchoVisual();
    }

    void ConfigurarJoint(Vector2 origen)
    {
        enganchado = true;

        joint.connectedBody = null;
        joint.connectedAnchor = puntoGancho;
        joint.anchor = rb.transform.InverseTransformPoint(origen);

        float distReal = Vector2.Distance(origen, puntoGancho);

        joint.distance = Mathf.Clamp(
            distReal,
            distanciaMinima,
            distanciaMaxima
        );

        joint.maxDistanceOnly = soloDistanciaMaxima;
        joint.enableCollision = true;
        joint.enabled = true;
    }

    void SoltarGancho()
    {
        enganchado = false;

        if (joint != null)
            joint.enabled = false;

        OcultarGanchoVisual();
    }

    void MostrarGanchoVisual(Vector2 posicion)
    {
        if (puntaGancho != null)
        {
            puntaGancho.gameObject.SetActive(true);
            puntaGancho.position = new Vector3(posicion.x, posicion.y, puntaGancho.position.z);
        }

        ActualizarLinea();
    }

    void OcultarGanchoVisual()
    {
        if (puntaGancho != null)
            puntaGancho.gameObject.SetActive(false);

        if (linea != null)
            linea.enabled = false;
    }

    void ActualizarLinea()
    {
        if (linea == null)
            return;

        Vector2 origen = ObtenerOrigen();

        linea.enabled = true;
        linea.positionCount = 2;
        linea.SetPosition(0, origen);

        Vector2 extremo = enganchado ? puntoGancho : posicionGanchoVisual;
        linea.SetPosition(1, extremo);
    }
}

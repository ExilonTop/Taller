using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Gancho : MonoBehaviour
{
    [Header("Configuración del gancho")]
    public float distanciaMaxima = 10f;         // Alcance máximo del gancho
    public float velocidadRecogida = 5f;        // Velocidad al recoger/soltar cuerda
    public LayerMask capasGancho;               // Capas que se pueden enganchar
    public Transform puntoLanzamiento;          // Punto desde donde sale la cuerda
    public LineRenderer linea;                  // Referencia al LineRenderer

    private Rigidbody2D rb;
    private DistanceJoint2D joint;
    private Vector2 puntoGancho;
    private bool enganchado;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (linea == null)
            linea = GetComponent<LineRenderer>();

        if (linea != null)
            linea.enabled = false;
    }

    void Update()
    {
        // Obtener posición del mouse en el mundo
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // Lanzar gancho con clic izquierdo
        if (Input.GetMouseButtonDown(0))
        {
            LanzarGancho(mouseWorld);
        }
        // Soltar gancho al soltar el clic
        else if (Input.GetMouseButtonUp(0))
        {
            SoltarGancho();
        }

        // Si está enganchado, permitir recoger o soltar cuerda
        if (enganchado && joint != null)
        {
            // W (positivo) acorta la cuerda -> sube
            // S (negativo) alarga la cuerda -> baja
            float ajuste = Input.GetAxis("Vertical") * velocidadRecogida * Time.deltaTime;
            if (Mathf.Abs(ajuste) > 0.01f)
            {
                joint.distance = Mathf.Clamp(joint.distance - ajuste, 0.5f, distanciaMaxima);
            }

            ActualizarLinea();
        }
    }

    void LanzarGancho(Vector2 destino)
    {
        Vector2 origen = puntoLanzamiento != null ? puntoLanzamiento.position : transform.position;
        Vector2 direccion = destino - origen;
        float distancia = direccion.magnitude;

        if (distancia < 0.1f)
            return;

        // Limitar el raycast a la distancia máxima
        float distanciaRaycast = Mathf.Min(distancia, distanciaMaxima);
        RaycastHit2D hit = Physics2D.Raycast(origen, direccion.normalized, distanciaRaycast, capasGancho);

        if (hit.collider != null)
        {
            puntoGancho = hit.point;
            enganchado = true;

            if (linea != null)
            {
                linea.enabled = true;
                ActualizarLinea();
            }

            // Crear o configurar el DistanceJoint2D para simular la cuerda
            if (joint == null)
            {
                joint = gameObject.AddComponent<DistanceJoint2D>();
                joint.autoConfigureDistance = false;
                joint.enableCollision = true;
                joint.maxDistanceOnly = true; // Permite que la cuerda tenga holgura
            }

            // Conectar el joint al punto fijo del mundo
            joint.connectedBody = null;
            joint.connectedAnchor = puntoGancho;
            joint.distance = Mathf.Clamp(hit.distance, 0.5f, distanciaMaxima);
        }
    }

    void SoltarGancho()
    {
        enganchado = false;

        if (linea != null)
            linea.enabled = false;

        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }
    }

    void ActualizarLinea()
    {
        if (linea == null)
            return;

        Vector3 origen = puntoLanzamiento != null ? puntoLanzamiento.position : transform.position;
        linea.positionCount = 2;
        linea.SetPosition(0, origen);
        linea.SetPosition(1, puntoGancho);
    }

    void OnDestroy()
    {
        // Limpiar el joint si el personaje se destruye
        if (joint != null)
            Destroy(joint);
    }
}
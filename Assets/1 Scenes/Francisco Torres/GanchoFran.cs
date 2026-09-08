using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GanchoFran : MonoBehaviour
{
    [Header("Configuración del gancho")]
    public float distanciaMaxima = 10f;         
    public float velocidadRecogida = 5f;        
    public LayerMask capasGancho;               
    public Transform puntoLanzamiento;          
    public LineRenderer linea;                  

    public float fuerzaBalanceo = 5f;
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
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        if (Input.GetMouseButtonDown(0))
        {
            LanzarGancho(mouseWorld);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            SoltarGancho();
        }

        if (enganchado && joint != null)
        {
            float ajuste = Input.GetAxis("Vertical") * velocidadRecogida * Time.deltaTime;
            if (Mathf.Abs(ajuste) > 0.01f)
            {
                joint.distance = Mathf.Clamp(joint.distance - ajuste, 0.5f, distanciaMaxima);
            }

            float movHorizontal = Input.GetAxis("Horizontal");
            if (Mathf.Abs(movHorizontal) > 0.1f)
            {
                rb.AddForce(new Vector2(movHorizontal * fuerzaBalanceo, 0f), ForceMode2D.Force);
            }
        }
    }

    // AÑADIMOS LATEUPDATE PARA LA LÍNEA
    // LateUpdate se ejecuta después de que las físicas (Update) mueven al personaje,
    // asegurando que la línea se dibuje exactamente donde está la mano.
    void LateUpdate()
    {
        if (enganchado)
        {
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

            if (joint == null)
            {
                joint = gameObject.AddComponent<DistanceJoint2D>();
                joint.autoConfigureDistance = false;
                joint.enableCollision = true;
                joint.maxDistanceOnly = false; 
            }

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
        if (joint != null)
            Destroy(joint);
    }
}
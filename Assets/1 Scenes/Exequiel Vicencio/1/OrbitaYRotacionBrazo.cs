using UnityEngine;

public class ControlBrazoYDisparo : MonoBehaviour
{
    [Header("Referencias")]
    public Rigidbody2D rbJugador;    // Arrastra el Rigidbody2D del Player
    public GameObject prefabGancho;  
    public Transform puntoDisparo;   
    public LineRenderer lineRenderer; // Añade un componente LineRenderer al brazo y arrástralo aquí
    public MonoBehaviour scriptMovimiento;

    [Header("Configuración")]
    public float radioOrbita = 1.5f; 
    public float velocidadGancho = 25f;

    private SpringJoint2D cuerdaFisica;
    private Vector3 puntoEnganche;
    private bool estaEnganchado = false;

    void Update()
    {
        // --- SISTEMA DE ÓRBITA Y ROTACIÓN ---
        Vector3 posicionRaton = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        posicionRaton.z = 0f; 

        Vector3 direccionGlobal = (posicionRaton - rbJugador.transform.position).normalized;
        transform.position = rbJugador.transform.position + (direccionGlobal * radioOrbita);

        float angulo = Mathf.Atan2(direccionGlobal.y, direccionGlobal.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angulo);

        // --- SISTEMA DE ENTRADA (CLIC) ---
        if (Input.GetMouseButtonDown(0)) 
        {
            if (estaEnganchado)
                SoltarGancho();
            else
                DispararGancho();
        }

        // --- DIBUJAR LA CUERDA VISUAL ---
        if (estaEnganchado)
        {
            lineRenderer.SetPosition(0, puntoDisparo.position);
            lineRenderer.SetPosition(1, puntoEnganche);
        }
    }

    void DispararGancho()
    {
        GameObject nuevoGancho = Instantiate(prefabGancho, puntoDisparo.position, puntoDisparo.rotation);
        nuevoGancho.GetComponent<GanchoProyectil>().Inicializar(this);
        
        Rigidbody2D rbGancho = nuevoGancho.GetComponent<Rigidbody2D>();
        if (rbGancho != null)
        {
            rbGancho.linearVelocity = puntoDisparo.right * velocidadGancho;
        }
    }

    // Este método es llamado por el gancho cuando choca con una Pared
    public void CrearCuerda(Vector2 puntoImpacto)
    {
        puntoEnganche = puntoImpacto;
        estaEnganchado = true;

        // 1. Crear el componente físico en el jugador de forma dinámica
        cuerdaFisica = rbJugador.gameObject.AddComponent<SpringJoint2D>();
        cuerdaFisica.autoConfigureDistance = false;
        
        // Configurar el punto de anclaje en el mundo
        cuerdaFisica.connectedAnchor = puntoImpacto;
        
        // Distancia de la cuerda (la distancia actual entre el jugador y el impacto)
        cuerdaFisica.distance = Vector2.Distance(rbJugador.transform.position, puntoImpacto);

        // Ajustes para que se sienta elástico estilo Spider-Man
        cuerdaFisica.frequency = 0f;  // Elasticidad (más alto = más rígido)
        cuerdaFisica.dampingRatio = 0.5f; // Amortiguación del rebote

        if (scriptMovimiento != null) scriptMovimiento.enabled = false;

        // Activar el renderizador de la línea
        lineRenderer.enabled = true;
    }

    void SoltarGancho()
    {
        estaEnganchado = false;
        lineRenderer.enabled = false;
        
        // Destruir el componente físico para liberar al jugador
        if (cuerdaFisica != null)
        {
            Destroy(cuerdaFisica);
        }
        if (scriptMovimiento != null) scriptMovimiento.enabled = true;
    }
}

using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    [Header("Configuración del Gancho")]
    public float hookSpeed = 20f;
    public float maxDistance = 15f;
    public LayerMask grappleLayer;

    [Header("Efecto de Cuerda")]
    public LineRenderer lineRenderer;

    private Vector2 targetPosition;
    private Vector2 hookPosition;
    private bool isGrappling = false;
    private bool isRetracting = false;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.enabled = false;
    }

    void Update()
    {
        // 1. Detectar clic para disparar
        if (Input.GetMouseButtonDown(0) && !isGrappling)
        {
            StartGrapple();
        }

        // 2. Soltar el gancho con Espacio o Segundo Clic (Estilo Terraria)
        if (isGrappling && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            StopGrapple();
        }

        // 3. Dibujar la línea si el gancho está activo
        if (lineRenderer.enabled)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, hookPosition);
        }
    }

    void FixedUpdate()
    {
        if (isGrappling)
        {
            if (isRetracting)
            {
                // Mover la punta del gancho hacia el objetivo
                hookPosition = Vector2.MoveTowards(hookPosition, targetPosition, hookSpeed * Time.fixedDeltaTime);

                // Si llega al objetivo y no golpeó nada, regresa
                if (Vector2.Distance(hookPosition, targetPosition) < 0.1f)
                {
                    StopGrapple();
                }
            }
            else
            {
                // El gancho ya impactó. Jalamos al jugador hacia la pared (Física de Terraria)
                Vector2 grappleDirection = (hookPosition - (Vector2)transform.position).normalized;
                
                // Mantenemos una velocidad constante de tracción anulando la gravedad temporalmente
                rb.linearVelocity = grappleDirection * hookSpeed; 

                // Si el jugador llega muy cerca del punto de impacto, se queda estático ahí
                if (Vector2.Distance(transform.position, hookPosition) < 0.5f)
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }
    }

    void StartGrapple()
    {
        // Calcular dirección hacia el mouse
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mousePos - transform.position).normalized;

        // Lanzar Raycast para ver si golpea una superficie válida
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, maxDistance, grappleLayer);

        isGrappling = true;
        lineRenderer.enabled = true;
        hookPosition = transform.position; // El gancho sale desde el jugador

        if (hit.collider != null)
        {
            // Encontró pared: el gancho viajará hasta este punto estático
            targetPosition = hit.point;
            isRetracting = false; 
            
            // Re-calcular la posición para la animación de viaje del gancho
            StartCoroutine(AnimateHookTravel(hit.point));
        }
        else
        {
            // No encontró nada: viaja hasta la distancia máxima y falla
            targetPosition = (Vector2)transform.position + (direction * maxDistance);
            isRetracting = true; 
        }
    }

    System.Collections.IEnumerator AnimateHookTravel(Vector2 hitPoint)
    {
        isRetracting = true; // Usamos esto para simular el viaje de ida
        while (Vector2.Distance(hookPosition, hitPoint) > 0.2f && isGrappling)
        {
            hookPosition = Vector2.MoveTowards(hookPosition, hitPoint, hookSpeed * Time.deltaTime);
            yield return null;
        }
        if (isGrappling)
        {
            hookPosition = hitPoint;
            isRetracting = false; // Ya impactó, empieza la tracción del jugador
        }
    }

    void StopGrapple()
    {
        isGrappling = false;
        isRetracting = false;
        lineRenderer.enabled = false;
    }
}

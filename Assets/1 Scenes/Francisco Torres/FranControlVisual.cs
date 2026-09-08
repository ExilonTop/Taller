using UnityEngine;

public class FranControlVisual : MonoBehaviour
{
    [Header("Pivotes de Rotación")]
    [Tooltip("Arrastra aquí los Empty GameObjects que usas como bisagras")]
    public Transform pivoteCabeza;
    public Transform pivoteBrazo;

    [Header("Sprites para Voltear")]
    [Tooltip("Arrastra aquí los Sprite Renderers de cada parte")]
    public SpriteRenderer spriteCabeza;
    public SpriteRenderer spriteBrazo;
    public SpriteRenderer spriteCuerpo; 

    void Update()
    {
        // 1. Obtener la posición del mouse
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // 2. Calcular las direcciones desde los pivotes
        Vector3 dirCabeza = mouseWorld - pivoteCabeza.position;
        Vector3 dirBrazo = mouseWorld - pivoteBrazo.position;

        // 3. Convertir direcciones a grados
        float anguloCabeza = Mathf.Atan2(dirCabeza.y, dirCabeza.x) * Mathf.Rad2Deg;
        float anguloBrazo = Mathf.Atan2(dirBrazo.y, dirBrazo.x) * Mathf.Rad2Deg;

        // 4. Aplicar rotación a los pivotes
        pivoteCabeza.rotation = Quaternion.Euler(0, 0, anguloCabeza);
        pivoteBrazo.rotation = Quaternion.Euler(0, 0, anguloBrazo);

        // 5. LÓGICA DE SENTIDO (Voltear sprites si se apunta a la izquierda)
        // Comparamos si el mouse está a la izquierda de la posición central del jugador
        bool mirandoIzquierda = mouseWorld.x < transform.position.x;

        // Al voltear en Y los sprites rotados, evitamos que queden boca abajo
        spriteCabeza.flipY = mirandoIzquierda;
        spriteBrazo.flipY = mirandoIzquierda;
        
        // El cuerpo no rota, por lo que lo volteamos en X para que la espalda cambie de lado
        if (spriteCuerpo != null) 
        {
            spriteCuerpo.flipX = mirandoIzquierda;
        }
    }
}
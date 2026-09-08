using UnityEngine;

public class FranBrazoApuntar : MonoBehaviour
{
    [Header("Configuración del Brazo")]
    [Tooltip("El objeto que rotará (el brazo o la mano)")]
    public Transform brazo;

    void Update()
    {
        // 1. Obtener la posición del mouse en el mundo
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // 2. Calcular la dirección desde el brazo hacia el mouse
        Vector3 direccion = mouseWorld - brazo.position;

        // 3. Calcular el ángulo usando trigonometría (Atan2) y convertirlo a grados
        float angulo = Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg;

        // 4. Aplicar la rotación al eje Z del brazo
        brazo.rotation = Quaternion.Euler(new Vector3(0, 0, angulo));
    }
}
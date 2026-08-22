using UnityEngine;

public class GanchoProyectil : MonoBehaviour
{
    private ControlBrazoYDisparo scriptBrazo;

    public void Inicializar(ControlBrazoYDisparo brazo)
    {
        scriptBrazo = brazo;
    }

    void OnTriggerEnter2D(Collider2D otro)
    {
        // Asegúrate de que las superficies escalables tengan la etiqueta "Pared"
        if (otro.CompareTag("Pared"))
        {
            // Detener el proyectil
            GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            
            // Avisar al brazo que impactamos con éxito enviando el punto de choque
            scriptBrazo.CrearCuerda(transform.position);
            
            // Destruir la punta del gancho voladora ya que la cuerda la reemplaza
            Destroy(gameObject);
        }
        else if (otro.CompareTag("Suelo"))
        {
            // Si choca con el suelo, simplemente destruye el gancho
            Destroy(gameObject);
        }
    }
}

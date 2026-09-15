using UnityEngine;
using Cinemachine;

public class TransicionCamaraFran : MonoBehaviour
{
    [Header("Cámara destino")]
    public CinemachineVirtualCamera camaraDestino;

    [Header("Administrador")]
    public AdministradorCamarasFran administrador;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        CambiarCamara();
    }

    private void CambiarCamara()
    {
        if (camaraDestino == null)
        {
            Debug.LogError(
                "[TransicionCamaraFran] No se asignó cámara destino en "
                + gameObject.name
            );

            return;
        }

        if (administrador == null)
        {
            Debug.LogError(
                "[TransicionCamaraFran] No se asignó AdministradorCamarasFran en "
                + gameObject.name
            );

            return;
        }

        int indice = BuscarCamara();

        if (indice == -1)
        {
            Debug.LogError(
                "[TransicionCamaraFran] La cámara "
                + camaraDestino.name
                + " no está registrada en el administrador."
            );

            return;
        }

        administrador.ActivarCamara(indice);
    }

    private int BuscarCamara()
    {
        for (int i = 0; i < administrador.camaras.Length; i++)
        {
            if (administrador.camaras[i] == camaraDestino)
                return i;
        }

        return -1;
    }
}
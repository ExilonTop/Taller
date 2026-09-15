using UnityEngine;
using Cinemachine;

public class AdministradorCamarasFran : MonoBehaviour
{
    [Header("Cámaras estáticas")]
    public CinemachineVirtualCamera[] camaras;

    [Header("Cámara inicial")]
    public int indiceCamaraInicial = 0;

    private int indiceCamaraActual = -1;

    private void Awake()
    {
        PrepararCamaras();
    }

    private void Start()
    {
        ActivarCamara(indiceCamaraInicial, true);
    }

    private void PrepararCamaras()
    {
        if (camaras == null || camaras.Length == 0)
        {
            Debug.LogError(
                "[AdministradorCamarasFran] No hay cámaras asignadas."
            );

            return;
        }

        for (int i = 0; i < camaras.Length; i++)
        {
            if (camaras[i] == null)
                continue;

            camaras[i].Priority = 0;
            camaras[i].enabled = false;
        }
    }

    public void ActivarCamara(int indice)
    {
        ActivarCamara(indice, false);
    }

    private void ActivarCamara(int indice, bool inicial)
    {
        if (camaras == null || camaras.Length == 0)
        {
            Debug.LogError(
                "[AdministradorCamarasFran] No hay cámaras."
            );

            return;
        }

        if (indice < 0 || indice >= camaras.Length)
        {
            Debug.LogError(
                "[AdministradorCamarasFran] Índice inválido: "
                + indice
            );

            return;
        }

        CinemachineVirtualCamera nuevaCamara = camaras[indice];

        if (nuevaCamara == null)
        {
            Debug.LogError(
                "[AdministradorCamarasFran] La cámara "
                + indice + " es null."
            );

            return;
        }

        // Si ya estamos usando esta cámara, no hacemos nada.
        if (indiceCamaraActual == indice && !inicial)
            return;

        // Apagamos absolutamente todas las cámaras.
        for (int i = 0; i < camaras.Length; i++)
        {
            if (camaras[i] == null)
                continue;

            camaras[i].Priority = 0;
            camaras[i].enabled = false;
        }

        // Activamos solamente la cámara nueva.
        nuevaCamara.enabled = true;
        nuevaCamara.Priority = 100;

        // Evita reutilizar el estado anterior.
        nuevaCamara.PreviousStateIsValid = false;

        indiceCamaraActual = indice;

        Debug.Log(
            "[AdministradorCamarasFran] Cámara activa: "
            + nuevaCamara.name
        );
    }
}
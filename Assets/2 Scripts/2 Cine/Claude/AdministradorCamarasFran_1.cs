using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

/// <summary>
/// Administra las cámaras estáticas de Cinemachine: solo una está activa a la vez.
/// Las zonas (TransicionCamaraFran) avisan cuando el jugador entra y sale. Manda la
/// última zona en la que entró y, al salir de ella, vuelve la cámara de la zona anterior
/// que siga ocupando. Si no queda ninguna, se mantiene la cámara actual.
///
/// Revisión: los cambios respecto de la versión anterior están marcados con [FIX n].
/// </summary>
public class AdministradorCamarasFran : MonoBehaviour
{
    [Header("Cámaras estáticas")]
    public CinemachineVirtualCamera[] camaras;

    [Header("Cámara inicial")]
    [Tooltip("Cámara al empezar, mientras el jugador no esté dentro de ninguna zona.")]
    public int indiceCamaraInicial = 0;

    private int indiceCamaraActual = -1;

    // [FIX 1] Zonas que el jugador ocupa en este momento, en orden de entrada (la última manda).
    private readonly List<TransicionCamaraFran> zonasOcupadas = new List<TransicionCamaraFran>();

    private void Awake()
    {
        PrepararCamaras();
    }

    private void Start()
    {
        // [FIX 5] Si una zona ya eligió cámara antes de Start, no se pisa con la inicial.
        if (indiceCamaraActual == -1)
            ActivarCamara(indiceCamaraInicial, true);
    }

    private void OnValidate()
    {
        // [FIX 5] Mantiene el índice inicial dentro del arreglo.
        if (camaras != null && camaras.Length > 0)
            indiceCamaraInicial = Mathf.Clamp(indiceCamaraInicial, 0, camaras.Length - 1);
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

    // ==================================================================
    // [FIX 1] Zonas: reemplaza el aviso en cada paso de física (OnTriggerStay2D)
    // ==================================================================
    public void EntrarZona(TransicionCamaraFran zona)
    {
        if (zona == null)
            return;

        // Si ya estaba en la lista, pasa al final: manda la última zona en la que se entró.
        zonasOcupadas.Remove(zona);
        zonasOcupadas.Add(zona);

        ActivarCamara(zona.camaraDestino);
    }

    public void SalirZona(TransicionCamaraFran zona)
    {
        if (!zonasOcupadas.Remove(zona))
            return;

        // Por seguridad, descarta zonas destruidas.
        for (int i = zonasOcupadas.Count - 1; i >= 0; i--)
        {
            if (zonasOcupadas[i] == null)
                zonasOcupadas.RemoveAt(i);
        }

        // Vuelve a la última zona que el jugador sigue ocupando.
        if (zonasOcupadas.Count > 0)
            ActivarCamara(zonasOcupadas[zonasOcupadas.Count - 1].camaraDestino);
    }

    // ==================================================================
    // Activación
    // ==================================================================

    // [FIX 4] Activa por referencia: la búsqueda del índice queda en un solo lugar
    // (antes cada zona recorría el arreglo en cada paso de física).
    public void ActivarCamara(CinemachineVirtualCamera camara)
    {
        if (camara == null)
        {
            Debug.LogError("[AdministradorCamarasFran] Se pidió activar una cámara null.");
            return;
        }

        int indice = BuscarIndice(camara);

        if (indice == -1)
        {
            Debug.LogError(
                "[AdministradorCamarasFran] La cámara "
                + camara.name
                + " no está registrada en el administrador."
            );

            return;
        }

        ActivarCamara(indice, false);
    }

    public void ActivarCamara(int indice)
    {
        ActivarCamara(indice, false);
    }

    public int BuscarIndice(CinemachineVirtualCamera camara)
    {
        if (camaras == null || camara == null)
            return -1;

        for (int i = 0; i < camaras.Length; i++)
        {
            if (camaras[i] == camara)
                return i;
        }

        return -1;
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

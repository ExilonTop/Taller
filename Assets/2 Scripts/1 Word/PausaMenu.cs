using UnityEngine;

/// <summary>
/// Abre/cierra un Canvas (por ejemplo, el menú de pausa) al presionar ESC.
/// Pensado para colocarse SOLO en las escenas de juego (no en menús principales),
/// ya que además pausa el tiempo del juego mientras el panel está abierto.
///
/// Uso:
///   1. Creá tu Canvas de pausa (ej. "PanelPausa") y dejalo desactivado por defecto.
///   2. Agregá este script a un GameObject de la escena de juego (ej. "GameManager" o "UIManager").
///   3. Arrastrá el Canvas/Panel al campo "Panel Pausa" en el Inspector.
/// </summary>
public class PausaMenu : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Canvas o Panel que se abre/cierra con ESC.")]
    [SerializeField] private GameObject panelPausa;

    [Header("Opciones")]
    [Tooltip("Si está activo, el juego se pausa (Time.timeScale = 0) mientras el panel está abierto.")]
    [SerializeField] private bool pausarTiempo = true;

    private bool juegoPausado = false;

    private void Start()
    {
        // Por las dudas, nos aseguramos de que el panel arranque cerrado.
        if (panelPausa != null)
            panelPausa.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePausa();
        }
    }

    /// <summary>
    /// Alterna el estado del panel de pausa (abierto/cerrado).
    /// También se puede llamar desde un botón (ej. botón "Reanudar" dentro del panel).
    /// </summary>
    public void TogglePausa()
    {
        if (juegoPausado)
            Reanudar();
        else
            Pausar();
    }

    public void Pausar()
    {
        if (panelPausa != null)
            panelPausa.SetActive(true);

        if (pausarTiempo)
            Time.timeScale = 0f;

        juegoPausado = true;
    }

    public void Reanudar()
    {
        if (panelPausa != null)
            panelPausa.SetActive(false);

        if (pausarTiempo)
            Time.timeScale = 1f;

        juegoPausado = false;
    }

    private void OnDisable()
    {
        // Seguridad extra: si este objeto se desactiva o se cambia de escena
        // mientras el juego está pausado, restauramos el timeScale para que
        // la siguiente escena no arranque congelada.
        if (juegoPausado && pausarTiempo)
            Time.timeScale = 1f;
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador central de escenas para juegos 2D.
/// Colócalo en un GameObject (por ejemplo "GameManager") y no lo destruyas entre escenas
/// si quieres que persista (ver Awake).
///
/// Funciones principales:
///   - LoadSceneByName(string)   -> cargar una escena eligiéndola de la lista (para botones con dropdown)
///   - LoadSceneByIndex(int)     -> cargar por índice de Build Settings
///   - RestartScene()            -> recargar la escena actual (reintentar)
///   - QuitGame()                -> salir del juego (funciona en build; en el editor detiene el Play)
/// </summary>
public class SceneManagerController : MonoBehaviour 
{
    public static SceneManagerController Instance { get; private set; }

    [Header("Lista de escenas (se autocompleta al arrancar)")]
    [Tooltip("Nombres de todas las escenas agregadas en Build Settings.")]
    [SerializeField] private List<string> escenasDisponibles = new List<string>();

    [Header("Opciones")]
    [Tooltip("Si está activo, este objeto no se destruye al cambiar de escena.")]
    [SerializeField] private bool persistirEntreEscenas = true;

    private void Awake()
    {
        // Patrón Singleton simple
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistirEntreEscenas)
            DontDestroyOnLoad(gameObject);

        CargarListaDeEscenasDesdeBuildSettings();
    }

    /// <summary>
    /// Lee automáticamente todas las escenas que están agregadas en
    /// File > Build Settings y arma la lista de nombres disponibles.
    /// </summary>
    private void CargarListaDeEscenasDesdeBuildSettings()
    {
        escenasDisponibles.Clear();

        int totalEscenas = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < totalEscenas; i++)
        {
            string ruta = SceneUtility.GetScenePathByBuildIndex(i);
            // La ruta viene como "Assets/Scenes/NombreEscena.unity", nos quedamos solo con el nombre
            string nombre = System.IO.Path.GetFileNameWithoutExtension(ruta);
            escenasDisponibles.Add(nombre);
        }
    }

    /// <summary>
    /// Devuelve la lista de nombres de escenas disponibles (por si querés
    /// generar botones dinámicamente en un menú).
    /// </summary>
    public List<string> ObtenerEscenasDisponibles()
    {
        return escenasDisponibles;
    }

    /// <summary>
    /// Carga una escena por nombre. Ideal para conectar a un botón (OnClick)
    /// o para llamar desde un dropdown/menú generado con ObtenerEscenasDisponibles().
    /// </summary>
    public void LoadSceneByName(string nombreEscena)
    {
        if (string.IsNullOrEmpty(nombreEscena))
        {
            Debug.LogWarning("SceneManagerController: el nombre de escena está vacío.");
            return;
        }

        if (!escenasDisponibles.Contains(nombreEscena))
        {
            Debug.LogWarning($"SceneManagerController: la escena '{nombreEscena}' no está en Build Settings.");
        }

        SceneManager.LoadScene(nombreEscena);
    }

    /// <summary>
    /// Carga una escena por índice de Build Settings. Útil para botones
    /// simples tipo "Siguiente nivel" (index + 1).
    /// </summary>
    public void LoadSceneByIndex(int indice)
    {
        if (indice < 0 || indice >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning($"SceneManagerController: índice {indice} fuera de rango.");
            return;
        }

        SceneManager.LoadScene(indice);
    }

    /// <summary>
    /// Carga la siguiente escena en el orden de Build Settings.
    /// Útil para un botón "Siguiente nivel".
    /// </summary>
    public void LoadNextScene()
    {
        int siguiente = SceneManager.GetActiveScene().buildIndex + 1;

        if (siguiente < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(siguiente);
        else
            Debug.LogWarning("SceneManagerController: no hay más escenas después de esta.");
    }

    /// <summary>
    /// Reinicia (recarga) la escena actual. Perfecto para un botón de "Reintentar".
    /// </summary>
    public void RestartScene()
    {
        Scene escenaActual = SceneManager.GetActiveScene();
        SceneManager.LoadScene(escenaActual.buildIndex);
    }

    /// <summary>
    /// Cierra el juego. En un build funciona normalmente; en el Editor de Unity
    /// solo detiene el modo Play (Application.Quit no funciona ahí).
    /// Ideal para un botón "Salir".
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
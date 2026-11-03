using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pausa el juego (Time.timeScale = 0), detiene el audio y muestra un menú con
/// Reanudar, Reiniciar nivel, Volumen, Menú principal (opcional) y Salir.
///
/// Uso rápido: agrégalo a un GameObject vacío de la escena. Escape o P pausan y reanudan.
/// Si no asignas un panel propio, se crea uno básico automáticamente.
///
/// Los scripts de control (GanchoFran, movimiento, brazo) deben empezar su Update con:
///     if (PauseManager.ControlesBloqueados) return;
/// </summary>
[DisallowMultipleComponent]
public class PauseManager : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Estado global: se puede leer desde cualquier script
    // ------------------------------------------------------------------
    public static PauseManager Instancia { get; private set; }

    /// <summary>True mientras el juego está en pausa.</summary>
    public static bool EstaPausado { get; private set; }

    /// <summary>
    /// True en pausa y también durante el frame en que se reanuda: así el click en
    /// "Reanudar" no se interpreta como una acción del juego (por ejemplo, soltar el gancho).
    /// </summary>
    public static bool ControlesBloqueados => EstaPausado || Time.frameCount == frameReanudacion;

    /// <summary>Se dispara con true al pausar y con false al reanudar.</summary>
    public static event Action<bool> AlCambiarPausa;

    private static int frameReanudacion = -1;

    // ------------------------------------------------------------------
    // Inspector
    // ------------------------------------------------------------------
    [Header("Teclas")]
    public KeyCode teclaPausa = KeyCode.Escape;
    public KeyCode teclaAlternativa = KeyCode.P;

    [Tooltip("Botón Start de un mando de Xbox con el Input Manager clásico. None para desactivarlo.")]
    public KeyCode botonMando = KeyCode.JoystickButton7;

    [Header("Comportamiento")]
    [Tooltip("Pausa todo el audio. Las fuentes con ignoreListenerPause = true siguen sonando (útil para la música del menú).")]
    public bool pausarAudio = true;

    [Tooltip("Pausa sola si la ventana pierde el foco o la app pasa a segundo plano. No aplica dentro del editor.")]
    public bool pausarAlPerderFoco = true;

    [Header("Menú")]
    [Tooltip("Panel de pausa propio. Si queda vacío y 'Crear Panel Automatico' está activo, se crea uno básico.")]
    public GameObject panelPausa;

    [Tooltip("Elemento que queda seleccionado al pausar, para navegar con teclado o mando. Opcional.")]
    public Selectable seleccionInicial;

    public bool crearPanelAutomatico = true;

    [Header("Panel automático")]
    public string textoTitulo = "PAUSA";
    public bool mostrarReiniciar = true;
    public bool mostrarVolumen = true;

    [Tooltip("Nombre exacto de la escena del menú principal (debe estar en Build Settings). Vacío = no se muestra el botón.")]
    public string escenaMenuPrincipal = "";

    public bool mostrarSalir = true;

    public Color colorFondo = new Color(0f, 0f, 0f, 0.65f);
    public Color colorCaja = new Color(0.10f, 0.11f, 0.14f, 0.95f);
    public Color colorBoton = new Color(0.93f, 0.93f, 0.93f, 1f);
    public Color colorAcento = new Color(0.98f, 0.76f, 0.24f, 1f);

    // ------------------------------------------------------------------
    // Estado interno
    // ------------------------------------------------------------------
    private const string ClaveVolumen = "Fran_VolumenGeneral";

    private bool pausadoPorEste;
    private bool audioPausadoPorEste;
    private float escalaTiempoPrevia = 1f;
    private bool cursorVisiblePrevio = true;
    private CursorLockMode cursorBloqueoPrevio = CursorLockMode.None;

    private Slider sliderVolumen;
    private Font fuente;

    // ==================================================================
    // Ciclo de vida
    // ==================================================================

    // Limpia el estado estático al entrar a Play, aunque la recarga de dominio esté desactivada.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarEstadoEstatico()
    {
        Instancia = null;
        EstaPausado = false;
        frameReanudacion = -1;
        AlCambiarPausa = null;
    }

    private void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(ClaveVolumen, 1f);
    }

    private void OnEnable()
    {
        // Si hubiera más de uno, solo el último activado responde a las teclas.
        Instancia = this;
    }

    private void Start()
    {
        if (panelPausa == null && crearPanelAutomatico)
            CrearPanelAutomatico();

        if (panelPausa != null)
            panelPausa.SetActive(false);
    }

    private void OnDisable()
    {
        // Si se desactiva o se destruye en plena pausa, el juego no debe quedar congelado.
        if (pausadoPorEste)
            RestaurarEstadoGlobal();

        if (Instancia == this)
            Instancia = null;
    }

    private void Update()
    {
        if (Instancia != this)
            return;

        if (Input.GetKeyDown(teclaPausa) ||
            Input.GetKeyDown(teclaAlternativa) ||
            Input.GetKeyDown(botonMando))
        {
            AlternarPausa();
        }
    }

    private void OnApplicationFocus(bool tieneFoco)
    {
        if (!tieneFoco)
            PausarPorSegundoPlano();
    }

    private void OnApplicationPause(bool enPausa)
    {
        if (enPausa)
            PausarPorSegundoPlano();
    }

    private void PausarPorSegundoPlano()
    {
        if (pausarAlPerderFoco && !Application.isEditor && Instancia == this)
            Pausar();
    }

    // ==================================================================
    // API pública (sirve también para los OnClick de un panel propio)
    // ==================================================================
    public void Pausar()
    {
        if (EstaPausado || !isActiveAndEnabled)
            return;

        escalaTiempoPrevia = Time.timeScale;
        Time.timeScale = 0f;

        audioPausadoPorEste = pausarAudio;
        if (audioPausadoPorEste)
            AudioListener.pause = true;

        cursorVisiblePrevio = Cursor.visible;
        cursorBloqueoPrevio = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EstaPausado = true;
        pausadoPorEste = true;

        if (sliderVolumen != null)
            sliderVolumen.SetValueWithoutNotify(AudioListener.volume);

        if (panelPausa != null)
            panelPausa.SetActive(true);

        Seleccionar(seleccionInicial != null ? seleccionInicial.gameObject : null);

        AlCambiarPausa?.Invoke(true);
    }

    public void Reanudar()
    {
        if (!pausadoPorEste)
            return;

        RestaurarEstadoGlobal();

        if (panelPausa != null)
            panelPausa.SetActive(false);

        Seleccionar(null);
        PlayerPrefs.Save();

        AlCambiarPausa?.Invoke(false);
    }

    /// <summary>Pausa o reanuda. Útil para un botón de pausa en el HUD.</summary>
    public void AlternarPausa()
    {
        if (EstaPausado)
            Reanudar();
        else
            Pausar();
    }

    public void Reiniciar()
    {
        PrepararCambioDeEscena();

        Scene actual = SceneManager.GetActiveScene();

        if (actual.buildIndex >= 0)
            SceneManager.LoadScene(actual.buildIndex);
        else
            SceneManager.LoadScene(actual.name);
    }

    public void IrAlMenuPrincipal()
    {
        if (string.IsNullOrEmpty(escenaMenuPrincipal))
        {
            Debug.LogWarning("[PauseManager] No se definió 'Escena Menu Principal'.", this);
            return;
        }

        PrepararCambioDeEscena();
        SceneManager.LoadScene(escenaMenuPrincipal);
    }

    public void SalirDelJuego()
    {
        PlayerPrefs.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Volumen general entre 0 y 1. Se guarda entre partidas.</summary>
    public void CambiarVolumen(float valor)
    {
        AudioListener.volume = Mathf.Clamp01(valor);
        PlayerPrefs.SetFloat(ClaveVolumen, AudioListener.volume);
    }

    // ==================================================================
    // Internos
    // ==================================================================
    private void RestaurarEstadoGlobal()
    {
        Time.timeScale = escalaTiempoPrevia;

        if (audioPausadoPorEste)
            AudioListener.pause = false;

        Cursor.visible = cursorVisiblePrevio;
        Cursor.lockState = cursorBloqueoPrevio;

        EstaPausado = false;
        pausadoPorEste = false;
        audioPausadoPorEste = false;
        frameReanudacion = Time.frameCount;
    }

    private void PrepararCambioDeEscena()
    {
        // Time.timeScale y AudioListener.pause son globales: si no se restauran,
        // la escena nueva empezaría congelada o en silencio.
        if (pausadoPorEste)
            RestaurarEstadoGlobal();

        Time.timeScale = 1f;
        AudioListener.pause = false;
        PlayerPrefs.Save();
    }

    private static void Seleccionar(GameObject objeto)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(objeto);
    }

    // ==================================================================
    // Panel automático (solo si no asignas uno propio)
    // ==================================================================
    private void CrearPanelAutomatico()
    {
        fuente = ObtenerFuente();
        AsegurarEventSystem();

        GameObject canvasGO = new GameObject(
            "CanvasPausa (automático)",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // por encima del HUD

        CanvasScaler escalador = canvasGO.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;

        // Fondo oscuro a pantalla completa: además bloquea los clics al HUD.
        Image fondo = CrearImagen("PanelPausa", canvasGO.transform, colorFondo);
        Estirar(fondo.rectTransform);
        panelPausa = fondo.gameObject;

        // Caja central con los controles apilados.
        Image caja = CrearImagen("Caja", fondo.transform, colorCaja);
        RectTransform rtCaja = caja.rectTransform;
        rtCaja.anchorMin = new Vector2(0.5f, 0.5f);
        rtCaja.anchorMax = new Vector2(0.5f, 0.5f);
        rtCaja.pivot = new Vector2(0.5f, 0.5f);
        rtCaja.sizeDelta = new Vector2(560f, 0f);

        VerticalLayoutGroup pila = caja.gameObject.AddComponent<VerticalLayoutGroup>();
        pila.padding = new RectOffset(48, 48, 40, 48);
        pila.spacing = 18f;
        pila.childAlignment = TextAnchor.UpperCenter;
        pila.childControlWidth = true;
        pila.childControlHeight = true;
        pila.childForceExpandWidth = true;
        pila.childForceExpandHeight = false;

        ContentSizeFitter ajuste = caja.gameObject.AddComponent<ContentSizeFitter>();
        ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text titulo = CrearTexto("Titulo", caja.transform, textoTitulo, 64, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        titulo.gameObject.AddComponent<LayoutElement>().minHeight = 90f;

        Button reanudar = CrearBoton(caja.transform, "Reanudar", Reanudar);
        if (seleccionInicial == null)
            seleccionInicial = reanudar;

        if (mostrarReiniciar)
            CrearBoton(caja.transform, "Reiniciar nivel", Reiniciar);

        if (mostrarVolumen)
            CrearControlVolumen(caja.transform);

        if (!string.IsNullOrEmpty(escenaMenuPrincipal))
            CrearBoton(caja.transform, "Menú principal", IrAlMenuPrincipal);

        if (mostrarSalir)
            CrearBoton(caja.transform, "Salir del juego", SalirDelJuego);
    }

    private Button CrearBoton(Transform padre, string etiqueta, UnityAction accion)
    {
        Image imagen = CrearImagen("Boton " + etiqueta, padre, colorBoton);

        LayoutElement alto = imagen.gameObject.AddComponent<LayoutElement>();
        alto.minHeight = 84f;
        alto.preferredHeight = 84f;

        Button boton = imagen.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;

        ColorBlock colores = boton.colors;
        colores.normalColor = Color.white;
        colores.highlightedColor = colorAcento;
        colores.selectedColor = colorAcento;
        colores.pressedColor = new Color(colorAcento.r * 0.8f, colorAcento.g * 0.8f, colorAcento.b * 0.8f, 1f);
        boton.colors = colores;

        boton.onClick.AddListener(accion);

        Text texto = CrearTexto("Texto", imagen.transform, etiqueta, 36, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.12f, 0.12f, 0.12f, 1f));
        Estirar(texto.rectTransform);

        return boton;
    }

    private void CrearControlVolumen(Transform padre)
    {
        CrearTexto("Etiqueta Volumen", padre, "Volumen", 30, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);

        GameObject raiz = new GameObject("Volumen", typeof(RectTransform));
        raiz.transform.SetParent(padre, false);

        LayoutElement alto = raiz.AddComponent<LayoutElement>();
        alto.minHeight = 44f;
        alto.preferredHeight = 44f;

        Image barra = CrearImagen("Barra", raiz.transform, new Color(1f, 1f, 1f, 0.25f));
        Anclar(barra.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f), 0f);

        RectTransform areaRelleno = CrearContenedor("Area Relleno", raiz.transform);
        Anclar(areaRelleno, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f), 14f);
        Image relleno = CrearImagen("Relleno", areaRelleno, colorAcento);
        Estirar(relleno.rectTransform);

        RectTransform areaManija = CrearContenedor("Area Manija", raiz.transform);
        Anclar(areaManija, Vector2.zero, Vector2.one, 14f);
        Image manija = CrearImagen("Manija", areaManija, Color.white);
        manija.rectTransform.sizeDelta = new Vector2(28f, 0f);

        sliderVolumen = raiz.AddComponent<Slider>();
        sliderVolumen.fillRect = relleno.rectTransform;
        sliderVolumen.handleRect = manija.rectTransform;
        sliderVolumen.targetGraphic = manija;
        sliderVolumen.direction = Slider.Direction.LeftToRight;
        sliderVolumen.minValue = 0f;
        sliderVolumen.maxValue = 1f;
        sliderVolumen.SetValueWithoutNotify(AudioListener.volume);
        sliderVolumen.onValueChanged.AddListener(CambiarVolumen);
    }

    private Text CrearTexto(string nombre, Transform padre, string contenido, int tamano, FontStyle estilo, TextAnchor alineacion, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        Text texto = go.AddComponent<Text>();
        texto.font = fuente;
        texto.text = contenido;
        texto.fontSize = tamano;
        texto.fontStyle = estilo;
        texto.alignment = alineacion;
        texto.color = color;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Overflow;
        texto.raycastTarget = false;
        return texto;
    }

    private static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        Image imagen = go.AddComponent<Image>();
        imagen.color = color;
        return imagen;
    }

    private static RectTransform CrearContenedor(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    private static void Anclar(RectTransform rt, Vector2 minimo, Vector2 maximo, float margenHorizontal)
    {
        rt.anchorMin = minimo;
        rt.anchorMax = maximo;
        rt.offsetMin = new Vector2(margenHorizontal, 0f);
        rt.offsetMax = new Vector2(-margenHorizontal, 0f);
    }

    private static void Estirar(RectTransform rt)
    {
        Anclar(rt, Vector2.zero, Vector2.one, 0f);
    }

    private static void AsegurarEventSystem()
    {
        if (EventSystem.current != null)
            return;

#if UNITY_2023_1_OR_NEWER
        EventSystem existente = FindAnyObjectByType<EventSystem>();
#else
        EventSystem existente = FindObjectOfType<EventSystem>();
#endif

        // Sin EventSystem los botones no responden. Usa el Input Manager clásico,
        // igual que el resto de los scripts del proyecto.
        if (existente == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Font ObtenerFuente()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }
}

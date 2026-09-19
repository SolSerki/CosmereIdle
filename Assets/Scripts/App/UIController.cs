using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIController : MonoBehaviour
{
    // Variable global estática para que la escena 'main' sepa qué personaje elegiste
    public static GameObject prefabSeleccionado;

    [Header("Paneles")]
    [SerializeField] private GameObject _panelInicio;
    [SerializeField] private GameObject _panelSeleccion;

    [Header("Botones de Menú")]
    [SerializeField] private Button _btnAbrirSeleccion;
    [SerializeField] private Button _btnVolver;

    [Header("Botones de Personajes")]
    [SerializeField] private Button _btnJoch;

    [Header("Prefabs de Personajes")]
    [SerializeField] private GameObject _prefabJoch;

    private void Start()
    {
        // Fuerza el estado correcto apenas carga la escena de menú
        _panelInicio.SetActive(true);
        _panelSeleccion.SetActive(false);
    }
    private void OnEnable()
    {
        _btnAbrirSeleccion.onClick.AddListener(AbrirSeleccion);
        _btnVolver.onClick.AddListener(CerrarSeleccion);
        
        // Usamos expresiones lambda para pasarle el prefab correspondiente a cada botón
        _btnJoch.onClick.AddListener(() => ElegirPersonaje(_prefabJoch));
    }

    private void OnDisable()
    {
        _btnAbrirSeleccion.onClick.RemoveListener(AbrirSeleccion);
        _btnVolver.onClick.RemoveListener(CerrarSeleccion);
        _btnJoch.onClick.RemoveAllListeners();
    }

    private void AbrirSeleccion()
    {
        _panelInicio.SetActive(false);
        _panelSeleccion.SetActive(true);
    }

    private void CerrarSeleccion()
    {
        _panelSeleccion.SetActive(false);
        _panelInicio.SetActive(true);
    }

    private void ElegirPersonaje(GameObject personajeElegido)
    {
        prefabSeleccionado = personajeElegido;
        SceneManager.LoadScene("main");
    }
}
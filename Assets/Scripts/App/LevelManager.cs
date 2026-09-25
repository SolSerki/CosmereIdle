using UnityEngine;

/// <summary>
/// Instancia el personaje elegido en la escena de menu.
///
/// Solo se usa si se entra por Menu.unity. El camino por defecto hoy es
/// CharacterPicker, que hace la seleccion sin cambiar de escena.
/// </summary>
public class LevelManager : MonoBehaviour
{
    private void Start()
    {
        if (UIController.prefabSeleccionado == null) return;

        // En el piso de la franja, no en Vector3.zero: con el pivot abajo, el
        // centro de la franja deja al personaje flotando media franja en el aire.
        var cam = Camera.main;
        float floorY = cam != null ? -cam.orthographicSize : -0.6f;

        Instantiate(UIController.prefabSeleccionado,
                    new Vector3(0f, floorY, 0f), Quaternion.identity);
    }
}

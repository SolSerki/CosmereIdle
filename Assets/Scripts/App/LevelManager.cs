using UnityEngine;

public class LevelManager : MonoBehaviour
{
    private void Start()
    {
        if (UIController.prefabSeleccionado != null)
        {
            // Crea una copia del prefab elegido en el centro de la pantalla
            Instantiate(UIController.prefabSeleccionado, Vector3.zero, Quaternion.identity);
        }
    }
}
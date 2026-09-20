using System.Collections;
using UnityEngine;

public class MascotaController : MonoBehaviour
{
    private Animator animator;

    public float tiempoMinimo = 2f;
    public float tiempoMaximo = 5f;

    void Start()
    {
        animator = GetComponent<Animator>();
        // Iniciamos la rutina automática apenas arranca el juego
        StartCoroutine(RutinaMascota());
    }

   private IEnumerator RutinaMascota()
{
    while (true) 
    {
        // 1. Siempre obligamos a la mascota a volver a Idle para resetear el Animator
        animator.SetInteger("Estado", 0);
        
        // Esperamos un segundito para que se quede quieto y respire
        yield return new WaitForSeconds(1.5f); 

        // 2. Elegimos qué hacer: 1 (Walk) o 2 (Read). 
        // Como Random.Range excluye el máximo en enteros, (1, 3) solo dará 1 o 2.
        int estadoAleatorio = Random.Range(1, 3); 
        animator.SetInteger("Estado", estadoAleatorio);

        // 3. Ejecuta la acción por un tiempo aleatorio
        float tiempoEspera = Random.Range(tiempoMinimo, tiempoMaximo);
        yield return new WaitForSeconds(tiempoEspera);

        // 4. Si la acción que acabamos de hacer fue caminar...
        if (estadoAleatorio == 1)
        {
            Debug.Log("Aplicando el freno (Idle Slide) obligatorio");
            animator.SetInteger("Estado", 3); 
            yield return new WaitForSeconds(2f); 
        }
        
        // Al terminar, el bucle vuelve al inicio y lo pone en Idle (0) de nuevo.
    }
}

    public void CambiarEscala(float multiplicador)
    {
    transform.localScale = new Vector3(multiplicador, multiplicador, multiplicador);
    }
}
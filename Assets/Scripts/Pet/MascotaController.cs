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
            int estadoAleatorio = Random.Range(0, 3);
            animator.SetInteger("Estado", estadoAleatorio);

            float tiempoEspera = Random.Range(tiempoMinimo, tiempoMaximo);
            yield return new WaitForSeconds(tiempoEspera);

            if (estadoAleatorio == 1)
            {
                Debug.Log("Aplicando el freno (Idle Slide) obligatorio");
                animator.SetInteger("Estado", 3); 
                yield return new WaitForSeconds(2f); 
            }
        }
    }

    public void CambiarEscala(float multiplicador)
    {
    transform.localScale = new Vector3(multiplicador, multiplicador, multiplicador);
    }
}
using UnityEngine;

public class VidaJugador : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int vidaMaxima = 100;

    public int vidaActual;  //solo publica para debuggear, luego se puede hacer privada

    private void Start()
    {
        vidaActual = vidaMaxima;

        Debug.Log("Vida inicial del jugador: " + vidaActual);
    }

    public void RecibirDaño(int cantidad)
    {
        vidaActual -= cantidad;

        if (vidaActual < 0)
        {
            vidaActual = 0;
        }

        Debug.Log("Jugador recibe daño: " + cantidad);
        Debug.Log("Vida actual: " + vidaActual);

        if (vidaActual <= 0)
        {
            Morir();
        }
    }

    private void Morir()
    {
        Debug.Log("El jugador ha muerto.");

        gameObject.SetActive(false);
    }
}
using UnityEngine;

public class ObstaculosMovibles : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float distancia = 2f;
    [SerializeField] private float velocidad = 2f;

    private Vector3 posicionInicial;
    private bool moverDerecha = true;

    private void Start()
    {
        posicionInicial = transform.position;
        moverDerecha = Random.value > 0.5f; // Randomiza la dirección inicial
    }

    private void Update()
    {
        float desplazamiento = velocidad * Time.deltaTime;

        if (moverDerecha)
        {
            transform.Translate(Vector3.right * desplazamiento);

            if (transform.position.x >= posicionInicial.x + distancia)
            {
                moverDerecha = false;
            }
        }
        else
        {
            transform.Translate(Vector3.left * desplazamiento);

            if (transform.position.x <= posicionInicial.x - distancia)
            {
                moverDerecha = true;
            }
        }
    }
}
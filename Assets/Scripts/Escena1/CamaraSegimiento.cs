using UnityEngine;

public class CamaraSeguimiento : MonoBehaviour
{
    [Header("Objetivo"), Tooltip("El objeto que la cámara seguirá, generalmente el jugador.")]
    public Transform objetivo;

    [Header("Offset"),  Tooltip("La distancia y altura relativa a la que la cámara seguirá al objetivo.")]
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Suavizado"), Tooltip("La velocidad a la que la cámara se moverá para seguir al objetivo.")]
    public float suavizado = 5f;

    private float alturaInicial;

    private void Start()
    {
        alturaInicial = transform.position.y;
    }

    private void LateUpdate()
    {
        if (objetivo == null)
            return;

        Vector3 posicionDeseada = new Vector3(
            objetivo.position.x + offset.x,
            alturaInicial + offset.y,
            objetivo.position.z + offset.z
        );

        transform.position = Vector3.Lerp(
            transform.position,
            posicionDeseada,
            suavizado * Time.deltaTime
        );
    }
}
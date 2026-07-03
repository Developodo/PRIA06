using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class EnemigoPercepcion : MonoBehaviour
{
    private enum EstadoEnemigo
    {
        Patrullando,
        Viendo,
        Alerta
    }

    [Header("Referencias")]
    [SerializeField] private Transform jugador;

    [Header("Patrulla")]
    [SerializeField] private Transform puntoA;
    [SerializeField] private Transform puntoB;
    [SerializeField] private float velocidadPatrulla = 2f;

    [Header("Percepción")]
    [SerializeField] private float distanciaVision = 10f;
    [SerializeField] private float anguloVision = 120f;
    [SerializeField] private float distanciaDeteccionCercana = 3f;
    [SerializeField] private LayerMask capaObstaculos;

    [Header("Memoria")]
    [SerializeField] private float tiempoMemoria = 3f;
    [SerializeField] private float tiempoAlerta = 2f;

    private EstadoEnemigo estadoActual =
        EstadoEnemigo.Patrullando;

    private Renderer rend;

    private Transform destinoPatrulla;

    private float contadorMemoria;
    private float contadorAlerta;

    private Color colorPatrulla;
    private readonly Color colorViendo = Color.red;
    private readonly Color colorAlerta =
        new Color(1f, 0.5f, 0f);

    private void Awake()
    {
        rend = GetComponent<Renderer>();

        colorPatrulla = rend.material.color;
    }

    private void Start()
    {
        destinoPatrulla = puntoA;
    }

    private void Update()
    {
        bool veJugador = PuedeDetectarJugador();

        if (veJugador)
        {
            contadorMemoria = tiempoMemoria;
        }

        switch (estadoActual)
        {
            case EstadoEnemigo.Patrullando:
                EstadoPatrullando(veJugador);
                break;

            case EstadoEnemigo.Viendo:
                EstadoViendo(veJugador);
                break;

            case EstadoEnemigo.Alerta:
                EstadoAlerta(veJugador);
                break;
        }
    }

    private void EstadoPatrullando(bool veJugador)
    {
        rend.material.color = colorPatrulla;

        // Por ahora NO movemos al enemigo.
        // Más adelante la patrulla se hará con NavMeshAgent.
        // Patrullar();

        if (veJugador)
        {
            CambiarEstado(EstadoEnemigo.Viendo);
        }
    }

    private void EstadoViendo(bool veJugador)
    {
        rend.material.color = colorViendo;

        MirarAlJugador();

        if (!veJugador)
        {
            contadorMemoria -= Time.deltaTime;

            if (contadorMemoria <= 0f)
            {
                contadorAlerta = tiempoAlerta;
                CambiarEstado(EstadoEnemigo.Alerta);
            }
        }
    }

    private void EstadoAlerta(bool veJugador)
    {
        rend.material.color = colorAlerta;

        if (veJugador)
        {
            CambiarEstado(EstadoEnemigo.Viendo);
            return;
        }

        contadorAlerta -= Time.deltaTime;

        if (contadorAlerta <= 0f)
        {
            CambiarEstado(
                EstadoEnemigo.Patrullando
            );
        }
    }



    private void MirarAlJugador()
    {
        if (jugador == null)
        {
            return;
        }

        Vector3 direccion =
            jugador.position -
            transform.position;

        direccion.y = 0f;

        if (direccion != Vector3.zero)
        {
            transform.rotation =
                Quaternion.LookRotation(
                    direccion
                );
        }
    }

    private void CambiarEstado(
        EstadoEnemigo nuevoEstado)
    {
        estadoActual = nuevoEstado;

        Debug.Log(
            "Nuevo estado: " +
            estadoActual
        );
    }

    private bool PuedeDetectarJugador()
    {
        if (jugador == null)
        {
            return false;
        }

        Vector3 origen =
            transform.position +
            Vector3.up * 1.2f;

        Vector3 destino =
            jugador.position +
            Vector3.up * 1.2f;

        Vector3 direccion =
            destino - origen;

        float distancia =
            direccion.magnitude;

        if (distancia > distanciaVision)
        {
            return false;
        }

        bool hayObstaculo =
            Physics.Raycast(
                origen,
                direccion.normalized,
                distancia,
                capaObstaculos
            );

        if (hayObstaculo)
        {
            return false;
        }

        if (distancia <= distanciaDeteccionCercana)
        {
            return true;
        }

        float angulo =
            Vector3.Angle(
                transform.forward,
                direccion
            );

        return angulo <=
            anguloVision / 2f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            distanciaVision
        );

        Vector3 izquierda =
            Quaternion.Euler(
                0,
                -anguloVision / 2,
                0
            ) * transform.forward;

        Vector3 derecha =
            Quaternion.Euler(
                0,
                anguloVision / 2,
                0
            ) * transform.forward;

        Gizmos.color = Color.blue;

        Gizmos.DrawRay(
            transform.position,
            izquierda * distanciaVision
        );

        Gizmos.DrawRay(
            transform.position,
            derecha * distanciaVision
        );
    }
}
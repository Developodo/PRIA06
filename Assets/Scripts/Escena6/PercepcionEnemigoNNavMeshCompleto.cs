using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemigoPercepcionNavMeshCompleto : MonoBehaviour
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

    [Header("Percepción")]
    [SerializeField] private float distanciaVision = 10f;
    [SerializeField] private float anguloVision = 120f;
    [SerializeField] private float distanciaDeteccionCercana = 3f;
    [SerializeField] private LayerMask capaObstaculos;

    [Header("Memoria")]
    [SerializeField] private float tiempoMemoria = 2f;
    [SerializeField] private float tiempoAlerta = 1f;

    [Header("Movimiento")]
    [SerializeField] private float velocidadPatrulla = 0.5f;
    [SerializeField] private float velocidadMinimaPersecucion = 0.5f;
    [SerializeField] private float velocidadMaximaPersecucion = 3f;
    [SerializeField] private float distanciaCambioPunto = 0.2f;

    [Header("Ataque")]
    [SerializeField] private float distanciaAtaque = 0.6f;
    [SerializeField] private float tiempoEntreAtaques = 1.5f;
    [SerializeField] private int dañoMinimoAtaque = 5;
    [SerializeField] private int dañoMaximoAtaque = 15;
    [SerializeField] private float tiempoColorAtaque = 0.2f;

    [Header("Dificultad dinámica")]
    [SerializeField] private bool dificultadDinamica = true;
    [SerializeField] private float tiempoParaMaximaDificultad = 10f;

    private EstadoEnemigo estadoActual = EstadoEnemigo.Patrullando;

    private Renderer rend;
    private NavMeshAgent agent;
    private VidaJugador vidaJugador;

    private Transform destinoPatrulla;

    private float contadorMemoria;
    private float contadorAlerta;
    private float contadorAtaque;

    private float tiempoPersiguiendo;
    private float velocidadPersecucionActual;
    private int dañoAtaqueActual;

    private bool mostrandoColorAtaque;

    private Color colorPatrulla;
    private readonly Color colorViendo = Color.red;
    private readonly Color colorAlerta = new Color(1f, 0.5f, 0f);
    private readonly Color colorAtaque = Color.black;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        agent = GetComponent<NavMeshAgent>();

        colorPatrulla = rend.material.color;

        velocidadPersecucionActual = velocidadMinimaPersecucion;
        dañoAtaqueActual = dañoMinimoAtaque;
    }

    private void Start()
    {
        if (jugador != null)
        {
            vidaJugador = jugador.GetComponent<VidaJugador>();
        }

        destinoPatrulla = puntoA;
        agent.speed = velocidadPatrulla;

        if (destinoPatrulla != null)
        {
            agent.SetDestination(destinoPatrulla.position);
        }
    }

    private void Update()
    {
        contadorAtaque -= Time.deltaTime;

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
        CambiarColor(colorPatrulla);

        agent.speed = velocidadPatrulla;
        Patrullar();

        ReiniciarDificultad();

        if (veJugador)
        {
            CambiarEstado(EstadoEnemigo.Viendo);
        }
    }

    private void EstadoViendo(bool veJugador)
    {
        CambiarColor(colorViendo);

        ActualizarDificultadPorTiempo();

        agent.speed = velocidadPersecucionActual;

        if (jugador != null)
        {
            agent.SetDestination(jugador.position);

            float distanciaJugador = Vector3.Distance(
                transform.position,
                jugador.position
            );

            if (distanciaJugador <= distanciaAtaque)
            {
                Atacar();
            }
        }

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
        CambiarColor(colorAlerta);

        agent.ResetPath();

        ReiniciarDificultad();

        if (veJugador)
        {
            CambiarEstado(EstadoEnemigo.Viendo);
            return;
        }

        contadorAlerta -= Time.deltaTime;

        if (contadorAlerta <= 0f)
        {
            CambiarEstado(EstadoEnemigo.Patrullando);
        }
    }

    private void Patrullar()
    {
        if (puntoA == null || puntoB == null)
        {
            return;
        }

        if (destinoPatrulla == null)
        {
            destinoPatrulla = puntoA;
        }

        if (!agent.pathPending && agent.remainingDistance <= distanciaCambioPunto)
        {
            destinoPatrulla = destinoPatrulla == puntoA ? puntoB : puntoA;
            agent.SetDestination(destinoPatrulla.position);
        }
    }

    private void Atacar()
    {
        if (contadorAtaque > 0f)
        {
            return;
        }

        contadorAtaque = tiempoEntreAtaques;

        StartCoroutine(ColorAtaqueTemporal());

        if (vidaJugador != null)
        {
            vidaJugador.RecibirDaño(dañoAtaqueActual);
        }

        Debug.Log("El enemigo ataca. Daño: " + dañoAtaqueActual);

        ReiniciarDificultad();
    }

    private void ActualizarDificultadPorTiempo()
    {
        if (!dificultadDinamica)
        {
            velocidadPersecucionActual = velocidadMinimaPersecucion;
            dañoAtaqueActual = dañoMinimoAtaque;
            return;
        }

        tiempoPersiguiendo += Time.deltaTime;

        float progreso = tiempoPersiguiendo / tiempoParaMaximaDificultad;
        progreso = Mathf.Clamp01(progreso);

        velocidadPersecucionActual = Mathf.Lerp(
            velocidadMinimaPersecucion,
            velocidadMaximaPersecucion,
            progreso
        );

        dañoAtaqueActual = Mathf.RoundToInt(
            Mathf.Lerp(
                dañoMinimoAtaque,
                dañoMaximoAtaque,
                progreso
            )
        );
    }

    private void ReiniciarDificultad()
    {
        tiempoPersiguiendo = 0f;
        velocidadPersecucionActual = velocidadMinimaPersecucion;
        dañoAtaqueActual = dañoMinimoAtaque;
    }

    private IEnumerator ColorAtaqueTemporal()
    {
        mostrandoColorAtaque = true;
        rend.material.color = colorAtaque;

        yield return new WaitForSeconds(tiempoColorAtaque);

        mostrandoColorAtaque = false;
    }

    private void CambiarColor(Color nuevoColor)
    {
        if (mostrandoColorAtaque)
        {
            return;
        }

        rend.material.color = nuevoColor;
    }

    private void CambiarEstado(EstadoEnemigo nuevoEstado)
    {
        estadoActual = nuevoEstado;

        Debug.Log("Nuevo estado: " + estadoActual);

        if (nuevoEstado == EstadoEnemigo.Patrullando && destinoPatrulla != null)
        {
            agent.SetDestination(destinoPatrulla.position);
        }
    }

    private bool PuedeDetectarJugador()
    {
        if (jugador == null)
        {
            return false;
        }

        Vector3 origen = transform.position + Vector3.up * 1.2f;
        Vector3 destino = jugador.position + Vector3.up * 1.2f;

        Vector3 direccion = destino - origen;
        float distancia = direccion.magnitude;

        if (distancia > distanciaVision)
        {
            return false;
        }

        bool hayObstaculo = Physics.Raycast(
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

        float angulo = Vector3.Angle(
            transform.forward,
            direccion
        );

        return angulo <= anguloVision / 2f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaVision);

        Vector3 izquierda = Quaternion.Euler(
            0,
            -anguloVision / 2,
            0
        ) * transform.forward;

        Vector3 derecha = Quaternion.Euler(
            0,
            anguloVision / 2,
            0
        ) * transform.forward;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, izquierda * distanciaVision);
        Gizmos.DrawRay(transform.position, derecha * distanciaVision);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaAtaque);
    }
}
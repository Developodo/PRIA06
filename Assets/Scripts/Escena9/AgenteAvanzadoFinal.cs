using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

/// <summary>
/// Agente entrenable con ML-Agents para perseguir al jugador saltando entre plataformas.
///
/// La IA NO calcula la física del salto.
/// La IA solo decide:
///     0 = esperar
///     1 = saltar al primer objetivo detectado
///     2 = saltar al segundo objetivo detectado
///
/// El script se encarga de:
/// - detectar plataformas alcanzables;
/// - filtrar las que están en dirección al jugador;
/// - aplicar el salto con Rigidbody;
/// - premiar o castigar según el resultado.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class AgenteAvanzadoFinal : Agent
{
    [Header("Referencias")]
    [SerializeField] private Transform jugador;

    [Header("Detección de objetivos")]
    [SerializeField] private float radioDeteccionObjetivos = 10f;

    [SerializeField, Range(0, 180)]
    private float anguloVision = 90f;

    // Número máximo de objetivos que verá la red neuronal.
    // Debe coincidir con las acciones:
    // 0 = esperar, 1 = objetivo 0, 2 = objetivo 1.
    private const int MAX_OBJETIVOS = 2;

    [Header("Salto")]
    [SerializeField] private float fuerzaHorizontalMinima = 1f;
    [SerializeField] private float fuerzaHorizontalMaxima = 9f;
    [SerializeField] private float fuerzaVertical = 7f;
    [SerializeField] private float distanciaSaltoMaxima = 10f;

    // Impide que el agente vuelva a saltar inmediatamente al tocar una plataforma.
    [SerializeField] private float tiempoMinimoEntreSaltos = 1.75f;

    [Header("Normalización")]
    [SerializeField] private float distanciaMaxima = 25f;

    [Header("Apoyo")]
    [SerializeField] private Transform puntoPies;
    [SerializeField] private float radioPies = 0.25f;

    private Rigidbody rb;

    private int layerSuelo;
    private int layerPlataforma;
    private int layerMuerte;

    private LayerMask mascaraApoyo;
    private LayerMask mascaraSuelo;

    private Vector3 posicionInicial;
    private Quaternion rotacionInicial;

    // Superficie sobre la que está el agente.
    // Si está en una plataforma, se hace hijo de ella.
    // Si está en suelo, queda sin padre.
    private Transform apoyoActual;

    private Transform ultimoObjetivo;

    // Array fijo porque ML-Agents necesita siempre el mismo número de observaciones.
    private readonly Transform[] objetivosActuales = new Transform[MAX_OBJETIVOS];

    private int accionManual = 0;
    private float tiempoInicio;
    private float instanteUltimoSalto;
    private float anteriorDistanciaJugador;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();

        layerSuelo = LayerMask.NameToLayer("Suelo");
        layerPlataforma = LayerMask.NameToLayer("Plataforma");
        layerMuerte = LayerMask.NameToLayer("Muerte");

        mascaraApoyo = (1 << layerSuelo) | (1 << layerPlataforma);
        mascaraSuelo = 1 << layerSuelo;

        posicionInicial = transform.position;
        rotacionInicial = transform.rotation;

        anteriorDistanciaJugador = float.MaxValue;
    }

    public override void OnEpisodeBegin()
    {
        tiempoInicio = Time.time;

        DesvincularDeApoyo();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.position = posicionInicial;
        transform.rotation = rotacionInicial;

        ultimoObjetivo = null;
        accionManual = 0;

        // Permite que pueda saltar al comenzar el episodio.
        instanteUltimoSalto = -tiempoMinimoEntreSaltos;

        anteriorDistanciaJugador = float.MaxValue;

        LimpiarObjetivosActuales();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        ActualizarObjetivosAlcanzables();

        // Observación 1: si está tocando suelo o plataforma.
        sensor.AddObservation(EstaApoyado() ? 1f : 0f);

        // Observación 2: si está sobre una plataforma móvil.
        sensor.AddObservation(EstaEnPlataforma() ? 1f : 0f);

        // Observaciones 3 y 4: posición relativa del jugador.
        AgregarObservacionJugador(sensor);

        // Cada objetivo aporta 6 observaciones.
        for (int i = 0; i < MAX_OBJETIVOS; i++)
        {
            AgregarObservacionObjetivo(sensor, objetivosActuales[i]);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        ActualizarObjetivosAlcanzables();

        int accion = actions.DiscreteActions[0];

        // Pequeño coste constante para que no alargue el episodio sin necesidad.
        AddReward(-0.001f);

        if (accion == 0)
        {
            GestionarAccionEsperar();
            return;
        }

        if (!PuedeSaltar())
        {
            AddReward(-0.01f);
            return;
        }

        Transform objetivo = ObtenerObjetivoDesdeAccion(accion);

        if (objetivo == null)
            return;

        DesvincularDeApoyo();

        GirarHacia(objetivo.position);
        SaltarHacia(objetivo);

        ultimoObjetivo = objetivo;
    }

    /// <summary>
    /// Añade la posición relativa del jugador.
    /// No usamos la posición absoluta porque al agente le interesa saber
    /// dónde está el jugador respecto a él.
    /// </summary>
    private void AgregarObservacionJugador(VectorSensor sensor)
    {
        if (jugador == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 jugadorRelativo = jugador.position - transform.position;

        sensor.AddObservation(jugadorRelativo.x / distanciaMaxima);
        sensor.AddObservation(jugadorRelativo.z / distanciaMaxima);
    }

    /// <summary>
    /// Añade las observaciones de un objetivo.
    ///
    /// Si el objetivo no existe, se rellenan ceros.
    /// Esto es obligatorio porque la red neuronal necesita siempre
    /// el mismo número de datos de entrada.
    /// </summary>
    private void AgregarObservacionObjetivo(VectorSensor sensor, Transform objetivo)
    {
        if (objetivo == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 relativo = objetivo.position - transform.position;

        sensor.AddObservation(relativo.x / distanciaMaxima);
        sensor.AddObservation(relativo.z / distanciaMaxima);

        if (objetivo.TryGetComponent<Rigidbody>(out Rigidbody rbObjetivo))
        {
            sensor.AddObservation(rbObjetivo.linearVelocity.x / 10f);
        }
        else
        {
            sensor.AddObservation(0f);
        }

        // Indica que este hueco del array sí contiene un objetivo real.
        sensor.AddObservation(1f);
    }

    /// <summary>
    /// Acción 0: esperar.
    ///
    /// Esperar no siempre es malo.
    /// Si no hay plataformas útiles, esperar es correcto.
    /// Si hay una plataforma disponible y espera, se castiga.
    /// </summary>
    private void GestionarAccionEsperar()
    {
        if (HayObjetivosDisponibles())
            AddReward(-0.5f);
        else
            AddReward(+0.1f);
    }

    /// <summary>
    /// Convierte la acción discreta en un objetivo del array.
    ///
    /// Acción 1 -> objetivosActuales[0]
    /// Acción 2 -> objetivosActuales[1]
    /// </summary>
    private Transform ObtenerObjetivoDesdeAccion(int accion)
    {
        int indiceObjetivo = accion - 1;

        if (indiceObjetivo < 0 || indiceObjetivo >= MAX_OBJETIVOS)
            return null;

        return objetivosActuales[indiceObjetivo];
    }

    /// <summary>
    /// Busca objetivos de salto alrededor del agente.
    ///
    /// Primero se usa OverlapSphere para obtener candidatos cercanos.
    /// Después se filtran para quedarse solo con los que están en dirección
    /// hacia el jugador.
    /// </summary>
    private void ActualizarObjetivosAlcanzables()
    {
        LimpiarObjetivosActuales();

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            radioDeteccionObjetivos,
            mascaraApoyo
        );

        List<Transform> candidatos = new List<Transform>();
        bool enPlataforma = EstaEnPlataforma();

        foreach (Collider col in colliders)
        {
            Transform candidato = col.transform;

            if (!EsCandidatoValido(candidato))
                continue;

            if (!EstaEnConoHaciaJugador(candidato))
                continue;

            if (!EsTipoDeObjetivoPermitido(candidato, enPlataforma))
                continue;

            candidatos.Add(candidato);
        }

        OrdenarCandidatosPorCercaniaAlJugador(candidatos);

        int cantidad = Mathf.Min(MAX_OBJETIVOS, candidatos.Count);

        for (int i = 0; i < cantidad; i++)
            objetivosActuales[i] = candidatos[i];
    }

    /// <summary>
    /// Descarta objetos que no deben considerarse como objetivos.
    /// </summary>
    private bool EsCandidatoValido(Transform candidato)
    {
        if (candidato == transform)
            return false;

        if (candidato.IsChildOf(transform))
            return false;

        /*if (apoyoActual != null && candidato == apoyoActual)
            return false; //podría querer dar un salto en la misma plataforma para acercarme*/

        return true;
    }

    /// <summary>
    /// Comprueba si el candidato está dentro del cono orientado hacia el jugador.
    ///
    /// Importante: el cono no depende de transform.forward.
    /// Depende de la dirección agente -> jugador.
    /// Así el agente solo considera saltos que avanzan en la persecución.
    /// </summary>
    private bool EstaEnConoHaciaJugador(Transform candidato)
    {
        if (jugador == null)
            return false;

        Vector3 direccionHaciaJugador = jugador.position - transform.position;
        direccionHaciaJugador.y = 0f;

        if (direccionHaciaJugador.sqrMagnitude < 0.01f)
            return false;

        direccionHaciaJugador.Normalize();

        Vector3 direccionHaciaObjetivo = candidato.position - transform.position;
        direccionHaciaObjetivo.y = 0f;

        if (direccionHaciaObjetivo.sqrMagnitude < 0.01f)
            return false;

        direccionHaciaObjetivo.Normalize();

        float angulo = Vector3.Angle(
            direccionHaciaJugador,
            direccionHaciaObjetivo
        );

        return angulo <= anguloVision * 0.5f;
    }

    /// <summary>
    /// Aplica la regla:
    ///
    /// - Si está en suelo, solo puede buscar plataformas.
    /// - Si está en plataforma, puede buscar plataformas o suelo.
    /// </summary>
    private bool EsTipoDeObjetivoPermitido(Transform candidato, bool enPlataforma)
    {
        if (!enPlataforma)
            return candidato.gameObject.layer == layerPlataforma;

        return candidato.gameObject.layer == layerPlataforma ||
               candidato.gameObject.layer == layerSuelo;
    }

    /// <summary>
    /// Ordena los objetivos por cercanía al jugador.
    ///
    /// Así, si hay varios candidatos, los dos que verá la red neuronal
    /// serán los que más ayudan a perseguir al jugador.
    /// </summary>
    private void OrdenarCandidatosPorCercaniaAlJugador(List<Transform> candidatos)
    {
        if (jugador == null)
            return;

        candidatos.Sort((a, b) =>
        {
            float distanciaA = Vector3.SqrMagnitude(a.position - jugador.position);
            float distanciaB = Vector3.SqrMagnitude(b.position - jugador.position);

            return distanciaA.CompareTo(distanciaB);
        });
    }

    /// <summary>
    /// Devuelve true si hay al menos un objetivo útil.
    ///
    /// No basta con que exista una plataforma.
    /// Si es móvil, debe estar cerca y venir hacia el agente o estar quieta.
    /// </summary>
    private bool HayObjetivosDisponibles()
    {
        for (int i = 0; i < MAX_OBJETIVOS; i++)
        {
            Transform objetivo = objetivosActuales[i];

            if (objetivo == null)
                continue;

            if (objetivo.gameObject.layer == layerSuelo &&
                EstaEnConoHaciaJugador(objetivo) &&
                AcercaAlJugador(objetivo))
            {
                return true;
            }

            if (PlataformaDisponible(objetivo))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Una plataforma móvil se considera disponible si:
    /// - está suficientemente cerca;
    /// - y está quieta o se mueve hacia el agente.
    /// </summary>
    private bool PlataformaDisponible(Transform objetivo)
    {
        if (!objetivo.TryGetComponent<Rigidbody>(out Rigidbody rbObjetivo))
            return false;

        Vector3 velocidad = rbObjetivo.linearVelocity;
        Vector3 direccionHaciaMi = transform.position - objetivo.position;

        if (direccionHaciaMi.sqrMagnitude < 0.01f)
            return true;

        bool estaCerca = DistanciaAlBordeDelObjetivo(objetivo) < distanciaMaxima * 0.8f;

        bool vieneHaciaMi =
            velocidad.sqrMagnitude < 0.01f ||
            Vector3.Dot(
                velocidad.normalized,
                direccionHaciaMi.normalized
            ) > 0f;

        return estaCerca && vieneHaciaMi;
    }

    private bool AcercaAlJugador(Transform objetivo)
    {
        if (jugador == null)
            return false;

        float distanciaActual = Vector3.SqrMagnitude(
            jugador.position - transform.position
        );

        float distanciaDesdeObjetivo = Vector3.SqrMagnitude(
            jugador.position - objetivo.position
        );

        return distanciaDesdeObjetivo < distanciaActual;
    }

    /// <summary>
    /// Calcula la distancia desde los pies del agente hasta la parte
    /// más cercana del collider del objetivo.
    ///
    /// Esto es mejor que usar objetivo.position, porque position suele ser
    /// el centro del objeto, no el borde real de la plataforma.
    /// </summary>
    private float DistanciaAlBordeDelObjetivo(Transform objetivo)
    {
        if (puntoPies == null)
            return float.MaxValue;

        Collider col = objetivo.GetComponent<Collider>();

        if (col == null)
            return float.MaxValue;

        Vector3 puntoMasCercano = col.ClosestPoint(puntoPies.position);

        return Vector3.Distance(
            puntoPies.position,
            puntoMasCercano
        );
    }

    private void LimpiarObjetivosActuales()
    {
        for (int i = 0; i < MAX_OBJETIVOS; i++)
            objetivosActuales[i] = null;
    }

    /// <summary>
    /// En modo Heuristic, al hacer click se elige automáticamente
    /// el objetivo que más acerca al jugador.
    /// </summary>
    private int ObtenerMejorObjetivoManual()
    {
        int mejorIndice = -1;
        float mejorDistanciaAlJugador = float.MaxValue;

        for (int i = 0; i < MAX_OBJETIVOS; i++)
        {
            Transform objetivo = objetivosActuales[i];

            if (objetivo == null)
                continue;

            float distanciaAlJugador = jugador != null
                ? Vector3.SqrMagnitude(objetivo.position - jugador.position)
                : Vector3.SqrMagnitude(objetivo.position - transform.position);

            if (distanciaAlJugador < mejorDistanciaAlJugador)
            {
                mejorDistanciaAlJugador = distanciaAlJugador;
                mejorIndice = i;
            }
        }

        return mejorIndice;
    }

    /// <summary>
    /// Ejecuta físicamente el salto.
    ///
    /// La dirección va hacia el objetivo.
    /// La fuerza horizontal depende de la distancia:
    /// - objetivo cercano: fuerza menor;
    /// - objetivo lejano: fuerza mayor;
    /// - nunca supera fuerzaHorizontalMaxima.
    /// </summary>
    private void SaltarHacia(Transform objetivo)
    {
        instanteUltimoSalto = Time.time;

        rb.linearVelocity = Vector3.zero;

        Vector3 desplazamiento = objetivo.position - transform.position;
        desplazamiento.y = 0f;

        float distancia = desplazamiento.magnitude;

        if (distancia < 0.01f)
            return;

        Vector3 direccion = desplazamiento.normalized;

        float factorDistancia = Mathf.Clamp01(distancia / distanciaSaltoMaxima);

        float fuerzaHorizontalCalculada = Mathf.Lerp(
            fuerzaHorizontalMinima,
            fuerzaHorizontalMaxima,
            factorDistancia
        );

        rb.AddForce(
            direccion * fuerzaHorizontalCalculada +
            Vector3.up * fuerzaVertical,
            ForceMode.Impulse
        );
    }

    private void GirarHacia(Vector3 destino)
    {
        Vector3 direccion = destino - transform.position;
        direccion.y = 0f;

        if (direccion.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(direccion);
    }

    /// <summary>
    /// Comprueba si los pies del agente tocan una superficie válida.
    /// </summary>
    private bool EstaApoyado()
    {
        if (puntoPies == null)
            return false;

        return Physics.CheckSphere(
            puntoPies.position,
            radioPies,
            mascaraApoyo
        );
    }

    public bool EstaEnSuelo()
    {
        if (puntoPies == null)
            return false;

        return Physics.CheckSphere(
            puntoPies.position,
            radioPies,
            mascaraSuelo
        );
    }

    private bool EstaEnPlataforma()
    {
        return apoyoActual != null &&
               apoyoActual.gameObject.layer == layerPlataforma;
    }

    /// <summary>
    /// El agente solo puede saltar si:
    /// - está apoyado;
    /// - ha pasado el tiempo mínimo desde el último salto.
    /// </summary>
    private bool PuedeSaltar()
    {
        bool haPasadoTiempo =
            Time.time - instanteUltimoSalto >= tiempoMinimoEntreSaltos;

        return EstaApoyado() && haPasadoTiempo;
    }

    /// <summary>
    /// Registra la superficie sobre la que ha aterrizado.
    ///
    /// Si es plataforma, se hace hijo para moverse con ella.
    /// Si es suelo, no necesita padre.
    /// </summary>
    private void VincularAApoyo(Transform nuevoApoyo)
    {
        apoyoActual = nuevoApoyo;

        if (nuevoApoyo.gameObject.layer == layerPlataforma)
            transform.SetParent(nuevoApoyo);
        else
            transform.SetParent(null);
    }

    private void DesvincularDeApoyo()
    {
        apoyoActual = null;
        transform.SetParent(null);
    }

    private void FixedUpdate()
    {
        if (Time.time - tiempoInicio < 0.2f)
            return;

        if (PuedeSaltar())
            RequestDecision();

        if (jugador == null)
            return;

        float distanciaJugador = Vector3.Distance(
            transform.position,
            jugador.position
        );

        AddReward(-distanciaJugador * 0.0005f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == layerMuerte)
        {
            DesvincularDeApoyo();
            AddReward(-1f);
            EndEpisode();
            return;
        }

        if (collision.gameObject.layer == layerPlataforma ||
            collision.gameObject.layer == layerSuelo)
        {
            VincularAApoyo(collision.transform);
        }

        RecompensarSiSeAcercaAlJugador();

        if (EstaEnLaMismaSuperficieQueJugador())
        {
            AddReward(4f);
            //EndEpisode();
        }
    }

    /// <summary>
    /// Premia cada aterrizaje que reduzca la distancia al jugador.
    /// </summary>
    private void RecompensarSiSeAcercaAlJugador()
    {
        if (jugador == null)
            return;

        float distanciaJugador = Vector3.Distance(
            transform.position,
            jugador.position
        );

        if (distanciaJugador < anteriorDistanciaJugador)
        {
            AddReward(0.5f);
            ultimoObjetivo = null;
            anteriorDistanciaJugador = distanciaJugador;
        }
    }

    /// <summary>
    /// Termina el episodio cuando agente y jugador están sobre la misma superficie.
    /// </summary>
    private bool EstaEnLaMismaSuperficieQueJugador()
    {
        if (jugador == null || apoyoActual == null)
            return false;

        if (apoyoActual.gameObject.layer == layerPlataforma)
            return jugador.parent == apoyoActual;

        if (apoyoActual.gameObject.layer == layerSuelo)
            return jugador.parent == null;

        return false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> acciones = actionsOut.DiscreteActions;

        acciones[0] = accionManual;

        accionManual = 0;
    }

    private void OnClick()
    {
        ActualizarObjetivosAlcanzables();

        int mejorIndice = ObtenerMejorObjetivoManual();

        if (mejorIndice >= 0)
            accionManual = mejorIndice + 1;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, radioDeteccionObjetivos);

        if (puntoPies != null)
            Gizmos.DrawWireSphere(puntoPies.position, radioPies);
    }
}
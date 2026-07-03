using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class ControladorCambioHibridoAgente : MonoBehaviour
{
    private enum ModoControl
    {
        Hibrido,
        AgenteEntrenado
    }

    [Header("Referencias")]
    [SerializeField] private EnemigoPercepcionNavMeshCompleto enemigoPercepcionNavMeshFinal;
    [SerializeField] private AgenteAvanzadoFinal agenteEntrenado;
    [SerializeField] private Transform jugador;

    [Header("Capas")]
    [SerializeField] private LayerMask capaZonaSalto;
    [SerializeField] private LayerMask capaPlataforma;
    [SerializeField] private LayerMask capaSuelo;

    [Header("Detección")]
    [SerializeField] private float radioDeteccionPlataformas = 6f;

    private NavMeshAgent navMeshAgent;
    private Rigidbody rb;

    private ModoControl modoActual = ModoControl.Hibrido;
    private bool enZonaSalto;
    private bool enSuelo;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        ActivarModoHibrido();
    }

    private void Update()
    {
        
        //Debug.Log($"Modo actual: {modoActual}");
        if (modoActual == ModoControl.Hibrido)
        {
            if (DebeActivarAgenteEntrenado())
            {
                ActivarModoAgenteEntrenado();
            }
        }
        else
        {
            //Debug.Log($"Jugador en otra superficie: {JugadorEnOtraSuperficie()}");
            //Debug.Log(enZonaSalto ? "En zona salto" : "No en zona salto");
            //Debug.Log(agenteEntrenado.EstaEnSuelo() ? "En suelo" : "No en suelo");
            //Si el jugador no está en otra superficie y no estoy en plataforma, en plataforma no puedo activar el modo hibrido, porque me quedaría flotando en el aire. Por eso agrego la condición de que no esté en otra superficie.
            if (!JugadorEnOtraSuperficie() && (enZonaSalto || agenteEntrenado.EstaEnSuelo()))   
            {
                ActivarModoHibrido();
            }
        }
    }

    private bool DebeActivarAgenteEntrenado()
    {
        //Debug.Log($"En zona salto: {enZonaSalto}");
        if (!enZonaSalto)
            return false;

        //Debug.Log($"Jugador en otra superficie: {JugadorEnOtraSuperficie()}");
        if (!JugadorEnOtraSuperficie())
            return false;

        //Debug.Log($"Hay plataformas cerca: {HayPlataformasCerca()}");
        if (!HayPlataformasCerca())
            return false;

        return true;
    }

    private bool JugadorEnOtraSuperficie()
    {
        if (jugador == null)
            return false;

        Transform apoyoEnemigo = ObtenerSuperficieActual(transform);
        Transform apoyoJugador = ObtenerSuperficieActual(jugador);

        if (apoyoEnemigo == null || apoyoJugador == null)
            return false;

        return apoyoEnemigo != apoyoJugador;
    }

    private Transform ObtenerSuperficieActual(Transform personaje)
    {
        // Si es hijo de una plataforma, esa es su superficie actual.
        if (personaje.parent != null)
            return personaje.parent;

        // Si no tiene padre, buscamos el suelo justo debajo.
        if (Physics.Raycast(
                personaje.position + Vector3.up * 0.2f,
                Vector3.down,
                out RaycastHit hit,
                1.5f,
                capaSuelo
            ))
        {
            return hit.collider.transform;
        }

        return null;
    }



    private bool HayPlataformasCerca()
    {
        Collider[] plataformas = Physics.OverlapSphere(
            transform.position,
            radioDeteccionPlataformas,
            capaPlataforma
        );

        return plataformas.Length > 0;
    }

    private void ActivarModoAgenteEntrenado()
    {
        modoActual = ModoControl.AgenteEntrenado;

        if (navMeshAgent.enabled)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.enabled = false;
        }

        rb.isKinematic = false;

        if (enemigoPercepcionNavMeshFinal != null)
            enemigoPercepcionNavMeshFinal.enabled = false;

        if (agenteEntrenado != null)
            agenteEntrenado.enabled = true;
    }

    private void ActivarModoHibrido()
    {
        modoActual = ModoControl.Hibrido;

        if (agenteEntrenado != null)
            agenteEntrenado.enabled = false;

        if (enemigoPercepcionNavMeshFinal != null)
            enemigoPercepcionNavMeshFinal.enabled = true;

        transform.SetParent(null);



        if (!NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                3f,
                NavMesh.AllAreas
            ))
        {

                 rb.isKinematic = false;
                 return;
        }
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            navMeshAgent.enabled = true;
            navMeshAgent.Warp(hit.position);
            navMeshAgent.ResetPath();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (EstaEnCapa(other.gameObject, capaZonaSalto))
        {
            enZonaSalto = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (EstaEnCapa(other.gameObject, capaZonaSalto))
        {
            enZonaSalto = false;
        }
    }

    private bool EstaEnCapa(GameObject objeto, LayerMask mascara)
    {
        return (mascara.value & (1 << objeto.layer)) != 0;
    }

}

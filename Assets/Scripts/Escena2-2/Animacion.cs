using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class AnimacionHibridaPersonaje : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerControllerHibridoAdv controlador;

    [Header("Parámetros del Animator")]
    [SerializeField] private string parametroVelocidad = "Velocidad";
    [SerializeField] private string parametroLateral = "Lateral";
    [SerializeField] private string parametroVelocidadVertical = "VelocidadVertical";

    [SerializeField] private string parametroEnSuelo = "EnSuelo";
    [SerializeField] private string parametroSaltando = "Saltando";
    [SerializeField] private string parametroCayendo = "Cayendo";
    [SerializeField] private string parametroEscalando = "Escalando";
    [SerializeField] private string parametroEnPlataforma = "EnPlataforma";

    [Header("Nombres de NavMeshLink")]
    [SerializeField] private string nombreLinkEscalera = "EscaleraVertical";
    [SerializeField] private string nombreLinkSaltoPequeno = "SaltoPequeño";
    [SerializeField] private string nombreLinkSaltoGrande = "SaltoGrande";

    [Header("Auto Link")]
    [SerializeField] private float tiempoMantenerSaltoLink = 0.25f;

    [Header("Suavizado")]
    [SerializeField] private float suavizadoMovimiento = 0.15f;
    [SerializeField] private float suavizadoVertical = 0.10f;

    [Header("Umbrales")]
    [SerializeField] private float velocidadCaida = -0.2f;

    private Animator anim;
    private NavMeshAgent agent;
    private Rigidbody rb;

    private Vector3 posicionAnterior;

    private float velocidadSuavizada;
    private float lateralSuavizado;
    private float verticalSuavizada;

    private float tiempoUltimoSaltoLink;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        if (controlador == null)
        {
            controlador = GetComponent<PlayerControllerHibridoAdv>();
        }

        posicionAnterior = transform.position;

        /*
         * El movimiento real lo hacen:
         * - NavMeshAgent
         * - Rigidbody
         * - Corrutinas de OffMeshLink
         *
         * El Animator solo representa visualmente.
         */
        anim.applyRootMotion = false;
    }

    private void Update()
    {
        if (controlador == null)
        {
            return;
        }

        Vector3 velocidadMundo = ObtenerVelocidadMundo();
        Vector3 velocidadLocal = transform.InverseTransformDirection(velocidadMundo);

        velocidadSuavizada = Mathf.Lerp(
            velocidadSuavizada,
            velocidadLocal.z,
            suavizadoMovimiento
        );

        lateralSuavizado = Mathf.Lerp(
            lateralSuavizado,
            velocidadLocal.x,
            suavizadoMovimiento
        );

        verticalSuavizada = Mathf.Lerp(
            verticalSuavizada,
            velocidadMundo.y,
            suavizadoVertical
        );

        bool modoRigidBody = controlador.ModoRigidBody;
        bool enPlataforma = controlador.EnPlataforma;

        bool linkEscalera = false;
        bool linkSaltoPequeno = false;
        bool linkSaltoGrande = false;

        /*
         * Detección de OffMeshLinks.
         *
         * EscaleraVertical → Escalando
         * SaltoPequeño     → Saltando
         * SaltoGrande      → Saltando + Cayendo
         */
       string nombreLink = "";

if (agent.enabled && agent.isOnOffMeshLink)
{
    OffMeshLinkData data = agent.currentOffMeshLinkData;

    if (data.owner is NavMeshLink navMeshLink)
    {
        nombreLink = navMeshLink.gameObject.name;
    }
    else if (data.owner is OffMeshLink offMeshLink)
    {
        nombreLink = offMeshLink.gameObject.name;
    }

    if (nombreLink.Contains(nombreLinkEscalera))
    {
        linkEscalera = true;
    }
    else if (nombreLink.Contains(nombreLinkSaltoPequeno))
    {
        linkSaltoPequeno = true;
        tiempoUltimoSaltoLink = Time.time;
    }
    else if (nombreLink.Contains(nombreLinkSaltoGrande))
    {
        linkSaltoGrande = true;
        tiempoUltimoSaltoLink = Time.time;
    }
}
        /*
         * Mantiene el estado Saltando durante unas décimas.
         * Esto ayuda cuando el OffMeshLink automático dura
         * muy pocos frames y el Animator no llega a detectarlo.
         */
        bool saltandoLinkTemporal =
            Time.time - tiempoUltimoSaltoLink < tiempoMantenerSaltoLink;

        bool escalando =
            controlador.UsandoLink ||
            linkEscalera;

        bool saltando =
            (modoRigidBody && verticalSuavizada > 0.1f) ||
            linkSaltoPequeno ||
            linkSaltoGrande ||
            saltandoLinkTemporal;

        bool cayendo =
            (modoRigidBody && verticalSuavizada < velocidadCaida) ||
            (linkSaltoGrande && verticalSuavizada < velocidadCaida);

        bool enSuelo =
            !modoRigidBody &&
            !escalando &&
            !saltando &&
            !cayendo;

        float velocidadNormalizada = 0f;

        if (agent.speed > 0f)
        {
            velocidadNormalizada = Mathf.Clamp(
                velocidadSuavizada / agent.speed,
                -1f,
                1f
            );
        }

        float lateralNormalizado = 0f;

        if (agent.speed > 0f)
        {
            lateralNormalizado = Mathf.Clamp(
                lateralSuavizado / agent.speed,
                -1f,
                1f
            );
        }

        anim.SetFloat(parametroVelocidad, velocidadNormalizada);
        anim.SetFloat(parametroLateral, lateralNormalizado);
        anim.SetFloat(parametroVelocidadVertical, verticalSuavizada);

        anim.SetBool(parametroEnSuelo, enSuelo);
        anim.SetBool(parametroSaltando, saltando);
        anim.SetBool(parametroCayendo, cayendo);
        anim.SetBool(parametroEscalando, escalando);
        anim.SetBool(parametroEnPlataforma, enPlataforma);

        posicionAnterior = transform.position;
    }

    private Vector3 ObtenerVelocidadMundo()
    {
        /*
         * Durante un link manual, como la escalera,
         * el movimiento se calcula por diferencia de posición.
         */
        if (controlador.UsandoLink)
        {
            return (transform.position - posicionAnterior) / Time.deltaTime;
        }

        /*
         * Durante saltos o plataformas manda el Rigidbody.
         */
        if (controlador.ModoRigidBody)
        {
            return rb.linearVelocity;
        }

        /*
         * En navegación normal manda el NavMeshAgent.
         */
        if (agent.enabled)
        {
            return agent.velocity;
        }

        return Vector3.zero;
    }
}
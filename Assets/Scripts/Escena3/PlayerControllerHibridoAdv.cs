using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Renderer))]
public class PlayerControllerHibridoAdv : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera cam;

    [Header("Capas")]
    [SerializeField] private LayerMask capaSuelo;
    [SerializeField] private LayerMask capaPlataforma;
    [SerializeField] private LayerMask capaMuerte;

    [Header("Salto")]
    [SerializeField] private float fuerzaArriba = 7f;
    [SerializeField] private float fuerzaAdelante = 5f;

    [Header("Escalada")]
    [SerializeField] private float duracionEscalada = 0.5f;
    private bool usandoLink;

    [Header("Configuración")]
    [SerializeField] private float tiempoIgnorarSueloTrasSalto = 0.2f;

    [Header("Colores")]
    [SerializeField] private Color colorNormal = Color.white;
    [SerializeField] private Color colorRigidBody = Color.yellow;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Renderer rend;

    private Vector3 posicionInicial;

    private bool modoRigidBody;
    private bool enPlataforma;
    private bool puedeSaltar = true;

    private float tiempoInicioSalto;

    public bool ModoRigidBody => modoRigidBody;
    public bool UsandoLink => usandoLink;
    public bool EnPlataforma => enPlataforma;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();

        if (cam == null)
        {
            cam = Camera.main;
        }

        posicionInicial = transform.position;

        ActivarModoAgent();
    }

    /*
     * Entrada principal del jugador.
     *
     * - Click en suelo: movimiento con NavMeshAgent.
     * - Click en plataforma: salto físico.
     * - Si ya está en plataforma: cualquier click provoca salto.
     */
    public void OnClick(InputValue value)
    {
        if (value.Get<float>() <= 0f) return;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return;
        }

        GameObject objetoClick = hit.collider.gameObject;

        if (enPlataforma)
        {
            SaltarHacia(hit.point);
            return;
        }

        if (EstaEnCapa(objetoClick, capaPlataforma))
        {
            SaltarHacia(hit.point);
            return;
        }

        if (EstaEnCapa(objetoClick, capaSuelo) && !modoRigidBody)
        {
            agent.SetDestination(hit.point);
        }
    }

    private void Update()
    {
        /*
         * Si el agente entra en un OffMeshLink,
         * detenemos el movimiento automático y
         * ejecutamos nuestra acción especial.
         */
        if (agent.enabled && agent.isOnOffMeshLink && !usandoLink)
        {
            OffMeshLinkData data = agent.currentOffMeshLinkData;

            NavMeshLink link = data.owner as NavMeshLink;

            if (link != null && link.gameObject.name == "EscaleraVertical")
            {
                StartCoroutine(EscalarVertical(data));
            }
        }
    }

    /*
     * Escalada vertical con OffMeshLink.
     *
     * Si sube:
     * 1. Sube verticalmente.
     * 2. Entra hacia el NavMesh superior.
     *
     * Si baja:
     * 1. Se coloca sobre la vertical de la escalera.
     * 2. Baja verticalmente.
     */
    private IEnumerator EscalarVertical(OffMeshLinkData data)
    {
        usandoLink = true;

        agent.isStopped = true;
        agent.autoTraverseOffMeshLink = false;
        agent.updatePosition = false;
        agent.updateRotation = false;

        Vector3 inicio = transform.position;

        /*
         * finNavMesh es el punto real del NavMesh.
         * fin es el punto visual del personaje, ajustado
         * con la mitad de la altura del agente.
         */
        Vector3 finNavMesh = data.endPos;
        Vector3 fin = finNavMesh + Vector3.up * (agent.height * 0.5f);

        bool subiendo = fin.y > inicio.y;

        /*
         * Punto intermedio:
         *
         * - Subiendo: primero cambia la altura.
         * - Bajando: primero se coloca en la vertical
         *   de la escalera.
         */
        Vector3 puntoIntermedio = subiendo
            ? new Vector3(inicio.x, fin.y, inicio.z)
            : new Vector3(fin.x, inicio.y, fin.z);

        /*
         * Primera fase del movimiento.
         */
        for (float t = 0; t < 1; t += Time.deltaTime / duracionEscalada)
        {
            float suavizado = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(
                inicio,
                puntoIntermedio,
                suavizado
            );

            yield return null;
        }

        transform.position = puntoIntermedio;

        /*
         * Segunda fase del movimiento.
         */
        for (float t = 0; t < 1; t += Time.deltaTime / duracionEscalada)
        {
            float suavizado = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(
                puntoIntermedio,
                fin,
                suavizado
            );

            yield return null;
        }

        transform.position = fin;

        /*
         * Sincronizamos internamente el NavMeshAgent
         * con el punto real del NavMesh.
         */
        agent.nextPosition = finNavMesh;

        /*
         * Indicamos que el OffMeshLink ha terminado.
         */
        agent.CompleteOffMeshLink();

        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.autoTraverseOffMeshLink = true;
        agent.isStopped = false;

        usandoLink = false;
    }

    /*
     * Salto físico hacia el punto pulsado.
     */
    private void SaltarHacia(Vector3 puntoDestino)
    {
        if (!puedeSaltar) return;

        ActivarModoRigidBody();

        puedeSaltar = false;
        enPlataforma = false;

        tiempoInicioSalto = Time.time;

        transform.SetParent(null);

        Vector3 direccion = puntoDestino - transform.position;
        float distancia = direccion.magnitude;

        direccion.y = 0f;
        direccion.Normalize();

        if (direccion != Vector3.zero)
        {
            transform.forward = direccion;
        }

        float fuerzaHorizontal = Mathf.Clamp(
            distancia,
            0f,
            fuerzaAdelante
        );

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 fuerzaFinal =
            direccion * fuerzaHorizontal +
            Vector3.up * fuerzaArriba;

        rb.AddForce(fuerzaFinal, ForceMode.Impulse);
    }

    /*
     * Detecta aterrizajes, plataformas y zonas de muerte.
     */
    private void OnCollisionEnter(Collision collision)
    {
        GameObject objeto = collision.gameObject;

        /*
         * Evita que el suelo reactive el NavMeshAgent
         * justo al iniciar el salto.
         */
        if (Time.time - tiempoInicioSalto < tiempoIgnorarSueloTrasSalto)
        {
            return;
        }

        if (EstaEnCapa(objeto, capaMuerte))
        {
            Respawn();
            return;
        }

        if (EstaEnCapa(objeto, capaPlataforma))
        {
            ActivarModoRigidBody();

            enPlataforma = true;
            puedeSaltar = true;

            /*
             * El jugador se mueve junto con la plataforma.
             */
            transform.SetParent(collision.transform);

            return;
        }

        if (EstaEnCapa(objeto, capaSuelo))
        {
            ActivarModoAgent();
        }
    }

    /*
     * Activa el modo Rigidbody.
     *
     * Se usa durante saltos y plataformas.
     */
    private void ActivarModoRigidBody()
    {
        modoRigidBody = true;

        if (agent.enabled)
        {
            agent.ResetPath();
            agent.enabled = false;
        }

        rb.isKinematic = false;

        rend.material.color = colorRigidBody;
    }

    /*
     * Activa el modo NavMeshAgent.
     *
     * Se usa al volver al suelo navegable.
     */
    private void ActivarModoAgent()
    {
        modoRigidBody = false;
        enPlataforma = false;
        puedeSaltar = true;

        transform.SetParent(null);

        

        /*
         * Buscamos el punto navegable más cercano
         * antes de reactivar el agente.
         */

        if (!NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                3f,
                NavMesh.AllAreas
            ))
        {
            // No hay NavMesh cerca: seguimos en Rigidbody. Por ejemplo, si el jugador golpea a la pared
            rb.isKinematic = false;
            modoRigidBody = true;
            return;
        }
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        agent.enabled = true;
        agent.Warp(hit.position);
        agent.ResetPath();

        rend.material.color = colorNormal;
    }

    /*
     * Devuelve al jugador al punto inicial.
     */
    private void Respawn()
    {
        transform.SetParent(null);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        agent.enabled = false;

        transform.position = posicionInicial;

        agent.enabled = true;
        agent.Warp(posicionInicial);
        agent.ResetPath();

        modoRigidBody = false;
        enPlataforma = false;
        puedeSaltar = true;
        usandoLink = false;

        rend.material.color = colorNormal;
    }

    /*
     * Comprueba si un objeto pertenece
     * a una LayerMask concreta.
     */
    private bool EstaEnCapa(GameObject objeto, LayerMask mascara)
    {
        return (mascara.value & (1 << objeto.layer)) != 0;
    }

    /*
     * Dibujo visual de depuración.
     */
    private void OnDrawGizmos()
    {
        if (agent != null && agent.enabled && agent.hasPath)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawSphere(
                agent.destination,
                0.3f
            );

            Gizmos.color = Color.blue;

            Gizmos.DrawLine(
                transform.position,
                transform.position + transform.forward * 2f
            );
        }
    }
}
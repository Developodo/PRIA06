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
public class PlayerControllerHibrido : MonoBehaviour
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
        if(agent.enabled)
        {
            return;
        }
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
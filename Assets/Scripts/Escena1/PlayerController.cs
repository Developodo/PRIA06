using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera cam;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    /**
     * Método llamado automáticamente desde el componente
     * PlayerInput mediante Send Messages.
     *
     * Acción Input Action:
     * Player/Click
     */
    public void OnClick(InputValue value)
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            agent.SetDestination(hit.point);
        }
    }

    /**
     * Dibuja información visual de depuración.
     */
    private void OnDrawGizmos()
    {
        if (agent != null && agent.hasPath)
        {
            // Destino
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(agent.destination, 0.3f);

            // Dirección
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(
                transform.position,
                transform.position + transform.forward * 2f
            );
        }
    }
}
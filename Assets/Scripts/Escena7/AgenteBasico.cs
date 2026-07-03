/**
    python -m pip install torch==2.2.2
    python -m pip install onnx==1.15.0 protobuf==3.20.3
*/
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class AgenteBasico : Agent
{
    [Header("Referencias")]
    [SerializeField] private Transform objetivo;

    [Header("Movimiento")]
    [SerializeField] private float velocidadMovimiento = 2f;

    private Rigidbody rb;

    [Header("Episodio")]
    [SerializeField] private int maxPasosEpisodio = 500;

    private int pasosEpisodio;
    private float distanciaAnterior;
    private Vector2 movimientoManual;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        pasosEpisodio = 0;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.localPosition = new Vector3(0f, 0.5f, 0f);

        objetivo.localPosition = new Vector3(
            Random.Range(-4f, 4f),
            0.5f,
            Random.Range(-4f, 4f)
        );

        distanciaAnterior = Vector3.Distance(
            transform.localPosition,
            objetivo.localPosition
        );
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 direccion = objetivo.localPosition - transform.localPosition;

        sensor.AddObservation(Mathf.Clamp(direccion.x / 8f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(direccion.z / 8f, -1f, 1f));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {

        pasosEpisodio++;

        float movimientoX = actions.ContinuousActions[0];
        float movimientoZ = actions.ContinuousActions[1];

        Vector3 movimiento = new Vector3(
            movimientoX,
            0f,
            movimientoZ
        );

        rb.MovePosition(
            rb.position +
            movimiento * velocidadMovimiento * Time.fixedDeltaTime
        );

        float distanciaActual = Vector3.Distance(
            transform.localPosition,
            objetivo.localPosition
        );

        if (distanciaActual < distanciaAnterior)
        {
            AddReward(0.002f);
        }
        else
        {
            AddReward(-0.002f);
        }

        distanciaAnterior = distanciaActual;

        AddReward(-0.001f);

        if (distanciaActual < 1.2f)
        {
            AddReward(1f);
            EndEpisode();
        }


        if (pasosEpisodio >= maxPasosEpisodio)
        {
            SetReward(-0.5f);
            EndEpisode();
        }
        Debug.Log($" Recompensa: {GetCumulativeReward()}");
    }

    public void OnMove(InputValue value)
    {
        movimientoManual = value.Get<Vector2>();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> acciones = actionsOut.ContinuousActions;
        acciones[0] = movimientoManual.x;
        acciones[1] = movimientoManual.y;
    }
}
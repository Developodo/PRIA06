using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PlayerController2 : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cam;
    public NavMeshAgent agent;
    public Transform puntoReinicio;

    [Header("Tags")]
    public string tagPlataforma = "Plataforma";
    public string tagPlataformaSuelo = "PlataformaSuelo";
    public string tagMuerte = "Muerte";

    [Header("Salto manual")]
    public float alturaSalto = 2f;
    public float duracionSalto = 0.6f;
    public float distanciaMaximaSalto = 8f;

    private bool modoManual = false;
    public bool saltando = false;

    private Collider col;
    private Renderer[] renderers;
    private Color[] coloresOriginales;

    private Transform plataformaActual;
    private Vector3 ultimaPosicionPlataforma;

    private void Start()
    {
        col = GetComponent<Collider>();

        renderers = GetComponentsInChildren<Renderer>();
        coloresOriginales = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            coloresOriginales[i] = renderers[i].material.color;
        }

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        PonerColorNormal();
    }

    private void Update()
    {
        if (modoManual && !saltando && plataformaActual != null)
        {
            Vector3 movimientoPlataforma = plataformaActual.position - ultimaPosicionPlataforma;
            transform.position += movimientoPlataforma;
            ultimaPosicionPlataforma = plataformaActual.position;
        }

        if (Input.GetMouseButtonDown(0) && !saltando)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (modoManual)
                {
                    IntentarSaltoManual(hit);
                }
                else
                {
                    agent.SetDestination(hit.point);
                }
            }
        }
    }

    private void IntentarSaltoManual(RaycastHit hit)
    {
        bool destinoValido =
            hit.collider.CompareTag(tagPlataforma) ||
            hit.collider.CompareTag(tagPlataformaSuelo);

        if (!destinoValido)
        {
            StartCoroutine(CaerYReiniciar());
            return;
        }

        Vector3 destino = ObtenerDestino(hit);

        Vector3 origenPlano = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 destinoPlano = new Vector3(destino.x, 0, destino.z);

        float distancia = Vector3.Distance(origenPlano, destinoPlano);

        if (distancia > distanciaMaximaSalto)
        {
            StartCoroutine(CaerYReiniciar());
            return;
        }

        StartCoroutine(SaltoManual(destino, hit.collider));
    }

    private Vector3 ObtenerDestino(RaycastHit hit)
    {
        Transform aterrizaje = hit.collider.transform.Find("Aterrizaje");

        float mediaAltura = col != null ? col.bounds.extents.y : 0.5f;

        if (aterrizaje != null)
            return aterrizaje.position + Vector3.up * mediaAltura;

        return hit.point + Vector3.up * mediaAltura;
    }

    private IEnumerator SaltoManual(Vector3 destino, Collider destinoCollider)
    {
        saltando = true;
        plataformaActual = null;

        if (agent.enabled)
            agent.enabled = false;

        if (col != null)
            col.enabled = false;

        Vector3 inicio = transform.position;
        float tiempo = 0f;

        while (tiempo < duracionSalto)
        {
            float t = tiempo / duracionSalto;

            Vector3 posicion = Vector3.Lerp(inicio, destino, t);
            posicion.y += Mathf.Sin(t * Mathf.PI) * alturaSalto;

            transform.position = posicion;

            tiempo += Time.deltaTime;
            yield return null;
        }

        transform.position = destino;

        if (col != null)
            col.enabled = true;

        if (destinoCollider.CompareTag(tagPlataforma))
        {
            ActivarModoManual(destinoCollider.transform);
            saltando = false;

        }

        if (destinoCollider.CompareTag(tagPlataformaSuelo))
        {
            ActivarNavMesh();
        }

    }

    private IEnumerator CaerYReiniciar()
    {
        saltando = true;
        plataformaActual = null;

        if (agent.enabled)
            agent.enabled = false;

        if (col != null)
            col.enabled = false;

        Vector3 inicio = transform.position;
        Vector3 destino = inicio + Vector3.down * 6f;

        float tiempo = 0f;
        float duracionCaida = 0.8f;

        while (tiempo < duracionCaida)
        {
            float t = tiempo / duracionCaida;
            transform.position = Vector3.Lerp(inicio, destino, t);

            tiempo += Time.deltaTime;
            yield return null;
        }

        if (col != null)
            col.enabled = true;

        ReiniciarJugador();
    }

    private void ActivarNavMesh()
    {
        modoManual = false;
        plataformaActual = null;

        if (!agent.enabled)
            agent.enabled = true;

        agent.Warp(transform.position);
        agent.ResetPath();

        PonerColorNormal();

        Debug.Log("Modo NavMesh activado.");
    }

    private void ActivarModoManual(Transform plataforma = null)
    {
        modoManual = true;

        if (agent.enabled)
        {
            agent.ResetPath();
            agent.enabled = false;
        }

        if (plataforma != null)
        {
            plataformaActual = plataforma;
            ultimaPosicionPlataforma = plataformaActual.position;
        }

        PonerColorVerde();

        Debug.Log("Modo manual activado.");
    }

    private void AlternarModo(Transform plataforma)
    {
        Debug.Log("Alternando modo. Plataforma: " + plataforma.name);
        if (modoManual)
        {
            Debug.Log("Cambiando a NavMesh.");
            ActivarNavMesh();
        }
        else
        {
            Debug.Log("Cambiando a modo manual.");
            ActivarModoManual(plataforma);
        }
    }

    private void ReiniciarJugador()
    {
        StopAllCoroutines();

        saltando = false;
        modoManual = false;
        plataformaActual = null;

        if (col != null)
            col.enabled = true;

        if (!agent.enabled)
            agent.enabled = true;

        Vector3 posicion = puntoReinicio != null ? puntoReinicio.position : Vector3.zero;

        agent.Warp(posicion);
        agent.ResetPath();

        PonerColorNormal();

        Debug.Log("Jugador reiniciado.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(tagPlataforma))
        {
            Debug.Log("Entró en plataforma: " + other.name);
            ActivarModoManual(other.transform);
        }

        if (other.CompareTag(tagPlataformaSuelo))
        {
            if(saltando)
            {
                saltando = false;  // Evita cambiar de modo al aterrizar después de un salto
                return;
            }
            
            Debug.Log("Entró en plataforma de suelo: " + other.name);
            AlternarModo(other.transform);
        }

        if (other.CompareTag(tagMuerte))
        {
            Debug.Log("Entró en área de muerte: " + other.name);    
            ReiniciarJugador();
        }
    }

    private void PonerColorVerde()
    {
        foreach (Renderer r in renderers)
        {
            r.material.color = Color.green;
        }
    }

    private void PonerColorNormal()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = coloresOriginales[i];
        }
    }
}
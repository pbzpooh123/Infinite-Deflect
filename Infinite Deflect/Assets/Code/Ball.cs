using UnityEngine;
using Unity.Netcode;

public class Ball : NetworkBehaviour
{
    public float speed = 10f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Launch();
    }

    public void Launch()
    {
        Vector3 direction = Random.insideUnitSphere;
        rb.velocity = direction * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement player = collision.gameObject.GetComponent<PlayerMovement>();

            if (player != null && player.CanDeflect())
            {
                DeflectBall(collision);
            }
        }
    }

    void DeflectBall(Collision collision)
    {
        Vector3 incomingVelocity = rb.velocity;
        Vector3 normal = collision.contacts[0].normal;
        Vector3 reflectDir = Vector3.Reflect(incomingVelocity, normal);
        rb.velocity = reflectDir.normalized * speed * 1.2f; // Slight speed boost on deflect
    }
}
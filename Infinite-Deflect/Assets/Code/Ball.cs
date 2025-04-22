using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class GameBall : NetworkBehaviour
{
    [Header("Ball Settings")]
    [SerializeField] private float initialSpeed = 10f;
    [SerializeField] private float speedIncreasePercentage = 10f;
    [SerializeField] private int ballDamage = 1;

    private NetworkVariable<float> currentSpeed = new NetworkVariable<float>();
    private Rigidbody rb;
    private NetworkObject currentTarget;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();

        if (IsServer)
        {
            currentSpeed.Value = initialSpeed;
            SelectNewTarget();
        }
    }

    private void SelectNewTarget()
    {
        if (!IsServer) return;

        // Get all valid player objects
        List<NetworkObject> players = NetworkManager.Singleton.ConnectedClients
            .Select(client => client.Value.PlayerObject)
            .Where(player => player != null && player.CompareTag("Player"))
            .ToList();

        // Stop ball if only one or zero players
        if (players.Count <= 1)
        {
            currentTarget = null;
            rb.velocity = Vector3.zero;
            return;
        }

        // Avoid targeting same player again
        if (currentTarget != null)
        {
            players.Remove(currentTarget);
        }

        // Pick a new random target
        currentTarget = players[Random.Range(0, players.Count)];
        Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
        rb.velocity = direction * currentSpeed.Value;
    }


    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Ball hits player directly
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamageServerRpc(ballDamage);
            }

            // Increase ball speed
            float speedIncrease = currentSpeed.Value * (speedIncreasePercentage / 100f);
            currentSpeed.Value += speedIncrease;

            // Select new target
            SelectNewTarget();
        }
        // Ball hits player deflect zone
        else if (collision.gameObject.CompareTag("PlayerDeflect"))
        {
            Vector3 deflectDirection = Vector3.Reflect(rb.velocity.normalized, collision.contacts[0].normal);
            rb.velocity = deflectDirection * currentSpeed.Value;

            // Optional: pick new target on deflect
            SelectNewTarget();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetBallServerRpc()
    {
        if (!IsServer) return;

        currentSpeed.Value = initialSpeed;
        SelectNewTarget();
    }

    public float GetCurrentSpeed()
    {
        return currentSpeed.Value;
    }
}

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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentSpeed.Value = initialSpeed;
            Invoke(nameof(SelectNewTarget), 0.2f); // Allow time for players to be fully spawned
        }
    }

    private void SelectNewTarget()
    {
        if (!IsServer) return;

        List<NetworkObject> players = NetworkManager.Singleton.ConnectedClients
            .Select(client => client.Value.PlayerObject)
            .Where(player => player != null && player.CompareTag("Player"))
            .ToList();

        if (players.Count <= 1)
        {
            Debug.Log("[Ball] Not enough players to target.");
            currentTarget = null;
            rb.velocity = Vector3.zero;
            return;
        }

        if (currentTarget != null)
        {
            players.Remove(currentTarget);
        }

        currentTarget = players[Random.Range(0, players.Count)];
        Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
        rb.velocity = direction * currentSpeed.Value;

        Debug.Log($"[Ball] New target: {currentTarget.name}, velocity: {rb.velocity}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamageServerRpc(ballDamage);
            }

            float speedIncrease = currentSpeed.Value * (speedIncreasePercentage / 100f);
            currentSpeed.Value += speedIncrease;

            SelectNewTarget();
        }
        else if (collision.gameObject.CompareTag("PlayerDeflect"))
        {
            Vector3 deflectDirection = Vector3.Reflect(rb.velocity.normalized, collision.contacts[0].normal);
            rb.velocity = deflectDirection * currentSpeed.Value;

            SelectNewTarget(); // Optional: change target after deflection
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

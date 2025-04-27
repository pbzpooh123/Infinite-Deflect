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
    private NetworkObject lastDeflector; // New: track last deflecting player

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentSpeed.Value = initialSpeed;
            Invoke(nameof(SelectNewTarget), 0.2f); // Allow time for players to spawn
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
            players.Remove(currentTarget); // Avoid same player immediately after hit
        }

        currentTarget = players[Random.Range(0, players.Count)];
        Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
        rb.velocity = direction * currentSpeed.Value;

        Debug.Log($"[Ball] New target: {currentTarget.name}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            NetworkObject hitPlayer = collision.gameObject.GetComponent<NetworkObject>();

            if (hitPlayer != null && currentTarget != null && hitPlayer.NetworkObjectId == currentTarget.NetworkObjectId)
            {
                // Correct target hit
                PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamageServerRpc(ballDamage);
                }

                IncreaseSpeed();
                SelectNewTarget();
            }
            else
            {
                Debug.Log($"[Ball] Hit wrong player ({hitPlayer?.name}), ignoring.");
            }
        }
        else if (collision.gameObject.CompareTag("PlayerDeflect"))
        {
            NetworkObject deflector = collision.gameObject.GetComponentInParent<NetworkObject>();
            if (deflector != null)
            {
                lastDeflector = deflector; // Remember who deflected
            }

            IncreaseSpeed();
            SelectRandomTargetAfterDeflect();
            Debug.Log("[Ball] Deflected! Speed increased and targeting new player.");
        }
    }

    private void SelectRandomTargetAfterDeflect()
    {
        List<NetworkObject> players = NetworkManager.Singleton.ConnectedClients
            .Select(client => client.Value.PlayerObject)
            .Where(player => player != null && player.CompareTag("Player"))
            .ToList();

        if (players.Count == 0)
        {
            Debug.LogWarning("[Ball] No players to target after deflect.");
            return;
        }

        if (lastDeflector != null)
        {
            players.Remove(lastDeflector); // Remove the deflector if possible
        }

        if (players.Count == 0 && lastDeflector != null)
        {
            // Only the deflector is left, so allow targeting them
            players.Add(lastDeflector);
        }

        currentTarget = players[Random.Range(0, players.Count)];
        Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
        rb.velocity = direction * currentSpeed.Value;

        Debug.Log($"[Ball] New random target after deflect: {currentTarget.name}");
        lastDeflector = null; // Clear after picking new target
    }

    private void IncreaseSpeed()
    {
        float speedIncrease = currentSpeed.Value * (speedIncreasePercentage / 100f);
        currentSpeed.Value += speedIncrease;
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

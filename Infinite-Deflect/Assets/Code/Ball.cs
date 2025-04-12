using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameBall : NetworkBehaviour
{
    [Header("Ball Settings")]
    [SerializeField] private float initialSpeed = 10f;
    [SerializeField] private float speedIncreasePercentage = 10f;
    [SerializeField] private float minTimeBetweenTargetChanges = 0.5f;
    [SerializeField] private int ballDamage = 1; // Damage dealt to players

    private NetworkVariable<float> currentSpeed = new NetworkVariable<float>();
    private Rigidbody rb;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();

        if (IsServer)
        {
            currentSpeed.Value = initialSpeed;
            StartCoroutine(SelectNewTargetCoroutine());
            SelectNewTarget(); // Start moving immediately
        }
    }

    private IEnumerator SelectNewTargetCoroutine()
    {
        while (true)
        {
            SelectNewTarget();
            yield return new WaitForSeconds(minTimeBetweenTargetChanges);
        }
    }

    private void SelectNewTarget()
    {
        if (!IsServer) return;

        // Find all connected players
        List<NetworkObject> players = NetworkManager.Singleton.ConnectedClients
            .Select<KeyValuePair<ulong, NetworkClient>, NetworkObject>(client => client.Value.PlayerObject)
            .Where(player => player != null && player.CompareTag("Player"))
            .ToList();

        if (players.Count == 0)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // Pick a random player to target
        NetworkObject targetPlayer = players[Random.Range(0, players.Count)];

        // Calculate direction
        Vector3 direction = (targetPlayer.transform.position - transform.position).normalized;

        // Apply velocity
        rb.linearVelocity = direction * currentSpeed.Value;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // If ball hits a player directly
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamageServerRpc(ballDamage);
            }

            // Increase speed by percentage
            float speedIncrease = currentSpeed.Value * (speedIncreasePercentage / 100f);
            currentSpeed.Value += speedIncrease;

            // Pick new target
            SelectNewTarget();
        }
        // If ball hits a player's deflect zone
        else if (collision.gameObject.CompareTag("PlayerDeflect"))
        {
            Vector3 deflectDirection = Vector3.Reflect(rb.linearVelocity.normalized, collision.contacts[0].normal);
            rb.linearVelocity = deflectDirection * currentSpeed.Value;
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

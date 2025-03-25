using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;

public class GameBall : NetworkBehaviour
{
    [Header("Ball Settings")]
    [SerializeField] private float initialSpeed = 10f;
    [SerializeField] private float speedIncreasePercentage = 10f;
    [SerializeField] private float minTimeBetweenTargetChanges = 0.1f;
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

        var players = FindObjectsOfType<NetworkObject>()
            .Where(no => no.CompareTag("Player"))
            .ToList();
        
        if (players.Count == 0) return;

        var targetPlayer = players[Random.Range(0, players.Count)];
        Vector3 direction = (targetPlayer.transform.position - transform.position).normalized;
        
        rb.linearVelocity = direction * currentSpeed.Value;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            // Apply damage to the player
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamageServerRpc(ballDamage);
            }

            // Reset ball speed to original
            currentSpeed.Value = initialSpeed;

            // Pick a new target
            SelectNewTarget();
        }
        else if (collision.gameObject.CompareTag("PlayerDeflect"))
        {
            Vector3 deflectDirection = Vector3.Reflect(
                rb.linearVelocity.normalized, 
                collision.contacts[0].normal
            );
            
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

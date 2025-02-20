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
    
    // Networked speed value so all clients know the current speed
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

        // Find all players in the game
        var players = FindObjectsOfType<NetworkObject>()
            .Where(no => no.CompareTag("Player"))
            .ToList();
        
        if (players.Count == 0) return;

        // Pick a random player to target
        var targetPlayer = players[Random.Range(0, players.Count)];
        
        // Calculate direction to the chosen player
        Vector3 direction = (targetPlayer.transform.position - transform.position).normalized;
        
        // Set the ball's velocity
        rb.velocity = direction * currentSpeed.Value;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // If ball hits a player directly
        if (collision.gameObject.CompareTag("Player"))
        {
            // Increase speed by percentage
            float speedIncrease = currentSpeed.Value * (speedIncreasePercentage / 100f);
            currentSpeed.Value += speedIncrease;
            
            // Find new target
            SelectNewTarget();
        }
        // If ball hits a player's deflect zone
        else if (collision.gameObject.CompareTag("PlayerDeflect"))
        {
            // Calculate deflection direction
            Vector3 deflectDirection = Vector3.Reflect(
                rb.velocity.normalized, 
                collision.contacts[0].normal
            );
            
            // Apply deflected velocity
            rb.velocity = deflectDirection * currentSpeed.Value;
        }
    }

    // Optional: Add method to reset ball speed
    [ServerRpc(RequireOwnership = false)]
    public void ResetBallServerRpc()
    {
        if (!IsServer) return;
        currentSpeed.Value = initialSpeed;
        SelectNewTarget();
    }

    // Optional: Add method to get current speed (useful for UI or other mechanics)
    public float GetCurrentSpeed()
    {
        return currentSpeed.Value;
    }
}
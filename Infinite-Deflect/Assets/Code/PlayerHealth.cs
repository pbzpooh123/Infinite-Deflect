using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 1;
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("References")]
    [SerializeField] private GameObject playerModel; // Just visuals
    [SerializeField] private Collider playerCollider; // Reference to the actual collider (not GameObject)
    [SerializeField] private MonoBehaviour movementScript; // e.g., NetworkedMovementStateManager
    [SerializeField] private Rigidbody rb;

    private Slider healthBar;
    public bool IsDead => isDead.Value;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            healthBar = FindObjectOfType<Slider>();
            if (healthBar != null)
            {
                healthBar.maxValue = maxHealth;
                healthBar.value = currentHealth.Value;
            }
        }

        currentHealth.OnValueChanged += HandleHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int previousValue, int newValue)
    {
        UpdateHealthUI(newValue);

        if (newValue <= 0 && !isDead.Value)
        {
            if (IsServer)
            {
                isDead.Value = true;
                Die();
            }
        }
    }

    private void UpdateHealthUI(int health)
    {
        if (IsOwner && healthBar != null)
        {
            healthBar.value = health;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damageAmount)
    {
        if (!IsServer) return;

        if (currentHealth.Value > 0)
        {
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - damageAmount);
        }
    }

    private void Die()
    {
        if (IsServer)
        {
            Debug.Log($"{OwnerClientId} has died!");
            DisablePlayerClientRpc();
            GameManager.Instance.CheckRoundOver();
        }
    }

    [ClientRpc]
    private void DisablePlayerClientRpc()
    {
        Debug.Log($"Disabling player on client {OwnerClientId}");

        if (playerCollider != null)
            playerCollider.enabled = false;

        if (movementScript != null)
            movementScript.enabled = false;

        if (rb != null)
        {
            rb.velocity = Vector3.zero; // 🧠 Clear velocity
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
        
        gameObject.tag = "Untagged"; 
    }

    [ClientRpc]
    public void EnablePlayerClientRpc()
    {
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // Temporarily disable movement script during teleport
        if (movementScript != null)
            movementScript.enabled = false;

        StartCoroutine(SafeTeleportRoutine());
    }

    private IEnumerator SafeTeleportRoutine()
    {
        yield return new WaitForFixedUpdate(); // wait for one physics frame
        
        RequestTeleportToSpawnServerRpc();

        // Re-enable everything AFTER movement is stable
        if (playerCollider != null)
            playerCollider.enabled = true;

        if (movementScript != null)
            movementScript.enabled = true;
        
        gameObject.tag = "Player"; 

        UpdateHealthUI(currentHealth.Value);
    }

    

    public void ForceRespawn()
    {
        if (!IsServer) return;

        currentHealth.Value = maxHealth;
        isDead.Value = false;
        EnablePlayerClientRpc();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestTeleportToSpawnServerRpc()
    {
        var teleportHandler = FindObjectOfType<TeleportHandler>();
        if (teleportHandler != null)
        {
            teleportHandler.TeleportRequestServerRpc(OwnerClientId);
        }
    }

}

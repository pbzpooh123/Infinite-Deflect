using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private GameObject playerModel;
    [SerializeField] private GameObject playerCollider;

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
            isDead.Value = true;
            Die();
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
        if (playerModel != null) playerModel.SetActive(false);
        if (playerCollider != null) playerCollider.SetActive(false);

        var movement = GetComponent<NetworkedMovementStateManager>();
        if (movement != null)
        {
            movement.enabled = false;
        }
    }

    [ClientRpc]
    public void EnablePlayerClientRpc()
    {
        if (playerModel != null) playerModel.SetActive(true);
        if (playerCollider != null) playerCollider.SetActive(true);

        var movement = GetComponent<NetworkedMovementStateManager>();
        if (movement != null)
        {
            movement.enabled = true;
        }

        isDead.Value = false;
        UpdateHealthUI(currentHealth.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RespawnServerRpc(Vector3 spawnPosition)
    {
        if (!IsServer) return;

        transform.position = spawnPosition;
        currentHealth.Value = maxHealth;
        isDead.Value = false;

        EnablePlayerClientRpc();
    }

    [ClientRpc]
    public void TeleportClientRpc(Vector3 newPosition)
    {
        transform.position = newPosition;
    }
}

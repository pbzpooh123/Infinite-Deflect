using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3; // Set max HP to 3

    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        3, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner
    );

    [Header("References")]
    [SerializeField] private GameObject playerModel;
    [SerializeField] private GameObject playerCollider;

    private Slider healthBar; // UI Health Bar (found at runtime)

    // Events for health changes
    public event Action<int> OnHealthChanged;
    public event Action OnDeath;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Find the UI dynamically after spawning
            healthBar = FindObjectOfType<Slider>(); // Assumes only one health bar for the local player
            if (healthBar != null)
            {
                healthBar.maxValue = maxHealth;
                healthBar.value = currentHealth.Value; // Initialize UI
            }

            currentHealth.OnValueChanged += HandleHealthChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            currentHealth.OnValueChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(int previousValue, int newValue)
    {
        UpdateHealthUI(newValue);
        OnHealthChanged?.Invoke(newValue);

        if (newValue <= 0)
        {
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

    [ServerRpc]
    public void TakeDamageServerRpc(int damageAmount)
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damageAmount);
        Debug.Log($"Player {OwnerClientId} took {damageAmount} damage. Remaining Health: {currentHealth.Value}");
    }

    private void Die()
    {
        if (IsOwner)
        {
            DisablePlayerClientRpc();
            OnDeath?.Invoke();
        }
    }

    [ClientRpc]
    private void DisablePlayerClientRpc()
    {
        if (playerModel != null) playerModel.SetActive(false);
        if (playerCollider != null) playerCollider.SetActive(false);
    }

    public int GetCurrentHealth()
    {
        return currentHealth.Value;
    }

    [ServerRpc]
    public void HealServerRpc(int healAmount)
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + healAmount);
    }

    [ServerRpc]
    public void KillPlayerServerRpc()
    {
        if (!IsServer) return;
        currentHealth.Value = 0;
    }

    [ServerRpc]
    public void ResetHealthServerRpc()
    {
        if (!IsServer) return;
        currentHealth.Value = maxHealth;
        EnablePlayerClientRpc();
    }

    [ClientRpc]
    private void EnablePlayerClientRpc()
    {
        if (playerModel != null) playerModel.SetActive(true);
        if (playerCollider != null) playerCollider.SetActive(true);
        UpdateHealthUI(currentHealth.Value);
    }
}

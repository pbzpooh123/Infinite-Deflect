using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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
            if (IsServer) // ✅ Only the server sets this
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
        Debug.Log("EnableClient on client: " + NetworkManager.Singleton.LocalClientId);

        if (playerModel != null) playerModel.SetActive(true);
        if (playerCollider != null) playerCollider.SetActive(true);

        var movement = GetComponent<NetworkedMovementStateManager>();
        if (movement != null)
        {
            movement.enabled = true;
        }

        UpdateHealthUI(currentHealth.Value);
    }




    [ServerRpc(RequireOwnership = false)]
    public void RespawnServerRpc()
    {
        Debug.Log("Server respawn");
        isDead.Value = false; // ✅ Server is allowed to write
        EnablePlayerClientRpc();
    }

    public void ForceRespawn()
    {
        if (!IsServer) return;

        currentHealth.Value = maxHealth;
        isDead.Value = false;

        // Delay the reactivation just a bit to avoid timing issues
        StartCoroutine(DelayedEnableClient());
    }

    private IEnumerator  DelayedEnableClient()
    {
        yield return new WaitForSeconds(0.1f); // small delay
        EnablePlayerClientRpc();
    }

}

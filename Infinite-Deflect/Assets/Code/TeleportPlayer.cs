using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class TeleportHandler : NetworkBehaviour
{
    public Transform[] teleportLocations; // List of teleport points
    public SpawnArea ballSpawner; // Reference to the BallSpawner component
    public float countdownTime = 5f; // Time for players to step into the teleport zone
    public TextMeshProUGUI countdownText; // UI text for countdown

    private HashSet<ulong> registeredPlayers = new HashSet<ulong>(); // Players who stepped in
    private bool isCountdownActive = false; // Prevents multiple countdowns

    private NetworkVariable<float> syncedCountdownTime = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        syncedCountdownTime.OnValueChanged += UpdateCountdownUI;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TeleportHandler] Player entered: {other.name}");
        if (other.CompareTag("Player") && other.TryGetComponent(out NetworkObject networkObject))
        {
            if (IsServer) // Only the server registers players
            {
                RegisterPlayerServerRpc(networkObject.OwnerClientId);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"[TeleportHandler] Player exited: {other.name}");
        if (other.CompareTag("Player") && other.TryGetComponent(out NetworkObject networkObject))
        {
            if (IsServer)
            {
                StartCoroutine(DelayedUnregister(networkObject.OwnerClientId));
            }
        }
    }

    private IEnumerator DelayedUnregister(ulong clientId)
    {
        yield return new WaitForSeconds(0.2f); // Small delay to prevent accidental removal due to physics issues

        if (!registeredPlayers.Contains(clientId)) yield break; // Ensure the player was still in the list

        registeredPlayers.Remove(clientId);
        Debug.Log($"[TeleportHandler] Player {clientId} left. Remaining: {registeredPlayers.Count}");

        if (registeredPlayers.Count < 2)
        {
            StopCoroutine(TeleportCountdown());
            isCountdownActive = false;
            syncedCountdownTime.Value = 0;
            Debug.Log("[TeleportHandler] Not enough players! Countdown stopped.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterPlayerServerRpc(ulong clientId)
    {
        if (!registeredPlayers.Contains(clientId))
        {
            registeredPlayers.Add(clientId);
            Debug.Log($"[TeleportHandler] Player {clientId} registered. Total: {registeredPlayers.Count}");

            // Start countdown if it's not already running
            if (!isCountdownActive && registeredPlayers.Count == 1)
            {
                StartCoroutine(TeleportCountdown());
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void UnregisterPlayerServerRpc(ulong clientId)
    {
        if (registeredPlayers.Contains(clientId))
        {
            registeredPlayers.Remove(clientId);
            Debug.Log($"[TeleportHandler] Player {clientId} left. Remaining: {registeredPlayers.Count}");

            // Stop countdown if no players remain
            if (registeredPlayers.Count < 2)
            {
                StopCoroutine(TeleportCountdown());
                isCountdownActive = false;
                syncedCountdownTime.Value = 0; // Reset UI
                Debug.Log("[TeleportHandler] Not enough players! Countdown stopped.");
            }
        }
    }

    private IEnumerator TeleportCountdown()
    {
        isCountdownActive = true;
        syncedCountdownTime.Value = countdownTime; // Sync timer start
        Debug.Log("[TeleportHandler] Countdown started!");

        while (syncedCountdownTime.Value > 0)
        {
            yield return new WaitForSeconds(1f);
            syncedCountdownTime.Value -= 1f;

            // If all players leave, cancel teleport
            if (registeredPlayers.Count < 2)
            {
                Debug.Log("[TeleportHandler] Not enough players! Countdown stopped.");
                isCountdownActive = false;
                syncedCountdownTime.Value = 0; // Reset UI
                yield break; // Stop coroutine
            }
        }

        countdownText.gameObject.SetActive(false); // Hide UI

        if (registeredPlayers.Count >= 2)
        {
            Debug.Log("[TeleportHandler] Teleporting Players!");

            foreach (var clientId in registeredPlayers)
            {
                Debug.Log($"[TeleportHandler] Requesting teleport for ClientID: {clientId}");
                TeleportRequestServerRpc(clientId);
            }

            ballSpawner?.TrySpawnBall(transform.position, registeredPlayers.Count);
        }
        else
        {
            Debug.Log("[TeleportHandler] Not enough players to teleport.");
        }

        // Reset for next teleport
        registeredPlayers.Clear();
        isCountdownActive = false;
    }

    [ServerRpc(RequireOwnership = false)]
    private void TeleportRequestServerRpc(ulong clientId)
    {
        if (teleportLocations.Length == 0) return;

        Transform randomPoint = teleportLocations[Random.Range(0, teleportLocations.Length)];

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            var playerObject = client.PlayerObject;
            if (playerObject != null)
            {
                Debug.Log($"[TeleportHandler] Teleporting ClientID {clientId} to {randomPoint.position}");
                TeleportClientRpc(randomPoint.position, randomPoint.rotation, clientId);
            }
            else
            {
                Debug.LogError($"[TeleportHandler] PlayerObject for ClientID {clientId} is null!");
            }
        }
        else
        {
            Debug.LogError($"[TeleportHandler] Client {clientId} not found in ConnectedClients!");
        }
    }

    [ClientRpc]
    private void TeleportClientRpc(Vector3 position, Quaternion rotation, ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId) // Only teleport the intended player
        {
            var player = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            if (player != null)
            {
                Debug.Log($"[TeleportHandler] Teleporting Local Player {targetClientId} to {position}");
                player.transform.position = position;
                player.transform.rotation = rotation;
            }
            else
            {
                Debug.LogError("[TeleportHandler] Local Player object not found!");
            }
        }
    }

    private void UpdateCountdownUI(float oldTime, float newTime)
    {
        if (newTime > 0)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = $"Teleporting in: {newTime:F1}s";
        }
        else
        {
            countdownText.gameObject.SetActive(false);
        }
    }
}

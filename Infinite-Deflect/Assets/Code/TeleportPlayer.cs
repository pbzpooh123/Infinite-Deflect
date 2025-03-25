using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Unity.Netcode.Components;

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
        if (!IsServer) return; // Only the server handles player registration

        if (other.CompareTag("Player") && other.TryGetComponent(out NetworkObject networkObject))
        {
            ulong clientId = networkObject.OwnerClientId;
            
            if (!registeredPlayers.Contains(clientId))
            {
                registeredPlayers.Add(clientId);
                Debug.Log($"[TeleportHandler] Player {clientId} ENTERED. Total Players: {registeredPlayers.Count}");

                if (!isCountdownActive && registeredPlayers.Count == 1)
                {
                    StartCoroutine(TeleportCountdown());
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return; // Only the server handles player removal

        if (other.CompareTag("Player") && other.TryGetComponent(out NetworkObject networkObject))
        {
            ulong clientId = networkObject.OwnerClientId;

            if (registeredPlayers.Contains(clientId))
            {
                registeredPlayers.Remove(clientId);
                Debug.Log($"[TeleportHandler] Player {clientId} EXITED. Remaining Players: {registeredPlayers.Count}");

                if (registeredPlayers.Count < 2)
                {
                    StopAllCoroutines();
                    isCountdownActive = false;
                    syncedCountdownTime.Value = 0;
                    Debug.Log("[TeleportHandler] Countdown stopped due to insufficient players.");
                }
            }
        }
    }

    private IEnumerator TeleportCountdown()
    {
        isCountdownActive = true;
        syncedCountdownTime.Value = countdownTime;
        Debug.Log("[TeleportHandler] Countdown started!");

        while (syncedCountdownTime.Value > 0)
        {
            yield return new WaitForSeconds(1f);
            syncedCountdownTime.Value -= 1f;
            Debug.Log($"[TeleportHandler] Countdown: {syncedCountdownTime.Value}s - Players: {registeredPlayers.Count}");
        }

        countdownText.gameObject.SetActive(false);

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
            Debug.Log("[TeleportHandler] Countdown finished, but not enough players. No teleport.");
        }

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
                Debug.Log($"[TeleportHandler] Attempting to teleport ClientID {clientId} to {randomPoint.position}");

                // Disable NetworkTransform temporarily
                var networkTransform = playerObject.GetComponent<NetworkTransform>();
                if (networkTransform != null)
                {
                    // Disable interpolation and synchronization
                    networkTransform.Interpolate = false;
                }

                // Force teleport on the server
                ForcePlayerTeleportClientRpc(randomPoint.position, randomPoint.rotation, clientId);
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
    private void ForcePlayerTeleportClientRpc(Vector3 position, Quaternion rotation, ulong targetClientId)
    {
        // Only update for the specific client
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            var localPlayerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            if (localPlayerObject != null)
            {
                Debug.Log($"[TeleportHandler] Forcing Local Player {targetClientId} teleport to {position}");

                // Get all components that might interfere with positioning
                var networkTransform = localPlayerObject.GetComponent<NetworkTransform>();
                var characterController = localPlayerObject.GetComponent<CharacterController>();
                var rigidbody = localPlayerObject.GetComponent<Rigidbody>();

                // Disable movement components temporarily
                if (networkTransform != null)
                {
                    networkTransform.Interpolate = false;
                }

                if (characterController != null)
                {
                    characterController.enabled = false;
                }

                if (rigidbody != null)
                {
                    rigidbody.isKinematic = true;
                }

                // Directly set position
                localPlayerObject.transform.SetPositionAndRotation(position, rotation);

                // Re-enable components after a short delay
                StartCoroutine(ReenableMovementComponents(localPlayerObject, networkTransform, characterController, rigidbody));
            }
            else
            {
                Debug.LogError("[TeleportHandler] Local Player object not found!");
            }
        }
    }

    private IEnumerator ReenableMovementComponents(NetworkObject playerObject, 
        NetworkTransform networkTransform, 
        CharacterController characterController, 
        Rigidbody rigidbody)
    {
        yield return new WaitForSeconds(0.1f);

        // Re-enable components
        if (networkTransform != null)
        {
            networkTransform.Interpolate = true;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (rigidbody != null)
        {
            rigidbody.isKinematic = false;
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
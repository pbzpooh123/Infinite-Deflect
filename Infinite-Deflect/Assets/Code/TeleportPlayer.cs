using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Unity.Netcode.Components;
using Random = UnityEngine.Random;

public class TeleportHandler : NetworkBehaviour
{
    public Transform[] teleportLocations; 
    public SpawnArea ballSpawner; 
    public float countdownTime = 5f; 
    public float soloCountdownTime = 3f; 
    public TextMeshProUGUI countdownText; 
    public TextMeshProUGUI soloCountdownText; 

    private HashSet<ulong> registeredPlayers = new HashSet<ulong>();
    private bool isCountdownActive = false;
    public Transform soloSpawnPoint; 


    private NetworkVariable<float> syncedCountdownTime = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private Coroutine soloTeleportCoroutine;

    private void Start()
    {
        syncedCountdownTime.OnValueChanged += UpdateCountdownUI;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player") && other.TryGetComponent(out NetworkObject networkObject))
        {
            ulong clientId = networkObject.OwnerClientId;

            if (!registeredPlayers.Contains(clientId))
            {
                registeredPlayers.Add(clientId);
                Debug.Log($"[TeleportHandler] Player {clientId} ENTERED. Total Players: {registeredPlayers.Count}");

                if (registeredPlayers.Count == 1 && !isCountdownActive)
                {
                    StartCoroutine(TeleportCountdown());
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

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
                    countdownText.gameObject.SetActive(false);
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
            Debug.Log("[TeleportHandler] Countdown finished, but not enough players.");
        }

        registeredPlayers.Clear();
        isCountdownActive = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void TeleportRequestServerRpc(ulong clientId)
    {
        if (teleportLocations.Length == 0) return;

        Transform targetPoint;

        if (registeredPlayers.Count <= 1)
        {
            // Use a special solo spawn point
            targetPoint = soloSpawnPoint;
        }
        else
        {
            // Use random from group spawn points
            targetPoint = teleportLocations[Random.Range(0, teleportLocations.Length)];
        }


        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            var playerObject = client.PlayerObject;
            if (playerObject != null)
            {
                Debug.Log($"[TeleportHandler] Attempting to teleport ClientID {clientId} to {targetPoint.position}");

                var networkTransform = playerObject.GetComponent<NetworkTransform>();
                if (networkTransform != null) networkTransform.Interpolate = false;

                ForcePlayerTeleportClientRpc(targetPoint.position, targetPoint.rotation, clientId);
            }
        }
    }

    [ClientRpc]
    private void ForcePlayerTeleportClientRpc(Vector3 position, Quaternion rotation, ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            var localPlayerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            if (localPlayerObject != null)
            {
                var networkTransform = localPlayerObject.GetComponent<NetworkTransform>();
                var characterController = localPlayerObject.GetComponent<CharacterController>();
                var rigidbody = localPlayerObject.GetComponent<Rigidbody>();

                if (networkTransform != null) networkTransform.Interpolate = false;
                if (characterController != null) characterController.enabled = false;
                if (rigidbody != null) rigidbody.isKinematic = true;

                localPlayerObject.transform.SetPositionAndRotation(position, rotation);

                StartCoroutine(ReenableMovementComponents(localPlayerObject, networkTransform, characterController, rigidbody));
            }
        }
    }

    private IEnumerator ReenableMovementComponents(NetworkObject playerObject,
        NetworkTransform networkTransform,
        CharacterController characterController,
        Rigidbody rigidbody)
    {
        yield return new WaitForSeconds(0.1f);

        if (networkTransform != null) networkTransform.Interpolate = true;
        if (characterController != null) characterController.enabled = true;
        if (rigidbody != null) rigidbody.isKinematic = false;
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

    // Called externally when checking player state
    public void CheckForSoloPlayer()
    {
        Debug.Log("[TeleportHandler] CheckForSoloPlayer called");

        if (!IsServer)
        {
            Debug.LogWarning("[TeleportHandler] Not running on server, aborting CheckForSoloPlayer.");
            return;
        }

        int alivePlayers = 0;
        ulong lastAliveId = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            if (client.Value.PlayerObject != null && client.Value.PlayerObject.CompareTag("Player"))
            {
                var playerHealth = client.Value.PlayerObject.GetComponent<PlayerHealth>();
                if (playerHealth != null && !playerHealth.IsDead)
                {
                    alivePlayers++;
                    lastAliveId = client.Key;
                }
            }
        }


        Debug.Log($"[TeleportHandler] Alive Players: {alivePlayers}, Coroutine Running: {soloTeleportCoroutine != null}");

        if (alivePlayers == 1 && soloTeleportCoroutine == null)
        {
            Debug.Log("[TeleportHandler] Only one player alive. Starting solo teleport coroutine.");
            soloTeleportCoroutine = StartCoroutine(SoloTeleportAfterDelay(lastAliveId));
        }
        else if (alivePlayers > 1 && soloTeleportCoroutine != null)
        {
            Debug.Log("[TeleportHandler] More than one player. Stopping solo teleport coroutine.");
            StopCoroutine(soloTeleportCoroutine);
            soloTeleportCoroutine = null;
            soloCountdownText.gameObject.SetActive(false);
        }
    }


    private IEnumerator SoloTeleportAfterDelay(ulong clientId)
    {
        Debug.Log($"[TeleportHandler] SoloTeleportAfterDelay started for client {clientId}");

        float timer = soloCountdownTime;
        soloCountdownText.gameObject.SetActive(true);

        while (timer > 0)
        {
            soloCountdownText.text = $"Returning to spawn in: {timer:F1}s";
            yield return new WaitForSeconds(1f);
            timer -= 1f;
        }

        soloCountdownText.gameObject.SetActive(false);
        soloTeleportCoroutine = null;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                Debug.Log($"[TeleportHandler] Teleporting player {clientId} back to spawn.");
                TeleportRequestServerRpc(clientId);
            }
            else
            {
                Debug.LogWarning($"[TeleportHandler] PlayerObject is null for client {clientId}");
            }
        }
        else
        {
            Debug.LogWarning($"[TeleportHandler] Could not find client with ID {clientId}");
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("[Test] Manual check for solo player via 'P' key.");
            CheckForSoloPlayer();
        }
    }

}

using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class SpawnArea : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private float spawnDelay = 3f;
    [SerializeField] private Material readyMaterial;
    [SerializeField] private Material notReadyMaterial;
    [SerializeField] private float ballSpawnHeight = 5f;
    
    [Header("Player Positions")]
    [SerializeField] private Transform[] playerPositions;
    
    private MeshRenderer meshRenderer;
    private float currentTime = 0f;
    private bool isSpawning = false;
    private bool hasSpawnedBall = false;
    [SerializeField] private List<NetworkObject> playersToTeleport = new List<NetworkObject>();

    // NetworkVariable to track if teleport should happen
    private NetworkVariable<bool> shouldTeleport = new NetworkVariable<bool>(false);

    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = notReadyMaterial;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasSpawnedBall)
        {
            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            if (playerNetObj != null && !playersToTeleport.Contains(playerNetObj))
            {
                playersToTeleport.Add(playerNetObj);
                if (!isSpawning)
                {
                    StartSpawnSequenceServerRpc();
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            if (playerNetObj != null)
            {
                playersToTeleport.Remove(playerNetObj);
                if (playersToTeleport.Count == 0 && !hasSpawnedBall)
                {
                    CancelSpawnSequenceServerRpc();
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartSpawnSequenceServerRpc()
    {
        if (isSpawning || hasSpawnedBall) return;
        
        isSpawning = true;
        currentTime = 0f;
        StartCoroutine(SpawnSequence());
    }

    [ServerRpc(RequireOwnership = false)]
    private void CancelSpawnSequenceServerRpc()
    {
        isSpawning = false;
        currentTime = 0f;
        UpdateMaterialClientRpc(0f);
    }

    private IEnumerator SpawnSequence()
    {
        while (currentTime < spawnDelay && isSpawning)
        {
            currentTime += Time.deltaTime;
            float progress = currentTime / spawnDelay;
            UpdateMaterialClientRpc(progress);
            yield return null;
        }

        if (isSpawning)
        {
            TeleportPlayersAndSpawnBall();
        }
    }

    [ClientRpc]
    private void UpdateMaterialClientRpc(float progress)
    {
        meshRenderer.material.color = Color.Lerp(notReadyMaterial.color, readyMaterial.color, progress);
    }

    private void TeleportPlayersAndSpawnBall()
    {
        if (!IsServer) return;

        // Teleport players to positions
        for (int i = 0; i < playersToTeleport.Count && i < playerPositions.Length; i++)
        {
            NetworkObject player = playersToTeleport[i];
            if (player != null && playerPositions[i] != null)
            {
                // Force teleport through RPCs
                ForcePlayerPositionClientRpc(
                    player.NetworkObjectId,
                    playerPositions[i].position,
                    playerPositions[i].rotation
                );
            }
        }

        StartCoroutine(SpawnBallAfterDelay());
    }

    [ClientRpc]
    private void ForcePlayerPositionClientRpc(ulong playerNetId, Vector3 position, Quaternion rotation)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out NetworkObject player))
            return;

        Debug.Log($"Teleporting player {playerNetId} to position {position}"); // Debug log

        // Disable all movement components temporarily
        var rb = player.GetComponent<Rigidbody>();
        var cc = player.GetComponent<CharacterController>();
        
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
        }
        
        if (cc != null)
            cc.enabled = false;

        // Force the position change
        player.transform.position = position;
        player.transform.rotation = rotation;

        // Re-enable components
        if (rb != null)
            rb.isKinematic = false;
        
        if (cc != null)
            cc.enabled = true;

        // If this is the local player, handle the camera
        if (player.IsOwner)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                // Force camera to new position
                cam.transform.position = position + cam.transform.rotation * new Vector3(0, 2, -5);
            }
        }
    }

    private IEnumerator SpawnBallAfterDelay()
    {
        yield return new WaitForSeconds(1f);

        Vector3 spawnPos = transform.position + (Vector3.up * ballSpawnHeight);
        GameObject ball = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        NetworkObject ballNetObj = ball.GetComponent<NetworkObject>();
        if (ballNetObj != null)
        {
            ballNetObj.Spawn();
        }
        
        hasSpawnedBall = true;
        isSpawning = false;
    }
}
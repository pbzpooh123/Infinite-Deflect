using UnityEngine;
using Unity.Netcode;
using System.Collections;

// Spawn Area Script
public class SpawnArea : NetworkBehaviour
{
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private float spawnTime = 5f;
    [SerializeField] private Material readyMaterial;
    [SerializeField] private Material notReadyMaterial;
    
    private MeshRenderer meshRenderer;
    private float currentTime = 0f;
    private bool isPlayerInArea = false;
    private bool hasSpawnedBall = false;

    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = notReadyMaterial;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasSpawnedBall)
        {
            isPlayerInArea = true;
            currentTime = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInArea = false;
            currentTime = 0f;
            meshRenderer.material = notReadyMaterial;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (isPlayerInArea && !hasSpawnedBall)
        {
            currentTime += Time.deltaTime;
            
            // Update material color based on progress
            float progress = currentTime / spawnTime;
            meshRenderer.material.color = Color.Lerp(notReadyMaterial.color, readyMaterial.color, progress);

            if (currentTime >= spawnTime)
            {
                SpawnBallServerRpc();
                hasSpawnedBall = true;
            }
        }
    }

    [ServerRpc]
    private void SpawnBallServerRpc()
    {
        GameObject ball = Instantiate(ballPrefab, transform.position + Vector3.up * 2f, Quaternion.identity);
        ball.GetComponent<NetworkObject>().Spawn();
    }
}
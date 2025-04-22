using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class SpawnArea : NetworkBehaviour
{
    [Header("Ball Settings")]
    public GameObject ballPrefab; // The ball prefab to spawn
    public float ballSpawnDelay = 5f; // Delay before spawning the ball
    public float checkRadius = 10f; // Radius to check for players in the play zone
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.3f); // Color of the gizmo sphere

    [Header("Spawn Settings")]
    public Transform[] spawnPoints; // Array of predefined spawn points

    private GameObject currentBall; // Reference to the currently spawned ball

    /// <summary>
    /// Attempts to spawn a ball if enough players are in the play zone.
    /// Destroys the existing ball if one already exists.
    /// </summary>
    /// <param name="spawnPosition">Position to consider for spawning (not used directly in this version)</param>
    /// <param name="teleportedPlayers">Number of players currently in the teleport zone</param>
    public void TrySpawnBall(Vector3 spawnPosition, int teleportedPlayers)
    {
        if (teleportedPlayers >= 2 && spawnPoints.Length > 0)
        {
            Transform chosenSpawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
            StartCoroutine(SpawnBallAfterDelay(chosenSpawn.position));
        }
    }

    private IEnumerator SpawnBallAfterDelay(Vector3 spawnPosition)
    {
        yield return new WaitForSeconds(ballSpawnDelay);

        // Destroy the old ball if it exists
        if (currentBall != null && currentBall.TryGetComponent(out NetworkObject oldNetObj))
        {
            if (IsServer)
            {
                oldNetObj.Despawn(true); // Despawn and destroy the object across the network
            }
        }

        // Spawn a new ball
        GameObject newBall = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);

        if (newBall.TryGetComponent(out NetworkObject newNetObj))
        {
            newNetObj.Spawn();
            currentBall = newBall;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, checkRadius); // Visual debug radius
    }
}

using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class SpawnArea : NetworkBehaviour
{
    public GameObject ballPrefab; // The ball prefab to spawn
    public float ballSpawnDelay = 5f; // Delay before spawning the ball
    public float checkRadius = 10f; // Radius to check for players in the play zone
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.3f); // Color of the gizmo sphere

    public void TrySpawnBall(Vector3 spawnPosition, int teleportedPlayers)
    {
        if (teleportedPlayers >= 2) // Only spawn if at least 2 players teleported
        {
            StartCoroutine(SpawnBallAfterDelay(spawnPosition));
        }
    }

    private IEnumerator SpawnBallAfterDelay(Vector3 spawnPosition)
    {
        yield return new WaitForSeconds(ballSpawnDelay);

        GameObject ball = Instantiate(ballPrefab, spawnPosition + Vector3.up * 2, Quaternion.identity);

        if (ball.TryGetComponent(out NetworkObject networkObject))
        {
            networkObject.Spawn(); // Spawn the ball on the network
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, checkRadius); // Draw the check radius sphere in the Scene view
    }
}

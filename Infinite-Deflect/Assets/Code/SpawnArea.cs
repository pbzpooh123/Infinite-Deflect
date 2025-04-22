using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class SpawnArea : NetworkBehaviour
{
    [Header("Ball Settings")]
    public GameObject ballPrefab;
    public float ballSpawnDelay = 5f;
    public float checkRadius = 10f;
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.3f);

    [Header("Spawn Settings")]
    public Transform[] spawnPoints;

    private GameObject currentBall;

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

        if (currentBall != null && currentBall.TryGetComponent(out NetworkObject oldNetObj))
        {
            if (IsServer)
            {
                oldNetObj.Despawn(true);
            }
        }

        GameObject newBall = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);

        if (newBall.TryGetComponent(out NetworkObject newNetObj))
        {
            newNetObj.Spawn();
            currentBall = newBall;

            if (IsServer)
            {
                GameBall ballScript = currentBall.GetComponent<GameBall>();
                if (ballScript != null)
                {
                    ballScript.ResetBallServerRpc(); // Ensure velocity is applied
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, checkRadius);
    }
}
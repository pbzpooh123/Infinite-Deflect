using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Settings")]
    [SerializeField] private float roundEndDelay = 3f;
    [SerializeField] private Transform winnerTeleportLocation;
    [SerializeField] private Transform[] playerSpawnPoints;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Called when a player dies to check if only one player is left alive.
    /// </summary>
    public void CheckRoundOver()
    {
        if (!IsServer) return;

        List<PlayerHealth> alivePlayers = new List<PlayerHealth>();

        foreach (var player in FindObjectsOfType<PlayerHealth>())
        {
            if (!player.IsDead)
            {
                alivePlayers.Add(player);
            }
        }

        if (alivePlayers.Count == 1)
        {
            // Despawn the ball if it exists
            var ball = FindObjectOfType<GameBall>();
            if (ball != null && ball.NetworkObject.IsSpawned)
            {
                ball.NetworkObject.Despawn();
            }

            Debug.Log($"Round Over! Winner is player {alivePlayers[0].OwnerClientId}");
            StartCoroutine(HandleRoundEnd(alivePlayers[0]));
        }
    }

    /// <summary>
    /// Ends the round, teleports winner, respawns others, then respawns all players for the next round.
    /// </summary>
    private IEnumerator HandleRoundEnd(PlayerHealth winner)
    {
        yield return new WaitForSeconds(roundEndDelay);

        // Optional: Move winner to a special spot
        if (winnerTeleportLocation != null)
        {
            winner.TeleportClientRpc(winnerTeleportLocation.position);

        }

        // Respawn all players (including winner)
        int spawnIndex = 0;
        foreach (var player in FindObjectsOfType<PlayerHealth>())
        {
            if (player != winner && player.IsDead)
            {
                Vector3 spawnPos = playerSpawnPoints[spawnIndex % playerSpawnPoints.Length].position;
                player.RespawnServerRpc(spawnPos);
                spawnIndex++;
            }
        }


        Debug.Log("Next round ready!");
    }
    
}

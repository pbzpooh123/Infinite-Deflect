using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

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
            Debug.Log($"Round Over! Winner is player {alivePlayers[0].OwnerClientId}");
            StartCoroutine(HandleRoundEnd(alivePlayers[0]));
        }
    }

    private IEnumerator HandleRoundEnd(PlayerHealth winner)
    {
        yield return new WaitForSeconds(roundEndDelay);

        // Teleport winner out
        winner.transform.position = winnerTeleportLocation.position;

        // Respawn other players
        int spawnIndex = 0;
        foreach (var player in FindObjectsOfType<PlayerHealth>())
        {
            if (player != winner)
            {
                Vector3 spawnPos = playerSpawnPoints[spawnIndex % playerSpawnPoints.Length].position;
                player.RespawnServerRpc(spawnPos);
                spawnIndex++;
            }
        }

        // Reset winner's health to full too if needed
        winner.RespawnServerRpc(winner.transform.position);

        Debug.Log("Next round ready!");
    }
}
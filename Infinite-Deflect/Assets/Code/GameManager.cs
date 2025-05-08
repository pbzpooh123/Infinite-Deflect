using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Settings")]
    [SerializeField] private float roundEndDelay = 10f;
    [SerializeField] private Transform winnerTeleportLocation;
    [SerializeField] private Transform[] playerSpawnPoints;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    
    /// Called when a player dies to check if only one player is left alive.
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
            teleportHandler.CheckForSoloPlayer();
            var ball = FindObjectOfType<GameBall>();
            if (ball != null && ball.NetworkObject.IsSpawned)
            {
                ball.NetworkObject.Despawn();
            }

            Debug.Log($"Round Over! Winner is player {alivePlayers[0].OwnerClientId}");
            StartCoroutine(HandleRoundEnd());
        }
    }
    
    /// Ends the round, teleports the winner, and respawns all players.
    private IEnumerator HandleRoundEnd()
    {
        yield return new WaitForSeconds(roundEndDelay);

        foreach (var player in FindObjectsOfType<PlayerHealth>())
        {
            
            player.ForceRespawn();
        }
        
        Debug.Log("Next round ready!");
    }
    
    public TeleportHandler teleportHandler;
}

using UnityEngine;

public class DeflectZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            GameBall gameBall = other.GetComponent<GameBall>();
            
            if (gameBall != null && gameBall.IsServer)
            {
                gameBall.SelectRandomTargetAfterDeflectServerRpc(); 
                Debug.Log("Ball hit");
            }
        }
    }
}
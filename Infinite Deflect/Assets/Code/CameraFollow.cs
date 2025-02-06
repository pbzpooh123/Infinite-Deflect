using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player; // Assign the player in the Inspector
    public Vector3 offset = new Vector3(0, 5, -7); // Default camera position
    public float smoothSpeed = 5f; // Adjust for smoother movement

    void LateUpdate()
    {
        if (player == null) return;

        // Target position with offset
        Vector3 targetPosition = player.position + offset;

        // Smooth transition
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        // Ensure camera always looks at the player
        transform.LookAt(player);
    }
}
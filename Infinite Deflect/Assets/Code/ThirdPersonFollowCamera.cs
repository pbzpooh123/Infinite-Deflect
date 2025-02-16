using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target; // Assign player here
    public float sensitivity = 3f; 
    public float rotationSmoothTime = 0.1f;

    private Vector2 rotation = Vector2.zero;
    private Vector2 rotationSmoothVelocity;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; // Locks cursor to center
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        rotation.x += mouseX;
        rotation.y = Mathf.Clamp(rotation.y - mouseY, -15f, 30f); // Limits vertical look

        Vector2 smoothedRotation = Vector2.SmoothDamp(rotation, rotation, ref rotationSmoothVelocity, rotationSmoothTime);

        transform.position = target.position;
        transform.rotation = Quaternion.Euler(smoothedRotation.y, smoothedRotation.x, 0f);
    }
}
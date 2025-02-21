using Unity.Netcode;
using UnityEngine;

public class NetworkedThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float cameraDist = 5f; // Distance from target
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 2, 0); // Height offset
    
    private Transform target;
    private Vector2 rotation;
    private Vector2 currentRotation;
    private Vector2 rotationVelocity;
    private bool isInitialized = false;

    private void Start()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        NetworkObject localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        
        if (localPlayer != null)
        {
            target = localPlayer.transform;
            isInitialized = true;
            Cursor.lockState = CursorLockMode.Locked;
            
            // Initialize rotation to match current angles
            rotation = new Vector2(target.eulerAngles.y, 0f);
            currentRotation = rotation;
            
            // Set initial position
            UpdateCameraPosition();
        }
    }

    private void Update()
    {
        if (!isInitialized && NetworkManager.Singleton != null)
        {
            InitializeCamera();
            return;
        }

        if (!isInitialized || target == null) return;
        
        HandleCameraMovement();
    }

    private void HandleCameraMovement()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        // Update target rotation
        rotation.x += mouseX;
        rotation.y -= mouseY;
        
        // Clamp vertical rotation
        rotation.y = Mathf.Clamp(rotation.y, -60f, 80f);

        // Smoothly interpolate current rotation
        currentRotation = Vector2.SmoothDamp(
            currentRotation,
            rotation,
            ref rotationVelocity,
            rotationSmoothTime
        );

        // Apply rotation to camera
        transform.rotation = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);

        // Update camera position
        UpdateCameraPosition();

        // Update player rotation (only Y axis)
        target.rotation = Quaternion.Euler(0, currentRotation.x, 0);
    }

    private void UpdateCameraPosition()
    {
        if (target == null) return;

        // Calculate desired camera position
        Vector3 targetPosition = target.position + cameraOffset;
        Vector3 cameraDirection = -transform.forward * cameraDist;
        
        // Check for obstacles (optional)
        if (Physics.Raycast(targetPosition, -transform.forward, out RaycastHit hit, cameraDist))
        {
            transform.position = hit.point + transform.forward * 0.2f;
        }
        else
        {
            transform.position = targetPosition + cameraDirection;
        }
    }

    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
    }
}
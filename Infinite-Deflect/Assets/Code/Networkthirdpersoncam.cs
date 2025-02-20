using Unity.Netcode;
using UnityEngine;

public class NetworkedThirdPersonCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float cameraDist = 5f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 2, 0);
    
    [Header("Collision Settings")]
    [SerializeField] private float collisionOffset = 0.2f;
    [SerializeField] private LayerMask collisionMask;

    private Transform target;
    private Vector2 rotation;
    private Vector2 currentRotation;
    private Vector2 rotationVelocity;
    private bool isInitialized = false;
    private NetworkObject playerNetworkObject;

    private void Start()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        if (NetworkManager.Singleton?.LocalClient?.PlayerObject == null) return;

        playerNetworkObject = NetworkManager.Singleton.LocalClient.PlayerObject;
        target = playerNetworkObject.transform;
        isInitialized = true;
        Cursor.lockState = CursorLockMode.Locked;
        
        // Initialize rotation to match current angles
        rotation = new Vector2(target.eulerAngles.y, 0f);
        currentRotation = rotation;
        
        // Set initial position
        UpdateCameraPosition();
    }

    private void LateUpdate()
    {
        if (!isInitialized && NetworkManager.Singleton != null)
        {
            InitializeCamera();
            return;
        }

        if (!isInitialized || target == null) return;
        
        // Only handle camera if we own the player
        if (playerNetworkObject.IsOwner)
        {
            HandleCameraMovement();
        }
    }

    private void HandleCameraMovement()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        // Update target rotation
        rotation.x += mouseX;
        rotation.y -= mouseY;
        
        // Clamp vertical rotation to prevent over-rotation
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

        // Smoothly rotate the player model to match camera horizontal rotation
        float targetYRotation = currentRotation.x;
        if (target.GetComponent<Rigidbody>() != null)
        {
            // If using Rigidbody, make sure to only rotate the visual model if there is movement
            if (Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)
            {
                target.rotation = Quaternion.Lerp(
                    target.rotation,
                    Quaternion.Euler(0, targetYRotation, 0),
                    Time.deltaTime * 10f
                );
            }
        }
        else
        {
            // For non-Rigidbody characters, always update rotation
            target.rotation = Quaternion.Euler(0, targetYRotation, 0);
        }
    }

    private void UpdateCameraPosition()
    {
        if (target == null) return;

        // Calculate desired camera position
        Vector3 targetPosition = target.position + cameraOffset;
        Vector3 directionToCamera = -transform.forward;
        
        // Perform collision check
        if (Physics.SphereCast(targetPosition, 0.2f, directionToCamera, out RaycastHit hit, 
            cameraDist, collisionMask))
        {
            // If there's an obstacle, position the camera at the hit point with offset
            transform.position = hit.point + directionToCamera * collisionOffset;
        }
        else
        {
            // No obstacle, set to desired position
            transform.position = targetPosition + directionToCamera * cameraDist;
        }
    }

    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
    }
}
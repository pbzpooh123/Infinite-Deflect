using Unity.Netcode;
using UnityEngine;

public class NetworkedThirdPersonCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float cameraSmoothTime = 0.05f; // Camera smoothing
    [SerializeField] private float playerRotationSpeed = 15f; // Separate player rotation speed
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
    private float targetYaw;

    private void Awake()
    {
        TryInitializeCamera();
    }

    private void Start()
    {
        if (!isInitialized)
        {
            TryInitializeCamera();
        }
    }

    private void TryInitializeCamera()
    {
        if (NetworkManager.Singleton?.LocalClient?.PlayerObject != null)
        {
            playerNetworkObject = NetworkManager.Singleton.LocalClient.PlayerObject;
            target = playerNetworkObject.transform;
            isInitialized = true;
            Cursor.lockState = CursorLockMode.Locked;

            // Set initial rotations
            targetYaw = target.eulerAngles.y;
            rotation = new Vector2(targetYaw, 0f);
            currentRotation = rotation;
            transform.rotation = Quaternion.Euler(0, targetYaw, 0);
            
            UpdateCameraPosition();
        }
    }

    private void LateUpdate()
    {
        if (!isInitialized)
        {
            TryInitializeCamera();
            return;
        }

        if (target == null || !playerNetworkObject.IsOwner) return;
        
        HandleCameraMovement();
    }

    private void HandleCameraMovement()
    {
        // Get mouse input
        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        // Update rotation target
        rotation.x += mouseX;
        rotation.y = Mathf.Clamp(rotation.y - mouseY, -60f, 80f);

        // Smooth camera rotation only
        currentRotation = Vector2.SmoothDamp(
            currentRotation,
            rotation,
            ref rotationVelocity,
            cameraSmoothTime,
            Mathf.Infinity,
            Time.deltaTime
        );

        // Apply camera rotation
        transform.rotation = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);
        
        // Update camera position
        UpdateCameraPosition();

        // Direct player rotation without Lerp
        targetYaw = currentRotation.x;
        Vector3 targetEuler = target.eulerAngles;
        float newYaw = Mathf.MoveTowardsAngle(targetEuler.y, targetYaw, playerRotationSpeed * Time.deltaTime * 100f);
        target.rotation = Quaternion.Euler(targetEuler.x, newYaw, targetEuler.z);
    }

    private void UpdateCameraPosition()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position + cameraOffset;
        Vector3 cameraDirection = -transform.forward;
        
        if (Physics.SphereCast(
            targetPosition,
            0.2f,
            cameraDirection,
            out RaycastHit hit,
            cameraDist,
            collisionMask))
        {
            transform.position = hit.point - cameraDirection * collisionOffset;
        }
        else
        {
            transform.position = targetPosition + cameraDirection * cameraDist;
        }
    }

    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
    }
}
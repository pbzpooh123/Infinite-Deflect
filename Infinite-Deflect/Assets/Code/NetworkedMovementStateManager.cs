using Unity.Netcode;
using UnityEngine;

public class NetworkedMovementStateManager : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundYOffset = 0.1f;
    [SerializeField] private LayerMask groundMask; 
    [SerializeField] private float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 movementDirection;
    private Vector3 spherePosition;

    // Network variable for position syncing
    private NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("CharacterController component is missing!");
            enabled = false;
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsOwner)
        {
            // If we're not the owner, just update transform based on network variables
            enabled = true;
        }
    }

    private void Update()
    {
        if (IsOwner)
        {
            // Owner controls movement
            HandleOwnerMovement();
        }
        else
        {
            // Non-owners interpolate based on network variables
            HandleClientMovement();
        }
    }

    private void HandleOwnerMovement()
    {
        GetDirection();
        HandleJump();
        ApplyGravity();

        Vector3 finalMove = movementDirection * moveSpeed + velocity;
        controller.Move(finalMove * Time.deltaTime);

        // Update network variables directly (owner has write permission)
        netPosition.Value = transform.position;
        netRotation.Value = transform.rotation;
    }

    private void HandleClientMovement()
    {
        // Smoothly interpolate to the networked position and rotation
        transform.position = Vector3.Lerp(transform.position, netPosition.Value, Time.deltaTime * 10f);
        transform.rotation = Quaternion.Lerp(transform.rotation, netRotation.Value, Time.deltaTime * 10f);
    }

    private void GetDirection()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        movementDirection = transform.forward * verticalInput + transform.right * horizontalInput;
    }

    private bool IsGrounded()
    {
        spherePosition = new Vector3(transform.position.x, transform.position.y - groundYOffset, transform.position.z);
        return Physics.CheckSphere(spherePosition, controller.radius - 0.05f, groundMask);
    }

    private void ApplyGravity()
    {
        if (IsGrounded() && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }

    private void HandleJump()
    {
        if (IsGrounded() && Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            new Vector3(transform.position.x, transform.position.y - groundYOffset, transform.position.z),
            controller != null ? controller.radius - 0.05f : 0.5f
        );
    }
}
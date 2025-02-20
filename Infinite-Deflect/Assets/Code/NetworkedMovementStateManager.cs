using Unity.Netcode;
using UnityEngine;

public class NetworkedMovementStateManager : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundMask;
    
    [Header("Deflect Settings")]
    [SerializeField] private float deflectCooldown = 0.5f;
    [SerializeField] private GameObject deflectZonePrefab;
    [SerializeField] private Vector3 deflectZoneOffset = new Vector3(0, 1f, 0.5f);
    [SerializeField] private Vector3 deflectZoneOffsetF = new Vector3(0, 0f, 0.5f); // Lower position for F key deflect
    
    private Rigidbody rb;
    private Vector3 movementDirection;
    private bool isGrounded;
    private bool canDeflect = true;
    private GameObject deflectZone;
    private GameObject deflectZoneF;
    
    // Network variables for position and rotation syncing
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
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing!");
            enabled = false;
            return;
        }

        // Configure Rigidbody settings
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Create both deflect zones
        CreateDeflectZones();
    }

    private void CreateDeflectZones()
    {
        // Create mouse deflect zone
        deflectZone = CreateDeflectZone("MouseDeflectZone", deflectZoneOffset);
        
        // Create F key deflect zone
        deflectZoneF = CreateDeflectZone("FKeyDeflectZone", deflectZoneOffsetF);
    }

    private GameObject CreateDeflectZone(string name, Vector3 offset)
    {
        GameObject zone;
        if (deflectZonePrefab != null)
        {
            zone = Instantiate(deflectZonePrefab, transform);
            zone.transform.localPosition = offset;
        }
        else
        {
            zone = new GameObject(name);
            zone.transform.SetParent(transform);
            zone.transform.localPosition = offset;
            
            BoxCollider deflectCollider = zone.AddComponent<BoxCollider>();
            deflectCollider.isTrigger = true;
            deflectCollider.size = new Vector3(1f, 1f, 0.2f);
        }
        
        zone.tag = "PlayerDeflect";
        zone.SetActive(false);
        return zone;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsOwner)
        {
            rb.isKinematic = true;
            enabled = true;
        }
        
        gameObject.tag = "Player";
    }

    private void Update()
    {
        if (IsOwner)
        {
            GetDirection();
            CheckGrounded();
            HandleJump();
            HandleDeflect();
        }
        else
        {
            HandleClientMovement();
        }
    }

    private void HandleDeflect()
    {
        if (!canDeflect) return;

        // Left click deflect
        if (Input.GetMouseButtonDown(0))
        {
            ActivateDeflect(deflectZone);
        }
        // F key deflect
        else if (Input.GetKeyDown(KeyCode.F))
        {
            ActivateDeflect(deflectZoneF);
        }
    }

    private void ActivateDeflect(GameObject zone)
    {
        if (!canDeflect) return;
        
        zone.SetActive(true);
        StartCoroutine(DeflectCooldown(zone));
    }

    private System.Collections.IEnumerator DeflectCooldown(GameObject zone)
    {
        canDeflect = false;
        
        // Keep deflect zone active briefly
        yield return new WaitForSeconds(0.2f);
        zone.SetActive(false);
        
        // Wait for cooldown
        yield return new WaitForSeconds(deflectCooldown);
        canDeflect = true;
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            HandleOwnerMovement();
        }
    }

    private void HandleOwnerMovement()
    {
        Vector3 targetVelocity = movementDirection * moveSpeed;
        Vector3 currentVelocity = rb.velocity;
        targetVelocity.y = currentVelocity.y;
        
        rb.velocity = targetVelocity;
        netPosition.Value = transform.position;
        netRotation.Value = transform.rotation;
    }

    private void HandleClientMovement()
    {
        transform.position = Vector3.Lerp(transform.position, netPosition.Value, Time.deltaTime * 10f);
        transform.rotation = Quaternion.Lerp(transform.rotation, netRotation.Value, Time.deltaTime * 10f);
    }

    private void GetDirection()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        movementDirection = transform.forward * verticalInput + transform.right * horizontalInput;
        movementDirection = Vector3.ClampMagnitude(movementDirection, 1f);
    }

    private void CheckGrounded()
    {
        isGrounded = Physics.SphereCast(
            transform.position + Vector3.up * 0.1f,
            0.5f,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance + 0.1f,
            groundMask
        );
    }

    private void HandleJump()
    {
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position + Vector3.down * groundCheckDistance,
            0.5f
        );
    }
}
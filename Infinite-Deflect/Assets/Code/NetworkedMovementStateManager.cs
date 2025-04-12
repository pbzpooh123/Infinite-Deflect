using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class NetworkedMovementStateManager : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Deflect Settings")]
    [SerializeField] private float deflectCooldown = 0.5f;
    [SerializeField] private GameObject deflectZonePrefab;
    [SerializeField] private Vector3 deflectZoneOffset = new Vector3(0, 1f, 0.5f);

    private Rigidbody rb;
    private Vector3 movementDirection;
    private bool isGrounded;
    private bool canDeflect = true;
    private GameObject deflectZone;
    private Transform cameraTransform;
    private Animator animator;

    private NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    // Network Variables for Animation Sync
    private NetworkVariable<float> netMoveMagnitude = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<bool> netIsIdle = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing!");
            enabled = false;
            return;
        }

        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Start()
    {
        
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            rb.isKinematic = true; // Prevents non-owners from affecting physics
            return;
        }

        StartCoroutine(WaitForCamera());
        gameObject.tag = "Player";

        // Subscribe to network animation updates
        netMoveMagnitude.OnValueChanged += OnAnimationChanged;
        netIsIdle.OnValueChanged += OnAnimationChanged;
    }

    private IEnumerator WaitForCamera()
    {
        while (cameraTransform == null)
        {
            GameObject cameraObj = GameObject.FindWithTag("PlayerCamera");
            if (cameraObj != null)
            {
                cameraTransform = cameraObj.transform;
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void Update()
    {
        if (IsOwner)
        {
            GetDirection();
            CheckGrounded();
            HandleJump();
            HandleAttack(); 
            HandleDeflect();
            UpdateAnimation();
        }
        else
        {
            HandleClientMovement();
        }
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
        Vector3 currentVelocity = rb.linearVelocity;
        targetVelocity.y = currentVelocity.y;

        rb.linearVelocity = targetVelocity;
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
        if (cameraTransform == null) return;

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        movementDirection = (forward * verticalInput + right * horizontalInput).normalized;

        if (movementDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movementDirection);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
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
        if (isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator.SetTrigger("Jump");
        }
    }

    private void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0)) 
        {
            PlayAttackAnimationServerRpc();
        }
    }

    [ServerRpc]
    private void PlayAttackAnimationServerRpc()
    {
        PlayAttackAnimationClientRpc();
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger("Hit"); 
        }
    }

    private void HandleDeflect()
    {
        if (!canDeflect || deflectZone == null) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            ActivateDeflect(deflectZone);
        }
    }

    private void ActivateDeflect(GameObject zone)
    {
        if (!canDeflect || zone == null) return;

        animator.SetTrigger("Hit"); 
        PlayAttackAnimationServerRpc(); 
        zone.SetActive(true);
        StartCoroutine(DeflectCooldown(zone));
    }

    private IEnumerator DeflectCooldown(GameObject zone)
    {
        canDeflect = false;

        yield return new WaitForSeconds(0.2f);
        zone.SetActive(false);

        yield return new WaitForSeconds(deflectCooldown);
        canDeflect = true;
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        if (IsOwner)
        {
            float moveMagnitude = movementDirection.magnitude;
            bool isIdle = (moveMagnitude == 0);

            // Update the network variables
            netMoveMagnitude.Value = moveMagnitude;
            netIsIdle.Value = isIdle;

            // Set local animation (for the owner)
            animator.SetFloat("Running", moveMagnitude);
            animator.SetBool("IsIdle", isIdle);
        }
        else
        {
            // Apply synced animation values for non-owners
            animator.SetFloat("Running", netMoveMagnitude.Value);
            animator.SetBool("IsIdle", netIsIdle.Value);
        }
    }

    private void OnAnimationChanged(float oldValue, float newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetFloat("Running", netMoveMagnitude.Value);
        }
    }

    private void OnAnimationChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetBool("IsIdle", netIsIdle.Value);
        }
    }
}

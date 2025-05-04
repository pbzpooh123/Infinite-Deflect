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

    private Rigidbody rb;
    private Vector3 movementDirection;
    [SerializeField] private bool isGrounded;
    private bool canDeflect = true;
    private bool isDeflecting = false;
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
        deflectZone = transform.Find("DeflectZone")?.gameObject;

        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing!");
            enabled = false;
            return;
        }

        if (deflectZone == null)
        {
            Debug.LogError("DeflectZone child object not found!");
        }

        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            rb.isKinematic = true;
            return;
        }

        StartCoroutine(WaitForCamera());
        gameObject.tag = "Player";

        netMoveMagnitude.OnValueChanged += OnMoveMagnitudeChanged;
        netIsIdle.OnValueChanged += OnIsIdleChanged;
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
            HandleDeflect();
        }

        UpdateAnimation();

        if (!IsOwner)
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
            Vector3 velocity = rb.velocity;
            velocity.y = 0f;
            rb.velocity = velocity;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            animator.SetTrigger("Jump");
        }
    }

    private void HandleDeflect()
    {
        if (!canDeflect)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F))
        {
            DeflectServerRpc();
        }
    }

    [ServerRpc]
    private void DeflectServerRpc()
    {
        DeflectClientRpc();
    }

    [ClientRpc]
    private void DeflectClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }

        if (deflectZone != null && !isDeflecting)
        {
            StartCoroutine(DoDeflect());
        }
    }

    private IEnumerator DoDeflect()
    {
        isDeflecting = true;
        canDeflect = false;

        deflectZone.SetActive(true);

        yield return new WaitForSeconds(1f);
        deflectZone.SetActive(false);

        yield return new WaitForSeconds(deflectCooldown);
        canDeflect = true;
        isDeflecting = false;
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        if (IsOwner)
        {
            float moveMagnitude = movementDirection.magnitude;
            bool isIdle = (moveMagnitude == 0f);

            netMoveMagnitude.Value = moveMagnitude;
            netIsIdle.Value = isIdle;

            animator.SetFloat("Running", moveMagnitude);
            animator.SetBool("IsIdle", isIdle);
        }
        else
        {
            animator.SetFloat("Running", netMoveMagnitude.Value);
            animator.SetBool("IsIdle", netIsIdle.Value);
        }
    }

    private void OnMoveMagnitudeChanged(float oldValue, float newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetFloat("Running", newValue);
        }
    }

    private void OnIsIdleChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetBool("IsIdle", newValue);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Vector3 endPoint = origin + Vector3.down * (groundCheckDistance + 0.1f);
        Gizmos.DrawWireSphere(endPoint, 0.5f);
    }
}

using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    public float moveSpeed = 5f;
    private bool canDeflect = false;
    public float deflectDuration = 0.5f;

    private InputSystem_Actions controls;
    private Vector2 moveInput;
    private Rigidbody rb; // Reference to Rigidbody
    
    void Awake()
    {
        controls = new InputSystem_Actions();
        rb = GetComponent<Rigidbody>(); // Get Rigidbody component
    }

    void OnEnable()
    {
        controls.Enable();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        controls.Player.Deflect.performed += _ => StartDeflection();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void FixedUpdate() // Use FixedUpdate for Rigidbody movement
    {
        if (!IsOwner) return;

        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed;
        rb.velocity = new Vector3(move.x, rb.velocity.y, move.z); // Preserve vertical velocity (gravity)
    }

    void StartDeflection()
    {
        canDeflect = true;
        Invoke(nameof(EndDeflection), deflectDuration);
    }

    void EndDeflection()
    {
        canDeflect = false;
    }

    public bool CanDeflect()
    {
        return canDeflect;
    }
}
// ============================================================
// 1. Script Name: Movement.cs
// 2. Purpose: Handles 2D character movement (walking, jumping) and custom gravity/physics via raycasts.
// 3. Unity Setup Instructions:
//    - Attach to: The Player GameObject.
//    - Required Components: Rigidbody2D, Animator, Main Camera.
//    - Tags/Layers: Ground objects must be on the "Ground" layer to enable jumping.
// ============================================================

using UnityEngine;

public class Movement : MonoBehaviour
{
    private new Camera camera;
    private new Rigidbody2D rigidbody;

    [Header("Input")]
    [SerializeField] private bool useMobileButtonInput = true;
    [SerializeField] private bool allowKeyboardFallback = false;
    [SerializeField] private bool disableInputWhenTerminalOpen = true;

    public float moveSpeed = 8f;
    public float maxJumpHeight = 3f;
    public float maxJumpTime = 0.75f;
    public float jumpForce => (2f * maxJumpHeight) / (maxJumpTime / 2f);
    public float Gravity => (-2f * maxJumpHeight) / Mathf.Pow(maxJumpTime / 2f, 2f);
    public bool Grounded { get; private set; }
    public bool Jumping { get; private set; }
    private float inputAxis;
    public Vector2 velocity;

    private bool forwardHeld;
    private bool backwardHeld;
    private bool jumpHeld;
    private bool jumpQueued;
    private bool inputLocked;

    public Animator animator;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        camera = Camera.main;
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        TerminalLevelController.OnTerminalModalVisibilityChanged += HandleTerminalModalVisibilityChanged;
    }

    private void OnDisable()
    {
        TerminalLevelController.OnTerminalModalVisibilityChanged -= HandleTerminalModalVisibilityChanged;
    }

    private void Update()
    {
        HorizontalMovement();

        Grounded = rigidbody.Raycast(Vector2.down);

        if (Grounded)
        {
            GroundedMovement();
        }

        ApplyGravity();

        if (animator != null && animator.enabled)
            animator.SetFloat("speed", Mathf.Abs(velocity.x));

    }

    private void GroundedMovement()
    {
        velocity.y = Mathf.Max(velocity.y, 0f);
        Jumping = velocity.y > 0f;

        if (inputLocked)
        {
            return;
        }

        bool keyboardJumpPressed = !useMobileButtonInput || allowKeyboardFallback ? Input.GetButtonDown("Jump") : false;
        if (jumpQueued || keyboardJumpPressed)
        {
            velocity.y = jumpForce;
            Jumping = true;
            jumpQueued = false;
            AudioManager.instance.Play("Jump");
        }
    }

    private void ApplyGravity()
    {
        bool keyboardJumpHeld = !useMobileButtonInput || allowKeyboardFallback ? Input.GetButton("Jump") : false;
        bool activeJumpHold = !inputLocked && ((useMobileButtonInput && jumpHeld) || keyboardJumpHeld);
        bool falling = velocity.y < 0f || !activeJumpHold;
        float multiplier = falling ? 3f : 1f;

        velocity.y += Gravity * multiplier * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, Gravity);
    }

    private void HorizontalMovement()
    {
        if (inputLocked)
        {
            inputAxis = 0f;
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, moveSpeed * Time.deltaTime);
            return;
        }

        if (useMobileButtonInput)
        {
            inputAxis = GetMobileHorizontalInput();

            if (allowKeyboardFallback)
            {
                float keyboardAxis = Input.GetAxisRaw("Horizontal");
                if (Mathf.Abs(keyboardAxis) > Mathf.Abs(inputAxis))
                {
                    inputAxis = keyboardAxis;
                }
            }
        }
        else
        {
            inputAxis = Input.GetAxis("Horizontal");
        }

        velocity.x = Mathf.MoveTowards(velocity.x, inputAxis * moveSpeed, moveSpeed * Time.deltaTime);

        if (rigidbody.Raycast(Vector2.right * velocity.x))
        {
            velocity.x = 0f;
        }

        if (velocity.x > 0f)
        {
            transform.eulerAngles = Vector3.zero;
        }
        else if (velocity.x < 0f)
        {
            transform.eulerAngles = new Vector3(0f, 180f, 0f);
        }
    }

    private void FixedUpdate()
    {
        Vector2 position = rigidbody.position;
        position += velocity * Time.fixedDeltaTime;

        Vector2 leftEdge = camera.ScreenToWorldPoint(Vector2.zero);
        Vector2 rightEdge = camera.ScreenToWorldPoint(new Vector2(Screen.width, Screen.height));
        position.x = Mathf.Clamp(position.x, leftEdge.x + 0.5f, rightEdge.x - 0.5f);

        rigidbody.MovePosition(position);

        if (animator != null && animator.enabled)
            animator.SetBool("isGrounded", Grounded);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer != LayerMask.NameToLayer("Ground"))
        {
            if (transform.DoTest(collision.transform, Vector2.up))
            {
                velocity.y = 0f;
            }
        }
    }

    private float GetMobileHorizontalInput()
    {
        if (forwardHeld == backwardHeld)
        {
            return 0f;
        }

        return forwardHeld ? 1f : -1f;
    }

    // UI Button Hook: Forward button OnPointerDown
    public void OnForwardDown()
    {
        if (inputLocked) return;
        forwardHeld = true;
    }

    // UI Button Hook: Forward button OnPointerUp
    public void OnForwardUp()
    {
        forwardHeld = false;
    }

    // UI Button Hook: Backward button OnPointerDown
    public void OnBackwardDown()
    {
        if (inputLocked) return;
        backwardHeld = true;
    }

    // UI Button Hook: Backward button OnPointerUp
    public void OnBackwardUp()
    {
        backwardHeld = false;
    }

    // UI Button Hook: Jump button OnPointerDown
    public void OnJumpDown()
    {
        if (inputLocked) return;
        jumpHeld = true;
        jumpQueued = true;
    }

    // UI Button Hook: Jump button OnPointerUp
    public void OnJumpUp()
    {
        jumpHeld = false;
    }

    private void HandleTerminalModalVisibilityChanged(bool visible)
    {
        if (!disableInputWhenTerminalOpen)
        {
            return;
        }

        inputLocked = visible;

        if (inputLocked)
        {
            ClearBufferedInput();
        }
    }

    private void ClearBufferedInput()
    {
        forwardHeld = false;
        backwardHeld = false;
        jumpHeld = false;
        jumpQueued = false;
    }

}

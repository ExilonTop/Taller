using UnityEngine;
using UnityEngine.InputSystem;

public class Move_Plataforma2 : MonoBehaviour
{
    [SerializeField]private Rigidbody2D m_rigi;
    [SerializeField]private float m_speed;
    [SerializeField]private InputActionReference m_move;
    [SerializeField] private float m_jumpForce;
    [SerializeField] private InputActionReference m_jumpAction;
    [SerializeField] private Transform m_groundCheck;		// La posición de detección, debajo de los pies
    [SerializeField] private float m_groundCheckRadius;		// Que tan grande es la detección
    [SerializeField] private LayerMask m_groundLayer;		// La layer del suelo
    [SerializeField] private int m_maxJumps = 2;		// La layer del suelo

    private bool m_isGrounded;
    private int jumpsCount;
    [SerializeField] private float m_jumpingGravity = 1;
    [SerializeField] private float m_fallingGravity = 3;
    [SerializeField] private float m_maxFallVelocity = -10;

    private float m_groundDetection = 0;

    private void OnEnable()
    {
        m_move.action.Enable();
        m_jumpAction.action.Enable();

        m_jumpAction.action.started += HandleJumpInput;
        m_jumpAction.action.canceled += HandleJumpInput;
    }

    void OnDisable()
    {
        m_move.action.Disable();
        m_jumpAction.action.Disable();

        m_jumpAction.action.started -= HandleJumpInput;
        m_jumpAction.action.canceled -= HandleJumpInput;
    }

    void Update()
    {
        if (m_groundDetection > 0)
            m_groundDetection -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        float horizontal = m_move.action.ReadValue<Vector2>().x;
        m_rigi.linearVelocityX = horizontal*m_speed;

        if (m_groundDetection <= 0)
            m_isGrounded = Physics2D.OverlapCircle(m_groundCheck.position, m_groundCheckRadius, m_groundLayer);

        float velocityY = Mathf.Max(m_rigi.linearVelocity.y, m_maxFallVelocity);
        m_rigi.linearVelocity = new Vector2(m_rigi.linearVelocity.x, velocityY);
        
        if(m_rigi.linearVelocity.y >= 0)
        {
	        m_rigi.gravityScale = m_jumpingGravity;
        }

        else
        { 
	        m_rigi.gravityScale = m_fallingGravity;
        }

        if (m_isGrounded)
            jumpsCount = 0;
    }

    public void HandleJumpInput(InputAction.CallbackContext context)
    {
	    if (context.started && jumpsCount < m_maxJumps)
        {
            if (jumpsCount == 0)
            {
                m_groundDetection = 0.2f;
                m_isGrounded = false;
            }

	        m_rigi.linearVelocityY = m_jumpForce;
	        jumpsCount ++;
        }

        else if (context.canceled && m_rigi.linearVelocityY > 0) 
        {
            m_rigi.linearVelocityY /= 2;
        }
    }
}
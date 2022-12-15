using UnityEngine;
using Riptide;


public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float airSpeed;

    [SerializeField] private float acceleration;
    [SerializeField] private float airAcceleration;

    [SerializeField] private float jumpTime;
    [SerializeField] private float coyoteTime;

    private ServerPlayer player;
    private Rigidbody2D rb;

    Vector2 moveDir;
    Vector2 checkpoint = Vector2.zero;

    float currentJump = 0;
    float jumpQueue = 0;

    float deathTimer = 0;
    float offGroundTimer = 0;
    bool isGrounded = false;

    private void Awake()
    {
        player = GetComponent<ServerPlayer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!isGrounded)
            offGroundTimer += Time.deltaTime;
        else
            offGroundTimer = 0;

        if (deathTimer > 0)
        {
            deathTimer -= Time.deltaTime;
            if (deathTimer <= 0)
                transform.position = checkpoint;
        }

        jumpQueue -= Time.deltaTime;
        currentJump -= Time.deltaTime;

        CheckJump();
    }

    private void FixedUpdate()
    {
        //movement code
        if (deathTimer > 0)
            rb.velocity = Vector2.zero;
        else
            rb.velocity = Vector2.Lerp(rb.velocity, moveDir.normalized * (currentJump <= 0 ? moveSpeed : airSpeed), Time.deltaTime * (currentJump <= 0 ? acceleration : airAcceleration));

        if (offGroundTimer >= coyoteTime && deathTimer <= 0 && currentJump <= 0)
            Death(0);

        SendPlayerTick();
    }

    public void SetMoveDir(Vector2 dir, bool jump)
    {
        moveDir = dir;
        if (jump)
            QueueJump();
    }

    void QueueJump()
    {
        jumpQueue = 0.05f;
        CheckJump();
    }

    void CheckJump()
    {
        if (jumpQueue > 0 && currentJump <= 0 && offGroundTimer < coyoteTime)
        {
            currentJump = jumpTime;
            jumpQueue = 0;
        }
    }

    void Death(int deathType)
    {
        //0 = fall
        //1 = tumble
        deathTimer = 0.5f;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Ground"))
            isGrounded = true;
        if (collision.CompareTag("Checkpoint"))
            checkpoint = collision.transform.position;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Ground"))
            isGrounded = false;
    }

    #region Messages
    private void SendPlayerTick()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.PlayerTick);
        message.AddUShort(player.Id);
        message.AddVector2(transform.position);
        message.AddVector2(moveDir);
        message.AddBool(currentJump > 0);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion
}

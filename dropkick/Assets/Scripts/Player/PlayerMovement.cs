using UnityEngine;
using Riptide;


public class PlayerMovement : MonoBehaviour
{
    public const float Gravity = -75f;
    public const float GravityPow = 1.025f;
    public const float JumpForceFactor = 1.75f;
    public const float JumpOffset = 10f;
    public const float LandingFactor = 0.5f;
    public const float DefaultDrag = 5.5f;
    public const float AirDrag = 0f;

    [SerializeField] private float maxJumpForce;
    [SerializeField] private float minJumpForceMultiplier = 0.2f;

    [SerializeField] private float knockback;
    [SerializeField] private float landRadius;
    [SerializeField] private LayerMask mask;

    private float verticalVelocity;
    private float gravity;
    private bool isJumping = false;
    private float proxyY = 0f;

    private ServerPlayer player;
    private Rigidbody2D rb;

    Vector2 checkpoint = Vector2.zero;

    float deathTimer = 0;
    bool isGrounded = false;
    float groundTimer = 0;

    private void Awake()
    {   
        player = GetComponent<ServerPlayer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (isGrounded)
            groundTimer = 0;
        else
            groundTimer += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!isJumping)
            rb.drag = DefaultDrag;
        else
        {
            rb.drag = AirDrag;
            verticalVelocity += gravity * Time.fixedDeltaTime;
            proxyY += verticalVelocity * Time.fixedDeltaTime;
            if (proxyY <= 0f) //landed
            {
                isJumping = false;
                proxyY = 0f;
                rb.velocity *= LandingFactor;

                Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, landRadius, mask);
                if (hits.Length > 0)
                {
                    foreach (Collider2D hit in hits)
                    {
                        if (hit.CompareTag("ServerPlayer") && hit.gameObject != this.gameObject)
                            hit.GetComponent<PlayerMovement>().Hit((hit.transform.position - transform.position).normalized, knockback);
                    }
                }
            }
        }


        //movement code
        if (deathTimer > 0)
            rb.velocity = Vector2.zero;

        if (groundTimer > 0.05 && !isJumping && deathTimer <= 0)
            Death(0);

        if (deathTimer <= 0)
            return;

        deathTimer -= Time.fixedDeltaTime;
        if (deathTimer <= 0)
        {
            isGrounded = true;
            transform.position = checkpoint;
            rb.velocity = Vector2.zero;
            PlayerRespawn();
        }
    }

    void Jump(float force) //the higher the force, the higher the jump
    {
        verticalVelocity = force * JumpForceFactor + JumpOffset;
        gravity = Gravity * Mathf.Pow(GravityPow, verticalVelocity);
        isJumping = true;
    }

    public void SetMoveDir(Vector2 jumpDir, float jumpForce)
    {
        if (isJumping || groundTimer > 0.05 || deathTimer > 0)
            return;

        jumpForce = Mathf.Clamp(jumpForce, minJumpForceMultiplier, 1.0f);
        jumpForce *= maxJumpForce;

        rb.velocity = jumpDir.normalized * jumpForce;
        Jump(jumpForce);

        PlayerJump(jumpDir.normalized, jumpForce, true);
    }

    private void Hit(Vector2 dir, float knockback)
    {
        if (isJumping || deathTimer > 0)
            return;

        rb.velocity = dir * knockback;
        Jump(knockback);
        PlayerJump(dir, knockback, true);
    }

    void Death(int deathType)
    {
        //0 = fall
        //1 = tumble
        deathTimer = 0.5f;
        PlayerDeath();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Ground"))
            isGrounded = true;
        if (collision.CompareTag("Checkpoint") && !isJumping)
            checkpoint = collision.transform.position;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Ground"))
            isGrounded = true;
        if (collision.CompareTag("Checkpoint") && !isJumping)
            checkpoint = collision.transform.position;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Ground"))
            isGrounded = false;
    }

    #region Messages
    private void PlayerJump(Vector2 dir, float force, bool jump)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.PlayerJump);
        message.AddUShort(player.Id);
        message.AddVector2(transform.position);
        message.AddVector2(dir);
        message.AddFloat(force);
        message.AddBool(jump);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    void PlayerDeath()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.PlayerDeath);
        message.AddUShort(player.Id);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    void PlayerRespawn()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.PlayerRespawn);
        message.AddUShort(player.Id);
        message.AddVector2(transform.position);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion
}

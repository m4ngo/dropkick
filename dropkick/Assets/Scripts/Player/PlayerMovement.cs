using UnityEngine;
using Riptide;


public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float maxJumpForce;
    [SerializeField] private float minJumpForceMultiplier = 0.2f;

    [SerializeField] private float knockback;
    [SerializeField] private float landRadius;
    [SerializeField] private LayerMask mask;

    private ServerPlayer player;
    private Rigidbody2D rb;

    Vector2 checkpoint = Vector2.zero;

    float deathTimer = 0;
    float jumpTimer = 0;
    bool isGrounded = false;

    private void Awake()
    {
        player = GetComponent<ServerPlayer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (deathTimer <= 0)
            return;

        deathTimer -= Time.deltaTime;
        if (deathTimer <= 0)
        {
            isGrounded = true;
            transform.position = checkpoint;
            PlayerRespawn();
        }
    }

    private void FixedUpdate()
    {
        //movement code
        if (deathTimer > 0)
            rb.velocity = Vector2.zero;

        if(jumpTimer > 0)
        {
            jumpTimer -= Time.deltaTime;
            if(jumpTimer <= 0)
            {
                //landed
                /*Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, landRadius, mask);
                if(hits.Length > 0)
                {
                    foreach (Collider2D hit in hits)
                    {
                        hit.GetComponent<PlayerMovement>().Hit((transform.position - hit.transform.position).normalized, knockback);
                    }
                }*/
            }
        }
        if (!isGrounded && jumpTimer <= 0 && deathTimer <= 0)
            Death(0);
    }

    public void SetMoveDir(Vector2 jumpDir, float jumpForce)
    {
        if (jumpTimer > 0 || !isGrounded)
            return;

        jumpForce = Mathf.Clamp(jumpForce, minJumpForceMultiplier, 1.0f);
        jumpForce *= maxJumpForce;

        rb.velocity = jumpDir.normalized * jumpForce;
        jumpTimer = 0.5f;

        PlayerJump(jumpDir.normalized, jumpForce, true);
    }

    private void Hit(Vector2 dir, float knockback)
    {
        rb.velocity = dir * knockback;
        PlayerJump(dir, knockback, false);
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
        if (collision.CompareTag("Checkpoint") && jumpTimer <= 0)
            checkpoint = collision.transform.position;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Ground"))
            isGrounded = true;
        if (collision.CompareTag("Checkpoint") && jumpTimer <= 0)
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

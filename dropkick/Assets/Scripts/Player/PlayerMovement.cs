using UnityEngine;
using Riptide;

public class PlayerMovement : MonoBehaviour
{
    //player movement constants 
    public const float Gravity = -50f;
    public const float GravityPow = 1.025f;
    public const float JumpForceFactor = 1.0f;
    public const float JumpOffset = 10f;
    public const float LandingFactor = 1.0f;
    public const float DefaultDrag = 5.5f;
    public const float AirDrag = 1f;
    public const float MaxJumpForce = 14f;
    public const float MinJumpForceMultiplier = 0.2f;
    public const float AirControlSpeed = 10.25f;
    public const float AirControlScale = 7f;

    //environment constants
    public const float IceDrag = 2f;
    public const float SlimeDrag = 15f;
    public static readonly string[] GroundTags = { "Ground", "Ice", "Slime" };

    [SerializeField] private float knockbackScale;
    [SerializeField] private float landRadius;
    [SerializeField] private LayerMask mask;

    [Header("Ground Detection")]
    [SerializeField] private float checkRadius;
    [SerializeField] private LayerMask checkMask;

    private float knockback;
    private float verticalVelocity;
    private float gravity;
    private bool isJumping = false;
    private float proxyY = 0f;

    private ServerPlayer player;
    private Rigidbody rb;

    Vector3 checkpoint = Vector3.zero;

    float deathTimer = 0;
    public bool isGrounded = false;
    Collider currentGround = null;

    private void Awake()
    {
        player = GetComponent<ServerPlayer>();
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        GroundChecks();

        if (!isJumping){
            rb.drag = GetCurrentGroundType(currentGround);
        }
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

                Collider[] hits = Physics.OverlapSphere(transform.position, landRadius, mask);
                if (hits.Length > 0)
                { //check hit players and send hits to all clients
                    foreach (Collider hit in hits)
                    {
                        if (hit.CompareTag("ServerPlayer") && hit.gameObject != this.gameObject)
                            hit.GetComponent<PlayerMovement>().Hit((hit.transform.position - transform.position).normalized, knockback * knockbackScale);
                    }
                }
            }
        }

        //check resync
        if (isGrounded && rb.velocity.sqrMagnitude > 0.2f)
        {
            SendResync();
        }

        //movement code
        if (deathTimer > 0)
            rb.velocity = Vector2.zero;

        if (!isGrounded && !isJumping && deathTimer <= 0)
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

    void GroundChecks(){
        Collider[] hits = Physics.OverlapSphere(transform.position, checkRadius, checkMask);
        isGrounded = hits.Length > 0;
        
        foreach(Collider col in hits){
            if(col.CompareTag("Checkpoint")){
                if(!isJumping){
                    checkpoint = col.transform.position;
                }
            }
            else {
                currentGround = col;
            }
        }
    }

    void Jump(float force) //the higher the force, the higher the jump
    {
        knockback = force;
        verticalVelocity = force * JumpForceFactor + JumpOffset;
        gravity = Gravity * Mathf.Pow(GravityPow, verticalVelocity);
        isJumping = true;
    }

    public void AirControl(Vector3 dir)
    {
        if (!isJumping || deathTimer > 0) return;
        rb.velocity += Time.fixedDeltaTime * dir * AirControlSpeed * (rb.velocity.magnitude / AirControlScale);
        SendAirControl();
    }

    public void SetMoveDir(Vector3 jumpDir, float jumpForce)
    {
        if (isJumping || !isGrounded || deathTimer > 0)
            return;

        jumpForce = Mathf.Clamp(jumpForce, MinJumpForceMultiplier, 1.0f) * MaxJumpForce;

        jumpDir.y = 0f;
        rb.velocity = jumpDir.normalized * jumpForce;
        Jump(jumpForce);

        SendJump(jumpDir.normalized, jumpForce, false);
    }

    private void Hit(Vector3 dir, float hitKnockback)
    {
        if (isJumping || deathTimer > 0)
            return;

        dir.y = 0f;
        rb.velocity = dir * hitKnockback;
        Jump(hitKnockback);
        SendJump(dir, hitKnockback, true);
    }

    void Death(int deathType)
    {
        //0 = fall
        //1 = tumble
        deathTimer = 0.5f;
        PlayerDeath();
    }

    public static bool GroundTagDetected(Collider col){
        foreach(string s in GroundTags){
            if(col.CompareTag(s)) return true;
        }
        return false;
    }

    public static float GetCurrentGroundType(Collider col){
        if(col == null) return DefaultDrag;
        switch (col.tag){
            case "Ice":
                return IceDrag;
            case "Slime":
                return SlimeDrag;
            default:
                return DefaultDrag;
        }
    }

    #region Messages
    private void SendJump(Vector2 dir, float force, bool hit)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.PlayerJump);
        message.AddUShort(player.Id);
        message.AddVector3(transform.position);
        message.AddVector3(dir);
        message.AddFloat(force);
        message.AddBool(hit);
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
        message.AddVector3(transform.position);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    void SendResync()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.ResyncPosition);
        message.AddVector3(transform.position);
        message.AddVector3(rb.velocity);
        NetworkManager.Singleton.Server.Send(message, player.Id);
    }

    void SendAirControl()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.ResyncAirControl);
        message.AddUShort(player.Id);
        message.AddVector3(rb.velocity);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion
}

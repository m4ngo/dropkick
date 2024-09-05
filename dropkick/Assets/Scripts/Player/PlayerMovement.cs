using UnityEngine;
using Riptide;
using System;

public class PlayerMovement : MonoBehaviour
{
    //player movement constants 
    public const float Gravity = -50f;
    public const float GravityPow = 1.025f;
    public const float JumpForceFactor = 1.0f;
    public const float JumpOffset = 10f;
    public const float LandingFactor = 1.0f;
    public const float DefaultDrag = 6.5f;
    public const float AirDrag = 1f;
    public const float MaxJumpForce = 14f;
    public const float MinJumpForceMultiplier = 0.2f;
    public const float AirControlSpeed = 10.25f;
    public const float AirControlScale = 7f;
    public const float Knockback = 15f;

    //jump cooldowns
    public const float MaxJumps = 3;
    public const float JumpReloadTime = 0.3f;

    //environment constants
    public const float SpeedDrag = 0f;
    public const float IceDrag = 2f;
    public const float SlimeDrag = 15f;
    public static readonly string[] GroundTags = { "Ground", "Ice", "Slime", "Speed" };

    [SerializeField] private float landRadius;
    [SerializeField] private LayerMask mask;

    [Header("Ground Detection")]
    [SerializeField] private float checkRadius;
    [SerializeField] private LayerMask checkMask;

    private float verticalVelocity;
    private float gravity;
    private bool isJumping = false;
    private float proxyY = 0f;
    private float hitLock = 0f; //freeze the player's movement when they're hit

    // private int curJumps = 3;
    // private float curReload = 0f; //reloading time for jumps

    private ServerPlayer player;
    private Rigidbody rb;

    Vector3 checkpoint = Vector3.zero;

    public float deathTimer { get; private set; } = 0;
    public bool freeze { get; private set;  } = false;
    public bool isGrounded = false;
    Collider[] currentGround = null;

    private void Awake()
    {
        player = GetComponent<ServerPlayer>();
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        Freeze(true);
    }

    private void FixedUpdate()
    {
        GroundChecks();

        if(hitLock > 0) hitLock -= Time.fixedDeltaTime;

        if (!isJumping){
            //set player drag to whatever ground they're standing on
            rb.drag = GetCurrentGroundType(currentGround);

            //jump reload handling
            // if(curJumps < MaxJumps){
            //     curReload += Time.fixedDeltaTime;
            //     if(curReload >= JumpReloadTime){
            //         curJumps++;
            //         curReload = 0f;
            //     }
            // }
        }
        else
        {
            // curReload = 0f;

            rb.drag = AirDrag;
            verticalVelocity += gravity * Time.fixedDeltaTime;
            proxyY += verticalVelocity * Time.fixedDeltaTime;
            if (proxyY <= 0f) //landed
            {
                //ALWAYS RESYNC WHEN LANDING
                SendResync();
                isJumping = false;
                proxyY = 0f;
                rb.velocity *= LandingFactor;

                Collider[] hits = Physics.OverlapSphere(transform.position, landRadius, mask);
                if (hits.Length > 0)
                { //check hit players and send hits to all clients
                    foreach (Collider hit in hits)
                    {
                        if (hit.CompareTag("ServerPlayer") && hit.gameObject != this.gameObject)
                            hit.GetComponent<PlayerMovement>().Hit((hit.transform.position - transform.position).normalized);
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

        if (!isGrounded && !isJumping && deathTimer <= 0 && !freeze)
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
        currentGround = hits;
        
        foreach(Collider col in hits){
            if(col.CompareTag("Checkpoint")){
                if(!isJumping){
                    checkpoint = col.transform.position;
                }
            }
        }
    }

    void Jump(float force) //the higher the force, the higher the jump
    {
        // curJumps--;
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
        if (isJumping || !isGrounded || deathTimer > 0 || hitLock > 0f || freeze /*|| curJumps <= 0*/) //things that prevent jumping
            return;

        jumpForce = Mathf.Clamp(jumpForce, MinJumpForceMultiplier, 1.0f) * MaxJumpForce;

        jumpDir.y = 0f;
        rb.velocity = jumpDir.normalized * jumpForce;
        Jump(jumpForce);

        SendJump(jumpDir.normalized, jumpForce);
    }

    private void Hit(Vector3 dir)
    {
        if (isJumping || deathTimer > 0)
            return;

        dir.y = 0f;
        rb.velocity = dir.normalized * Knockback;
        SendHit(dir);
        hitLock = 0.25f;
    }

    public void Freeze(bool freeze)
    {
        this.freeze = freeze;

        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.Freeze);
        message.AddUShort(player.Id);
        message.AddBool(freeze);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SetDeath(float time)
    {
        deathTimer = time;
        PlayerDeath();
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

    public static float GetCurrentGroundType(Collider[] cols){
        if(cols == null) return DefaultDrag;
        if(cols.Length <= 0) return DefaultDrag;
        float drag = 0;
        foreach(Collider col in cols){
            float cur = GetGroundExtend(col);
            drag = Mathf.Max(drag, cur);
        }
        return drag;
    }

    static float GetGroundExtend(Collider col){
        switch (col.tag){
            case "Ice":
                return IceDrag;
            case "Slime":
                return SlimeDrag;
            case "Speed":
                return SpeedDrag;
            case "Checkpoint":
                return 0f;
            case "ClientCheckpoint":
                return 0f;
            default:
                return DefaultDrag;
        }
    }

    #region Messages
    private void SendJump(Vector3 dir, float force)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.PlayerJump);
        message.AddUShort(player.Id);
        message.AddVector3(transform.position);
        message.AddVector3(dir);
        message.AddFloat(force);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    private void SendHit(Vector3 dir){
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.PlayerHit);
        message.AddUShort(player.Id);
        message.AddVector3(transform.position);
        message.AddVector3(dir);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    void PlayerDeath()
    {
        if (freeze)
        {
            return;
        }
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.PlayerDeath);
        message.AddUShort(player.Id);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    void PlayerRespawn()
    {
        if (freeze)
        {
            return;
        }
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.PlayerRespawn);
        message.AddUShort(player.Id);
        message.AddVector3(transform.position);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendResync()
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

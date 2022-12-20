using System.Collections.Generic;
using UnityEngine;
using Riptide;


public class ClientPlayer : MonoBehaviour
{
    public static Dictionary<ushort, ClientPlayer> list = new Dictionary<ushort, ClientPlayer>();

    [SerializeField] private ushort id;
    [SerializeField] private string username;

    [SerializeField] private Transform cam;
    [SerializeField] private float camSpeed = 2.5f;

    [SerializeField] private Transform playerSprite;
    private float verticalVelocity;
    private float gravity;

    public bool isJumping { get; private set; }  = false;
    private Vector2 startPos;

    [SerializeField] private ParticleSystem jumpParticle;
    [SerializeField] private ParticleSystem checkpointParticle;
    [SerializeField] private ParticleSystem landParticle;
    private Transform checkpoint;

    private Animator anim;
    private Rigidbody2D rb;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        startPos = playerSprite.localPosition;
        if(cam != null)
            cam.SetParent(null);
    }

    private void OnDestroy()
    {
        list.Remove(id);
    }

    public static void Spawn(ushort id, string username, Vector3 position)
    {
        ClientPlayer player;
        if (id == NetworkManager.Singleton.Client.Id)
            player = Instantiate(NetworkManager.Singleton.LocalPlayerPrefab, position, Quaternion.identity).GetComponent<ClientPlayer>();
        else
        {
            player = Instantiate(NetworkManager.Singleton.PlayerPrefab, position, Quaternion.identity).GetComponent<ClientPlayer>();
            player.GetComponentInChildren<PlayerUIManager>().SetName(username);
        }

        player.name = $"Client Player {id} ({username})";
        player.id = id;
        player.username = username;
        list.Add(player.id, player);
    }

    private void FixedUpdate()
    {
        if (cam != null)
            cam.position = Vector3.Lerp(cam.position, new Vector3(transform.position.x, transform.position.y, -10), Time.deltaTime * camSpeed);

        if (!isJumping)
        {
            rb.drag = PlayerMovement.DefaultDrag;
            return;
        }

        rb.drag = PlayerMovement.AirDrag;
        verticalVelocity += gravity * Time.fixedDeltaTime;
        playerSprite.localPosition += new Vector3(0, verticalVelocity, 0) * Time.fixedDeltaTime;

        anim.SetBool("Jump", verticalVelocity > 0);
        anim.SetBool("Fall", verticalVelocity <= 0);

        if (playerSprite.localPosition.y <= startPos.y)
        {
            isJumping = false;
            playerSprite.localPosition = startPos;
            rb.velocity *= PlayerMovement.LandingFactor;
            landParticle.Play();
            anim.SetTrigger("Land");
        }
    }

    void Jump(float force) //the higher the force, the higher the jump
    {
        verticalVelocity = force * PlayerMovement.JumpForceFactor + PlayerMovement.JumpOffset;
        gravity = PlayerMovement.Gravity * Mathf.Pow(PlayerMovement.GravityPow, verticalVelocity);
        isJumping = true;
        jumpParticle.Play();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (NetworkManager.Singleton.Client.Id != id)
            return;

        if (collision.CompareTag("ClientCheckpoint") && !isJumping)
        {
            if (checkpoint != collision.transform)
            {
                if (checkpoint != null)
                    checkpoint.GetChild(0).GetComponent<ParticleSystem>().Stop();
                checkpoint = collision.transform;
                checkpoint.GetChild(0).GetComponent<ParticleSystem>().Play();
                checkpointParticle.transform.position = checkpoint.position;
                checkpointParticle.Play();
            }
        }
    }

    #region Messages
    [MessageHandler((ushort)ServerToClientId.SpawnPlayer, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void SpawnPlayer(Message message)
    {
        Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
    }

    [MessageHandler((ushort)ServerToClientId.PlayerJump, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerJump(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.transform.position = message.GetVector2(); //update player position
        Vector2 dir = message.GetVector2();
        float force = message.GetFloat();
        player.rb.velocity = dir * force;

        bool anim = message.GetBool();
        if (anim)
            player.Jump(force);

        player.anim.SetFloat("X", Mathf.RoundToInt(dir.x));
        player.anim.SetFloat("Y", Mathf.RoundToInt(dir.y));
    }

    [MessageHandler((ushort)ServerToClientId.PlayerDeath, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerDeath(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.rb.velocity = Vector2.zero;
        player.anim.SetTrigger("Death");
    }

    [MessageHandler((ushort)ServerToClientId.PlayerRespawn, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerRespawn(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.transform.position = message.GetVector2(); //update player position
        player.rb.velocity = Vector2.zero;
    }
    #endregion
}

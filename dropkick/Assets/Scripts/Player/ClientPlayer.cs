using System.Collections.Generic;
using UnityEngine;
using Riptide;


public class ClientPlayer : MonoBehaviour
{
    public static Dictionary<ushort, ClientPlayer> list = new Dictionary<ushort, ClientPlayer>();

    [SerializeField] private ushort id;
    [SerializeField] private string username;

    [SerializeField] private Transform playerSprite;
    private float verticalVelocity;
    public bool isJumping { get; private set; }  = false;
    private Vector2 startPos;

    [SerializeField] private ParticleSystem checkpointParticle;
    private Transform checkpoint;

    private Animator anim;
    private Rigidbody2D rb;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        startPos = playerSprite.position;
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
        if (!isJumping)
        {
            rb.drag = PlayerMovement.DefaultDrag;
            return;
        }

        rb.drag = PlayerMovement.AirDrag;
        verticalVelocity += PlayerMovement.Gravity * Time.fixedDeltaTime;
        playerSprite.localPosition += new Vector3(0, verticalVelocity, 0) * Time.fixedDeltaTime;
        if(playerSprite.localPosition.y <= startPos.y)
        {
            isJumping = false;
            playerSprite.localPosition = startPos;
            rb.velocity *= PlayerMovement.LandingFactor;
        }
    }

    void Jump(float force) //the higher the force, the higher the jump
    {
        verticalVelocity = Mathf.Pow(PlayerMovement.Pow, (force * PlayerMovement.JumpForceFactor)) + PlayerMovement.JumpOffset;
        isJumping = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Checkpoint") && !isJumping)
        {
            if (checkpoint != collision.transform)
            {
                checkpoint = collision.transform;
                checkpointParticle.transform.position = checkpoint.position;
                checkpointParticle.Play();
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Checkpoint") && !isJumping)
        {
            if (checkpoint != collision.transform)
            {
                checkpoint = collision.transform;
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

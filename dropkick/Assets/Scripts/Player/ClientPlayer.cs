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

    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private SpriteRenderer faceSprite;
    [SerializeField] private Transform shadowSprite;
    private float verticalVelocity;
    private float gravity;

    public bool isJumping { get; private set; }  = false;
    private Vector2 startPos;
    private Vector2 defaultScale;

    [SerializeField] private ParticleSystem jumpParticle;
    [SerializeField] private ParticleSystem checkpointParticle;
    [SerializeField] private ParticleSystem landParticle;
    private Transform checkpoint;

    [SerializeField] private Color color;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        startPos = playerSprite.transform.localPosition;
        defaultScale = playerSprite.transform.localScale;
        if (cam != null)
            cam.SetParent(null);
    }

    private void OnDestroy()
    {
        list.Remove(id);
    }

    public static void Spawn(ushort id, string username, int face, int color, Vector3 position)
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
        player.faceSprite.sprite = UIManager.Singleton.faces[face].sprite;
        player.faceSprite.color = UIManager.Singleton.faces[face].color;
        player.color = UIManager.Singleton.colors[color];
        player.playerSprite.color = player.color;
        player.username = username;
        list.Add(player.id, player);
    }

    private void FixedUpdate()
    {
        if (cam != null)
            cam.position = Vector3.Lerp(cam.position, new Vector3(transform.position.x, transform.position.y, -10), Time.deltaTime * camSpeed);

        float x = defaultScale.x - Mathf.Log10(Mathf.Clamp(rb.velocity.magnitude * .1f, 1f, 3f));
        Vector2 scale = new Vector2(x, defaultScale.y*defaultScale.y / x);
        playerSprite.transform.localScale = scale;
        shadowSprite.localScale = scale;


        if (playerSprite.color != color)
            playerSprite.color = new Color(Mathf.MoveTowards(playerSprite.color.r, color.r, Time.deltaTime * 2f), Mathf.MoveTowards(playerSprite.color.g, color.g, Time.deltaTime * 2f), Mathf.MoveTowards(playerSprite.color.b, color.b, Time.deltaTime * 2f), 1);


        if (!isJumping)
        {
            rb.drag = PlayerMovement.DefaultDrag;
            return;
        }

        rb.drag = PlayerMovement.AirDrag;
        verticalVelocity += gravity * Time.fixedDeltaTime;
        playerSprite.transform.localPosition += new Vector3(0, verticalVelocity, 0) * Time.fixedDeltaTime;

        if (playerSprite.transform.localPosition.y <= startPos.y)
        {
            isJumping = false;
            playerSprite.transform.localPosition = startPos;
            rb.velocity *= PlayerMovement.LandingFactor;
            landParticle.Play();
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
        Spawn(message.GetUShort(), message.GetString(), message.GetInt(), message.GetInt(), message.GetVector3());
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

        if (message.GetBool()) //check if the player was hit, or if they jumped willingly
            player.playerSprite.color = Color.white;

        player.Jump(force);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90;
        player.playerSprite.transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
        player.shadowSprite.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    [MessageHandler((ushort)ServerToClientId.PlayerDeath, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerDeath(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.rb.velocity = Vector2.zero;
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

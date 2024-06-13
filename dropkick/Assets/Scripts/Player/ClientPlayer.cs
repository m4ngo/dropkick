using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Riptide;
using Steamworks;
using System;
using UnityEngine.Events;

public class ClientPlayer : MonoBehaviour
{
    public static Dictionary<ushort, ClientPlayer> list = new Dictionary<ushort, ClientPlayer>();

    [Header("Player Info")]
    [SerializeField] private ushort id;
    [SerializeField] private string username;

    [Header("Camera")]
    [SerializeField] private Transform cam;
    [SerializeField] private float camSpeed = 2.5f;

    [Header("Sprites")]
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private SpriteRenderer faceSprite;
    [SerializeField] private Transform shadowSprite;
    private float verticalVelocity;
    private float gravity;

    public bool isJumping { get; private set; } = false;
    private Vector2 startPos;
    private Vector2 defaultScale;

    [Header("Particles/Effects")]
    [SerializeField] private ParticleSystem jumpParticle;
    [SerializeField] private ParticleSystem checkpointParticle;
    [SerializeField] private ParticleSystem landParticle;
    [SerializeField] private ParticleSystem hitParticle;
    private Transform checkpoint;

    [SerializeField] private Color color;
    private float colorDelay = 0f;
    private Rigidbody2D rb;

    [Header("Events")]
    [SerializeField] private UnityEvent onLandEvents;

    //open variables the local player can check with
    public bool dead { get; private set; }

    public float GetSpriteDist() { return Math.Abs(startPos.y - playerSprite.transform.localPosition.y); }
    public float GetVerticalVel() { return verticalVelocity; }

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
        Destroy(cam);
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

        if (rb.velocity.sqrMagnitude > 1)
        {
            float x = defaultScale.x - Mathf.Log10(Mathf.Clamp(rb.velocity.magnitude * .1f, 1f, 3f));
            Vector2 scale = new Vector2(x, defaultScale.y * defaultScale.y / x);
            playerSprite.transform.localScale = scale;
            shadowSprite.localScale = scale;
        }

        if (playerSprite.color != color && colorDelay <= 0)
            playerSprite.color = new Color(Mathf.MoveTowards(playerSprite.color.r, color.r, Time.deltaTime * 2f), Mathf.MoveTowards(playerSprite.color.g, color.g, Time.deltaTime * 2f), Mathf.MoveTowards(playerSprite.color.b, color.b, Time.deltaTime * 2f), 1);
        colorDelay -= Time.deltaTime;

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
            onLandEvents.Invoke();
            isJumping = false;
            playerSprite.transform.localPosition = startPos;
            rb.velocity *= PlayerMovement.LandingFactor;
            landParticle.Play();
        }
    }

    void AirControl(Vector2 vel)
    {
        rb.velocity = vel;
        RotateSprite(rb.velocity.normalized);
    }

    void DeathAnim()
    {
        StartCoroutine(_DeathAnim());
    }

    void RotateSprite(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90;
        playerSprite.transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
        shadowSprite.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    private IEnumerator _DeathAnim()
    {
        shadowSprite.localScale = Vector2.zero;
        playerSprite.transform.localScale = defaultScale;
        while (playerSprite.transform.localScale.x > 0.05f)
        {
            playerSprite.transform.Rotate(0, 0, 500 * Time.deltaTime);
            Vector2 scale = playerSprite.transform.localScale;
            playerSprite.transform.localScale = new Vector2(scale.x - 1.4f * Time.deltaTime, scale.y - 1.4f * Time.deltaTime);
            yield return new WaitForEndOfFrame();
        }
        playerSprite.transform.localScale = Vector2.zero;
    }

    void TriggerJump(float force) //the higher the force, the higher the jump
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
    private static void ReceiveJump(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;

        Vector2 pos = message.GetVector2();
        Vector2 dir = message.GetVector2();
        float force = message.GetFloat();
        bool hit = message.GetBool();

        if (!hit)
        {
            if (playerId == NetworkManager.Singleton.Client.Id)
                return;
        }

        player.transform.position = pos; //update player position
        player.ClientJump(dir, force, hit);
    }

    public void ClientJump(Vector2 dir, float force, bool hit)
    {
        rb.velocity = dir * force;

        TriggerJump(force);

        if (hit) //check if the player was hit, or if they jumped willingly
        {
            playerSprite.color = Color.white;
            colorDelay = 0.1f;
            hitParticle.Play();
        }
        RotateSprite(dir);
    }

    [MessageHandler((ushort)ServerToClientId.PlayerDeath, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerDeath(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.rb.velocity = Vector2.zero;
        player.DeathAnim();
        player.dead = true;
    }

    [MessageHandler((ushort)ServerToClientId.PlayerRespawn, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerRespawn(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.transform.position = message.GetVector2(); //update player position
        player.rb.velocity = Vector2.zero;
        player.playerSprite.transform.localScale = player.defaultScale;
        player.dead = false;
    }

    [MessageHandler((ushort)ServerToClientId.ResyncPosition, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReceiveResync(Message message)
    {
        if (!list.TryGetValue(NetworkManager.Singleton.Client.Id, out ClientPlayer player))
            return;
        player.transform.position = message.GetVector2(); //update player position
        player.rb.velocity = message.GetVector2();
    }

    [MessageHandler((ushort)ServerToClientId.ResyncAirControl, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReceiveAirControl(Message message)
    {
        if (!list.TryGetValue(NetworkManager.Singleton.Client.Id, out ClientPlayer player))
            return;
        player.AirControl(message.GetVector2());
    }
    #endregion
}

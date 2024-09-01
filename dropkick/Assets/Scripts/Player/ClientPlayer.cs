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
    [SerializeField] private Animator anim;
    [SerializeField] private MeshRenderer[] playerModel;
    [SerializeField] private Transform modelParent;
    private float verticalVelocity;
    private float gravity;

    public bool isJumping { get; private set; } = false;
    private Vector3 startPos;
    private Vector3 defaultScale;

    [Header("Particles/Effects")]
    [SerializeField] private ParticleSystem jumpParticle;
    [SerializeField] private ParticleSystem checkpointParticle;
    [SerializeField] private ParticleSystem landParticle;
    [SerializeField] private ParticleSystem[] hitParticle;
    private Transform checkpoint;

    [SerializeField] private Material whiteMat;
    [SerializeField] private Material color;
    private float colorDelay = 0f;
    private Rigidbody rb;

    [Header("Events")]
    [SerializeField] private UnityEvent onLandEvents;
    [SerializeField] private UnityEvent onHitEvents;

    [Header("Ground Detection")]
    [SerializeField] private float checkRadius;
    [SerializeField] private LayerMask checkMask;
    Collider[] currentGround = null;

    //open variables the local player can check with
    public bool dead { get; private set; }
    public string GetUsername(){return username;}
    public float GetSpriteDist() { return Math.Abs(startPos.y - modelParent.localPosition.y); }
    public float GetVerticalVel() { return verticalVelocity; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPos = modelParent.localPosition;
        defaultScale = modelParent.localScale;
        if (cam != null)
            cam.SetParent(null);
    }

    private void OnDestroy()
    {
        Destroy(cam);
        list.Remove(id);
        UIManager.Singleton.RemoveEntry(id);
    }

    public static void Spawn(ushort id, string username, /*int face,*/ int color, Vector3 position, bool readyStatus)
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
        player.color = UIManager.Singleton.colors[color];
        player.SetModelMaterial(player.color);
        player.username = username;
        list.Add(player.id, player);
        
        UIManager.Singleton.AddEntry(id, readyStatus);
    }

    private void FixedUpdate()
    {
        GroundChecks();

        //animate
        anim.SetBool("grounded", !isJumping);

        //camera position
        if (cam != null)
            cam.position = Vector3.Lerp(cam.position, new Vector3(transform.position.x, 10, transform.position.z - 6.5f), Time.deltaTime * camSpeed);

        //model squash and stretch
        if (rb.velocity.sqrMagnitude > 1 && !dead)
        {
            float x = defaultScale.x - Mathf.Log10(Mathf.Clamp(rb.velocity.magnitude * .1f, 1f, 3f));
            Vector3 scale = new Vector3(x, 1f, defaultScale.z * defaultScale.z / x);
            modelParent.localScale = isJumping ? scale : Vector3.one;
        }

        //model color flash when hit
        if (playerModel[1].material != color){
            colorDelay -= Time.deltaTime;
            if(colorDelay <= 0){
                SetModelMaterial(color);
            }
        }

        //set rb drag based on landing ground
        if (!isJumping)
        {
            rb.drag = PlayerMovement.GetCurrentGroundType(currentGround);
            return;
        }

        //if in air, set drag and velocities of model
        rb.drag = PlayerMovement.AirDrag;
        verticalVelocity += gravity * Time.fixedDeltaTime;
        modelParent.localPosition += new Vector3(0, verticalVelocity, 0) * Time.fixedDeltaTime;

        if (modelParent.localPosition.y <= startPos.y)
        {
            onLandEvents.Invoke();
            isJumping = false;
            modelParent.localPosition = startPos;
            rb.velocity *= PlayerMovement.LandingFactor;
            landParticle.Play();
        }
    }

    void SetModelMaterial(Material mat){
        foreach(MeshRenderer rend in playerModel){
            rend.material = mat;
        }
    }

    void AirControl(Vector3 vel)
    {
        rb.velocity = vel;
        RotateSprite(rb.velocity.normalized);
    }

    void DeathAnim()
    {
        StartCoroutine(_DeathAnim());
    }

    void RotateSprite(Vector3 dir)
    {
        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        modelParent.rotation = Quaternion.Euler(new Vector3(0, angle, 0));
    }

    private IEnumerator _DeathAnim()
    {
        modelParent.localScale = defaultScale;
        while (modelParent.localScale.x > 0.05f)
        {
            rb.velocity = Vector2.zero;
            modelParent.Rotate(0, 500 * Time.deltaTime, 0);
            Vector3 scale = modelParent.localScale;
            modelParent.localScale = new Vector3(scale.x - 4f * Time.deltaTime, scale.y - 4f * Time.deltaTime, scale.z - 4f * Time.deltaTime);
            yield return new WaitForEndOfFrame();
        }
        modelParent.localScale = Vector2.zero;
    }

    void TriggerJump(float force) //the higher the force, the higher the jump
    {
        verticalVelocity = force * PlayerMovement.JumpForceFactor + PlayerMovement.JumpOffset;
        gravity = PlayerMovement.Gravity * Mathf.Pow(PlayerMovement.GravityPow, verticalVelocity);
        isJumping = true;
        jumpParticle.Play();
    }

    void GroundChecks(){
        Collider[] hits = Physics.OverlapSphere(transform.position, checkRadius, checkMask);
        currentGround = hits;
        foreach(Collider col in hits){
            if(col.CompareTag("ClientCheckpoint")){
                if (NetworkManager.Singleton.Client.Id == id){
                    if(!isJumping && checkpoint != col.transform){
                        if (checkpoint != null)
                            checkpoint.GetChild(0).GetComponent<ParticleSystem>().Stop();
                        checkpoint = col.transform;
                        checkpoint.GetChild(0).GetComponent<ParticleSystem>().Play();
                        checkpointParticle.transform.position = checkpoint.position;
                        checkpointParticle.Play();
                    }
                }
            }
        }
    }

    #region Messages
    [MessageHandler((ushort)ServerToClientId.SpawnPlayer, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void SpawnPlayer(Message message)
    {
        Spawn(message.GetUShort(), message.GetString(), message.GetInt(), message.GetVector3(), message.GetBool());
    }

    [MessageHandler((ushort)ServerToClientId.PlayerJump, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReceiveJump(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;

        Vector3 pos = message.GetVector3();
        Vector3 dir = message.GetVector3();
        float force = message.GetFloat();

        if (playerId == NetworkManager.Singleton.Client.Id)
            return;

        player.transform.position = pos; //update player position
        player.ClientJump(dir, force);
    }

    [MessageHandler((ushort)ServerToClientId.PlayerHit, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReceiveHit(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;

        Vector3 pos = message.GetVector3();
        Vector3 dir = message.GetVector3();

        player.transform.position = pos; //update player position
        player.ClientHit(dir);
    }

    public void ClientJump(Vector3 dir, float force)
    {
        rb.velocity = dir.normalized * force;
        TriggerJump(force);
        RotateSprite(dir);
    }

    void ClientHit(Vector3 dir)
    {
        rb.velocity = dir.normalized * PlayerMovement.Knockback;
        SetModelMaterial(whiteMat);
        colorDelay = 0.1f;

        foreach(ParticleSystem particle in hitParticle){
            particle.Play();
        }

        anim.SetTrigger("hit");
        onHitEvents.Invoke();
    }

    [MessageHandler((ushort)ServerToClientId.PlayerDeath, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerDeath(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.DeathAnim();
        player.dead = true;
    }

    [MessageHandler((ushort)ServerToClientId.PlayerRespawn, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerRespawn(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.transform.position = message.GetVector3(); //update player position
        player.rb.velocity = Vector2.zero;
        player.modelParent.localScale = player.defaultScale;
        player.dead = false;
    }

    [MessageHandler((ushort)ServerToClientId.ResyncPosition, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReceiveResync(Message message)
    {
        if (!list.TryGetValue(NetworkManager.Singleton.Client.Id, out ClientPlayer player))
            return;
        player.transform.position = message.GetVector3(); //update player position
        player.rb.velocity = message.GetVector3();
    }

    [MessageHandler((ushort)ServerToClientId.ResyncAirControl, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReceiveAirControl(Message message)
    {
        if (!list.TryGetValue(message.GetUShort(), out ClientPlayer player))
            return;
        player.AirControl(message.GetVector3());
    }

    [MessageHandler((ushort)ServerToClientId.Freeze, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void Freeze(Message message)
    {
        if (!ClientPlayer.list.TryGetValue(message.GetUShort(), out ClientPlayer player))
            return;
        player.GetComponent<PlayerInput>().Freeze(message.GetBool());
    }
    #endregion
}

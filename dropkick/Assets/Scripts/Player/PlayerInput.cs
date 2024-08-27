using UnityEngine;
using UnityEngine.UI;
using Riptide;
using System.Collections.Generic;


public class PlayerInput : MonoBehaviour
{
    [Header("Pointer")]
    [SerializeField] private Color pointerStart;
    [SerializeField] private Color pointerEnd;
    [SerializeField] private Transform pointer;
    [SerializeField] private Transform pointerHolder;
    [SerializeField] private SpriteRenderer pointerSprite;

    [Header("Clientside Jump")]
    [SerializeField] private float holdTime = 0f;
    [SerializeField] private float chargeSpeed;
    [SerializeField] private float clickQueueTime = 0.1f;

    [Header("Jump Display")]
    [SerializeField] private Slider[] jumpDisplays;
    private float jumpQueue = 0f;
    private float hitLock = 0f;
    private bool freeze = false;

    private int curJumps = 3;
    private float curReload = 0f;

    ClientPlayer player;
    bool up = true;
    Vector3 dir;

    private void Start()
    {
        player = GetComponent<ClientPlayer>();
    }

    private void FixedUpdate()
    {
        //jump display handling
        for (int i = 0; i < jumpDisplays.Length; i++){
            jumpDisplays[i].value = 0f;
            if(i <= curJumps){
                jumpDisplays[i].value = 1f;
                if(i==curJumps){
                    jumpDisplays[i].value = curReload / PlayerMovement.JumpReloadTime;
                }
            }
        }

        if (player.isJumping)
        {
            SendAirControl(); //air control
            curReload = 0f;
        }
        else{
            //jump reload handling
            if(curJumps < PlayerMovement.MaxJumps){
                curReload += Time.fixedDeltaTime;
                if(curReload >= PlayerMovement.JumpReloadTime){
                    curJumps++;
                    curReload = 0f;
                }
            }
            else{
                curReload = 3f;
            }
        }

        if(hitLock > 0) hitLock -= Time.fixedDeltaTime;
    }

    public void SetHitLock(float time){
        hitLock = time;
    }

    void Freeze(bool freeze)
    {
        this.freeze = freeze;
    }

    private void Update()
    {
        //pointer indicator handling
        pointerHolder.localScale = new Vector2(1f, Mathf.Clamp(holdTime, 0.2f, 1.0f));
        pointerSprite.color = Color.Lerp(pointerStart, pointerEnd, Mathf.Clamp(holdTime, 0.2f, 1.0f));

        //get mouse position relative to player
        dir = Input.mousePosition - Camera.main.WorldToScreenPoint(transform.position);
        // print(dir);
        dir.z = dir.y;
        dir.y = 0;
        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        pointer.rotation = Quaternion.Lerp(pointer.rotation, Quaternion.Euler(new Vector3(90, angle, 0)), Time.deltaTime * 25f);

        //queuing inputs for better responsiveness
        if (jumpQueue > 0)
            jumpQueue -= Time.deltaTime;
        if (Input.GetMouseButtonUp(0))
        {
            jumpQueue = clickQueueTime;
        }

        //don't execute movement logic if the player is dead
        if (player.dead || player.isJumping || hitLock > 0 || curJumps <= 0 || freeze)
            return;

        if (Input.GetMouseButton(0))
        {
            if (up)
            {
                holdTime += Time.deltaTime * chargeSpeed;
                if (holdTime > 1.1f)
                    up = false;
            }
            else
            {
                holdTime -= Time.deltaTime * chargeSpeed;
                if (holdTime < 0f)
                    up = true;
            }
        }
        else if (jumpQueue > 0)
        {
            SendInput(holdTime);

            float clientSideForce = Mathf.Clamp(holdTime, PlayerMovement.MinJumpForceMultiplier, 1.0f) * PlayerMovement.MaxJumpForce;
            player.ClientJump(dir.normalized, clientSideForce);

            curJumps--;
            holdTime = 0;
            jumpQueue = 0;
        }
    }

    #region Messages
    private void SendInput(float jumpForce)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.PlayerInput);
        message.AddVector3(dir);
        message.AddFloat(jumpForce);
        NetworkManager.Singleton.Client.Send(message);
    }

    private void SendAirControl()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.PlayerAirControl);
        message.AddVector3(dir);
        NetworkManager.Singleton.Client.Send(message);
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

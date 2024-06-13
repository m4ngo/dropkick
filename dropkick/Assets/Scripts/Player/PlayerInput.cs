using UnityEngine;
using Riptide;


public class PlayerInput : MonoBehaviour
{
    [Header("Pointer")]
    [SerializeField] private Color pointerStart;
    [SerializeField] private Color pointerEnd;
    [SerializeField] private Transform pointer;
    [SerializeField] private SpriteRenderer pointerSprite;

    [Header("Clientside Jump")]
    [SerializeField] private float holdTime = 0f;
    [SerializeField] private float chargeSpeed;
    [SerializeField] private float jumpCooldown = 0.25f;

    private float currentJumpCooldown = 0f;
    private ClientPlayer player;

    bool up = true;
    Vector2 dir;

    private void Start()
    {
        player = GetComponent<ClientPlayer>();
    }

    public void SetJumpCooldown(){
        currentJumpCooldown = jumpCooldown;
    }

    private void Update()
    {
        pointer.gameObject.SetActive(!player.isJumping && Input.GetMouseButton(0) && !player.dead && currentJumpCooldown <= 0);

        pointer.localScale = new Vector2(1f, Mathf.Clamp(holdTime, 0.2f, 1.0f));
        pointerSprite.color = Color.Lerp(pointerStart, pointerEnd, Mathf.Clamp(holdTime, 0.2f, 1.0f));

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        dir = mousePos - transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        pointer.rotation = Quaternion.Euler(new Vector3(0, 0, angle - 90));

        //handling cooldown between jumps
        //currently using clientside because its will prevent inconsistency from lag
        //may need to add checks later on serverside to prevent memory manipulation to hack the cooldown
        if (currentJumpCooldown > 0)
            currentJumpCooldown -= Time.deltaTime;

        if (player.dead || currentJumpCooldown > 0)
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
        else if (Input.GetMouseButtonUp(0))
        {
            if (player.isJumping)
            {
                SendAirDash(player.GetSpriteDist(), player.GetVerticalVel());
            }
            else
            {
                SendInput(holdTime);

                float clientSideForce = Mathf.Clamp(holdTime, PlayerMovement.MinJumpForceMultiplier, 1.0f) * PlayerMovement.MaxJumpForce;
                player.ClientJump(dir.normalized, clientSideForce, false);

                holdTime = 0;
            }
        }
    }

    #region Messages
    private void SendInput(float jumpForce)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.PlayerInput);
        message.AddVector2(dir);
        message.AddFloat(jumpForce);
        NetworkManager.Singleton.Client.Send(message);
    }


    private void SendAirDash(float spriteDist, float velocity)
    {
        /*print(spriteDist <= 1f && velocity < 0);
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.PlayerInput);
        message.AddVector2(dir);
        message.AddFloat(jumpForce);
        NetworkManager.Singleton.Client.Send(message);*/
    }
    #endregion
}

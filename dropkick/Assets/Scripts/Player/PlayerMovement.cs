using UnityEngine;
using Riptide;


public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float airSpeed;
    [SerializeField] private float jumpTime;
    [SerializeField] private float acceleration;
    [SerializeField] private float airAcceleration;
    private ServerPlayer player;
    private Rigidbody2D rb;
    Vector2 moveDir;
    float currentJump = 0;

    private void Awake()
    {
        player = GetComponent<ServerPlayer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (currentJump <= 0)
            return;
        currentJump -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        //movement code
        rb.velocity = Vector2.Lerp(rb.velocity, moveDir.normalized * (currentJump <= 0 ? moveSpeed : airSpeed), Time.deltaTime * (currentJump <= 0 ? acceleration : airAcceleration));
        Move();
    }

    public void SetMoveDir(Vector2 dir, bool jump)
    {
        moveDir = dir;
        if (currentJump <= 0 && jump)
            Jump();
    }

    void Jump()
    {
        currentJump = jumpTime;
    }

    private void Move()
    {
        SendPlayerTick();
    }

    #region Messages
    private void SendPlayerTick()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.PlayerTick);
        message.AddUShort(player.Id);
        message.AddVector2(transform.position);
        message.AddVector2(moveDir);
        message.AddBool(currentJump > 0);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion
}

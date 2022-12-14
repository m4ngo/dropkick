using UnityEngine;
using Riptide;


public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float acceleration;
    private ServerPlayer player;
    private Rigidbody2D rb;
    Vector2 moveDir;

    private void Awake()
    {
        player = GetComponent<ServerPlayer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        //movement code
        rb.velocity = Vector2.Lerp(rb.velocity, moveDir.normalized * moveSpeed, Time.deltaTime * acceleration);
        Move();
    }

    public void SetMoveDir(Vector2 dir) { moveDir = dir; }

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
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion
}

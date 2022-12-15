using UnityEngine;
using Riptide;


public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Transform cam;
    [SerializeField] private float camSpeed = 2.5f;
    bool jump = false;
    Vector2 moveDir;

    private void Start()
    {
        cam.SetParent(null);
    }

    private void Update()
    {
        moveDir.x = Input.GetAxisRaw("Horizontal");
        moveDir.y = Input.GetAxisRaw("Vertical");
        jump = Input.GetKey(KeyCode.Space);
    }

    private void FixedUpdate()
    {
        SendInput();
        
        //camera follow
        cam.position = Vector3.Lerp(cam.position, new Vector3(transform.position.x, transform.position.y, -10), Time.deltaTime * camSpeed);
    }

    #region Messages
    private void SendInput()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.PlayerInput);
        message.AddVector2(moveDir);
        message.AddBool(jump);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}

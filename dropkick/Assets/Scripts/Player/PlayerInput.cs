using UnityEngine;
using Riptide;


public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Transform cam;
    [SerializeField] private float camSpeed = 2.5f;

    [SerializeField] private Transform pointer;
    [SerializeField] private float holdTime = 0f;
    [SerializeField] private float chargeSpeed;

    bool up = true;
    Vector2 dir;

    private void Start()
    {
        cam.SetParent(null);
    }

    private void Update()
    {
        pointer.localScale = new Vector2(1f, holdTime + 0.2f);
        if (Input.GetMouseButton(0))
        {
            if (up)
            {
                holdTime += Time.deltaTime * chargeSpeed;
                if (holdTime > 1.2f)
                    up = false;
            } else
            {
                holdTime -= Time.deltaTime * chargeSpeed;
                if (holdTime < 0f)
                    up = true;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            SendInput(holdTime);
            holdTime = 0;
        }
        
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        dir = mousePos - transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        pointer.rotation = Quaternion.Euler(new Vector3(0, 0, angle - 90));
    }

    private void FixedUpdate()
    {
        //camera follow
        cam.position = Vector3.Lerp(cam.position, new Vector3(transform.position.x, transform.position.y, -10), Time.deltaTime * camSpeed);
    }

    #region Messages
    private void SendInput(float jumpForce)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.PlayerInput);
        message.AddVector2(dir);
        message.AddFloat(jumpForce);
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}

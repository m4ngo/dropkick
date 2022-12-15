using System.Collections.Generic;
using UnityEngine;
using Riptide;


public class ClientPlayer : MonoBehaviour
{
    public static Dictionary<ushort, ClientPlayer> list = new Dictionary<ushort, ClientPlayer>();

    [SerializeField] private ushort id;
    [SerializeField] private string username;

    private Animator anim;
    private Vector2 targetPos;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void OnDestroy()
    {
        list.Remove(id);
    }

    private void FixedUpdate()
    {
        transform.position = Vector2.Lerp(transform.position, targetPos, Time.deltaTime * 10f);
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

    #region Messages
    [MessageHandler((ushort)ServerToClientId.SpawnPlayer, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void SpawnPlayer(Message message)
    {
        Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
    }

    [MessageHandler((ushort)ServerToClientId.PlayerTick, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerTick(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!list.TryGetValue(playerId, out ClientPlayer player))
            return;
        player.targetPos = message.GetVector2(); //update player position

        Vector2 moveDir = message.GetVector2();
        if (moveDir == Vector2.zero)
            player.anim.SetBool("Run", false);
        else
        {
            player.anim.SetBool("Run", true);
            player.anim.SetFloat("X", moveDir.x);
            player.anim.SetFloat("Y", moveDir.y);
        }
        if (message.GetBool() && !player.anim.GetCurrentAnimatorStateInfo(0).IsName("Jump"))
            player.anim.SetTrigger("Jump");
    }
    #endregion
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Riptide;

public class GamemodeClientIsland : MonoBehaviour
{
    [MessageHandler((ushort)ServerToClientId.CrumbleTile, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void CrumbleTile(Message message)
    {
        NetworkManager.Singleton.currentGamemodeClient.transform.GetChild(message.GetInt()).GetComponent<RoomCrumbleClient>().Crumble();
    }
}

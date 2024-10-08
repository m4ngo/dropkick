﻿using System.Collections.Generic;
using UnityEngine;
using Riptide;


[RequireComponent(typeof(PlayerMovement))]
public class ServerPlayer : MonoBehaviour
{
    public static Dictionary<ushort, ServerPlayer> List { get; private set; } = new Dictionary<ushort, ServerPlayer>();

    public ushort Id { get; private set; }
    public string Username { get; private set; }

    private int color;
    public bool ready {get; private set;} = false;
    public int crowns = 0; //how many points they have

    [SerializeField] private PlayerMovement movement;

    public void SetReady(bool read) { ready = read; }

    private void OnValidate()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
    }

    private void OnDestroy()
    {
        List.Remove(Id);
    }

    public static void Spawn(ushort id, string username, int color)
    {
        ServerPlayer player = Instantiate(NetworkManager.Singleton.ServerPlayerPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity).GetComponent<ServerPlayer>();
        player.name = $"Server Player {id} ({(username == "" ? "Guest" : username)})";
        player.color = color;
        player.Id = id;
        player.Username = username;

        player.SendSpawn();
        List.Add(player.Id, player);
    }

    #region Messages
    /// <summary>Sends a player's info to the given client.</summary>
    /// <param name="toClient">The client to send the message to.</param>
    public void SendSpawn(ushort toClient)
    {
        NetworkManager.Singleton.Server.Send(GetSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.SpawnPlayer)), toClient);
    }
    /// <summary>Sends a player's info to all clients.</summary>
    private void SendSpawn()
    {
        NetworkManager.Singleton.Server.SendToAll(GetSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.SpawnPlayer)));
    }

    private Message GetSpawnData(Message message)
    {
        message.AddUShort(Id);
        message.AddString(Username);
        // message.AddInt(face);
        message.AddInt(color);
        message.AddVector3(transform.position);
        message.AddBool(ready);
        return message;
    }

    [MessageHandler((ushort)ClientToServerId.PlayerName, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerName(ushort fromClientId, Message message)
    {
        if (NetworkManager.Singleton.started)
        {
            return;
        }
        Spawn(fromClientId, message.GetString(), message.GetInt());
    }

    [MessageHandler((ushort)ClientToServerId.PlayerInput, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerInput(ushort fromClientId, Message message)
    {
        ServerPlayer player = List[fromClientId];
        player.movement.SetMoveDir(message.GetVector3(), message.GetFloat());
    }

    [MessageHandler((ushort)ClientToServerId.PlayerAirControl, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerAirControl(ushort fromClientId, Message message)
    {
        ServerPlayer player = List[fromClientId];
        player.movement.AirControl(message.GetVector3().normalized);
    }
    #endregion
}


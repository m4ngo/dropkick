﻿using System.Collections.Generic;
using UnityEngine;
using Riptide;


[RequireComponent(typeof(PlayerMovement))]
public class ServerPlayer : MonoBehaviour
{
    public static Dictionary<ushort, ServerPlayer> List { get; private set; } = new Dictionary<ushort, ServerPlayer>();

    public ushort Id { get; private set; }
    public string Username { get; private set; }

    private int face;
    private int color;

    [SerializeField] private PlayerMovement movement;

    private void OnValidate()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
    }

    private void OnDestroy()
    {
        List.Remove(Id);
    }

    public static void Spawn(ushort id, string username, int face, int color)
    {
        ServerPlayer player = Instantiate(NetworkManager.Singleton.ServerPlayerPrefab, new Vector3(0.25f, 0.25f, 0f), Quaternion.identity).GetComponent<ServerPlayer>();
        player.name = $"Server Player {id} ({(username == "" ? "Guest" : username)})";
        player.face = face;
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
        message.AddInt(face);
        message.AddInt(color);
        message.AddVector3(transform.position);
        return message;
    }

    [MessageHandler((ushort)ClientToServerId.PlayerName, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerName(ushort fromClientId, Message message)
    {
        Spawn(fromClientId, message.GetString(), message.GetInt(), message.GetInt());
    }

    [MessageHandler((ushort)ClientToServerId.PlayerInput, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void PlayerInput(ushort fromClientId, Message message)
    {
        ServerPlayer player = List[fromClientId];
        player.movement.SetMoveDir(message.GetVector2(), message.GetFloat());
    }
    #endregion
}


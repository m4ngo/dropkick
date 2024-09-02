using Riptide.Transports.Steam;
using Riptide.Utils;
using Riptide;
using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;


public enum ServerToClientId : ushort
{
    SpawnPlayer = 1,
    PlayerJump,
    PlayerDeath,
    PlayerRespawn,
    PlayerHit,
    ResyncPosition,
    ResyncAirControl,

    Freeze,

    Ready,
    SetSeed,
    StartGamemode,
    EndGamemode, //for when each individual gamemode is done
    GameStatus,
    SetScore,
    EndFullGame, //for when the game is entirely done
}

public enum ClientToServerId : ushort
{
    PlayerName = 1,
    PlayerInput,
    PlayerAirControl,
    Ready,
}

public class NetworkManager : MonoBehaviour
{
    public const byte PlayerHostedDemoMessageHandlerGroupId = 255;

    private static NetworkManager _singleton;
    internal static NetworkManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying object!");
                Destroy(value);
            }
        }
    }

    [SerializeField] private GameObject serverPlayerPrefab;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject localPlayerPrefab;

    public int seed { get; private set; }  = 0;
    public bool started { get; private set; }  = false;

    [SerializeField] private GameObject[] gamemodeServerPrefabs;
    [SerializeField] private GameObject[] gamemodeClientPrefabs;
    [SerializeField] private List<int> gamemodeOrder = new List<int>();
    [SerializeField] private GameObject currentGamemodeClient;
    [SerializeField] private Gamemode currentGamemodeServer;

    public GameObject ServerPlayerPrefab => serverPlayerPrefab;
    public GameObject PlayerPrefab => playerPrefab;
    public GameObject LocalPlayerPrefab => localPlayerPrefab;

    internal Server Server { get; private set; }
    internal Client Client { get; private set; }

    private void Awake()
    {
        Singleton = this;
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            UIManager.Singleton.SteamError();
            Debug.LogError("Steam is not initialized!");
            //TODO: add error display
            return;
        }

#if UNITY_EDITOR
        RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
#else
            RiptideLogger.Initialize(Debug.Log, true);
#endif

        SteamServer steamServer = new SteamServer();
        Server = new Server(steamServer);
        Server.ClientConnected += NewPlayerConnected;
        Server.ClientDisconnected += ServerPlayerLeft;

        Client = new Client(new SteamClient(steamServer));
        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        Client.ClientDisconnected += ClientPlayerLeft;
        Client.Disconnected += DidDisconnect;
    }

    private void FixedUpdate()
    {
        if (Server.IsRunning)
            Server.Update();

        Client.Update();
    }

    private void OnApplicationQuit()
    {
        StopServer();
        Server.ClientConnected -= NewPlayerConnected;
        Server.ClientDisconnected -= ServerPlayerLeft;

        DisconnectClient();
        Client.Connected -= DidConnect;
        Client.ConnectionFailed -= FailedToConnect;
        Client.ClientDisconnected -= ClientPlayerLeft;
        Client.Disconnected -= DidDisconnect;
    }

    internal void StopServer()
    {
        Server.Stop();
        started = false;
        UIManager.Singleton.SetReady(false);
        foreach (ServerPlayer player in ServerPlayer.List.Values)
            Destroy(player.gameObject);
    }

    internal void DisconnectClient()
    {
        Client.Disconnect();
        foreach (ClientPlayer player in ClientPlayer.list.Values)
            Destroy(player.gameObject);
        if(Camera.main != null)
            Destroy(Camera.main.gameObject);
        foreach (GameObject g in GameObject.FindGameObjectsWithTag("ClientPlayer"))
        {
            Destroy(g);
        }
    }

    [MessageHandler((ushort)ServerToClientId.GameStatus, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReceiveGameStatus(Message message)
    {
        bool started = message.GetBool();
        if(started)
        {
            //in the future, maybe add a way to spectate
            UIManager.Singleton.GameAlreadyStartedNotif();
            NetworkManager.Singleton.DisconnectClient();
        }
    }

    private void NewPlayerConnected(object sender, ServerConnectedEventArgs e)
    {
        SendGameStatus(e.Client.Id);
        if (started)
        {
            return;
        }

        foreach (ServerPlayer player in ServerPlayer.List.Values)
        {
            if (player.Id != e.Client.Id){
                player.SendSpawn(e.Client.Id);
            }
        }
    }

    void SendGameStatus(ushort id)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.GameStatus);
        message.AddBool(started);
        Server.Send(message, id);
    }

    private void ServerPlayerLeft(object sender, ServerDisconnectedEventArgs e)
    {
        Destroy(ServerPlayer.List[e.Client.Id].gameObject);
    }

    private void DidConnect(object sender, EventArgs e)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.PlayerName);
        message.AddString(Steamworks.SteamFriends.GetPersonaName());
        message.AddInt(UIManager.Singleton.color);
        Client.Send(message);
        UIManager.Singleton.SetReady(false);
    }

    private void FailedToConnect(object sender, EventArgs e)
    {
        UIManager.Singleton.BackToMain();
        foreach(GameObject g in GameObject.FindGameObjectsWithTag("ClientPlayer"))
        {
            Destroy(g);
        }
    }

    private void ClientPlayerLeft(object sender, ClientDisconnectedEventArgs e)
    {
        Destroy(ClientPlayer.list[e.Id].gameObject);
    }

    private void DidDisconnect(object sender, EventArgs e)
    {
        foreach (ClientPlayer player in ClientPlayer.list.Values)
            Destroy(player.gameObject);

        ClientPlayer.list.Clear();
        UIManager.Singleton.BackToMain();
        if (Singleton.currentGamemodeServer != null)
        {
            Destroy(Singleton.currentGamemodeServer.gameObject);
        }
        if (Singleton.currentGamemodeClient != null)
        {
            Destroy(Singleton.currentGamemodeClient);
        }
    }



    public void DestroyGamemodes()
    {
        if(currentGamemodeClient != null)
        {
            Destroy(currentGamemodeClient);
        }

        if(currentGamemodeServer != null)
        {
            Destroy(currentGamemodeServer);
        }
    }

    public void StartGamemode()
    {
        if(gamemodeOrder.Count <= 0)
        {
            //TODO: end the entire game
            return;
        }
        
        //reset all player positions
        foreach(ServerPlayer p in ServerPlayer.List.Values)
        {
            p.transform.position = Vector3.zero;
            p.GetComponent<PlayerMovement>().Freeze(false);
        }

        int mode = gamemodeOrder[0];
        gamemodeOrder.RemoveAt(0);
        currentGamemodeServer = Instantiate(gamemodeServerPrefabs[mode]).GetComponent<Gamemode>();

        seed = UnityEngine.Random.Range(0, 214748364);

        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.StartGamemode);
        message.AddInt(mode);
        message.AddInt(seed);
        Server.SendToAll(message);
    }

    public void StartGame()
    {
        gamemodeOrder = Enumerable.Range(0, gamemodeServerPrefabs.Length).ToList().OrderBy(_ => Guid.NewGuid()).ToList();
        UIManager.Singleton.GameStarted();

        started = true;

        StartGamemode();
    }

    public void EndGamemode()
    {
        int count = 3;
        //TODO
        foreach(ushort id in currentGamemodeServer.EndGame())
        {
            ServerPlayer.List[id].crowns += count;

            Message scoreMsg = Message.Create(MessageSendMode.Reliable, ServerToClientId.SetScore);
            scoreMsg.AddUShort(id);
            scoreMsg.AddInt(ServerPlayer.List[id].crowns);
            NetworkManager.Singleton.Server.SendToAll(scoreMsg);

            count--;
        }

        Destroy(Singleton.currentGamemodeServer.gameObject);

        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.EndGamemode);
        Server.SendToAll(message);

        Singleton.StartCoroutine(Singleton.StartNextGamemode());
    }

    IEnumerator StartNextGamemode()
    {
        yield return new WaitForSeconds(5.0f);
        StartGamemode();
    }

    [MessageHandler((ushort)ServerToClientId.StartGamemode, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void StartGamemode(Message message)
    {
        int mode = message.GetInt();
        Singleton.seed = message.GetInt();
        Singleton.currentGamemodeClient = Instantiate(Singleton.gamemodeClientPrefabs[mode]);
        UIManager.Singleton.SetScoreMenu(false);
        UIManager.Singleton.GameStarted();

        foreach (ClientPlayer p in ClientPlayer.list.Values)
        {
            p.transform.position = Vector3.zero;
        }
    }

    [MessageHandler((ushort)ServerToClientId.EndGamemode, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void EndGamemode(Message message)
    {
        Singleton.StartCoroutine(Singleton.DestroyClientGamemode());
    }

    IEnumerator DestroyClientGamemode()
    {
        yield return new WaitForSeconds(2.0f);
        Destroy(Singleton.currentGamemodeClient);
        UIManager.Singleton.SetScoreMenu(true);
    }
}


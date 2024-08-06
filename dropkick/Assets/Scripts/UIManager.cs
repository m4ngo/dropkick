using System.Collections.Generic;
using Riptide;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private static UIManager _singleton;
    internal static UIManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(UIManager)} instance already exists, destroying object!");
                Destroy(value);
            }
        }
    }

    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject customMenu;
    [SerializeField] private GameObject lobbyMenu;
    [SerializeField] private InputField roomIdField;
    [SerializeField] private InputField[] roomIdDisplayFields;

    [SerializeField] private MeshRenderer[] proxyPlayer;
    [SerializeField] private GameObject proxyPlayerParent;

    [Header("Game Menu")]
    [SerializeField] private GameObject gameMenu;

    [SerializeField] private GameObject playerEntry;
    [SerializeField] private GameObject entryLayoutGroup;
    [SerializeField] private Text readyText;
    private bool ready = false;
    Dictionary<ushort, GameObject> entries = new Dictionary<ushort, GameObject>();

    [field: SerializeField] public Material[] colors { get; private set; }
    public int color { get; private set; }

    private void Awake()
    {
        Singleton = this;
    }

    private void Update() {
        GameObject menu = gameMenu.transform.GetChild(0).gameObject;
        if(Input.GetKeyDown(KeyCode.Escape)){
            menu.SetActive(!menu.activeInHierarchy);
        }
        if(!gameMenu.activeInHierarchy){
            menu.SetActive(false);
        }
    }

    public void HostClicked()
    {
        EnterRoom();
        LobbyManager.Singleton.CreateLobby();
    }

    internal void LobbyCreationFailed()
    {
        mainMenu.SetActive(true);
    }

    internal void LobbyCreationSucceeded(ulong lobbyId)
    {
        foreach(InputField display in roomIdDisplayFields){
            display.text = lobbyId.ToString();
            display.gameObject.SetActive(true);
        }
        lobbyMenu.SetActive(true);
    }

    public void CopyClicked(){
        TextEditor te = new TextEditor();
        te.text = roomIdDisplayFields[0].text;
        te.SelectAll();
        te.Copy();
    }

    public void JoinClicked()
    {
        if (string.IsNullOrEmpty(roomIdField.text))
        {
            Debug.Log("A room ID is required to join!");
            return;
        }

        LobbyManager.Singleton.JoinLobby(ulong.Parse(roomIdField.text));
        EnterRoom();
    }

    void EnterRoom(){
        mainMenu.SetActive(false);
        proxyPlayerParent.SetActive(false);
    }

    internal void LobbyEntered()
    {
        foreach(InputField display in roomIdDisplayFields){
            display.gameObject.SetActive(false);
        }
        lobbyMenu.SetActive(true);
    }

    public void LeaveClicked()
    {
        LobbyManager.Singleton.LeaveLobby();
        BackToMain();
    }

    public void CustomizeCharacterClicked()
    {
        customMenu.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void EnableMain()
    {
        mainMenu.SetActive(true);
        lobbyMenu.SetActive(false);
        customMenu.SetActive(false);
        gameMenu.SetActive(false);
    }

    public void SetPlayerColor(int i)
    {
        foreach(MeshRenderer rend in proxyPlayer){
            rend.material = colors[i];
        }
        color = i;
    }

    internal void BackToMain()
    {
        proxyPlayerParent.SetActive(true);
        EnableMain();
        foreach (Transform child in NetworkManager.Singleton.transform)
            Destroy(child.gameObject);
        foreach (Transform child in NetworkManager.Singleton.clientGen.transform)
            Destroy(child.gameObject); 
    }

    public void GameStarted(){
        lobbyMenu.SetActive(false);
        gameMenu.SetActive(true);
    }

    public void SetReady(bool temp){
        ready = temp;
        readyText.text = ready ? "CANCEL" : "READY";
    }

    public void ToggleReady(){
        SetReady(!ready);
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.Ready);
        message.AddBool(ready);
        NetworkManager.Singleton.Client.Send(message);
    }

    public void AddEntry(ushort id, bool status){
        GameObject entry = Instantiate(playerEntry, entryLayoutGroup.transform);
        entries.Add(id, entry);
        entry.transform.GetChild(1).GetChild(0).GetComponent<Text>().text = ClientPlayer.list[id].GetUsername();
        SetEntryStatus(id, status);
    }

    public void RemoveEntry(ushort id){
        Destroy(entries[id]); //destryo game object
        entries.Remove(id); //remove from dictionary
    }

    void SetEntryStatus(ushort id, bool status){
        entries[id].transform.GetChild(0).GetChild(0).GetComponent<Text>().text = status ? "READY" : "";
    }

    void AllReady(){
        foreach(ServerPlayer serverPlayer in ServerPlayer.List.Values){
            if(!serverPlayer.ready) return;
        }

        NetworkManager.Singleton.StartGame();
    }

    [MessageHandler((ushort)ClientToServerId.Ready, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReadyStatusReceived(ushort fromClientId, Message message)
    {
        ServerPlayer player = ServerPlayer.List[fromClientId];
        bool ready = message.GetBool();
        player.SetReady(ready);

        Message outMessage = Message.Create(MessageSendMode.Reliable, ServerToClientId.Ready);
        outMessage.AddUShort(fromClientId);
        outMessage.AddBool(ready);
        NetworkManager.Singleton.Server.SendToAll(outMessage);

        UIManager.Singleton.AllReady();
    }

    [MessageHandler((ushort)ServerToClientId.Ready, NetworkManager.PlayerHostedDemoMessageHandlerGroupId)]
    private static void ReadyStatusClient(Message message)
    {
        ushort playerId = message.GetUShort(); //get player id
        if (!ClientPlayer.list.TryGetValue(playerId, out ClientPlayer player))
            return;
        UIManager.Singleton.SetEntryStatus(playerId, message.GetBool());
    }
}
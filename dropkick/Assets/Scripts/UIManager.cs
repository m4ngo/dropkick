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
    [SerializeField] private InputField roomIdDisplayField;

    [SerializeField] private MeshRenderer[] proxyPlayer;
    [SerializeField] private GameObject proxyPlayerParent;

    [field: SerializeField] public Material[] colors { get; private set; }
    public int face { get; private set; }
    public int color { get; private set; }

    private void Awake()
    {
        Singleton = this;
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
        roomIdDisplayField.text = lobbyId.ToString();
        roomIdDisplayField.gameObject.SetActive(true);
        lobbyMenu.SetActive(true);
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
        roomIdDisplayField.gameObject.SetActive(false);
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
}
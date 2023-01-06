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

    [SerializeField] private Image proxyPlayer;
    [SerializeField] private Image proxyPlayerFace;

    [field: SerializeField] public Face[] faces { get; private set; }
    [field: SerializeField] public Color[] colors { get; private set; }
    public int face { get; private set; }
    public int color { get; private set; }

    private void Awake()
    {
        Singleton = this;
    }

    public void HostClicked()
    {
        mainMenu.SetActive(false);

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
        mainMenu.SetActive(false);
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
        proxyPlayer.color = colors[i];
        color = i;
    }
    public void SetPlayerFace(int i)
    {
        proxyPlayerFace.sprite = faces[i].sprite;
        proxyPlayerFace.color = faces[i].color;
        face = i;
    }

    internal void BackToMain()
    {
        EnableMain();
        foreach (Transform child in NetworkManager.Singleton.transform)
            Destroy(child.gameObject);
        foreach (Transform child in NetworkManager.Singleton.clientGen.transform)
            Destroy(child.gameObject); 
    }
}

[System.Serializable]
public class Face
{
    public Sprite sprite;
    public Color color;
}
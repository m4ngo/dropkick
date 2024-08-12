using UnityEngine;
using UnityEngine.UI;

internal class PlayerUIManager : MonoBehaviour
{
    [SerializeField] private Text usernameText;

    internal void SetName(string _name)
    {
        usernameText.text = _name;
    }
}


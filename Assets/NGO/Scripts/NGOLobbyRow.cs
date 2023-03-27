using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NGOLobbyRow : MonoBehaviour
{
    [SerializeField]
    TMPro.TextMeshPro lobbyRowText;

    public void SetRowText(string text)
    {
        lobbyRowText.text = text;
    }

    public string GetRowText()
    {
        return lobbyRowText.text;
    }
}

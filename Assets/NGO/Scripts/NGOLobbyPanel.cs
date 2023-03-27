using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class NGOLobbyPanel : MonoBehaviour
{
    [SerializeField]
    private GameObject menuPanel;

    [SerializeField]
    private NGOAnchorManager anchorManager;

    [SerializeField]
    private GameObject roomLayoutPanel;

    [SerializeField]
    private GameObject roomLayoutPanelRowPrefab;

    List<GameObject> lobbyRowList = new List<GameObject>();

    [SerializeField]
    private PokeInteractable createRoomPokeInter;

    [SerializeField]
    private PokeInteractable joinRoomPokeInter;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnCreateRoomButtonPressed()
    {
        SampleController.Instance.Log("OnCreateRoomButtonPressed");

        if (AuthenticationService.Instance.IsSignedIn)
        {         
            
            Random.InitState((int)(Time.time * 10000));
            string testName = "TestUser" + Random.Range(0, 1000);
            //PhotonPun.PhotonNetwork.NickName = testName;
            anchorManager.CreateNewRoomForLobby(testName);
            

            menuPanel.SetActive(true);
            gameObject.SetActive(false);
        }
        else
        {
            SampleController.Instance.Log("Attempting to reconnect");
            AuthenticationService.Instance.SignInAnonymouslyAsync();
            //PhotonPun.PhotonNetwork.ConnectUsingSettings();
            //OnCreateRoomButtonPressed();
        }
    }

    public void OnFindRoomButtonPressed()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            SampleController.Instance.Log("There are currently " + lobbyRowList.Count + " rooms in the lobby");
            roomLayoutPanel.SetActive(true);
        }
        else
        {
            SampleController.Instance.Log("Attempting to reconnect and rejoin a room");
            //PhotonPun.PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void OnJoinRoomButtonPressed(TMPro.TextMeshPro textObj)
    {
        AttemptToJoinRoom(textObj.text);
    }

    void AttemptToJoinRoom(string roomName)
    {
        SampleController.Instance.Log("OnJoinRoomButtonPressed");

        /*if (PhotonPun.PhotonNetwork.NickName == "")
        {
            string testName = "TestUser" + Random.Range(0, 1000);
            //PhotonPun.PhotonNetwork.NickName = testName;
        }*/

        anchorManager.JoinRoomFromLobby(roomName);

        menuPanel.SetActive(true);
        gameObject.SetActive(false);
    }

    public void SetRoomList(List<Lobby> roomList)
    {
        foreach (Transform roomTransform in roomLayoutPanel.transform)
        {
            if (roomTransform.gameObject != roomLayoutPanelRowPrefab)
                Destroy(roomTransform.gameObject);
        }
        lobbyRowList.Clear();

        if (roomList.Count > 0)
        {
            for (int i = 0; i < roomList.Count; i++)
            {
                if (roomList[i].Players.Count == 0)
                    continue;

                GameObject newLobbyRow = Instantiate(roomLayoutPanelRowPrefab, roomLayoutPanel.transform);
                newLobbyRow.SetActive(true);
                newLobbyRow.GetComponent<NGOLobbyRow>().SetRowText(roomList[i].Name);
                lobbyRowList.Add(newLobbyRow);
            }
        }
        else
        {
            //*
            //TODO - Remove this test data after using it to implement scrolling / paging
            for (int i = 0; i < 10; i++)
            {
                GameObject newLobbyRow = GameObject.Instantiate(roomLayoutPanelRowPrefab, roomLayoutPanel.transform);
                newLobbyRow.SetActive(true);
                newLobbyRow.GetComponent<NGOLobbyRow>().SetRowText("Room#" + i);
                lobbyRowList.Add(newLobbyRow);
            }
            //*
        }
    }

    private void DisableRoomButtons()
    {
        if (createRoomPokeInter)
        {
            createRoomPokeInter.enabled = false;
            TMPro.TextMeshPro buttonText = createRoomPokeInter.GetComponentInChildren<TMPro.TextMeshPro>();
            if (buttonText)
                buttonText.color = new Color(buttonText.color.r, buttonText.color.g, buttonText.color.b, 0.25f);
        }

        if (joinRoomPokeInter)
        {
            joinRoomPokeInter.enabled = false;
            TMPro.TextMeshPro buttonText = joinRoomPokeInter.GetComponentInChildren<TMPro.TextMeshPro>();
            if (buttonText)
                buttonText.color = new Color(buttonText.color.r, buttonText.color.g, buttonText.color.b, 0.25f);
        }
    }

    public void DisplayLobbyPanel()
    {
        gameObject.SetActive(true);
        menuPanel.SetActive(false);
    }

    public void EnableRoomButtons()
    {
        if (createRoomPokeInter)
        {
            createRoomPokeInter.enabled = true;
            TMPro.TextMeshPro buttonText = createRoomPokeInter.GetComponentInChildren<TMPro.TextMeshPro>();
            if (buttonText)
                buttonText.color = Color.white;
        }

        if (joinRoomPokeInter)
        {
            joinRoomPokeInter.enabled = true;
            TMPro.TextMeshPro buttonText = joinRoomPokeInter.GetComponentInChildren<TMPro.TextMeshPro>();
            if (buttonText)
                buttonText.color = Color.white;
        }
    }
}

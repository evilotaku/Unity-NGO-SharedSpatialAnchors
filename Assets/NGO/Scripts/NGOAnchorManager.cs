using Common;
using Oculus.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Relay;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using System.Collections;


public class NGOAnchorManager : NetworkBehaviour
{
    [SerializeField]
    private SharedAnchorControlPanel controlPanel;

    [SerializeField]
    private NGOLobbyPanel lobbyPanel;
    [SerializeField]
    NetworkObject passthroughAvatar;

    public static NGOAnchorManager Instance;

    private const string UserIdsKey = "userids";
    private const char Separator = ',';
    private const byte PacketFormat = 0;

    // The size of the packet we are sending and receiving
    private const int UuidSize = 16;

    // Reusable buffer to serialize the data into
    private byte[] _sendUuidBuffer = new byte[1];
    private byte[] _getUuidBuffer = new byte[UuidSize];
    private byte[] _fakePacket = new byte[1];
    private string _oculusUsername;
    private ulong _oculusUserId;
    private Guid _fakeUuid;

    private readonly HashSet<string> _usernameList = new HashSet<string>();
    LobbyEventCallbacks lobbyEvents = new();
    public Lobby lobby;
    private Player player;

    async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
        await UnityServices.InitializeAsync();
        AuthenticationService.Instance.SignedIn += OnSignedIn;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        lobbyEvents.LobbyChanged += LobbyChanged;
    }

    private void OnClientDisconnect(ulong obj)
    {
        SampleController.Instance.Log("Disconnected from the Relay...");
    }

    private void OnClientConnected(ulong clientId)
    {
        SampleController.Instance.Log("Connected to the Relay...");
        if (SampleController.Instance.automaticCoLocation && IsHost)
        {
            Instantiate(passthroughAvatar, Vector3.zero, Quaternion.identity).SpawnWithOwnership(clientId);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        SampleController.Instance.Log("System version: " + OVRPlugin.version);

        //PhotonPun.PhotonNetwork.ConnectUsingSettings();

        Core.Initialize();
        Users.GetLoggedInUser().OnComplete(GetLoggedInUserCallback);        
        AuthenticationService.Instance.SignInAnonymouslyAsync();

        Array.Resize(ref _fakePacket, 1 + UuidSize);
        _fakePacket[0] = PacketFormat;

        var offset = 1;
        var fakeBytes = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };

        _fakeUuid = new Guid(fakeBytes);
        PackUuid(_fakeUuid, _fakePacket, ref offset);
    }

    public void OnSignedIn()
    {
        //SampleController.Instance.Log("Photon::OnConnectedToMaster: successfully connected to photon: " + PhotonPun.PhotonNetwork.CloudRegion);

        //PhotonPun.PhotonNetwork.JoinLobby();

        if (lobbyPanel)
            lobbyPanel.EnableRoomButtons();
    }

    public static async Task<RelayServerData> AllocateRelayServerAndGetJoinCode(int maxConnections, string region = null)
    {
        Allocation allocation;
        string createJoinCode;
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections, region);
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay create allocation request failed {e.Message}");
            throw;
        }

        Debug.Log($"server: {allocation.ConnectionData[0]} {allocation.ConnectionData[1]}");
        Debug.Log($"server: {allocation.AllocationId}");

        try
        {
            createJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
        catch
        {
            Debug.LogError("Relay create join code request failed");
            throw;
        }

        return new RelayServerData(allocation, "dtls");
    }

    public static async Task<RelayServerData> JoinRelayServerFromJoinCode(string joinCode)
    {
        JoinAllocation allocation;
        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        }
        catch
        {
            Debug.LogError("Relay create join code request failed");
            throw;
        }

        Debug.Log($"client: {allocation.ConnectionData[0]} {allocation.ConnectionData[1]}");
        Debug.Log($"host: {allocation.HostConnectionData[0]} {allocation.HostConnectionData[1]}");
        Debug.Log($"client: {allocation.AllocationId}");

        return new RelayServerData(allocation, "dtls");
    }

    private void GetLoggedInUserCallback(Message msg)
    {
        if (msg.IsError)
        {
            SampleController.Instance.Log("GetLoggedInUserCallback: failed with error: " + msg.GetError());
            return;
        }

        SampleController.Instance.Log("GetLoggedInUserCallback: success with message: " + msg + " type: " + msg.Type);

        var isLoggedInUserMessage = msg.Type == Message.MessageType.User_GetLoggedInUser;

        if (!isLoggedInUserMessage) return;

        _oculusUsername = msg.GetUser().OculusID;
        _oculusUserId = msg.GetUser().ID;

        SampleController.Instance.Log("GetLoggedInUserCallback: oculus user name: " + _oculusUsername + " oculus id: " + _oculusUserId);

        if (_oculusUserId == 0)
            SampleController.Instance.Log("You are not authenticated to use this app. Shared Spatial Anchors will not work.");

        //PhotonPun.PhotonNetwork.LocalPlayer.NickName = _oculusUsername;
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public async void CreateNewRoomForLobby(string lobbyName)
    {
        int maxPlayers = 4;
        CreateLobbyOptions options = new CreateLobbyOptions();

        var serverRelayUtilityTask = AllocateRelayServerAndGetJoinCode(maxPlayers);
        while (!serverRelayUtilityTask.IsCompleted)
        {
            await Task.Yield();
        }
        if (serverRelayUtilityTask.IsFaulted)
        {
            Debug.LogError("Exception thrown when attempting to start Relay Server. Server not started. Exception: " + serverRelayUtilityTask.Exception.Message);            
        }

        var relayServerData = serverRelayUtilityTask.Result;

        player = new(id: AuthenticationService.Instance.PlayerId,
                     data: new()
                     {{
                        "Name", new(
                        visibility: PlayerDataObject.VisibilityOptions.Public,
                        value: _oculusUsername)
                     }},
                     allocationId: relayServerData.AllocationId.ToString(),
                     connectionInfo: relayServerData.HostConnectionData.ToString());

        //await Task.Yield();

        options.IsPrivate = false;
        options.Player = player;      
        

        Instance.lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options); 
        StartCoroutine(HeartbeatLobbyCoroutine(lobby.Id, 15));
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
        NetworkManager.Singleton.StartHost();

        OnJoinedRoom(lobby);

    }

    IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);

        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

     public async void JoinRoomFromLobby(string lobbycode)
    {       
        try
        {
            JoinLobbyByCodeOptions options = new();
            player = new(id: AuthenticationService.Instance.PlayerId,
                     data: new()
                     {{
                        "Name", new(
                        visibility: PlayerDataObject.VisibilityOptions.Public,
                        value: _oculusUsername)
                     }});
            options.Player = player;
            Instance.lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbycode, options);
            OnJoinedRoom(lobby);
            var alloc = lobby.Players.Find(x => x.Id == lobby.HostId).AllocationId;
            var RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(new Guid(alloc));
            var clientRelayUtilityTask = JoinRelayServerFromJoinCode(RelayJoinCode);

            while (!clientRelayUtilityTask.IsCompleted)
            {
                await Task.Yield();
            }

            if (clientRelayUtilityTask.IsFaulted)
            {
                Debug.LogError("Exception thrown when attempting to connect to Relay Server. Exception: " + clientRelayUtilityTask.Exception.Message);
                return;
            }

            var relayServerData = clientRelayUtilityTask.Result;

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
            await Task.Yield();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }

        
    }

    public void OnJoinedRoom(Lobby lobby)
    {
        SampleController.Instance.Log("NGO::OnJoinedRoom: joined lobby: " + lobby.Name);

        controlPanel.RoomText.text = "Lobby: " + lobby.Name;

        AddUserToUserListState(_oculusUserId);

        foreach (var player in lobby.Players)
        {
            AddToUsernameList(player.Data["Name"].Value);
        }

        if (lobbyPanel)
        {
            lobbyPanel.gameObject.SetActive(false);
        }

       

        GameObject sceneCaptureController = GameObject.Find("SceneCaptureController");
        if (sceneCaptureController)
        {
            if (IsHost)
            {
                sceneCaptureController.GetComponent<SceneApiSceneCaptureStrategy>().InitSceneCapture();
                sceneCaptureController.GetComponent<SceneApiSceneCaptureStrategy>().BeginCaptureScene();
            }
            else
            {
                LoadRoomFromProperties(lobby);
            }
        }
    }


    public async void UpdateLobbyList()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            options.Count = 25;

            // Filter for open lobbies only
            options.Filters = new List<QueryFilter>()
            {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };

            // Order by newest lobbies first
            options.Order = new List<QueryOrder>()
            {
                new QueryOrder(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };

            QueryResponse lobbies = await Lobbies.Instance.QueryLobbiesAsync(options);

            lobbyPanel.SetRoomList(lobbies.Results);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private void LobbyChanged(ILobbyChanges changes)
    {
        if (changes.Data.Changed)
        {
            if (changes.Data.Value.ContainsKey(UserIdsKey))
            {
                foreach (SharedAnchor anchor in SampleController.Instance.GetLocalPlayerSharedAnchors())
                {
                    anchor.ReshareAnchor();
                }
            }

            ChangedOrRemovedLobbyValue<DataObject> data;
            if (changes.Data.Value.TryGetValue("roomData", out data))
            {
                SampleController.Instance.Log("Room data recieved from master client.");
                DeserializeToScene(data.Value.Value);
            }
        }
    }

    private void WaitToSendAnchor()
    {
        SampleController.Instance.colocationAnchor.OnShareButtonPressed();
    }

    private void WaitToReshareAnchor()
    {
        if (SampleController.Instance.colocationCachedAnchor != null)
        {
            SampleController.Instance.colocationCachedAnchor.ReshareAnchor();
        }
    }
        

    private static void PackUuid(Guid uuid, byte[] buf, ref int offset)
    {
        SampleController.Instance.Log("PackUuid: packing uuid: " + uuid);

        Buffer.BlockCopy(uuid.ToByteArray(), 0, buf, offset, UuidSize);
        offset += 16;
    }

    public void PublishAnchorUuids(Guid[] uuids, uint numUuids, bool isBuffered)
    {
        SampleController.Instance.Log("PublishAnchorUuids: numUuids: " + numUuids);

        Array.Resize(ref _sendUuidBuffer, 1 + UuidSize * (int)numUuids);
        _sendUuidBuffer[0] = PacketFormat;

        var offset = 1;
        for (var i = 0; i < numUuids; i++)
        {
            PackUuid(uuids[i], _sendUuidBuffer, ref offset);
        }

        List<ulong> rpcTarget;

        //photonView.RPC(nameof(CheckForAnchorsShared), rpcTarget, _sendUuidBuffer);
        CheckForAnchorsSharedClientRpc(_sendUuidBuffer);
    }

    [ClientRpc]
    private void CheckForAnchorsSharedClientRpc(byte[] uuidsPacket)
    {
        Debug.Log(nameof(CheckForAnchorsSharedClientRpc) + " : found a packet...");

        var isInvalidPacketSize = uuidsPacket.Length % UuidSize != 1;

        if (isInvalidPacketSize)
        {
            SampleController.Instance.Log($"{nameof(CheckForAnchorsSharedClientRpc)}: invalid packet size: {uuidsPacket.Length} should be 1+{UuidSize}*numUuidsShared");
            return;
        }

        var isInvalidPacketType = uuidsPacket[0] != PacketFormat;

        if (isInvalidPacketType)
        {
            SampleController.Instance.Log(nameof(CheckForAnchorsSharedClientRpc) + " : invalid packet type: " + uuidsPacket.Length);
            return;
        }

        var numUuidsShared = (uuidsPacket.Length - 1) / UuidSize;
        var isEmptyUuids = numUuidsShared == 0;

        if (isEmptyUuids)
        {
            SampleController.Instance.Log(nameof(CheckForAnchorsSharedClientRpc) + " : we received a no-op packet");
            return;
        }

        SampleController.Instance.Log(nameof(CheckForAnchorsSharedClientRpc) + " : we received a valid uuid packet");

        var uuids = new HashSet<Guid>();
        var offset = 1;

        for (var i = 0; i < numUuidsShared; i++)
        {
            // We need to copy exactly 16 bytes here because Guid() expects a byte buffer sized to exactly 16 bytes

            Buffer.BlockCopy(uuidsPacket, offset, _getUuidBuffer, 0, UuidSize);
            offset += UuidSize;

            var uuid = new Guid(_getUuidBuffer);

            Debug.Log(nameof(CheckForAnchorsSharedClientRpc) + " : unpacked uuid: " + uuid);

            var shouldExit = uuid == _fakeUuid;

            if (shouldExit)
            {
                SampleController.Instance.Log(nameof(CheckForAnchorsSharedClientRpc) + " : received the fakeUuid/noop... exiting");
                return;
            }

            uuids.Add(uuid);
        }

        Debug.Log(nameof(CheckForAnchorsSharedClientRpc) + " : set of uuids shared: " + uuids.Count);
        SharedAnchorLoader.Instance.LoadAnchorsFromRemote(uuids);
    }

    private void LoadRoomFromProperties(Lobby lobby)
    {
        SampleController.Instance.Log(nameof(LoadRoomFromProperties));


        if (lobby == null)
        {
            SampleController.Instance.Log("no ROOm?");
            return;
        }

        //object data;
        if (lobby.Data.TryGetValue("roomData", out DataObject data))
        {
            DeserializeToScene(data.Value);
        }
    }

    void DeserializeToScene(string jsonData)
    {
        //string jsonData = System.Text.Encoding.UTF8.GetString((byte[])byteData);
        Scene deserializedScene = JsonUtility.FromJson<Scene>(jsonData);
        if (deserializedScene != null)
            SampleController.Instance.Log("deserializedScene num walls: " + deserializedScene.walls.Length);
        else
            SampleController.Instance.Log("deserializedScene is NULL");

        GameObject worldGenerationController = GameObject.Find("WorldGenerationController");
        if (worldGenerationController)
            worldGenerationController.GetComponent<WorldGenerationController>().GenerateWorld(deserializedScene);
    }

    public static HashSet<ulong> GetUserList()
    {
        if (Instance.lobby == null || !Instance.lobby.Data.ContainsKey(UserIdsKey))
        {
            return new HashSet<ulong>();
        }

        var userListAsString = Instance.lobby.Data[UserIdsKey].Value;
        var parsedList = userListAsString.Split(Separator).Select(ulong.Parse);

        return new HashSet<ulong>(parsedList);
    }

    private void AddUserToUserListState(ulong userId)
    {
        var userList = GetUserList();
        var isKnownUserId = userList.Contains(userId);

        if (isKnownUserId)
        {
            return;
        }

        userList.Add(userId);
        SaveUserList(userList);
    }

    public void RemoveUserFromUserListState(ulong userId)
    {
        var userList = GetUserList();
        var isUnknownUserId = !userList.Contains(userId);

        if (isUnknownUserId)
        {
            return;
        }

        userList.Remove(userId);
        SaveUserList(userList);
    }

    private static async void SaveUserList(HashSet<ulong> userList)
    {
        var userListAsString = string.Join(Separator.ToString(), userList);
        //var setValue = new ExitGames.Client.Photon.Hashtable { { UserIdsKey, userListAsString } };

        //PhotonPun.PhotonNetwork.CurrentRoom.SetCustomProperties(setValue);

        UpdateLobbyOptions options = new()
        {
            Data = new Dictionary<string, DataObject>()
            {
                {
                    UserIdsKey,
                    new DataObject(
                        visibility: DataObject.VisibilityOptions.Private,
                        value: userListAsString)
                }                
            }
        };

        await LobbyService.Instance.UpdateLobbyAsync(Instance.lobby.Id, options);
    }

    private void AddToUsernameList(string username)
    {
        var isKnownUserName = _usernameList.Contains(username);

        if (isKnownUserName)
        {
            return;
        }

        _usernameList.Add(username);
        UpdateUsernameListDebug();
    }

    private void RemoveFromUsernameList(string username)
    {
        var isUnknownUserName = !_usernameList.Contains(username);

        if (isUnknownUserName)
        {
            return;
        }

        _usernameList.Remove(username);
        UpdateUsernameListDebug();
    }

    private void UpdateUsernameListDebug()
    {
        controlPanel.UserText.text = "Users:";

        var usernameListAsString = string.Join(Separator.ToString(), _usernameList);
        var usernames = usernameListAsString.Split(',');

        foreach (var username in usernames)
        {
            controlPanel.UserText.text += "\n" + "- " + username;
        }
    }

    public static string[] GetUsers()
    {
        //var userIdsProperty = (string)PhotonPun.PhotonNetwork.CurrentRoom.CustomProperties[UserIdsKey];

        List<string> userIds = new();
        Instance.lobby.Players.ForEach((player) => 
        {
            userIds.Add(player.Data["Name"].Value);
        });

        Debug.Log("GetUsers: " + userIds);

        //var userIds = userIdsProperty.Split(',');
        return userIds.ToArray();
    }

    //Two users are now confirmed to be on the same anchor
    public void SessionStart()
    {
        if(IsServer)
        {
            //photonView.RPC("SendSessionStart", PhotonPun.RpcTarget.Others);
            SendSessionStartClientRpc();
        }

    }


    [ClientRpc]
    public void SendSessionStartClientRpc()
    {
        CoLocatedPassthroughManager.Instance.SessionStart();
    }

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.IO;

public class PassthroughAvatarNGO : NetworkBehaviour
{

    public GameObject headPrefab, leftPrefab, rightPrefab;
    private Transform head, right, left, body;

    private AvatarPassthrough passthrough;
    PassthroughAvatarStruct avatarStruct = new();
    public NetworkVariable<PassthroughAvatarStruct> avatarTransform = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    

    // Start is called before the first frame update
    public override void OnNetworkSpawn()
    {
        if(!IsOwner)
        {
            body = new GameObject("Player" + OwnerClientId).transform;
            head = headPrefab == null ?
                new GameObject("head").transform :
                Instantiate(headPrefab, Vector3.zero, Quaternion.identity).transform;
            right = rightPrefab == null ?
                new GameObject("right").transform :
                Instantiate(headPrefab, Vector3.zero, Quaternion.identity).transform;
            left = leftPrefab == null ?
                new GameObject("left").transform :
                Instantiate(headPrefab, Vector3.zero, Quaternion.identity).transform;
            head.SetParent(body);
            right.SetParent(body);
            left.SetParent(body);

            avatarTransform.OnValueChanged += OnAvatarTransformChanged;
        }

        passthrough = CoLocatedPassthroughManager.Instance.AddCoLocalUser(head, right, left);
        passthrough.IsMine = IsOwner;
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;
        avatarStruct.head_pos = CoLocatedPassthroughManager.Instance.localHead.position;
        avatarStruct.head_rot = CoLocatedPassthroughManager.Instance.localHead.eulerAngles;
        avatarStruct.left_pos = CoLocatedPassthroughManager.Instance.localLeft.position;
        avatarStruct.left_rot = CoLocatedPassthroughManager.Instance.localLeft.eulerAngles;
        avatarStruct.right_pos = CoLocatedPassthroughManager.Instance.localRight.position;
        avatarStruct.right_rot = CoLocatedPassthroughManager.Instance.localRight.eulerAngles;
        avatarStruct.passthrough_lcoation = CoLocatedPassthroughManager.Instance.location;

        avatarTransform.Value = avatarStruct;

    }

    public void OnAvatarTransformChanged(PassthroughAvatarStruct oldValue, PassthroughAvatarStruct newValue)
    {
        head.position = newValue.head_pos;
        head.eulerAngles = newValue.head_rot;
        left.position = newValue.left_pos;
        left.eulerAngles = newValue.left_rot;
        right.position = newValue.right_pos;
        right.eulerAngles = newValue.right_rot;
        passthrough.location = newValue.passthrough_lcoation;
    }

    private void OnDisable()
    {
        CoLocatedPassthroughManager.Instance.RemoveCoLocalUser(head);
        Destroy(body.gameObject);
    }
}

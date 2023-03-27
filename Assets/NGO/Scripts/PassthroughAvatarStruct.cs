using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public struct PassthroughAvatarStruct : INetworkSerializable
{
    public Vector3 head_pos, head_rot, left_pos, left_rot, right_pos, right_rot;
    public string passthrough_lcoation;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref head_pos);
        serializer.SerializeValue(ref head_rot);
        serializer.SerializeValue(ref left_pos);
        serializer.SerializeValue(ref left_rot);
        serializer.SerializeValue(ref right_pos);
        serializer.SerializeValue(ref right_rot);
        serializer.SerializeValue(ref passthrough_lcoation);
    }

}

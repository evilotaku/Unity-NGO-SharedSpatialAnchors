/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEngine;
using Unity.Netcode;
using Oculus.Interaction;

public class NGOGrabbable : NetworkBehaviour
{
    protected Grabbable _grabbable;
    

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
       
    }

    private void OnEnable()
    {
        _grabbable.WhenPointerEventRaised += OnPointerEventRaised;
    }

    private void OnDisable()
    {
        _grabbable.WhenPointerEventRaised -= OnPointerEventRaised;
    }

    virtual public void OnPointerEventRaised(PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case PointerEventType.Select:
                if (_grabbable.SelectingPointsCount == 1)
                {
                    SampleController.Instance.Log("Grabbable object grabbed");

                    TransferOwnershipToLocalPlayerServerRpc();
                }
                break;
            case PointerEventType.Unselect:
                if (_grabbable.SelectingPointsCount == 0)
                {
                    SampleController.Instance.Log("Grabbable object ungrabbed");
                }
                break;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TransferOwnershipToLocalPlayerServerRpc(ServerRpcParams param = default)
    {
        /*if (_photonView.Owner != PhotonNetwork.LocalPlayer)
        {
            SampleController.Instance.Log("TransferOwnershipToLocalPlayer: changing photon ownership of " + gameObject.name + " to local player.");
            
            _photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
        }*/

        if(OwnerClientId != param.Receive.SenderClientId)
        {
            SampleController.Instance.Log("TransferOwnershipToLocalPlayer: changing Netcode ownership of " + gameObject.name + " to local player.");

            NetworkObject.ChangeOwnership(param.Receive.SenderClientId);
        }
    }
}

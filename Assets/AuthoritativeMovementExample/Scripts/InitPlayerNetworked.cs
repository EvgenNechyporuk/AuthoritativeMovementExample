using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BeardedManStudios.Forge.Networking.Generated;
using UnityEngine.Events;
using BeardedManStudios.Forge.Networking;
using System;
using BeardedManStudios.Forge.Networking.Unity;

public class InitPlayerNetworked : PlayerBehavior
{

    public GameObject playerPrefab;
    public GameObject otherPlayerPrefab;

    private GameObject playerGO;

    private bool isInited;

    protected override void NetworkStart()
    {
        base.NetworkStart();

        networkObject.SendRpc(RPC_GET_PLAYER_ID, Receivers.ServerAndOwner);

        networkObject.onDestroy += (NetWorker sender) => {
            MainThreadManager.Run(() =>
			{
				Destroy(playerGO);
			});
        };
    }

    public override void GetPlayerId(RpcArgs args)
    {
        if (NetworkManager.Instance.IsServer)
        {
            MainThreadManager.Run(() =>
            {
                networkObject.SendRpc(RPC_ON_PLAYER_ID, Receivers.All, networkObject.inputOwnerId); 
            });
        }
    }

    public override void OnPlayerId(RpcArgs args)
    {
        MainThreadManager.Run(() =>
        {
            uint playerId = args.GetNext<uint>();

            if (isInited) return;

            PlayerNetworked pn;

            if (playerId == NetworkManager.Instance.Networker.Me.NetworkId)
            {
                playerGO = Instantiate(playerPrefab);
            } else {
                playerGO = Instantiate(otherPlayerPrefab);
            }

            pn = playerGO.GetComponent<PlayerNetworked>();
            pn.playerId = playerId;
            pn.networkObject = networkObject;
            pn.NetworkStart();
            isInited = true;
        });
    }

}
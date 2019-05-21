using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Generated;
using BeardedManStudios.Forge.Networking.Unity;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagerNetworked : GameManagerBehavior
{

    public static GameManagerNetworked Instance;
    public bool isInited;

    private List<NetworkingPlayer> players = new List<NetworkingPlayer>();
    private Dictionary<uint, PlayerBehavior> playersBehaviors = new Dictionary<uint, PlayerBehavior>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else if (Instance != this) {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Init()
    {

        PlayerBehavior p;

        MainThreadManager.Run(() => 
        {
            p = NetworkManager.Instance.InstantiatePlayer(position: new Vector3(0f, 1f, 0f));
            p.networkObject.inputOwnerId = NetworkManager.Instance.Networker.Me.NetworkId;
        });

        for (int i = 0; i < players.Count; i++)
        {
            NetworkingPlayer player = players[i];
            if (player != null && player.NetworkId != NetworkManager.Instance.Networker.Me.NetworkId)
            {
                MainThreadManager.Run(() =>
                {
                    p = NetworkManager.Instance.InstantiatePlayer(position: new Vector3(0f, 1f, 0f));
                    p.networkObject.inputOwnerId = player.NetworkId;
                    playersBehaviors.Add(player.NetworkId, p);
                });
            }
        }

        isInited = true;
    }

    protected override void NetworkStart()
    {
        base.NetworkStart();

        if(NetworkManager.Instance.IsServer)
        {

            players.Clear();
            
            PlayerBehavior p;

            NetworkManager.Instance.Networker.playerAccepted += (player, sender) =>
            {
                Debug.Log("GameManagerNetworked: playerAccepted #" + player.NetworkId);
                MainThreadManager.Run(() =>
                {
                    players.Add(player);
                    if(isInited)
                    {
                        p = NetworkManager.Instance.InstantiatePlayer(position: new Vector3(0f, 1f, 0f));
                        p.networkObject.inputOwnerId = player.NetworkId;
                        playersBehaviors.Add(player.NetworkId, p);
                    }
                });
            };

            NetworkManager.Instance.Networker.playerDisconnected += OnDisconnected;

        }

    }

    private void OnDisconnected(NetworkingPlayer player, NetWorker sender)
    {
        if (player.NetworkId == NetworkManager.Instance.Networker.Me.NetworkId) return;
        Debug.Log("GameManagerNetworked: playerDisconnected");
        NetworkObject[] no = sender.NetworkObjectList.FindAll(x => x.Owner.NetworkId == player.NetworkId).ToArray();
        for(int i = 0; i < no.Length; i++)
        {
            NetworkObject n = no[i];
            sender.NetworkObjectList.Remove(n);
            n.Destroy();
        }
        PlayerBehavior pb = playersBehaviors.FirstOrDefault(x => x.Key == player.NetworkId).Value;
        if (pb != null)
        {
            playersBehaviors.Remove(player.NetworkId);
            sender.NetworkObjectList.FirstOrDefault(x => x.NetworkId == pb.networkObject.NetworkId).Destroy();
        }
        players.Remove(player);
    }

}
using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Generated;
using BeardedManStudios.Forge.Networking.Unity;
using UnityEngine;

public class InstantiateInputListener : MonoBehaviour
{
    void Start()
    {
        NetworkManager.Instance.InstantiateInputListener();
    }
}

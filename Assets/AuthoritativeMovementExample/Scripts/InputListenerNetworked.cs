using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Generated;
using System.Collections.Generic;
using UnityEngine;
using System;

public class InputListenerNetworked : InputListenerBehavior
{

    [HideInInspector]
    public List<InputFrame> FramesToPlay;

    [HideInInspector]
    public List<InputFrame> FramesToReconcile;
    
    [HideInInspector]
    public InputFrame inputFrame = InputFrame.Empty;

    private bool networkReady;
    private uint frameNumber;

    private void Start()
    {
        FramesToPlay = new List<InputFrame>();
        FramesToReconcile = new List<InputFrame>();
    }

    protected override void NetworkStart()
    {
        base.NetworkStart();
        networkReady = true;
    }

    public void ResetInputs()
    {
        FramesToPlay.Clear();
        FramesToReconcile.Clear();
        inputFrame = InputFrame.Empty;
        frameNumber = 0;
    }

    void Update()
    {
        if (!networkReady) return;
        if (networkObject.IsOwner || (networkObject.IsServer && networkObject.IsOwner))
        {
            //collect the next input to send
            inputFrame = new InputFrame {
                right = Input.GetKey(KeyCode.A),
                down = Input.GetKey(KeyCode.S),
                left = Input.GetKey(KeyCode.D),
                up = Input.GetKey(KeyCode.W),
                horizontal = Input.GetAxisRaw("Horizontal"),
                vertical = Input.GetAxisRaw("Vertical")
            };
        }
    }

    private void FixedUpdate()
    {
        if (!networkReady) return;

        // If this is a client store and send the current polled input for processing
        if (networkObject.IsOwner || (networkObject.IsServer && networkObject.IsOwner))
        {
            inputFrame.frameNumber = frameNumber++;
            FramesToPlay.Add(inputFrame);
            if(!networkObject.IsServer)
            {
                byte[] bytes = ByteArray.Serialize(inputFrame);
                networkObject.SendRpc(RPC_ON_INPUT, Receivers.Server, bytes);
            }
        }
    }

    public override void OnInput(RpcArgs args)
    {
        if (networkObject.IsServer)
        {
            Byte[] bytes = args.GetNext<Byte[]>();
            InputFrame newest = (InputFrame) ByteArray.Deserialize(bytes);
            FramesToPlay.Add(newest);
        }
    }

}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BeardedManStudios.Forge.Networking.Generated;
using UnityEngine.Events;
using BeardedManStudios.Forge.Networking;
using System;
using System.Linq;
using BeardedManStudios.Forge.Networking.Unity;

public class PlayerNetworked : MonoBehaviour
{

    private float speed = 10f;

    [HideInInspector]
    public uint playerId;

    public PlayerNetworkObject networkObject;

    private GameObject view;
    private new Rigidbody rigidbody;
    private new SphereCollider collider;

    private InputFrame inputFrame = InputFrame.Empty;
    private InputListenerNetworked inputListener;

    private bool networkReady;
    private bool isLocalOwner;
    private bool isInited;

    // Last frame that was processed locally on this machine
    private uint lastLocalFrame;

    // Last frame that was sent (server)/received (client) on the network
    private uint lastNetworkFrame;

    // Calculates the current error between the Simulation and View
    private Vector3 errorVector = Vector3.zero;

    // The interpolation timer for error interpolation
    private float errorTimer;

    private void Awake()
    {
        rigidbody = transform.GetComponentInChildren<Rigidbody>();
        collider = transform.GetComponentInChildren<SphereCollider>();
        view = transform.GetComponentInChildren<Renderer>().gameObject;
    }

    public void NetworkStart()
    {
        networkReady = true;
    }

    private bool Initialize()
    {
        isLocalOwner = NetworkManager.Instance.Networker.Me.NetworkId == playerId;

        if (isLocalOwner || networkObject.IsServer)
        {
            networkObject.positionInterpolation.Enabled = false;
            if (inputListener == null)
            {
                inputListener = FindObjectsOfType<InputListenerNetworked>().FirstOrDefault(x => x.networkObject.Owner.NetworkId == playerId);
                if (inputListener == null)
                {
                    isInited = false;
                    return isInited;
                }
                inputListener.ResetInputs();
            }
        }

        isInited = true;
        return isInited;
    }

    private void Update()
    {
        if (!networkReady || !isInited) return;

        // Set the networked fields in Update so we are
        // up to date per the last physics update
        if (networkObject.IsServer)
        {
            if (lastNetworkFrame < lastLocalFrame)
            {
                networkObject.position = rigidbody.position;
                lastNetworkFrame = lastLocalFrame;
                networkObject.frame = lastLocalFrame;
            }
        }

        CorrectError();
    }

    private void FixedUpdate()
    {
        if (!networkReady) return;
        if (!isInited && !Initialize()) return;
        if ((networkObject.IsServer || isLocalOwner) && inputListener == null) return;

        if (isLocalOwner || networkObject.IsServer)
        {
            // Local client prediction & server authoritative logic
            if (inputListener.FramesToPlay.Count <= 0) return;
            inputFrame = inputListener.FramesToPlay.Pop();
            lastLocalFrame = inputFrame.frameNumber;

            // Try to do a player update (if this fails, something's weird)
            PlayerUpdate(inputFrame);

            inputListener.FramesToReconcile.Add(inputFrame);
        }

        if (!networkObject.IsServer)
        {
            rigidbody.position = networkObject.position;
            if (isLocalOwner && networkObject.frame != 0 && lastNetworkFrame <= networkObject.frame)
            {
                lastNetworkFrame = networkObject.frame;
                Reconcile();
            }
        }
    }

    private void PhysicsCollisions()
    {
        // We don't want to be pushed if we aren't moving.
        if (rigidbody.velocity.magnitude == 0) return;
            
        // Collision detection - get a list of colliders the player's collider overlaps with
        RaycastHit[] hitColliders = Physics.SphereCastAll(collider.center, collider.radius, Vector3.zero, 0f);

        // Collision Resolution - for each of these colliders check if that collider and the player overlap
        int i = 0;
        while (i < hitColliders.Length)
        {
            RaycastHit col = hitColliders[i];
            rigidbody.position += col.normal * col.distance;
            i++;
        }
    }

    private void Move(InputFrame input)
    {
        // Move the player, clamping the movement so diagonals aren't faster
        Vector3 translation = Vector3.ClampMagnitude(new Vector3(input.horizontal, 0, input.vertical), speed);
        rigidbody.position = Vector3.MoveTowards(rigidbody.position, rigidbody.position + translation, speed * Time.fixedDeltaTime);
        rigidbody.velocity = translation + Vector3.up * rigidbody.velocity.y;
    }

    private void PlayerUpdate(InputFrame input)
    {
        // Set the velocity to zero, move the player based on the next input, then detect & resolve collisions
        rigidbody.velocity = new Vector3(0f, rigidbody.velocity.y, 0f);
        if (input != null && input.HasInput)
        {
            Move(input);
        }
    }

    private void Reconcile()
    {
        // Remove any inputs up to and including the last input processed by the server
        inputListener.FramesToReconcile.RemoveAll(f => f.frameNumber < networkObject.frame);
            
        // Replay them all back to the last input processed by client prediction
        if (inputListener.FramesToReconcile.Count > 0)
        {
            for (int i = 0; i < inputListener.FramesToReconcile.Count; ++i)
            {
                inputFrame = inputListener.FramesToReconcile[i];
                PlayerUpdate(inputFrame);
            }
        }

        // The error vector measures the difference between the predicted & server updated sim position (this one)
        // and the view position (the position of the MonoBehavior holding your renderer/view)
        errorVector = rigidbody.position - view.transform.position;
        errorTimer = 0.0f;
    }

    private void CorrectError()
    {
        if (networkObject.IsServer)
        {
            view.transform.position = rigidbody.position;
            return;
        }

        Vector3 newPos = rigidbody.position;

        // If we have a measurable error
        if (errorVector.magnitude >= 0.00001f)
        {
            // Determine the weight, or amount we interpolate towards the simulation position
            float weight = Math.Max(0.0f, 0.75f - errorTimer);
               
            // Interpolate towards the simulation position
            newPos = view.transform.position * weight + rigidbody.position * (1.0f - weight);
               
            // Increase the timer - makes the weight smaller meaning more weight towards the simulation position
            // This is so that the bigger the error gets, or the longer it takes to smooth,
            // the more is smoothed away on the next frame
            errorTimer += Time.fixedDeltaTime;

            // New error vector, always the difference between sim and view
            errorVector = rigidbody.position - view.transform.position;

            // If the error is REALLY small we can discount the rest
            if (errorVector.magnitude < 0.00001f)
            {
                errorVector = Vector3.zero;
                errorTimer = 0.0f;
            }
        }

        view.transform.position = newPos;
    }

}
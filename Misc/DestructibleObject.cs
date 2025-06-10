using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;
using Photon.Realtime;

public class Destructible : MonoBehaviourPunCallbacks 
{
    [SerializeField] private GameObject destroyVFX;
    [SerializeField] private int health = 1; // Default is one hit to destroy
    
    private PhotonView pv;
    
    private void Awake()
    {
        pv = GetComponent<PhotonView>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.GetComponent<PlayerDamage>())
        {
            // Get damage amount from player (can be modified later)
            int damage = 1;
            int attackerViewID = 0;
            
            // If player has a PhotonView, get its ID
            PhotonView attackerPV = collision.gameObject.GetComponent<PhotonView>();
            if (attackerPV != null)
            {
                attackerViewID = attackerPV.ViewID;
            }
            
            if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom)
            {
                // Use the same pattern as enemy health - let Master Client validate
                pv.RPC("ApplyDamage", RpcTarget.MasterClient, damage, attackerViewID);
            }
            else
            {
                // Single-player mode
                DestroyLocally();
            }
        }
    }

    [PunRPC]
    private void ApplyDamage(int damage, int attackerViewID)
    {
        // Only Master Client can process damage
        if (!PhotonNetwork.IsMasterClient) return;

        health -= damage;
        bool willBeDestroyed = health <= 0;
        
        // Sync the damage effects to all clients
        pv.RPC("SyncDamageEffects", RpcTarget.All, health, attackerViewID, willBeDestroyed);
    }
    
    [PunRPC]
    private void SyncDamageEffects(int newHealth, int attackerViewID, bool shouldDestroy)
    {
        health = newHealth;
        
        if (shouldDestroy && PhotonNetwork.IsMasterClient)
        {
            // Show VFX on all clients
            pv.RPC("ShowDestroyVFX", RpcTarget.All);
            
            // Save position for DestructibleSpawner to track
            object[] content = new object[] { pv.ViewID, transform.position };
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(150, content, raiseEventOptions, SendOptions.SendReliable);
            
            // Master destroys the object on the network
            PhotonNetwork.Destroy(gameObject);
        }
    }

    [PunRPC]
    private void ShowDestroyVFX()
    {
        if (destroyVFX != null)
        {
            Instantiate(destroyVFX, transform.position, Quaternion.identity);
        }
    }

    private void DestroyLocally()
    {
        if (destroyVFX != null)
        {
            Instantiate(destroyVFX, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
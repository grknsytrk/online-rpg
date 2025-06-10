using UnityEngine;
using Photon.Pun;

public class BiomeTrigger : MonoBehaviour
{
    [Header("Biome Settings")]
    public string biomeName; 
    
    private void Awake()
    {
        
        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Sadece local player için müziği değiştir
            PhotonView playerPV = other.GetComponent<PhotonView>();
            if (playerPV != null && playerPV.IsMine)
            {
                if (BiomeMusicManager.Instance != null)
                {
                    BiomeMusicManager.Instance.ChangeBiomeMusic(biomeName);
                }
            }
        }
    }
}

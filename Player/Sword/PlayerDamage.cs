using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// Bu script muhtemelen Kılıç nesnesinin üzerinde olacak
public class PlayerDamage : MonoBehaviour
{
    // [SerializeField] private int baseDamageAmount; // Temel hasar miktarı - Kaldırıldı, PlayerStats kullanılacak
    private PhotonView playerPV;
    private PlayerStats playerStats; // PlayerStats referansı
    // private int currentDamage; // Kaldırıldı
    private bool initialized = false;

    private void Start()
    {
        // Kılıç başlangıçta aktif olmamalı (EquipmentManager kontrol ediyor)
        // gameObject.SetActive(false); // Bu satır EquipmentManager tarafından yönetilmeli
        Initialize();
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        // Find PlayerStats first, starting search from parent upwards
        // GetComponentInParent also checks the current object, so we need to be careful
        // Let's start search explicitly from parent
        Transform currentParent = transform.parent;
        while (currentParent != null)
        {
            playerStats = currentParent.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                // Found PlayerStats on an ancestor object
                break; 
            }
            currentParent = currentParent.parent; // Move up the hierarchy
        }

        if (playerStats == null)
        {
            Debug.LogError($"PlayerDamage: PlayerStats not found in parent hierarchy! Starting object: {gameObject.name}", this);
            enabled = false;
            return;
        }

        // Once PlayerStats is found, get the PhotonView from the *same* GameObject
        // This assumes PlayerStats and the main Player PhotonView are on the same object
        playerPV = playerStats.GetComponent<PhotonView>();

        if (playerPV == null)
        {
            // This should ideally not happen if the prefab is set up correctly
            Debug.LogError($"PlayerDamage: PhotonView not found on the same GameObject as PlayerStats ({playerStats.gameObject.name})! Check prefab setup. Trying GetComponentInParent as fallback.", this);
            // Fallback just in case, but log the error
            playerPV = playerStats.GetComponentInParent<PhotonView>(); 
             if (playerPV == null) {
                 Debug.LogError($"PlayerDamage: Fallback GetComponentInParent<PhotonView> also failed!", this);
                 enabled = false;
                 return;
             }
        }

        // Log the found Player PhotonView ID
        Debug.Log($"PlayerDamage Initialized: Found Player PhotonView ID: {playerPV.ViewID} on GameObject: {playerPV.gameObject.name}", this);

        initialized = true;
    }

    private void OnDestroy()
    {
        // Artık olay dinlemediğimiz için bu metoda gerek yok (veya boş bırakılabilir)
        /*
        if (playerPV != null && playerPV.IsMine && EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnItemEquipped -= OnEquipmentChanged;
            EquipmentManager.Instance.OnItemUnequipped -= OnEquipmentChanged;
        }
        */
    }

    // Bu metotlara artık gerek yok
    /*
    private void OnEquipmentChanged(SlotType slotType, InventoryItem item)
    {
        if (slotType == SlotType.Sword)
        {
            UpdateDamage();
        }
    }
    */

    /*
    private void UpdateDamage()
    {
        currentDamage = baseDamageAmount;
        if (EquipmentManager.Instance != null)
        {
            InventoryItem swordItem = EquipmentManager.Instance.GetEquippedItem(SlotType.Sword);
            if (swordItem != null)
            {
                currentDamage += swordItem.Damage;
            }
        }
        Debug.Log($"Hasar güncellendi: {currentDamage}");
    }
    */

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!initialized)
        {
            Initialize();
            if (!initialized) return; // Initialize başarısız olduysa çık
        }
        
        // playerPV ve playerStats null kontrolü (Initialize içinde yapılıyor ama ekstra güvenlik)
        if (playerPV == null || playerStats == null)
        {
             Debug.LogError("PlayerDamage.OnTriggerEnter2D: playerPV or playerStats is null after Initialize!");
            return;
        }

        // Sadece yerel oyuncunun kılıcı hasar vermeli
        if (!playerPV.IsMine) return;

        if (collision == null || collision.gameObject == null) return;

        // Düşmana Hasar
        EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>();
        if (enemyHealth != null) // EnemyAI yerine EnemyHealth kontrolü yeterli
        {
            int totalDamage = playerStats.TotalAttack; // Hasarı PlayerStats'tan al
             // --- ADDED DEBUG LOG ---
             Debug.Log($"PlayerDamage: Sending TakeDamage with AttackerViewID: {playerPV.ViewID} (from {playerPV.gameObject.name})");
             // -----------------------
            enemyHealth.TakeDamage(totalDamage, playerPV.ViewID);
            SFXManager.Instance?.PlaySound(SFXNames.PlayerDamageDealt); // Play damage dealt sound using constant
        }
        
        // Diğer Oyuncuya Hasar (PvP)
        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null && collision.gameObject.GetComponent<PlayerController>()) // PlayerController kontrolü kalsın
        {
            // PvP ayarını dışarıdan alabilirsin
            bool pvpEnabled = true; 
            
            if (pvpEnabled)
            {
                PhotonView targetPV = collision.gameObject.GetComponent<PhotonView>();
                if (targetPV != null && targetPV.ViewID != playerPV.ViewID) // Kendine vurma
                {
                    int totalDamage = playerStats.TotalAttack; // Hasarı PlayerStats'tan al
                     // --- ADDED DEBUG LOG ---
                     Debug.Log($"PlayerDamage (PvP): Sending TakeDamage with AttackerViewID: {playerPV.ViewID} (from {playerPV.gameObject.name}) to TargetViewID: {targetPV.ViewID}");
                     // -----------------------
                    // TakeDamage method'unu çağır, o RPC'yi gönderecek
                    playerHealth.TakeDamage(totalDamage, playerPV.ViewID);
                    SFXManager.Instance?.PlaySound(SFXNames.PlayerDamageDealt); // Play damage dealt sound (PvP) using constant
                }
            }
        }
    }
}

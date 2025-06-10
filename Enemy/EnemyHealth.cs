using UnityEngine;
using Photon.Pun;
using System; // Action için eklendi
using System.Collections.Generic; // Required for List
using System.Linq; // LINQ kullanmak için eklendi

public class EnemyHealth : MonoBehaviourPunCallbacks
{
    public event Action<int> OnDamagedByPlayer; // Hasar aldığında tetiklenecek event

    [Header("Health Settings")]
    [SerializeField] private int startingHealth = 30;
    [SerializeField] private GameObject deathVFXPrefab;
    [SerializeField] private float knockbackTakenPower = 15f; // Oyuncudan alınan knockback gücü
    [SerializeField] private int xpValue = 10; // Öldürüldüğünde verilecek XP miktarı

    [Header("Elite Settings")] // YENİ BAŞLIK
    [SerializeField] private int eliteHealthMultiplier = 3;
    [SerializeField] private int eliteXpMultiplier = 2;

    [Header("Effects")] // Added Header for VFX
    [SerializeField] private GameObject spawnVFXPrefab; // VFX to play on spawn
    [SerializeField] private Vector3 spawnVFXOffset = Vector3.zero; // Offset for the spawn VFX

    [Header("Loot Settings")] // Added Header
    [SerializeField] private LootTable lootTable; // Added LootTable reference
    [SerializeField] private GameObject lootItemPrefab; // Added LootItem prefab reference
    [SerializeField] private int rareItemWeightThreshold = 2; // YENİ: Nadir eşya sayılacak maksimum ağırlık

    private int currentHealth;
    private Knockback knockback;
    private Flash flash;
    private PhotonView pv;
    private Transform localPlayerTransform;

    private int baseMaxHealth; // YENİ EKLENDİ - Orijinal Max Health
    private int baseXpValue;   // YENİ EKLENDİ - Orijinal XP Değeri
    public bool IsElite { get; private set; } = false; // YENİ EKLENDİ

    private void Awake()
    {
        flash = GetComponent<Flash>();
        knockback = GetComponent<Knockback>();
        pv = GetComponent<PhotonView>();
        
        // Başlangıç canını direkt Awake'de ayarla
        currentHealth = startingHealth;

        // Store base values before they are potentially modified
        baseMaxHealth = startingHealth; 
        baseXpValue = xpValue;

        // If this enemy is instantiated by MasterClient and is not ours, health might be synced later
        // For locally controlled or MasterClient entities, health is set here.
        // Non-master client remote entities will get health updates via RPC_SyncHealth if needed.
    }

    private void Start() // Added Start method for spawn VFX
    {
        if (spawnVFXPrefab != null)
        {
            Instantiate(spawnVFXPrefab, transform.position + spawnVFXOffset, Quaternion.identity);
        }
    }

    public void TakeDamage(int damage, int attackerViewID)
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("Cannot deal damage when not connected to network!");
            return;
        }
        
        Debug.Log($"Current health before damage: {currentHealth}");
        pv.RPC("ApplyDamage", RpcTarget.MasterClient, damage, attackerViewID);
    }

    [PunRPC]
    private void ApplyDamage(int damage, int attackerViewID)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int oldHealth = currentHealth;
        currentHealth -= damage;
        bool willDie = currentHealth <= 0;
        
        // =============== DAMAGE NUMBER - SADECE SALDIRAN OYUNCUYA ===============
        // Hasarı veren oyuncuya damage number göster
        PhotonView attackerPV = PhotonView.Find(attackerViewID);
        if (attackerPV != null && attackerPV.Owner != null)
        {
            // Sadece saldıran oyuncuya damage number RPC'si gönder
            pv.RPC("ShowDamageToAttacker", attackerPV.Owner, damage);
        }
        // ====================================================================
        
        // Tüm clientlarda health, flash ve knockback efektlerini senkronize et
        pv.RPC("SyncDamageEffects", RpcTarget.All, currentHealth, attackerViewID, willDie);
    }

    [PunRPC]
    private void ShowDamageToAttacker(int damageAmount)
    {
        // Bu RPC sadece hasarı veren oyuncuda çalışır
        if (DamageNumberManager.Instance != null && damageAmount > 0)
        {
            Vector3 enemyPosition = transform.position + Vector3.up * 0.5f; // Düşmanın biraz üstünde
            DamageNumberManager.Instance.ShowDamageNumber(enemyPosition, damageAmount, Color.red);
            Debug.Log($"Damage number shown (LOCAL): {damageAmount} at position {enemyPosition}");
        }
    }

    [PunRPC]
    private void SyncDamageEffects(int newHealth, int attackerViewID, bool shouldDie)
    {
        currentHealth = newHealth;

        // Hasar olayını burada tetikle (sadece yaşayanlar için mantıklı olabilir, veya her zaman?)
        // Master Client AI kontrolü EnemyAI içinde yapılacak.
        OnDamagedByPlayer?.Invoke(attackerViewID);
        
        // Ölüm ve XP verme mantığı (Sadece Master Client kontrol etmeli)
        if (shouldDie)
        {
            if (PhotonNetwork.IsMasterClient) // Sadece Master Client XP verir, loot düşürür ve objeyi yok eder
            {
                 // Katleden oyuncuya XP ver
                 PhotonView attackerPV = PhotonView.Find(attackerViewID);
                 if (attackerPV != null && attackerPV.Owner != null) // Sahibi var mı kontrolü
                 {
                     // --- DEBUG LOGS START ---
                     Debug.Log($"Attempting to send RPC_AddXP to ViewID: {attackerPV.ViewID}, Owner: {attackerPV.Owner?.NickName}, GameObject: {attackerPV.gameObject.name}");
                     PlayerStats stats = attackerPV.GetComponent<PlayerStats>();
                     if (stats == null) {
                         // Hata logunu daha detaylı hale getirelim
                         Debug.LogError($"PlayerStats component NOT FOUND on GameObject '{attackerPV.gameObject.name}' (ViewID: {attackerPV.ViewID}). Check if PlayerStats script is on the same GameObject as the PhotonView.");
                         // Opsiyonel: Parent objede mi diye kontrol et (eğer yapı farklıysa)
                         PlayerStats statsInParent = attackerPV.GetComponentInParent<PlayerStats>();
                         if (statsInParent != null) {
                             Debug.LogWarning($"PlayerStats found in PARENT of {attackerPV.gameObject.name}. It should ideally be on the SAME object.");
                         }
                         // Opsiyonel: Child objelerde mi diye kontrol et (eğer yapı farklıysa)
                         PlayerStats statsInChildren = attackerPV.GetComponentInChildren<PlayerStats>();
                         if (statsInChildren != null) {
                             Debug.LogWarning($"PlayerStats found in CHILDREN of {attackerPV.gameObject.name}. This might cause RPC issues if not configured correctly.");
                         }
                     } else {
                          Debug.Log($"PlayerStats component FOUND on GameObject {attackerPV.gameObject.name} with ViewID {attackerPV.ViewID}. Sending RPC...");
                     }
                     // --- DEBUG LOGS END ---

                     // PlayerStats componentine RPC gönder, hedef Owner
                     attackerPV.RPC("RPC_AddXP", attackerPV.Owner, xpValue);
                     // Log mesajını Owner kontrolü olmadan önceye alalım ki MasterClient'ta görünsün
                     Debug.Log($"MasterClient: Sent RPC_AddXP to player {attackerViewID} ({attackerPV.Owner.NickName}) for {xpValue} XP.");
                 }
                 else
                 {
                      Debug.LogWarning($"XP verilecek oyuncu bulunamadı veya sahibi yok! AttackerViewID: {attackerViewID}");
                 }

                // --- LOOT DROP LOGIC --- Added Section
                if (lootTable != null && lootItemPrefab != null)
                {
                    // PREFAB YOLU: Bu yolun Assets/Resources klasörüne göre doğru olduğundan emin olun!
                    string prefabPath = "Items/Loots/LootItem"; 

                    // Yeni GetRandomDrop metodunu çağır
                    InventoryItem droppedItem = lootTable.GetRandomDrop();

                    // Eğer bir item düştüyse (null değilse)
                    if (droppedItem != null)
                    {
                        ItemData itemToDrop = ItemDatabase.Instance.GetItemById(droppedItem.ItemId); // ItemDatabase'den al
                        int quantity = droppedItem.Amount; // Miktarı al (gerekirse LootItem bunu kullanabilir)
                        
                        // ItemData null kontrolü ekle
                        if (itemToDrop == null)
                        {
                            Debug.LogError($"EnemyHealth: ItemDatabase'de item bulunamadı! ID: {droppedItem.ItemId}");
                            // Null item ile devam etme
                        }
                        else
                        {
                            Debug.Log($"Enemy dropping {quantity} x {itemToDrop.ItemName} (ID: {itemToDrop.ItemId})");

                            // Spawn the loot item prefab via RPC
                            Vector3 spawnPosition = transform.position + (Vector3)UnityEngine.Random.insideUnitCircle * 0.5f; // Add a slight random offset

                            // Instantiate the loot item ONLY on the MasterClient using PhotonNetwork
                            GameObject lootGO = PhotonNetwork.Instantiate(prefabPath, spawnPosition, Quaternion.identity);

                            if (lootGO == null)
                            {
                                Debug.LogError($"MasterClient: PhotonNetwork.Instantiate failed for loot prefab path: {prefabPath}. Item: {itemToDrop.itemName}");
                                // Hata durumunda devam etmeyebiliriz veya loglayıp geçebiliriz.
                            }
                            else
                            {
                                Debug.Log($"MasterClient: Instantiated loot item {itemToDrop.itemName} (ID: {itemToDrop.ItemId}) at {spawnPosition}. GameObject: {lootGO.name}");

                                // Send an RPC to all clients (buffered) to initialize the item data on the spawned object
                                PhotonView lootPV = lootGO.GetComponent<PhotonView>();
                                if (lootPV != null)
                                {
                                    // RPC_InitializeLoot'un artık (string itemId, int quantity) aldığını varsayıyoruz.
                                    lootPV.RPC("RPC_InitializeLoot", RpcTarget.AllBuffered, itemToDrop.ItemId, quantity); // Miktar (quantity) eklendi

                                    // Nadirlik kontrolü ve ses efekti
                                    LootDrop originalLootDropEntry = lootTable.possibleDrops.FirstOrDefault(ld => ld.itemData != null && ld.itemData.ItemId == droppedItem.ItemId);
                                    if (originalLootDropEntry != null)
                                    {
                                        Debug.Log($"Found original loot drop entry for {droppedItem.ItemId}. Weight: {originalLootDropEntry.weight}");
                                        if (originalLootDropEntry.weight > 0 && originalLootDropEntry.weight <= rareItemWeightThreshold)
                                        {
                                            // SFXManager'ın MasterClient'ta da erişilebilir olduğunu varsayıyoruz,
                                            // Veya bu RPC'yi tüm client'lara gönderip orada çalmasını sağlayabiliriz.
                                            // Şimdilik MasterClient üzerinden çalmayı deneyelim.
                                            // Eğer sesin tüm clientlarda duyulması gerekiyorsa, bu RPC_InitializeLoot gibi bir RPC içine alınabilir
                                            // ya da lootPV üzerinden yeni bir RPC ile tetiklenebilir.
                                            // En basit haliyle, eğer SFXManager DontDestroyOnLoad ise MasterClient'ta çalınan ses
                                            // diğer clientlar için bir anlam ifade etmez.
                                            // Bu sesi loot objesinin kendisi üzerinden bir RPC ile tüm client'lara duyurmak daha doğru olabilir.
                                            // Şimdilik debug için MasterClient'a loglayalım ve sesi çalmayı deneyelim.
                                            // Bu sesin hangi client'ta çalması gerektiği önemli bir tasarım kararı.
                                            // Eğer sadece loot'u alan kişi duyacaksa farklı, herkes duyacaksa farklı.
                                            // Biz herkesin duyması senaryosunu varsayarak, loot objesine bir RPC ekleyebiliriz veya
                                            // SFXManager'a tüm clientlarda ses çalacak bir RPC ekleyebiliriz.
                                            // Şimdilik SFXManager.Instance.PlaySound() çağrısını bırakalım,
                                            // bunun MasterClient'taki SFXManager'ı tetikleyeceğini ve sadece orada duyulacağını unutmayalım.
                                            // Daha sonra bunu tüm client'lara yaygınlaştırabiliriz.
                                            SFXManager.Instance?.PlaySound(SFXNames.RareItemDrop);
                                            Debug.Log($"Played RARE ITEM DROP sound ({SFXNames.RareItemDrop}) for item {droppedItem.ItemId} with weight {originalLootDropEntry.weight}");
                                            
                                            // Nadir item için sistem mesajı gönder
                                            ChatManager chatManager = FindObjectOfType<ChatManager>();
                                            if (chatManager != null)
                                            {
                                                // Eşyayı bulan oyuncunun ismini al
                                                PhotonView killerPV = PhotonView.Find(attackerViewID);
                                                string killerName = killerPV?.Owner?.NickName ?? "Bilinmeyen Oyuncu";
                                                
                                                // Düşmanın ismini al (gameObject'in ismini temizle)
                                                string enemyName = gameObject.name.Replace("(Clone)", "").Trim();
                                                if (string.IsNullOrEmpty(enemyName)) enemyName = "Düşman";
                                                
                                                string rareItemMessage = MessageColorUtils.BuildRareItemMessage(killerName, enemyName, itemToDrop.ItemName);
                                                chatManager.SendSystemMessage(rareItemMessage, SystemMessageType.RareItem);
                                                Debug.Log($"Rare Item Message: {rareItemMessage}");
                                            }
                                            else
                                            {
                                                Debug.LogWarning("ChatManager bulunamadı, nadir item mesajı gönderilemedi.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"Could not find original loot drop entry for {droppedItem.ItemId} in loot table to check weight.");
                                    }
                                }
                                else {
                                    Debug.LogError($"MasterClient: Instantiated loot item '{lootGO.name}' is missing a PhotonView component! Initialization RPC cannot be sent.");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("Enemy dropped no loot this time.");
                    }
                }
                else
                {
                    if (lootTable == null) Debug.LogWarning("LootTable is not assigned to the enemy.");
                    if (lootItemPrefab == null) Debug.LogWarning("LootItemPrefab is not assigned to the enemy.");
                }
                // --- END OF LOOT DROP LOGIC ---

                // Ölüm efektini tetikle
                if (deathVFXPrefab != null)
                {
                    pv.RPC("SpawnDeathVFX", RpcTarget.All);
                }
                // Objeyi yok et
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else // Ölmediyse normal efektleri uygula
        {
            StartCoroutine(flash.FlashRoutine());
            PhotonView attackerPV = PhotonView.Find(attackerViewID);
            if (attackerPV != null)
            {
                localPlayerTransform = attackerPV.transform;
                knockback.GetKnockbacked(localPlayerTransform, knockbackTakenPower);
            }
            SFXManager.Instance?.PlaySound(SFXNames.EnemyDamageTaken); // Play enemy damage taken sound using constant
        }
    }

    [PunRPC]
    private void SpawnDeathVFX()
    {
        if (deathVFXPrefab != null)
        {
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        // Obje aktif olduğunda canı yenile
        if (PhotonNetwork.IsMasterClient)
        {
            currentHealth = startingHealth;
        }
    }

    public bool DetectDeath()
    {
        return currentHealth <= 0;
    }

    // YENİ EKLENEN METOT
    public void SetEliteStatus(bool isElite)
    {
        this.IsElite = isElite;
        if (this.IsElite)
        {
            startingHealth = baseMaxHealth * eliteHealthMultiplier;
            xpValue = baseXpValue * eliteXpMultiplier; 
            Debug.Log($"Enemy {gameObject.name} (ViewID: {pv.ViewID}) SetEliteStatus(true): MaxHealth={startingHealth}, Xp={xpValue}");
        }
        else
        {
            // This case might not be used if elites are always elite, but good for completeness
            startingHealth = baseMaxHealth;
            xpValue = baseXpValue;
            Debug.Log($"Enemy {gameObject.name} (ViewID: {pv.ViewID}) SetEliteStatus(false): MaxHealth={startingHealth}, Xp={xpValue}");
        }
        // Current health should be set to the new max health, or capped if it was already damaged
        // For simplicity on spawn, we set it to the new max health.
        // If an enemy could become elite mid-game, this logic would need to be more complex.
        currentHealth = startingHealth; 

        // TODO: If a health bar UI is tied to this enemy, update its max value here.
        // Example: if (enemyHealthBar != null) enemyHealthBar.SetMaxHealth(startingHealth);
        // UpdateHealthBar(); // If such a method exists
    }
}
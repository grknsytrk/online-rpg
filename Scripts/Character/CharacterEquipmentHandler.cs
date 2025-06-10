using UnityEngine;
using Photon.Pun;

public class CharacterEquipmentHandler : MonoBehaviour
{
    [Header("Equipment References")]
    [SerializeField] private GameObject swordObject; // Active Weapon/Sword objesi
    // Diğer görünür ekipmanlar için referanslar eklenebilir
    
    private PlayerController playerController;
    private PhotonView photonView;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        photonView = GetComponent<PhotonView>();
    }
    
    private void Start()
    {
        // Karakter oyuna girdiğinde, EquipmentManager'dan mevcut durumu kontrol et
        if (EquipmentManager.Instance != null && playerController != null && photonView.IsMine)
        {
            // Özellikle kılıç için durumu kontrol et
            var swordItem = EquipmentManager.Instance.GetEquippedItem(SlotType.Sword);
            if (swordObject != null)
            {
                bool hasSword = swordItem != null;
                swordObject.SetActive(hasSword);
                Debug.Log($"Karakter başlangıçta kılıç durumu güncellendi: {hasSword}");
                
                // Diğer oyunculara başlangıç durumunu bildir
                if (photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                {
                    photonView.RPC("SyncEquipmentState", RpcTarget.AllBuffered, (int)SlotType.Sword, hasSword);
                }
            }
            
            // Diğer ekipman parçaları için de benzer şekilde kontrol et
            CheckAllEquipment();
        }
    }
    
    private void CheckAllEquipment()
    {
        if (EquipmentManager.Instance == null) return;
        
        // Tüm ekipman tiplerini kontrol et ve görsel durumu güncelle
        foreach (SlotType slotType in System.Enum.GetValues(typeof(SlotType)))
        {
            var item = EquipmentManager.Instance.GetEquippedItem(slotType);
            UpdateEquipment(slotType, item);
        }
        
        Debug.Log("Tüm ekipman parçaları için görsel durum güncellendi");
    }
    
    /// <summary>
    /// Ekipman değişikliklerinde çağrılır ve görsel güncellemeleri yapar
    /// </summary>
    public void UpdateEquipment(SlotType slotType, InventoryItem item)
    {
        switch (slotType)
        {
            case SlotType.Sword:
                // Kılıç için görsel güncelleme
                if (swordObject != null)
                {
                    bool shouldShow = item != null;
                    swordObject.SetActive(shouldShow);
                    Debug.Log($"Kılıç görünürlüğü güncellendi: {shouldShow}, Item: {(item != null ? item.ItemName : "Boş")}");
                    
                    // Diğer oyunculara kılıç durumunu bildir
                    if (photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                    {
                        photonView.RPC("SyncEquipmentState", RpcTarget.AllBuffered, (int)slotType, shouldShow);
                    }
                    
                    // Burada kılıcın modelini/görünümünü değiştirebilirsiniz
                    // if (item != null && item.ItemPrefab != null) { ... }
                }
                else
                {
                    Debug.LogWarning("Kılıç objesi atanmamış! CharacterEquipmentHandler'a swordObject atayın.");
                }
                break;

            // Diğer ekipman tipleri için sadece stat bonusları uygulanacak
            case SlotType.Helmet:
            case SlotType.Chestplate:
            case SlotType.Leggings:
            case SlotType.Boots:
            case SlotType.Ring:
            case SlotType.Necklace:
                // Burada ilgili ekipman parçalarının görünümünü güncelleyebilirsiniz
                Debug.Log($"{slotType} ekipmanı güncellendi. Item: {(item != null ? item.ItemName : "Boş")}");
                
                // Stat bonuslarını uygula
                ApplyStatBonuses(slotType, item);
                break;
            
            default:
                Debug.Log($"Bilinmeyen ekipman tipi: {slotType}");
                break;
        }
    }
    
    [PunRPC]
    private void SyncEquipmentState(int slotTypeInt, bool isEquipped)
    {
        // Diğer oyunculardan gelen ekipman durumu güncellemesi
        SlotType slotType = (SlotType)slotTypeInt;
        
        Debug.Log($"Diğer oyuncudan ekipman durumu alındı: {slotType}, Takılı: {isEquipped}");
        
        switch(slotType)
        {
            case SlotType.Sword:
                if (swordObject != null)
                {
                    swordObject.SetActive(isEquipped);
                    Debug.Log($"Diğer oyuncunun kılıcı güncellendi: {isEquipped}");
                    
                    // Sword bileşeni içindeki silah collider'ını da güncelle
                    Sword swordComponent = swordObject.GetComponent<Sword>();
                    if (swordComponent != null && swordComponent.activeWeapon != null)
                    {
                        // Aktif silahı da görünür veya görünmez yap
                        Transform weaponCollider = null;
                        
                        // GetComponentInChildren ile weaponCollider'ı bulabiliriz
                        var allChildren = swordObject.GetComponentsInChildren<Transform>(true);
                        foreach (var child in allChildren)
                        {
                            if (child.name.Contains("Collider"))
                            {
                                weaponCollider = child;
                                break;
                            }
                        }
                        
                        if (weaponCollider != null)
                        {
                            //weaponCollider.gameObject.SetActive(isEquipped);
                        }
                    }
                }
                break;
                
            // Diğer ekipman tipleri için de benzer işlemleri ekleyebilirsiniz
        }
    }
    
    /// <summary>
    /// Ekipman değişikliğinde karakter statlarını günceller
    /// </summary>
    private void ApplyStatBonuses(SlotType slotType, InventoryItem item)
    {
        // Hasar değerini güncelle
        if (slotType == SlotType.Sword)
        {
            var damageComponent = GetComponentInChildren<PlayerDamage>(true);
            if (damageComponent != null)
            {
                // PlayerDamage'deki OnEquipmentChanged zaten çağrılıyor, ama ek güvence olarak
                // burada direkt bir method çağırabilirsiniz
                Debug.Log($"Karakter ekipman bonusu uygulandı: {slotType}, Item: {(item != null ? item.ItemName + $", Hasar: {item.Damage}" : "Boş")}");
            }
            else
            {
                Debug.LogWarning("PlayerDamage bileşeni bulunamadı, hasar güncellemesi yapılamadı!");
            }
        }
        
        // Savunma değerini güncelle - İleride PlayerStats eklendiğinde burayı genişletebilirsiniz
        if (slotType == SlotType.Helmet || slotType == SlotType.Chestplate || 
            slotType == SlotType.Leggings || slotType == SlotType.Boots)
        {
            Debug.Log($"Savunma ekipmanı uygulandı: {slotType}, Item: {(item != null ? item.ItemName + $", Savunma: {item.Defense}" : "Boş")}");
            
            // İleride PlayerStats eklendiğinde:
            // var statsComponent = GetComponent<PlayerStats>();
            // if (statsComponent != null) statsComponent.UpdateDefense();
        }
        
        // Aksesuar bonuslarını uygula
        if (slotType == SlotType.Ring || slotType == SlotType.Necklace)
        {
            Debug.Log($"Aksesuar bonusu uygulandı: {slotType}, Item: {(item != null ? item.ItemName : "Boş")}");
            
            // İleride özel bonuslar eklenebilir
        }
    }
} 
using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Firebase.Database;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using Newtonsoft.Json.Linq;

public class EquipmentManager : MonoBehaviourPunCallbacks
{
    #region Singleton

    public static EquipmentManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Birden fazla EquipmentManager örneği bulundu. Son oluşturulan yok edildi.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeEquipmentSlots();
    }

    #endregion

    #region Fields

    [Header("Equipment Slots")]
    [SerializeField] private InventorySlotUI swordSlot;
    [SerializeField] private InventorySlotUI helmetSlot;
    [SerializeField] private InventorySlotUI chestplateSlot;
    [SerializeField] private InventorySlotUI leggingsSlot;
    [SerializeField] private InventorySlotUI bootsSlot;
    [SerializeField] private InventorySlotUI ringSlot;
    [SerializeField] private InventorySlotUI necklaceSlot;
    [SerializeField] private InventorySlotUI potionSlot;

    private Dictionary<SlotType, InventoryItem> _equippedItems = new Dictionary<SlotType, InventoryItem>();
    private Dictionary<SlotType, InventorySlotUI> _equipmentSlots = new Dictionary<SlotType, InventorySlotUI>();
    
    private string _userId;
    private DatabaseReference _databaseRef;
    private bool _isInitialized = false;

    #endregion

    #region Events

    public event Action<SlotType, InventoryItem> OnItemEquipped;
    public event Action<SlotType, InventoryItem> OnItemUnequipped;

    #endregion

    #region Initialization

    private async void Start()
    {
        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnAuthStateChanged += OnAuthChanged;
            
            // Eğer kullanıcı zaten giriş yapmışsa, kullanıcı bilgilerini hemen ayarla
            if (FirebaseAuthManager.Instance.IsLoggedIn && FirebaseAuthManager.Instance.CurrentUser != null)
            {
                _userId = FirebaseAuthManager.Instance.UserId;
                _databaseRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{_userId}/equipment");
            }
        }
        
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // Önce yerel depolamadan yüklemeyi dene
            bool loadedFromLocal = LoadEquipmentFromLocalStorage();
            
            // Eğer kullanıcı giriş yapmışsa Firebase'den yükle
            if (_userId != null && _databaseRef != null)
            {
                await LoadEquipmentFromFirebase();
            }
            else
            {
                Debug.Log("Firebase kullanıcısı henüz giriş yapmadı, sadece yerel veriler kullanılıyor.");
            }
        }
    }

    private void InitializeEquipmentSlots()
    {
        // Slot referanslarını sözlüğe ekle
        if (swordSlot != null) _equipmentSlots[SlotType.Sword] = swordSlot;
        if (helmetSlot != null) _equipmentSlots[SlotType.Helmet] = helmetSlot;
        if (chestplateSlot != null) _equipmentSlots[SlotType.Chestplate] = chestplateSlot;
        if (leggingsSlot != null) _equipmentSlots[SlotType.Leggings] = leggingsSlot;
        if (bootsSlot != null) _equipmentSlots[SlotType.Boots] = bootsSlot;
        if (ringSlot != null) _equipmentSlots[SlotType.Ring] = ringSlot;
        if (necklaceSlot != null) _equipmentSlots[SlotType.Necklace] = necklaceSlot;
        if (potionSlot != null) _equipmentSlots[SlotType.Potion] = potionSlot;

        // Tüm slotları temizle
        foreach (var slot in _equipmentSlots.Values)
        {
            if (slot != null)
            {
                slot.UpdateUI(null);
            }
        }
        
        _isInitialized = true;
    }

    private async void OnAuthChanged(Firebase.Auth.FirebaseUser user)
    {
        if (user != null)
        {
            Debug.Log($"EquipmentManager: Firebase kullanıcısı oturum açtı. UserId: {user.UserId}");
            _userId = user.UserId;
            _databaseRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{_userId}/equipment");
            
            // Photon bağlantısı varsa ekipmanları yükle
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            {
                await LoadEquipmentFromFirebase();
            }
        }
        else
        {
            Debug.Log("EquipmentManager: Firebase kullanıcısı oturumu kapattı.");
            _userId = null;
            _databaseRef = null;
            ClearAllEquipment();
        }
    }

    private void ClearAllEquipment()
    {
        foreach (var slotType in _equippedItems.Keys.ToArray())
        {
            RemoveEquipment(slotType, false);
        }
        
        _equippedItems.Clear();
        UpdateAllSlots();
    }

    #endregion

    #region Equipment Management

    public bool EquipItem(InventoryItem item, SlotType? forceSlotType = null, bool saveToFirebase = true)
    {
        if (item == null)
        {
            Debug.LogError("EquipItem'a null item gönderildi!");
            return false;
        }

        // Slot tipini belirle
        SlotType slotType = forceSlotType ?? item.ItemType;
        
        // Slot geçerli mi kontrol et
        if (slotType == SlotType.None)
        {
            Debug.LogWarning($"Bu item ({item.ItemName}) ekipman slotuna yerleştirilemez!");
            return false;
        }
        
        // Slotu bul
        if (!_equipmentSlots.TryGetValue(slotType, out InventorySlotUI slot) || slot == null)
        {
            Debug.LogWarning($"Ekipman slotu bulunamadı: {slotType}");
            return false;
        }
        
        // Zaten ekipman varsa ve aynı item ise, bir şey yapma
        if (_equippedItems.TryGetValue(slotType, out InventoryItem currentItem) && 
            currentItem != null && currentItem.ItemId == item.ItemId)
        {
            Debug.Log($"Bu item zaten ekipman olarak takılı: {item.ItemName}");
            return true;
        }
        
        // Önce eski ekipmanı çıkar
        if (currentItem != null)
        {
            RemoveEquipment(slotType, false);
        }
        
        // Yeni ekipmanı ekle
        _equippedItems[slotType] = item;
        
        // UI'ı güncelle
        slot.UpdateUI(item);
        
        // Ekipman değişikliği olayını tetikle
        OnItemEquipped?.Invoke(slotType, item);
        
        // Karakterin ekipmanını güncelle
        UpdateCharacterEquipment(slotType, item);
        
        // Firebase'e kaydet
        if (saveToFirebase && _userId != null)
        {
            SaveEquipmentToFirebase();
        }
        
        Debug.Log($"Item ekipman olarak giyildi: {item.ItemName}, Slot: {slotType}");
        return true;
    }

    public void UnequipItem(SlotType slotType, bool addToInventory = true)
    {
        if (!_equipmentSlots.ContainsKey(slotType))
        {
            Debug.LogWarning($"Geçersiz ekipman slotu: {slotType}");
            return;
        }
        
        // Ekipman varsa çıkar
        if (_equippedItems.TryGetValue(slotType, out InventoryItem item) && item != null)
        {
            // Envantere ekle
            if (addToInventory && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(item);
            }
            
            RemoveEquipment(slotType);
        }
    }

    public void RemoveEquipment(SlotType slotType, bool saveToFirebase = true)
    {
        if (!_equipmentSlots.TryGetValue(slotType, out InventorySlotUI slot))
        {
            return;
        }
        
        // Ekipmanı al
        if (_equippedItems.TryGetValue(slotType, out InventoryItem item))
        {
            // Ekipmanı sözlükten çıkar
            _equippedItems.Remove(slotType);
            
            // UI'ı güncelle
            slot.UpdateUI(null);
            
            // Ekipman değişikliği olayını tetikle
            OnItemUnequipped?.Invoke(slotType, item);
            
            // Karakterin ekipmanını güncelle
            UpdateCharacterEquipment(slotType, null);
            
            // Firebase'den sil
            if (saveToFirebase && _userId != null)
            {
                RemoveEquipmentFromFirebase(slotType);
            }
            
            Debug.Log($"Item ekipmandan çıkarıldı: {item.ItemName}, Slot: {slotType}");
        }
    }

    private void RemoveEquipmentFromFirebase(SlotType slotType)
    {
        if (_userId == null || _databaseRef == null)
        {
            return;
        }
        
        string slotKey = slotType.ToString().ToLower();
        _databaseRef.Child(slotKey).RemoveValueAsync().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"Ekipman Firebase'den silinirken hata oluştu: {task.Exception}");
            }
        });
    }

    public void UpdateCharacterEquipment(SlotType slotType, InventoryItem item)
    {
        // Yerel oyuncu için karakter ekipmanını güncelle
        var player = FindLocalPlayer();
        if (player != null)
        {
            var equipHandler = player.GetComponent<CharacterEquipmentHandler>();
            if (equipHandler != null)
            {
                equipHandler.UpdateEquipment(slotType, item);
            }
        }
    }

    private PlayerController FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            if (player.PV.IsMine)
                return player;
        }
        return null;
    }

    #endregion

    #region Data Persistence

    private void SaveEquipmentToFirebase()
    {
        if (_userId == null || _databaseRef == null)
        {
            Debug.LogWarning("Ekipman kaydedilemedi: Firebase kullanıcısı bulunamadı.");
            return;
        }
        
        // Ekipman verilerini hazırla
        var equipmentData = new Dictionary<string, object>();
        
        foreach (var pair in _equippedItems)
        {
            SlotType slotType = pair.Key;
            InventoryItem item = pair.Value;
            
            if (item == null) continue;
            
            string slotKey = slotType.ToString().ToLower();
            
            // Firebase için basit bir Dictionary oluştur
            var itemData = new Dictionary<string, object>
            {
                { "itemId", item.ItemId },
                { "amount", item.Amount },
                { "slotType", (int)slotType }
            };
            
            // Dictionary'yi doğrudan Firebase'e gönder
            equipmentData[slotKey] = itemData;
        }
        
        // Firebase'e kaydet
        _databaseRef.UpdateChildrenAsync(equipmentData).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"Ekipman Firebase'e kaydedilirken hata oluştu: {task.Exception}");
            }
            else
            {
                Debug.Log("Ekipman Firebase'e kaydedildi.");
            }
        });
    }

    /// <summary>
    /// Manually saves equipment data to Firebase
    /// </summary>
    public void SaveEquipmentManual()
    {
        SaveEquipmentToFirebase();
    }

    public async Task LoadEquipmentFromFirebase()
    {
        if (_userId == null || _databaseRef == null)
        {
            Debug.LogWarning("Ekipman yüklenemedi: Firebase kullanıcısı bulunamadı.");
            return;
        }
        
        try
        {
            // Önce mevcut ekipmanları temizle
            ClearAllEquipment();
            
            // Firebase'den verileri al
            var snapshot = await _databaseRef.GetValueAsync();
            
            if (!snapshot.Exists)
            {
                Debug.Log("Firebase'de ekipman verisi bulunamadı.");
                return;
            }
            
            // Her bir slotu işle
            foreach (var child in snapshot.Children)
            {
                try
                {
                    string slotKey = child.Key;
                    
                    // Firebase'den gelen veriyi kontrol et
                    if (!child.Exists || !child.HasChildren)
                    {
                        continue;
                    }
                    
                    // itemId ve slotType değerlerini doğrudan çocuk düğümlerden al
                    string itemId = child.Child("itemId").Value?.ToString();
                    
                    if (string.IsNullOrEmpty(itemId))
                    {
                        Debug.LogWarning($"Geçersiz itemId: {slotKey}");
                        continue;
                    }
                    
                    // slotType değerini al ve dönüştür
                    SlotType slotType = SlotType.None;
                    var slotTypeNode = child.Child("slotType");
                    
                    if (slotTypeNode.Exists)
                    {
                        if (int.TryParse(slotTypeNode.Value?.ToString(), out int slotTypeInt))
                        {
                            slotType = (SlotType)slotTypeInt;
                        }
                    }
                    
                    // Item verisini al
                    ItemData itemDataObj = ItemDatabase.Instance.GetItemById(itemId);
                    
                    if (itemDataObj == null)
                    {
                        Debug.LogWarning($"Item veritabanında bulunamadı: {itemId}");
                        continue;
                    }
                    
                    // ----- MİKTARI OKU -----
                    int amount = 1; // Varsayılan 1
                    var amountNode = child.Child("amount");
                    if (amountNode.Exists && int.TryParse(amountNode.Value?.ToString(), out int parsedAmount))
                    {
                        amount = parsedAmount;
                    }
                    // ------------------------

                    // Item oluştur ve ekipman olarak giy
                    InventoryItem item = new InventoryItem(itemDataObj, amount); // Miktarı constructor'a gönder
                    EquipItem(item, slotType, false); // saveToFirebase = false çünkü zaten yüklüyoruz
                    
                    Debug.Log($"Firebase'den ekipman yüklendi: {itemDataObj.itemName} (x{amount}), Slot: {slotType}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Ekipman verisi ayrıştırılırken hata: {ex.Message}");
                }
            }
            
            Debug.Log("Ekipman Firebase'den yüklendi.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Ekipman yüklenirken hata oluştu: {ex.Message}");
        }
    }

    #endregion

    #region Utility Methods

    public Dictionary<SlotType, InventoryItem> GetEquippedItems()
    {
        return new Dictionary<SlotType, InventoryItem>(_equippedItems);
    }

    public InventoryItem GetEquippedItem(SlotType slotType)
    {
        if (_equippedItems.TryGetValue(slotType, out InventoryItem item))
        {
            return item;
        }
        return null;
    }

    private void UpdateAllSlots()
    {
        foreach (var pair in _equipmentSlots)
        {
            SlotType slotType = pair.Key;
            InventorySlotUI slot = pair.Value;
            
            if (slot != null)
            {
                if (_equippedItems.TryGetValue(slotType, out InventoryItem item))
                {
                    slot.UpdateUI(item);
                }
                else
                {
                    slot.UpdateUI(null);
                }
            }
        }
    }

    public override async void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        
        if (_userId != null)
        {
            await LoadEquipmentFromFirebase();
        }
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        
        // Odadan çıkarken verileri sadece yerel depolamaya kaydet
        if (_isInitialized && _userId != null)
        {
            SaveEquipmentToLocalStorage();
            Debug.Log("Odadan çıkarken ekipman yerel depolamaya kaydedildi.");
        }
    }

    public override void OnDisable()
    {
        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnAuthStateChanged -= OnAuthChanged;
        }
    }

    private void OnApplicationQuit()
    {
        if (_isInitialized && _userId != null)
        {
            // Firebase'e kaydetmek yerine sadece yerel bir JSON dosyasına kaydet
            SaveEquipmentToLocalStorage();
            Debug.Log("Ekipman yerel depolamaya hızlıca kaydedildi. Oyun başlatıldığında Firebase'e senkronize edilecek.");
        }
    }

    private void OnDestroy()
    {
        if (_isInitialized && _userId != null)
        {
            // Sadece yerel kayıt yap
            SaveEquipmentToLocalStorage();
        }
        Instance = null;
    }

    // Ekipmanı yerel depolamaya kaydetmek için yeni metot
    private void SaveEquipmentToLocalStorage()
    {
        try
        {
            // Ekipman verilerini hazırla
            var equipmentData = new Dictionary<string, Dictionary<string, object>>();
            
            foreach (var pair in _equippedItems)
            {
                SlotType slotType = pair.Key;
                InventoryItem item = pair.Value;
                
                if (item == null) continue;
                
                string slotKey = slotType.ToString().ToLower();
                
                // Yerel depolama için basit bir Dictionary oluştur
                var itemData = new Dictionary<string, object>
                {
                    { "itemId", item.ItemId },
                    { "amount", item.Amount },
                    { "slotType", (int)slotType }
                };
                
                equipmentData[slotKey] = itemData;
            }
            
            // Dosya yolunu belirle
            string path = System.IO.Path.Combine(Application.persistentDataPath, $"equipment_{_userId}.json");
            
            // JSON verisini oluştur ve dosyaya yaz
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(equipmentData);
            System.IO.File.WriteAllText(path, json);
            
            Debug.Log($"Ekipman yerel depolamaya kaydedildi: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ekipman yerel kayıt hatası: {e.Message}");
        }
    }
    
    // Ekipmanı yerel depolamadan yüklemek için yeni metot (başlangıçta çağrılabilir)
    private bool LoadEquipmentFromLocalStorage()
    {
        try
        {
            if (string.IsNullOrEmpty(_userId))
            {
                return false;
            }
            
            string path = System.IO.Path.Combine(Application.persistentDataPath, $"equipment_{_userId}.json");
            
            if (!System.IO.File.Exists(path))
            {
                return false;
            }
            
            string json = System.IO.File.ReadAllText(path);
            
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("Yerel ekipman dosyası boş.");
                return false;
            }
            
            // Önce mevcut ekipmanları temizle
            ClearAllEquipment();
            
            int loadedCount = 0;
            
            try
            {
                // Yeni format: Dictionary<string, Dictionary<string, object>>
                var equipmentData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(json);
                
                if (equipmentData != null && equipmentData.Count > 0)
                {
                    foreach (var pair in equipmentData)
                    {
                        try
                        {
                            string slotKey = pair.Key;
                            var itemData = pair.Value;
                            
                            if (itemData == null || !itemData.ContainsKey("itemId"))
                            {
                                continue;
                            }
                            
                            // itemId ve slotType değerlerini al
                            string itemId = itemData["itemId"].ToString();
                            
                            if (string.IsNullOrEmpty(itemId))
                            {
                                continue;
                            }
                            
                            // slotType değerini al ve dönüştür
                            SlotType slotType = SlotType.None;
                            if (itemData.ContainsKey("slotType"))
                            {
                                if (itemData["slotType"] is long slotTypeLong)
                                {
                                    slotType = (SlotType)slotTypeLong;
                                }
                                else if (int.TryParse(itemData["slotType"].ToString(), out int slotTypeInt))
                                {
                                    slotType = (SlotType)slotTypeInt;
                                }
                            }
                            
                            // Item verisini al
                            ItemData itemDataObj = ItemDatabase.Instance.GetItemById(itemId);
                            
                            if (itemDataObj == null)
                            {
                                Debug.LogWarning($"Item veritabanında bulunamadı: {itemId}");
                                continue;
                            }
                            
                            // ----- MİKTARI OKU -----
                            int amount = 1; // Varsayılan 1
                            if (itemData.ContainsKey("amount"))
                            {
                                if (itemData["amount"] is long amountLong) // Firebase bazen long döndürür
                                {
                                    amount = (int)amountLong;
                                }
                                else if (int.TryParse(itemData["amount"].ToString(), out int parsedAmount))
                                {
                                    amount = parsedAmount;
                                }
                            }
                            // ------------------------
                            
                            // Item oluştur ve ekipman olarak giy
                            InventoryItem item = new InventoryItem(itemDataObj, amount); // Miktarı constructor'a gönder
                            EquipItem(item, slotType, false); // saveToFirebase = false çünkü zaten yüklüyoruz
                            loadedCount++;
                            
                            Debug.Log($"Yerel depolamadan ekipman yüklendi: {itemDataObj.itemName} (x{amount}), Slot: {slotType}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Ekipman verisi ayrıştırılırken hata: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Yeni format ayrıştırma hatası, eski formatı deniyorum: {ex.Message}");
                
                // Eski formatları denemeye gerek yok, çünkü yeni kayıtlar yeni formatta olacak
                // Ancak geriye dönük uyumluluk için eski format desteği eklenebilir
                return false;
            }
            
            Debug.Log($"Ekipman yerel depolamadan yüklendi: {loadedCount} adet");
            return loadedCount > 0;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ekipman yerel yükleme hatası: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Verilen slot'un ekipman slotu olup olmadığını kontrol eder
    /// </summary>
    /// <param name="slot">Kontrol edilecek slot</param>
    /// <returns>Ekipman slotu mu?</returns>
    public bool IsEquipmentSlot(InventorySlotUI slot)
    {
        if (slot == null)
        {
            return false;
        }
        
        // Ekipman slotları sözlüğünde bu slot var mı kontrol et
        return _equipmentSlots.ContainsValue(slot);
    }

    // Belirli bir slot tipine ait UI slotunu döndürür (Eklendi)
    public InventorySlotUI GetEquipmentSlotUI(SlotType slotType)
    {
        _equipmentSlots.TryGetValue(slotType, out InventorySlotUI slotUI);
        return slotUI;
    }

    #endregion
}

[System.Serializable]
public class EquipmentSaveData
{
    public string itemId;
    public SlotType slotType;
    
    // Parametre almayan constructor (JSON deserializasyon için gerekli)
    public EquipmentSaveData() { }
    
    // Parametreli constructor (kolaylık için)
    public EquipmentSaveData(string itemId, SlotType slotType)
    {
        this.itemId = itemId;
        this.slotType = slotType;
    }
    
    // Not: Bu sınıf artık doğrudan Firebase'e kaydedilmiyor.
    // Bunun yerine Dictionary<string, object> kullanılıyor.
} 
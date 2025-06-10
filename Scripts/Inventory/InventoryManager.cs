using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Threading.Tasks;
using Photon.Pun;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Firebase.Database;
using System; // DateTimeOffset için System namespace'i eklendi
using System.Linq; // FirstOrDefault için eklendi

/// <summary>
/// Envanter sisteminin merkezi yönetim sınıfı.
/// Singleton desenini kullanır ve envanter ile ilgili tüm işlemleri yönetir.
/// </summary>
public class InventoryManager : MonoBehaviourPunCallbacks
{
    #region Singleton

    public static InventoryManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEvents();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Inspector References

    [Header("Inventory Settings")]
    [SerializeField] private int inventorySize = 24;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotsParent;
    [SerializeField] private GameObject inventoryUIParent;

    [Header("Debug Options")]
    [SerializeField] private bool enableDebugLogs = true;

    #endregion

    #region Private Fields

    private readonly Dictionary<int, InventoryItem> _items = new Dictionary<int, InventoryItem>();
    private readonly List<GameObject> _slotObjects = new List<GameObject>();
    private string _userId;
    private bool _isInitialized;
    private DatabaseReference _databaseRef;
    private int _version = 0; // Veri senkronizasyonu için versiyon numarası
    private const string PLAYER_INITIALIZED_KEY = "PlayerInitialized_"; // Oyuncunun başlatıldığını takip etmek için key

    #endregion

    #region Properties

    /// <summary>
    /// Envanter boyutunu döndürür.
    /// </summary>
    public int InventorySize => inventorySize;

    /// <summary>
    /// Envanter UI ana objesini döndürür.
    /// </summary>
    public GameObject InventoryUIParent => inventoryUIParent;

    #endregion

    #region Events

    // Envanter olayları
    public delegate void InventoryChangedHandler(int slotId, InventoryItem item);
    public event InventoryChangedHandler OnItemAdded;
    public event InventoryChangedHandler OnItemRemoved;
    public event InventoryChangedHandler OnItemUpdated;
    public event System.Action OnInventoryLoaded;

    private void InitializeEvents()
    {
        OnItemAdded += (slot, item) => DebugLog($"Item eklendi: {item?.ItemName ?? "Null"}, Slot: {slot}");
        OnItemRemoved += (slot, item) => DebugLog($"Item kaldırıldı: {item?.ItemName ?? "Null"}, Slot: {slot}");
        OnItemUpdated += (slot, item) => DebugLog($"Item güncellendi: {item?.ItemName ?? "Null"}, Slot: {slot}");
        OnInventoryLoaded += () => DebugLog("Envanter yüklendi");
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // StartCoroutine(InitializeInventoryRoutine()); // Eski başlatma
        // Önce Auth event'ine abone ol, user gelince başlat
        StartCoroutine(WaitForAuthAndInitialize());
    }

    private IEnumerator WaitForAuthAndInitialize()
    {
        DebugLog("FirebaseAuthManager bekleniyor...");
        // Firebase Auth yüklenme kontrolü
        while (FirebaseAuthManager.Instance == null)
        {
            yield return null;
        }
        DebugLog("FirebaseAuthManager bulundu. AuthStateChanged olayına abone olunuyor.");
        FirebaseAuthManager.Instance.OnAuthStateChanged += HandleAuthStateChanged;

        // Başlangıçta mevcut kullanıcı durumu kontrol et (belki zaten giriş yapılmıştır)
        if (FirebaseAuthManager.Instance.IsLoggedIn)
        {
             DebugLog("Başlangıçta kullanıcı zaten giriş yapmış. InitializeInventoryRoutine başlatılıyor.");
             // Korutini hemen başlatmak yerine HandleAuthStateChanged'i manuel tetikleyelim
             // Bu, kod tekrarını önler ve _userId'nin doğru set edilmesini garantiler.
             HandleAuthStateChanged(FirebaseAuthManager.Instance.CurrentUser); 
        } else {
             DebugLog("Başlangıçta kullanıcı giriş yapmamış. Giriş bekleniyor...");
        }
    }
    
    private void HandleAuthStateChanged(Firebase.Auth.FirebaseUser user)
    {
        DebugLog($"HandleAuthStateChanged tetiklendi. User: {(user == null ? "NULL" : user.UserId)}");
        if (user != null)
        {
            // Kullanıcı giriş yaptı veya değişti
            if (!_isInitialized || _userId != user.UserId) // Sadece ilk kez veya kullanıcı değiştiyse başlat
            {
                 DebugLog($"Yeni veya farklı kullanıcı ({user.UserId}). InitializeInventoryRoutine başlatılıyor...");
                 _userId = user.UserId; // User ID'yi burada set et
                 StartCoroutine(InitializeInventoryRoutine());
            } else {
                 DebugLog($"Kullanıcı ({user.UserId}) zaten işlenmiş, tekrar başlatılmıyor.");
            }
        }
        else
        {
            // Kullanıcı çıkış yaptı
             DebugLog("Kullanıcı çıkış yaptı. Envanter temizleniyor.");
            _userId = null;
            _isInitialized = false; // Tekrar başlatılabilmesi için false yap
            _items.Clear();
            // UI slotlarını temizle
            foreach (var slot in _slotObjects)
            {
                if (slot != null) { // Null check eklendi
                    var slotUI = slot.GetComponent<InventorySlotUI>();
                    if (slotUI != null)
                    {
                        slotUI.UpdateUI(null);
                    }
                }
            }
             DebugLog("Envanter temizlendi.");
        }
    }


    private IEnumerator InitializeInventoryRoutine()
    {
         // _userId'nin artık dolu olduğunu varsayıyoruz (HandleAuthStateChanged'den geliyor)
         if (string.IsNullOrEmpty(_userId))
         {
             DebugLogError("InitializeInventoryRoutine başlatıldı ancak _userId boş! Bu olmamalı.");
             yield break; // _userId yoksa devam etme
         }
         DebugLog($"InitializeInventoryRoutine başlatılıyor... UserID: {_userId}");

        // ItemDatabase'in yüklenmesini bekle
        DebugLog("ItemDatabase bekleniyor...");
        while (ItemDatabase.Instance == null)
        {
            yield return null;
        }
         DebugLog("ItemDatabase bulundu.");

        // Diğer oyuncuların envanterini gizle (Bu kısım muhtemelen gereksiz, yorumda bırakılabilir)
        /*if (!photonView.IsMine)
        {
            if (inventoryUIParent != null)
            {
                inventoryUIParent.SetActive(false);
                Destroy(inventoryUIParent);
            }
            yield break;
        }*/

        // Envanter UI'ını görünür yap
        if (inventoryUIParent != null)
        {
            inventoryUIParent.SetActive(true);
            DebugLog("Envanter UI'ı görünür yapıldı");
        }
        else
        {
            DebugLogWarning("inventoryUIParent referansı null! Envanter UI görünmeyebilir.");
        }

        // Database referansını al (Zaten alınmış olabilir ama tekrar kontrol edelim)
        if (_databaseRef == null) {
             DebugLog("Database referansı alınıyor...");
             _databaseRef = FirebaseDatabase.DefaultInstance.RootReference;
             DebugLog("Database referansı alındı.");
        }


        // UI slotlarını oluştur (Eğer daha önce oluşturulmadıysa veya temizlendiyse)
        if (_slotObjects == null || _slotObjects.Count == 0) {
             DebugLog("UI Slotları oluşturuluyor...");
             CreateInventorySlots();
        } else {
             DebugLog("UI Slotları zaten mevcut.");
        }

        // Initialize flag'ini Load'dan önce set et
        _isInitialized = true;

        // Envanter verilerini yükle
        DebugLog("LoadInventoryData çağrılıyor...");
        yield return LoadInventoryData(); // _userId'nin dolu olduğu garanti

        // Auth değişikliklerini dinle (Zaten WaitForAuth içinde abone olundu)
        //FirebaseAuthManager.Instance.OnAuthStateChanged += OnAuthChanged; // Tekrar abone olma

        DebugLog("Envanter sistemi başlatıldı ve yüklendi.");
        
        // Tüm envanter slotlarını manuel olarak güncelle
        DebugLog("Tüm envanter slotları manuel olarak güncelleniyor...");
        for (int i = 0; i < inventorySize; i++)
        {
            if (_items.TryGetValue(i, out InventoryItem item))
            {
                UpdateSlotUI(i, item);
                //DebugLog($"Slot {i} güncellendi: {item.ItemName}"); // Çok fazla log üretebilir
            }
            else
            {
                UpdateSlotUI(i, null);
            }
        }
        
        // Tüm item icon'ları aktif et
        ForceActivateItemIcons();
        
        DebugLog($"InitializeInventoryRoutine tamamlandı. UserID: {_userId}");
    }

    public override void OnDisable()
    {
        // Aboneliği kaldır
        if (FirebaseAuthManager.Instance != null)
        {
            // FirebaseAuthManager.Instance.OnAuthStateChanged -= OnAuthChanged; // Eski metod
            FirebaseAuthManager.Instance.OnAuthStateChanged -= HandleAuthStateChanged; // Yeni metod
        }
        base.OnDisable(); // base.OnDisable() çağrısını ekle
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _isInitialized)
        {
            // Uygulama arka plana alındığında yerel kayıt yap
            SaveInventoryToLocalStorage();
            DebugLog("Uygulama arka planda, envanter yerel depolamaya kaydedildi.");
        }
    }

    private void OnApplicationQuit()
    {
        if (_isInitialized)
        {
            // Çıkış sırasında sadece yerel kayıt yap, Firebase'e senkron kayıt yapma
            SaveInventoryToLocalStorage();
            Debug.Log("Envanter yerel depolamaya hızlıca kaydedildi. Oyun başlatıldığında Firebase'e senkronize edilecek.");
        }
    }

    private void OnDestroy()
    {
        if (_isInitialized)
        {
            // Yok edilme sırasında sadece yerel kayıt yap
            SaveInventoryToLocalStorage();
        }
    }

    #endregion

    #region Photon Callbacks

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        /*if (!photonView.IsMine)
        {
            if (inventoryUIParent != null)
            {
                inventoryUIParent.SetActive(false);
                Destroy(inventoryUIParent);
            }
            enabled = false;
        }
        else if (inventoryUIParent != null)
        {
            inventoryUIParent.SetActive(true);
        }*/
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        
        if (_isInitialized)
        {
            // Odadan çıkarken Firebase'e senkron kayıt yapmak yerine sadece yerel kayıt yap
            SaveInventoryToLocalStorage();
            DebugLog("Odadan çıkarken envanter yerel depolamaya kaydedildi.");
        }
    }

    #endregion

    #region Inventory Methods

    /// <summary>
    /// Yeni bir item ekler
    /// </summary>
    /// <param name="item">Eklenecek item</param>
    /// <param name="targetSlot">Hedef slot (-1 ise otomatik bulunur)</param>
    /// <param name="saveToDatabase">Veritabanına kaydet</param>
    /// <returns>Ekleme başarılı mı?</returns>
    public bool AddItem(InventoryItem item, int targetSlot = -1, bool saveToDatabase = true)
    {
        if (item == null || item.Amount <= 0) // Amount kontrolü eklendi
        {
            DebugLogWarning("Null veya miktarı 0 olan item eklenemez!");
            return false;
        }

        // ----- Yığınlama Mantığı Eklendi ----- 
        if (targetSlot == -1 && item.IsStackable) // Sadece otomatik yerleştirme ve yığınlanabilir itemlar için
        {
            int remainingAmount = item.Amount;

            // 1. Adım: Mevcut yığınları kontrol et
            for (int i = 0; i < inventorySize; i++)
            {
                if (_items.TryGetValue(i, out InventoryItem existingItem) && existingItem != null)
                {
                    // Aynı item türü mü ve yığında yer var mı?
                    if (existingItem.IsSameItemType(item) && existingItem.Amount < existingItem.MaxStackSize)
                    {
                        int overflow = existingItem.AddToStack(remainingAmount);
                        remainingAmount = overflow;
                        DebugLog($"Item '{item.ItemName}' mevcut yığına eklendi (Slot {i}). Kalan Miktar: {remainingAmount}");
                        
                        // Yığını güncelledik, UI'ı yenile ve kaydet
                        UpdateSlotUI(i, existingItem);
                        if (saveToDatabase) SaveInventory(); // Kaydet
                        
                        // Eğer tüm itemlar yığına sığdıysa, işlem tamam
                        if (remainingAmount <= 0)
                        {
                            return true;
                        }
                        // Devam eden miktar varsa, diğer yığınları veya boş slotları aramaya devam et
                    }
                }
            }
            
            // Eğer hala eklenecek miktar kaldıysa, item'ın miktarını güncelle
            if (remainingAmount < item.Amount) // Bir kısmı yığına eklendiyse
            {
                 item.Amount = remainingAmount; // Kalan miktarla devam et
                 DebugLog($"Yığınlama sonrası kalan miktar: {item.Amount}");
            }
             // Eğer hiç yığınlama yapılamadıysa (remainingAmount == item.Amount ise), item miktarı aynı kalır.
        }
        // ----- Yığınlama Mantığı Sonu -----

        // Eklenecek item miktarı hala 0'dan büyükse devam et
        if (item.Amount <= 0) return true; // Yığınlamada her şey bittiyse başarılı say

        // 2. Adım: Belirtilen slota veya ilk boş slota ekle
        if (targetSlot >= 0 && targetSlot < inventorySize) // Belirli bir slot hedeflendiyse
        { 
             // Eğer hedef slotta başka bir item varsa veya bu item eklenemiyorsa ekleme
             if (_items.ContainsKey(targetSlot) && _items[targetSlot] != null) {
                  DebugLogWarning($"Hedef slot {targetSlot} zaten dolu.");
                  return false; // Veya SwapItems çağırılabilir duruma göre
             }
             // Slot boşsa veya null ise ekle
            var success = AddItemToSlot(item, targetSlot);
            if (success && saveToDatabase)
            { 
                SaveInventory();
            }
            return success;
        }
        else // Otomatik yerleştirme (veya yığınlamadan kalanlar)
        { 
            // Boş slot bul ve ekle
            for (int i = 0; i < inventorySize; i++)
            {
                if (!_items.ContainsKey(i) || _items[i] == null)
                {
                    var success = AddItemToSlot(item, i);
                    if (success && saveToDatabase)
                    {
                        SaveInventory();
                    }
                    return success; // İlk boş slotu bulduğunda ekle ve çık
                }
            }
        }

        DebugLogWarning("Envanterde boş slot bulunamadı!");
        return false;
    }

    /// <summary>
    /// Belirli bir slottaki itemi kaldırır
    /// </summary>
    /// <param name="slotId">Slot ID</param>
    /// <param name="saveToDatabase">Veritabanına kaydet</param>
    /// <returns>Kaldırılan item, yoksa null</returns>
    public InventoryItem RemoveItem(int slotId, bool saveToDatabase = true)
    {
        if (slotId < 0 || slotId >= inventorySize)
        {
            DebugLogWarning($"Geçersiz slot ID: {slotId}");
            return null;
        }

        InventoryItem removedItem = null;

        if (_items.TryGetValue(slotId, out removedItem))
        {
            _items.Remove(slotId);
            
            // UI güncelle
            if (slotId < _slotObjects.Count)
            {
                var slotUI = _slotObjects[slotId].GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.UpdateUI(null);
                }
            }

            // Olayı tetikle
            OnItemRemoved?.Invoke(slotId, removedItem);

            // Veritabanına kaydet
            if (saveToDatabase)
            {
                SaveInventory();
            }
        }

        return removedItem;
    }

    /// <summary>
    /// İki slot arasında itemleri takas eder veya yığınlar
    /// </summary>
    /// <param name="fromSlot">Kaynak slot</param>
    /// <param name="toSlot">Hedef slot</param>
    /// <param name="saveToDatabase">Veritabanına kaydet</param>
    /// <returns>İşlem başarılı mı?</returns>
    public bool SwapItems(int fromSlot, int toSlot, bool saveToDatabase = true)
    {
        if (fromSlot < 0 || fromSlot >= inventorySize || toSlot < 0 || toSlot >= inventorySize)
        {
            DebugLogWarning($"Geçersiz slot ID'leri: {fromSlot}, {toSlot}");
            return false;
        }

        if (fromSlot == toSlot)
        {
            DebugLogWarning("Aynı slot ID'leri, işlem yapılmıyor");
            return false;
        }

        DebugLog($"SwapItems/Stack Attempt çağrıldı: {fromSlot} -> {toSlot}");

        // Itemları getir
        InventoryItem fromItem = GetItemAt(fromSlot);
        InventoryItem toItem = GetItemAt(toSlot);

        // ----- Yığınlama Mantığı ----- 
        if (fromItem != null && toItem != null && fromItem.IsStackable && fromItem.IsSameItemType(toItem))
        {
            // Hedef yığında ne kadar yer var?
            int spaceAvailable = toItem.MaxStackSize - toItem.Amount;
            
            if (spaceAvailable > 0)
            {
                // Ne kadar transfer edilebilir?
                int amountToTransfer = Mathf.Min(fromItem.Amount, spaceAvailable);

                if (amountToTransfer > 0)
                {
                    // Hedef yığına ekle
                    toItem.AddToStack(amountToTransfer); // AddToStack taşmayı zaten ele alıyor, ama biz zaten boşluk kadar ekliyoruz
                    // Kaynak yığından çıkar
                    fromItem.RemoveFromStack(amountToTransfer);

                    DebugLog($"{amountToTransfer} adet '{fromItem.ItemName}' {fromSlot} slotundan {toSlot} slotuna yığınlandı.");
                    
                    // Eğer kaynak yığın boşaldıysa, onu kaldır
                    if (fromItem.Amount <= 0)
                    {
                        _items.Remove(fromSlot);
                        fromItem = null; // Referansı null yap
                        DebugLog($"Kaynak slot ({fromSlot}) boşaltıldı.");
                    }

                    // UI'ları güncelle
                    UpdateSlotUI(fromSlot, fromItem); // fromItem null olabilir
                    UpdateSlotUI(toSlot, toItem);

                    // Veritabanına kaydet
                    if (saveToDatabase)
                    { 
                        SaveInventory();
                        DebugLog("Yığınlama sonrası envanter kaydedildi");
                    }
                    return true; // Yığınlama başarılı oldu
                }
                else
                {
                     DebugLog("Hedef yığında yer yok veya kaynakta miktar yok, yığınlama yapılamadı.");
                     // Yığınlama yapılamadıysa normal takasa devam et
                }
            }
            else
            {
                DebugLog("Hedef yığın zaten dolu, yığınlama yapılamadı.");
                 // Hedef doluysa normal takasa devam et
            }
        }
        // ----- Yığınlama Mantığı Sonu -----

        // Yığınlama yapılamadıysa veya koşullar uygun değilse, normal takas yap
        DebugLog($"Yığınlama yapılamadı/uygun değil. Normal takas yapılıyor: {fromSlot} <-> {toSlot}");
        DebugLog($"Kaynak item: {(fromItem != null ? fromItem.ItemName : "boş")}, Hedef item: {(toItem != null ? toItem.ItemName : "boş")}");

        // Önceki takas mantığı buraya geliyor
        // Slotlardaki itemları değiştir
        _items.Remove(fromSlot); // Varsa kaldır
        _items.Remove(toSlot);   // Varsa kaldır

        if (toItem != null)
        {
            _items[fromSlot] = toItem; // Önceki hedefi kaynağa koy
        }
        if (fromItem != null)
        {
            _items[toSlot] = fromItem; // Önceki kaynağı hedefe koy
        }

        // UI'ları güncelle
        UpdateSlotUI(fromSlot, GetItemAt(fromSlot)); // Güncel itemları alarak UI güncelle
        UpdateSlotUI(toSlot, GetItemAt(toSlot));
        
        DebugLog($"Normal takas sonrası UI güncellendi: Slot {fromSlot} -> {(GetItemAt(fromSlot) == null ? "Boş" : GetItemAt(fromSlot).ItemName)}, Slot {toSlot} -> {(GetItemAt(toSlot) == null ? "Boş" : GetItemAt(toSlot).ItemName)}");

        // Veritabanına kaydet
        if (saveToDatabase)
        { 
            SaveInventory();
            DebugLog("Normal takas sonrası envanter kaydedildi");
        }

        DebugLog($"Normal takas işlemi tamamlandı: {fromSlot} <-> {toSlot}");
        return true;
    }

    /// <summary>
    /// Belirli bir slottaki itemi getirir
    /// </summary>
    /// <param name="slotId">Slot ID</param>
    /// <returns>Item, yoksa null</returns>
    public InventoryItem GetItemAt(int slotId)
    {
        if (slotId < 0 || slotId >= inventorySize)
        {
            return null;
        }

        if (_items.TryGetValue(slotId, out var item))
        {
            return item;
        }

        return null;
    }

    /// <summary>
    /// Veritabanındaki envanter verilerini yükler
    /// </summary>
    public async Task<bool> LoadInventory()
    {
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Envanter verilerini veritabanına kaydeder
    /// </summary>
    /// <param name="async">Asenkron olarak kaydet</param>
    public void SaveInventory(bool async = true)
    {
        if (!_isInitialized || string.IsNullOrEmpty(_userId))
        {
            DebugLogWarning("Envanter kaydedilemedi: Sistem başlatılmamış veya kullanıcı giriş yapmamış");
            return;
        }

        try
        {
            // Önce yerel olarak kaydet
            SaveInventoryToLocalStorage();

            // Envanter verisini oluştur
            var inventoryData = new InventoryData
            {
                version = ++_version,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                items = new Dictionary<string, InventoryItemData>()
            };

            // Itemları ekle
            foreach (var kvp in _items)
            {
                if (kvp.Value != null)
                {
                    inventoryData.items.Add(kvp.Key.ToString(), new InventoryItemData
                    {
                        itemId = kvp.Value.ItemId,
                        amount = kvp.Value.Amount
                    });
                }
            }

            // Firebase'e kaydet
            string json = JsonConvert.SerializeObject(inventoryData);
            var inventoryRef = _databaseRef.Child("users").Child(_userId).Child("inventory");

            if (async)
            {
                // Asenkron kaydet
                inventoryRef.SetRawJsonValueAsync(json).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        DebugLogError($"Firebase kayıt hatası: {task.Exception}");
                    }
                    else if (task.IsCompleted)
                    {
                        DebugLog("Envanter Firebase'e kaydedildi");
                    }
                });
            }
            else
            {
                // Senkron kaydet (uygulama kapanırken)
                inventoryRef.SetRawJsonValueAsync(json).Wait();
                DebugLog("Envanter Firebase'e senkron olarak kaydedildi");
            }
        }
        catch (System.Exception e)
        {
            DebugLogError($"Envanter kayıt hatası: {e.Message}");
        }
    }

    /// <summary>
    /// Envanteri manuel olarak kaydeder
    /// </summary>
    public void ManualSave()
    {
        SaveInventory();
    }

    // Yeni Eklendi: Para Birimi Dönüşüm Metodu
    public void AttemptCoinConversion(int slotIndex, InventoryItem clickedItem)
    {
        if (clickedItem == null || !_items.ContainsKey(slotIndex) || _items[slotIndex] != clickedItem)
        {
            DebugLogWarning("AttemptCoinConversion: Tıklanan item veya slot bilgisi geçersiz.");
            return;
        }

        ItemData copperItemData = ItemDatabase.Instance?.GetItemById(CurrencyUtils.COPPER_COIN_ID);
        ItemData silverItemData = ItemDatabase.Instance?.GetItemById(CurrencyUtils.SILVER_COIN_ID);
        ItemData goldItemData = ItemDatabase.Instance?.GetItemById(CurrencyUtils.GOLD_COIN_ID);

        if (copperItemData == null || silverItemData == null || goldItemData == null)
        {
            DebugLogError("AttemptCoinConversion: Para birimi ItemData'larından biri (Bakır, Gümüş veya Altın) bulunamadı!");
            UIFeedbackManager.Instance?.ShowTooltip("Para birimi ayarları eksik, dönüşüm yapılamıyor.");
            return;
        }

        // Dinamik dönüşüm oranlarını al
        int copperToSilverRate = (copperItemData.MaxStackSize > 1) ? copperItemData.MaxStackSize : 99;
        int silverToGoldRate = (silverItemData.MaxStackSize > 1) ? silverItemData.MaxStackSize : 99;

        bool conversionDone = false;

        // 1. Bakırdan Gümüşe Dönüşüm
        if (clickedItem.ItemId == CurrencyUtils.COPPER_COIN_ID && clickedItem.Amount >= copperToSilverRate)
        {
            int silverToAdd = clickedItem.Amount / copperToSilverRate;
            int copperToRemove = silverToAdd * copperToSilverRate;

            // Kaynak slottan bakırı azalt/kaldır
            clickedItem.Amount -= copperToRemove;
            if (clickedItem.Amount <= 0)
            {
                RemoveItem(slotIndex, false); // UI ve _items güncellenir, kaydetme sonra
            }
            else
            {
                UpdateSlotUI(slotIndex, clickedItem); // Sadece miktarı azaldıysa UI güncelle
            }

            // Gümüşü ekle
            InventoryItem newSilverCoins = new InventoryItem(silverItemData, silverToAdd);
            AddItem(newSilverCoins, -1, false); // Kaydetme sonra
            conversionDone = true;
            DebugLog($"{copperToRemove} Bakır, {silverToAdd} Gümüşe dönüştürüldü.");
            UIFeedbackManager.Instance?.ShowTooltip($"{copperToRemove} {copperItemData.ItemName} -> {silverToAdd} {silverItemData.ItemName}");
        }
        // 2. Gümüşten Altına Dönüşüm
        else if (clickedItem.ItemId == CurrencyUtils.SILVER_COIN_ID && clickedItem.Amount >= silverToGoldRate)
        {
            int goldToAdd = clickedItem.Amount / silverToGoldRate;
            int silverToRemove = goldToAdd * silverToGoldRate;

            // Kaynak slottan gümüşü azalt/kaldır
            clickedItem.Amount -= silverToRemove;
            if (clickedItem.Amount <= 0)
            {
                RemoveItem(slotIndex, false); // UI ve _items güncellenir, kaydetme sonra
            }
            else
            {
                UpdateSlotUI(slotIndex, clickedItem); // Sadece miktarı azaldıysa UI güncelle
            }

            // Altını ekle
            InventoryItem newGoldCoins = new InventoryItem(goldItemData, goldToAdd);
            AddItem(newGoldCoins, -1, false); // Kaydetme sonra
            conversionDone = true;
            DebugLog($"{silverToRemove} Gümüş, {goldToAdd} Altına dönüştürüldü.");
            UIFeedbackManager.Instance?.ShowTooltip($"{silverToRemove} {silverItemData.ItemName} -> {goldToAdd} {goldItemData.ItemName}");
        }

        if (conversionDone)
        {
            SaveInventory(); // Tüm işlemler bittikten sonra bir kez kaydet
        }
        else
        {
            // Dönüşüm için yeterli miktar yoksa veya tıklanan item para değilse
            // (Bu durumlar zaten yukarıdaki if bloklarında kontrol ediliyor ama yine de bir log)
            if (clickedItem.ItemId == CurrencyUtils.COPPER_COIN_ID || clickedItem.ItemId == CurrencyUtils.SILVER_COIN_ID)
            {
                // UIFeedbackManager.Instance?.ShowTooltip("Dönüşüm için yeterli miktar yok.");
                // Daha spesifik mesajlar yukarıda veriliyor.
            }
        }
    }

    /// <summary>
    /// Envanterdeki tüm para birimlerinin toplam bakır değerini hesaplar.
    /// </summary>
    /// <returns>Toplam bakır değeri.</returns>
    public int CalculateTotalCopperValue()
    {
        if (!_isInitialized)
        {
            DebugLogWarning("CalculateTotalCopperValue: Envanter başlatılmamış.");
            return 0;
        }

        int totalCopper = 0;
        foreach (InventoryItem item in _items.Values)
        {
            if (item != null)
            {
                // ItemData'yı alarak 'value' alanına erişiyoruz, çünkü bu temel bakır değerini tutuyor.
                ItemData itemData = ItemDatabase.Instance?.GetItemById(item.ItemId);
                if (itemData != null)
                {
                    if (itemData.itemType == SlotType.Currency)
                    {
                        totalCopper += item.Amount * itemData.value; 
                    }
                }
            }
        }
        DebugLog($"CalculateTotalCopperValue: Hesaplanan toplam bakır: {totalCopper}");
        return totalCopper;
    }

    /// <summary>
    /// Belirtilen toplam bakır değerini envanterden çıkarmaya çalışır.
    /// Paraları (Altın, Gümüş, Bakır) kullanarak eksiltir.
    /// </summary>
    /// <param name="totalCopperToRemove">Çıkarılacak toplam bakır miktarı.</param>
    /// <returns>İşlem başarılıysa true, aksi takdirde false.</returns>
    public bool TryRemoveCurrency(int totalCopperToRemove)
    {
        if (totalCopperToRemove <= 0) return true; // Çıkarılacak bir şey yoksa başarılı say

        int currentTotalCopper = CalculateTotalCopperValue();
        if (currentTotalCopper < totalCopperToRemove)
        {
            DebugLogWarning($"TryRemoveCurrency: Yetersiz bakiye. Mevcut: {currentTotalCopper}, Gereken: {totalCopperToRemove}");
            return false; // Yetersiz bakiye
        }

        // Çıkarma işlemi:
        // En basit yöntem: Tüm paraları topla, çıkar, kalanı geri dağıt.
        // Bu, en az sayıda item değişikliği yapmayabilir ama daha az hataya açıktır.

        List<InventoryItem> currencyItems = new List<InventoryItem>();
        List<int> currencySlots = new List<int>();

        // 1. Mevcut tüm para itemlerini ve slotlarını topla
        for (int i = 0; i < inventorySize; i++)
        {
            if (_items.TryGetValue(i, out InventoryItem item) && item != null)
            {
                ItemData itemData = ItemDatabase.Instance?.GetItemById(item.ItemId);
                if (itemData != null && itemData.itemType == SlotType.Currency)
                { 
                    currencyItems.Add(new InventoryItem(item)); // Kopyasını al
                    currencySlots.Add(i);
                }
            }
        }

        // 2. Toplanan para itemlerini envanterden geçici olarak kaldır (UI güncellemesi yapmadan)
        foreach (int slotId in currencySlots)
        {
            _items.Remove(slotId);
            // UpdateSlotUI(slotId, null); // Henüz UI güncelleme
        }

        // 3. Kalan bakır miktarını hesapla
        int remainingCopperAfterRemoval = currentTotalCopper - totalCopperToRemove;

        // 4. Kalan bakırı yeni para itemlerine dönüştür ve envantere ekle
        bool addSuccess = TryAddCurrencyInternal(remainingCopperAfterRemoval, false); // Internal, no save yet

        if (addSuccess)
        {
            // Tüm para slotlarını ve etkilenen diğer slotları güncelle
            foreach (int slotId in currencySlots) UpdateSlotUI(slotId, GetItemAt(slotId)); // Eski para slotları boşalmış olabilir
            // TryAddCurrencyInternal yeni item eklediği slotları da güncellemeli (kendi içinde yapıyor)
            SaveInventory(); // Değişiklikleri kaydet
            DebugLog($"TryRemoveCurrency: {totalCopperToRemove} bakır başarıyla çıkarıldı. Kalan: {remainingCopperAfterRemoval}");
            return true;
        }
        else
        {
            // Başarısız olursa, çıkarılan paraları geri yükle (çok nadir bir durum olmalı, yer sorunu vb.)
            DebugLogError("TryRemoveCurrency: Kalan para geri eklenemedi! Bu beklenmedik bir durum. İşlem geri alınıyor.");
            foreach (var currencyItemTuple in currencyItems.Zip(currencySlots, (item, slot) => (item, slot)))
            {
                _items[currencyItemTuple.slot] = currencyItemTuple.item; // Orijinal itemleri geri koy
                // UpdateSlotUI(currencyItemTuple.slot, currencyItemTuple.item); // UI sonra güncellenir
            }
            // SaveInventory(); // Geri alındığı için tekrar kaydetmeye gerek yok, ya da eski hali kaydedilmeli.
            // Bu senaryo çok karmaşık, şimdilik basit hata logu.
            return false;
        }
    }

    /// <summary>
    /// Belirtilen toplam bakır değerini envantere para itemleri olarak eklemeye çalışır.
    /// </summary>
    /// <param name="totalCopperToAdd">Eklenecek toplam bakır miktarı.</param>
    /// <returns>İşlem başarılıysa true, aksi takdirde false (örn: yer yoksa).</returns>
    public bool TryAddCurrency(int totalCopperToAdd)
    {
        return TryAddCurrencyInternal(totalCopperToAdd, true);
    }

    private bool TryAddCurrencyInternal(int totalCopperToAdd, bool shouldSave)
    {
        if (totalCopperToAdd < 0) return false;
        if (totalCopperToAdd == 0) return true; // Eklenecek bir şey yoksa başarılı

        ItemData goldData = ItemDatabase.Instance.GetItemById(CurrencyUtils.GOLD_COIN_ID);
        ItemData silverData = ItemDatabase.Instance.GetItemById(CurrencyUtils.SILVER_COIN_ID);
        ItemData copperData = ItemDatabase.Instance.GetItemById(CurrencyUtils.COPPER_COIN_ID);

        if (goldData == null || silverData == null || copperData == null)
        {
            DebugLogError("TryAddCurrencyInternal: Para birimi ItemData'larından biri bulunamadı!");
            return false;
        }

        // Para birimlerinin bakır değerleri (ItemData.value alanından)
        int goldValue = goldData.value;     // örn: 9801
        int silverValue = silverData.value;   // örn: 99
        int copperValue = copperData.value;   // örn: 1

        List<InventoryItem> coinsToAdd = new List<InventoryItem>();
        int remainingCopper = totalCopperToAdd;

        // 1. Altınları hesapla
        if (remainingCopper >= goldValue)
        {
            int goldCount = remainingCopper / goldValue;
            if (goldCount > 0) coinsToAdd.Add(new InventoryItem(goldData, goldCount));
            remainingCopper %= goldValue;
        }

        // 2. Gümüşleri hesapla
        if (remainingCopper >= silverValue)
        {
            int silverCount = remainingCopper / silverValue;
            if (silverCount > 0) coinsToAdd.Add(new InventoryItem(silverData, silverCount));
            remainingCopper %= silverValue;
        }

        // 3. Kalan bakırları hesapla
        if (remainingCopper >= copperValue) // Genelde >= 1 olur
        {
            int copperCount = remainingCopper / copperValue;
            if (copperCount > 0) coinsToAdd.Add(new InventoryItem(copperData, copperCount));
        }

        // Hesaplanan paraları envantere eklemeyi dene
        List<InventoryItem> addedCoinsTracker = new List<InventoryItem>();
        bool allAddedSuccessfully = true;

        foreach (InventoryItem coinStack in coinsToAdd)
        {
            // AddItem metodu yığınlamayı ve boş slot bulmayı dener.
            // Kendi içinde SaveInventory çağırmaması için saveToDatabase=false kullanıyoruz.
            if (AddItem(coinStack, -1, false)) 
            { 
                addedCoinsTracker.Add(coinStack);
            }
            else
            { 
                allAddedSuccessfully = false;
                DebugLogWarning($"TryAddCurrencyInternal: {coinStack.ItemName} x{coinStack.Amount} eklenemedi (muhtemelen yer yok).");
                // Başarısız olursa, o ana kadar eklenenleri geri almayı deneyebiliriz (opsiyonel, karmaşıklaştırır)
                // Şimdilik: Eğer biri bile eklenemezse, işlemi başarısız say ve eklenenleri geri al.
                foreach(InventoryItem addedCoin in addedCoinsTracker)
                {
                    // Bu geri alma işlemi de karmaşık, çünkü tam olarak hangi slottan ne kadar çıkarılacağını bilmek zor.
                    // Şimdilik basitçe logluyoruz ve false dönüyoruz.
                    DebugLogError($"TryAddCurrencyInternal: Geri alma implemente edilmedi. {addedCoin.ItemName} eklenmiş olabilir.");
                }
                break; 
            }
        }
        
        if (allAddedSuccessfully && shouldSave)
        {
            SaveInventory(); // Tüm paralar başarıyla eklendiyse kaydet
        }
        
        if (allAddedSuccessfully)
        {
             DebugLog($"TryAddCurrencyInternal: {totalCopperToAdd} bakır başarıyla eklendi.");
        }

        return allAddedSuccessfully;
    }

    /// <summary>
    /// Envanterden belirtilen ID ve miktarda itemi siler.
    /// </summary>
    /// <param name="itemId">Silinecek itemin ID'si.</param>
    /// <param name="amountToRemove">Silinecek miktar.</param>
    /// <param name="saveToDatabase">Veritabanına kaydedilsin mi?</param>
    /// <returns>İşlem başarılıysa true.</returns>
    public bool RemoveSpecificItem(string itemId, int amountToRemove, bool saveToDatabase)
    {
        if (string.IsNullOrEmpty(itemId) || amountToRemove <= 0)
        {
            return false;
        }

        int totalRemoved = 0;
        List<int> slotsToUpdate = new List<int>();

        // Envanteri tersten tarayarak itemleri bul ve sil (stackleri tüketmek için)
        for (int i = inventorySize - 1; i >= 0; i--)
        {
            if (totalRemoved >= amountToRemove) break; // Gerekli miktar silindi

            if (_items.TryGetValue(i, out InventoryItem item) && item != null && item.ItemId == itemId)
            {
                int canRemoveFromStack = Mathf.Min(item.Amount, amountToRemove - totalRemoved);
                item.Amount -= canRemoveFromStack;
                totalRemoved += canRemoveFromStack;
                slotsToUpdate.Add(i);

                if (item.Amount <= 0)
                {
                    _items.Remove(i);
                }
            }
        }

        if (totalRemoved < amountToRemove)
        {
            // Yeterli item bulunamadı, yapılan değişiklikleri geri al (bu kısım zor olabilir)
            // Şimdilik: Yetersizse false dön, yapılan kısmi silmeler kalıcı olur.
            // Daha iyi bir çözüm için, işlem öncesi envanterin bir kopyasını almak ve başarısızlıkta onu geri yüklemek gerekebilir.
            DebugLogWarning($"RemoveSpecificItem: {itemId} için yeterli miktar ({amountToRemove}) bulunamadı. Sadece {totalRemoved} adet silindi.");
            // Kısmi silme yapıldıysa bile UI güncellenmeli ve kaydedilmeli.
        }

        if (totalRemoved > 0) // En az bir item silindiyse
        {
            foreach (int slotId in slotsToUpdate)
            {
                UpdateSlotUI(slotId, GetItemAt(slotId)); // Slotu yeni durumuyla güncelle
            }
            if (saveToDatabase)
            {
                SaveInventory();
            }
            DebugLog($"RemoveSpecificItem: {itemId} iteminden {totalRemoved} adet silindi.");
            return totalRemoved >= amountToRemove; // Tamamı silindiyse true
        }
        return false; // Hiçbir şey silinmedi
    }

    #endregion

    #region Helper Methods

    private void CreateInventorySlots()
    {
        // Gerekli referansları kontrol et
        if (slotPrefab == null || slotsParent == null)
        {
            DebugLogError("Slot prefab veya slots parent referansı eksik!");
            return;
        }

        // ISlot adında fazladan bir slot olup olmadığını kontrol et ve varsa sil
        /*Transform iSlot = slotsParent.Find("ISlot");
        if (iSlot != null)
        {
            DebugLog("ISlot bulundu ve siliniyor...");
            Destroy(iSlot.gameObject);
        }*/

        // Mevcut slotları temizle
        foreach (var slot in _slotObjects)
        {
            Destroy(slot);
        }
        _slotObjects.Clear();

        // Slotları oluştur
        for (int i = 0; i < inventorySize; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotsParent);
            slotObj.name = $"Slot_{i}";
            
            // SlotUI'ı ayarla
            var slotUI = slotObj.GetComponent<InventorySlotUI>();
            if (slotUI != null)
            {
                slotUI.SetSlotType(SlotType.None);
                slotUI.UpdateUI(null);
                slotUI.SlotIndex = i;
                DebugLog($"Slot_{i} oluşturuldu ve SlotIndex={i} olarak ayarlandı");
            }
            
            _slotObjects.Add(slotObj);
        }

        DebugLog($"{inventorySize} adet envanter slotu oluşturuldu");
    }

    private bool AddItemToSlot(InventoryItem item, int slotId)
    {
        if (slotId < 0 || slotId >= inventorySize)
        {
            return false;
        }

        // Slot kontrolü
        var slotUI = _slotObjects[slotId].GetComponent<InventorySlotUI>();
        if (slotUI == null || !slotUI.CanAcceptItem(item))
        {
            return false;
        }

        // Item'ı ekle
        _items[slotId] = item;

        // UI güncelle
        UpdateSlotUI(slotId, item);

        // Olayı tetikle
        OnItemAdded?.Invoke(slotId, item);

        return true;
    }

    private void UpdateSlotUI(int slotId, InventoryItem item)
    {
        if (slotId < 0 || slotId >= _slotObjects.Count)
        {
            DebugLogWarning($"UpdateSlotUI: Geçersiz slot ID: {slotId}");
            return;
        }

        var slotObj = _slotObjects[slotId];
        if (slotObj == null)
        {
            DebugLogWarning($"UpdateSlotUI: Slot objesi bulunamadı: {slotId}");
            return;
        }

        var slotUI = slotObj.GetComponent<InventorySlotUI>();
        if (slotUI != null)
        {
            DebugLog($"UpdateSlotUI: Slot={slotId}, Item={(item != null ? item.ItemName : "null")}");
            slotUI.UpdateUI(item);
            
            // Item icon'unun aktif olduğundan emin ol
            if (item != null)
            {
                var itemIcon = slotObj.transform.Find("ItemIcon")?.GetComponent<UnityEngine.UI.Image>();
                if (itemIcon != null && !itemIcon.gameObject.activeSelf)
                {
                    DebugLogWarning($"UpdateSlotUI: Slot={slotId} için item icon aktif değil, aktif ediliyor");
                    itemIcon.gameObject.SetActive(true);
                }
            }
        }
        else
        {
            DebugLogWarning($"UpdateSlotUI: InventorySlotUI komponenti bulunamadı: {slotId}");
        }
    }

    private IEnumerator LoadInventoryData()
    {
        DebugLog($"LoadInventoryData başladı. UserID: {_userId}"); // UserID'yi logla
        // Önce yerel depolamadan yüklemeyi dene
        bool loadedFromLocal = LoadInventoryFromLocalStorage();
        DebugLog($"LoadInventoryData: Yerel yükleme sonucu (loadedFromLocal): {loadedFromLocal}"); // sonucu logla

        if (loadedFromLocal)
        {
            yield return null; // İşlemi bir frame'e yay
            //DebugLog("Envanter yerel depolamadan yüklendi"); // Zaten loglandı
        }

        // Firebase'den yükleme
         if (string.IsNullOrEmpty(_userId)) {
             DebugLogError("LoadInventoryData: Firebase yüklemesi öncesi _userId boş!");
             yield break;
         }
         if (_databaseRef == null) {
              DebugLogError("LoadInventoryData: Firebase yüklemesi öncesi _databaseRef null!");
              yield break;
         }
         
        var inventoryRef = _databaseRef.Child("users").Child(_userId).Child("inventory");
        DebugLog($"LoadInventoryData: Firebase'den veri çekiliyor... Ref: {inventoryRef.ToString()}");
        var task = inventoryRef.GetValueAsync();
        
        yield return new WaitUntil(() => task.IsCompleted);
        DebugLog($"LoadInventoryData: Firebase task tamamlandı. Durum: IsFaulted={task.IsFaulted}, IsCompletedSuccessfully={task.IsCompletedSuccessfully}");
        
        try
        {
            if (task.IsFaulted)
            {
                DebugLogError($"LoadInventoryData: Firebase yükleme hatası: {task.Exception?.Flatten().InnerExceptions.FirstOrDefault()?.Message ?? task.Exception?.Message}");
                // Hata olsa bile yerel veri varsa onunla devam edebilir mi? Evet.
                // Başlangıç item kontrolü için snapshot.Exists'e gitmemiz lazım.
                // yield break; // Burada çıkma
            }

            DataSnapshot snapshot = null; // Snapshot'ı tanımla
            bool snapshotExists = false; // Var olup olmadığını tut

            if (!task.IsFaulted && task.IsCompletedSuccessfully) {
                snapshot = task.Result;
                snapshotExists = snapshot.Exists;
                DebugLog($"LoadInventoryData: Snapshot alındı. Exists: {snapshotExists}");
            } else if (task.IsFaulted) {
                 DebugLogWarning("LoadInventoryData: Firebase task hatalı olduğu için snapshot alınamadı. Snapshot 'yok' varsayılıyor.");
                 snapshotExists = false; // Hata durumunda snapshot yok say
            }


            // if (snapshot.Exists) // Eski kontrol
            if (snapshotExists) // Yeni kontrol
            {
                 DebugLog("LoadInventoryData: Firebase snapshot VAR. Veri işleniyor...");
                 // Mevcut item temizleme ve Firebase'den yükleme mantığı... (içeriği aynı kalmalı)
                string json = snapshot.GetRawJsonValue();
                InventoryData inventoryData = null; 
                  try { // JSON Parse kısmı
                      // Önce JObject olarak parse et
                      JObject jsonObj = JObject.Parse(json);
                      int firebaseVersion = jsonObj["version"] != null ? (int)jsonObj["version"] : 0;

                      // Versiyonları karşılaştır
                      if (loadedFromLocal && firebaseVersion <= _version)
                      {
                          DebugLog($"LoadInventoryData: Yerel versiyon daha yeni veya eşit (Local:v{_version}, Firebase:v{firebaseVersion}), Firebase verileri atlanıyor");
                          // Firebase verisini işlemeye gerek yok, korutinden çıkabiliriz veya UI güncelleme adımına geçebiliriz.
                          // Şimdilik devam edip OnInventoryLoaded'ı çağıralım.
                      }
                      else
                      {
                          // Firebase daha yeni veya yerel veri yok, Firebase verisini işle
                          long firebaseTimestamp = jsonObj["timestamp"] != null ? (long)jsonObj["timestamp"] : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                          var firebaseItems = new Dictionary<string, InventoryItemData>();

                          if (jsonObj["items"] != null && jsonObj["items"].Type == JTokenType.Array)
                          {
                              DebugLog("LoadInventoryData: Firebase 'items' alanı dizi olarak algılandı, Dictionary'ye dönüştürülüyor...");
                              int index = 0;
                              foreach (var itemToken in jsonObj["items"])
                 {
                                  if (index >= inventorySize) {
                                      DebugLogWarning($"Firebase array item index {index} exceeds inventory size {inventorySize}. Skipping remaining array items.");
                                      break;
                                  }
                                  if (itemToken != null && itemToken.Type != JTokenType.Null)
                                  {
                                      try {
                                          var itemData = itemToken.ToObject<InventoryItemData>();
                                          if (itemData != null && !string.IsNullOrEmpty(itemData.itemId))
                                          {
                                              firebaseItems[index.ToString()] = itemData;
                                          } else {
                                              DebugLogWarning($"Firebase array item at index {index} could not be parsed correctly or has null/empty itemId.");
                                          }
                                      } catch (Exception arrayItemEx) {
                                          DebugLogError($"Firebase array item at index {index} çözümleme hatası: {arrayItemEx.Message}");
                                      }
                                  }
                                  index++;
                              }
                          }
                          else if (jsonObj["items"] != null && jsonObj["items"].Type == JTokenType.Object)
                          {
                              DebugLog("LoadInventoryData: Firebase 'items' alanı nesne olarak algılandı, doğrudan parse ediliyor...");
                              try{
                                  firebaseItems = jsonObj["items"].ToObject<Dictionary<string, InventoryItemData>>();
                              } catch (Exception objectParseException) {
                                  DebugLogError($"Firebase object 'items' çözümleme hatası: {objectParseException.Message}");
                                  firebaseItems = new Dictionary<string, InventoryItemData>(); // Hata durumunda boşalt
                              }
                          }
                          else {
                              DebugLog("LoadInventoryData: Firebase 'items' alanı bulunamadı veya desteklenmeyen tipte.");
                              // firebaseItems zaten boş olarak başlatıldı.
                          }

                          // InventoryData nesnesini oluştur
                          inventoryData = new InventoryData
                          {
                              version = firebaseVersion,
                              timestamp = firebaseTimestamp,
                              items = firebaseItems ?? new Dictionary<string, InventoryItemData>()
                          };

                          // --- Veriyi Gerçek Envantere Yükle ---
                        DebugLog("LoadInventoryData: Firebase verisi yükleniyor, mevcut envanter temizleniyor...");
                        _items.Clear();
                          // UI Slotlarını temizle
                          foreach (var slot in _slotObjects) {
                              if(slot != null) {
                                  var slotUI = slot.GetComponent<InventorySlotUI>();
                                  if (slotUI != null) slotUI.UpdateUI(null);
                              }
                          }

                          // Itemları yükle
                          int loadedCount = 0;
                          if (inventoryData.items != null) {
                              foreach (var kvp in inventoryData.items) {
                                  if (int.TryParse(kvp.Key, out int slotId) && slotId >= 0 && slotId < inventorySize) {
                                      var itemLoadData = kvp.Value;
                                      if (itemLoadData != null) {
                                          var itemDb = ItemDatabase.Instance?.GetItemById(itemLoadData.itemId);
                                          if (itemDb != null) {
                                              var newItem = new InventoryItem(itemDb, itemLoadData.amount);
                                              _items[slotId] = newItem;
                                              UpdateSlotUI(slotId, newItem);
                                              loadedCount++;
                                          } else { DebugLogWarning($"LoadInventoryData: ItemDatabase'de item bulunamadı: ID={itemLoadData.itemId}"); }
                                      }
                                  } else { DebugLogWarning($"LoadInventoryData: Geçersiz slot ID veya format: Key='{kvp.Key}'"); }
                              }
                          }
                          _version = inventoryData.version; // Yerel versiyonu güncelle
                          DebugLog($"LoadInventoryData: Firebase'den {loadedCount} item yüklendi. Yeni versiyon: {_version}");
                          // Düzeltilmiş/yeni veriyi yerel depolamaya kaydet
                          DebugLog("LoadInventoryData: Firebase verisi işlendiği için yerel depolama güncelleniyor...");
                        SaveInventoryToLocalStorage();
                    }
                  } catch (Exception ex) {
                      DebugLogError($"Firebase JSON genel çözümleme veya işleme hatası: {ex.Message}");
                      // yield break; // Burada çıkma, yerel veriyle devam edebilir
                 }
            }
            else // Snapshot yoksa veya hatalıysa
            {
                DebugLog("LoadInventoryData: Firebase snapshot YOK veya hatalı.");
                // İlk kez giriş yapıyorsa başlangıç itemlerini ekle
                if (!loadedFromLocal)
                {
                    DebugLog("LoadInventoryData: Yerel veri de bulunamadı -> YENİ OYUNCU. AddStartingItems çağrılıyor...");
                    AddStartingItems(); // <- Başlangıç itemleri burada ekleniyor
                }
                else
                {
                    // Yerel veri var ama Firebase yoksa, yerel veriyi Firebase'e yükle
                    DebugLog("LoadInventoryData: Yerel veri var, Firebase yok. Yerel veri Firebase'e kaydediliyor...");
                    SaveInventory(); // SaveInventory zaten yerel kaydı da yapar.
                }
            }
        }
        catch (System.Exception e)
        {
            DebugLogError($"LoadInventoryData genel hatası (Coroutine sonu): {e.Message}");
            DebugLogError($"Stack Trace: {e.StackTrace}"); // Log stack trace separately
        }

        // Envanter yüklendi olayını tetikle (Başarılı veya başarısız farketmez, UI güncellenmeli)
        DebugLog("LoadInventoryData: OnInventoryLoaded olayı tetikleniyor.");
        OnInventoryLoaded?.Invoke();
        DebugLog("LoadInventoryData tamamlandı.");
    }

    private void AddStartingItems()
    {
        if (string.IsNullOrEmpty(_userId)) {
            DebugLogError("AddStartingItems çağrıldı ancak _userId boş!");
            return;
        }
        
        DebugLog("AddStartingItems: Yeni oyuncu (Firebase/Yerel veri yok). Başlangıç itemleri ekleniyor...");
        
        try
        {
            // ItemDatabase null kontrolü
            if (ItemDatabase.Instance == null) {
                 DebugLogError("AddStartingItems: ItemDatabase.Instance is null! Items cannot be added.");
                 return;
            }
        
            int addedItemCount = 0;
            
            // Wooden Sword'u ID'ye göre bul (daha güvenilir)
            string swordId = "1"; // ID'yi değişkene alalım
            DebugLog($"AddStartingItems: Item aranıyor: ID={swordId}");
            var woodenSword = ItemDatabase.Instance.GetItemById(swordId); // Wooden_Sword ID'si
            
            if (woodenSword != null)
            {
                 // ... (Mevcut item ekleme ve loglama kodları) ...
                DebugLog($"AddStartingItems: Item bulundu ve eklendi: {woodenSword.ItemName}");

                // --- EKSİK KOD EKLENDİ --- 
                var swordItem = new InventoryItem(woodenSword, 1); // Kılıç için InventoryItem oluştur
                _items[0] = swordItem; // Slot 0'a ekle
                UpdateSlotUI(0, swordItem); // Slot 0 UI'ını güncelle
                // ------------------------- 

                 addedItemCount++;
            }
            else
            {
                 DebugLogError($"AddStartingItems: Item bulunamadı! ID={swordId}");
                // Alternatif arama... (Bu kısım aynı kalabilir)
            }
            
            // Deri zırh setini ekleme
             DebugLog("AddStartingItems: Deri zırh seti ekleniyor...");
            AddLeatherArmorSet(ref addedItemCount);
            
            // Veritabanına kaydet
            if (addedItemCount > 0)
            {
                DebugLog($"AddStartingItems: Toplam {addedItemCount} item eklendi. Kaydediliyor...");
                SaveInventory(); // Hem yerel hem Firebase'e kaydeder
                EquipmentManager.Instance?.SaveEquipmentManual(); // Ekipmanı da kaydet
                DebugLog($"AddStartingItems: Envanter ve Ekipman kaydedildi.");
                
                // Oyuncunun başlatıldığını kaydet - BU DA KALDIRILABİLİR VEYA YORUMA ALINABİLİR
                // PlayerPrefs.SetInt(playerInitKey, 1);
                // PlayerPrefs.Save(); // Save çağrısı önemli
                // DebugLog("AddStartingItems: PlayerPrefs bayrağı kaydedildi.");
                
                // UI Güncelleme (Initialize sonunda zaten yapılıyor ama burada da yapılabilir)
                 /* DebugLog("AddStartingItems: UI güncelleniyor...");
                 for (int i = 0; i < inventorySize; i++) {
                     UpdateSlotUI(i, GetItemAt(i));
                 }*/
            }
            else
            {
                DebugLogWarning("AddStartingItems: Hiçbir başlangıç itemi eklenemedi!");
            }
        }
        catch (System.Exception e)
        {
            DebugLogError($"Başlangıç itemleri eklenirken hata: {e.Message}\n{e.StackTrace}");
        }
        DebugLog("AddStartingItems tamamlandı.");
    }
    
    private void AddLeatherArmorSet(ref int addedItemCount)
    {
        // Leather Helmet
        AddStartingEquipment("100", "Items/Armours/Leather/Base/Leather_Helmet", SlotType.Helmet, 1, ref addedItemCount, false);
        
        // Leather Chestplate
        AddStartingEquipment("200", "Items/Armours/Leather/Base/Leather_Chestplate", SlotType.Chestplate, 2, ref addedItemCount, false);
        
        // Leather Leggings
        AddStartingEquipment("300", "Items/Armours/Leather/Base/Leather_Leggings", SlotType.Leggings, 3, ref addedItemCount, false);
        
        // Leather Boots
        AddStartingEquipment("400", "Items/Armours/Leather/Base/Leather_Boots", SlotType.Boots, 4, ref addedItemCount, false);
        
        // Leather Ring
        AddStartingEquipment("500", "Items/Jewelery/Rings/Leather_Ring", SlotType.Ring, 5, ref addedItemCount, false);
        
        // Leather Necklace
        AddStartingEquipment("600", "Items/Jewelery/Necklaces/Leather_Necklace", SlotType.Necklace, 6, ref addedItemCount, false);
    }
    
    private void AddStartingEquipment(string itemId, string resourcePath, SlotType slotType, int targetSlot, ref int addedItemCount, bool equipItem = true)
    {
        var item = ItemDatabase.Instance.GetItemById(itemId);
        
        if (item != null)
        {
            // Icon kontrolü
            if (item.icon == null)
            {
                DebugLogWarning($"{item.itemName} icon'u null! Resources'dan yüklemeyi deniyorum...");
                
                // Resources'dan tekrar yüklemeyi dene
                var loadedResource = Resources.Load<ItemData>(resourcePath);
                if (loadedResource != null && loadedResource.icon != null)
                {
                    item = loadedResource;
                    DebugLog($"{item.itemName} Resources'dan başarıyla yüklendi. Icon: {item.icon != null}");
                }
                else
                {
                    DebugLogWarning($"{resourcePath} Resources'dan da yüklenemedi!");
                }
            }
            
            DebugLog($"{item.itemName} bulundu: ID={item.itemId}, Name={item.itemName}, Icon={item.icon != null}");
            
            // Item'ı ekle
            var inventoryItem = new InventoryItem(item, 1);
            
            // Önce _items Dictionary'sine manuel olarak ekleyelim
            _items[targetSlot] = inventoryItem;
            
            // UI'ı manuel olarak güncelleyelim
            if (_slotObjects != null && _slotObjects.Count > targetSlot)
            {
                var slotUI = _slotObjects[targetSlot].GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    // Envanter UI'ını görünür yap
                    if (inventoryUIParent != null)
                    {
                        inventoryUIParent.SetActive(true);
                    }
                    
                    slotUI.UpdateUI(inventoryItem);
                    DebugLog($"Slot {targetSlot} UI'ı manuel olarak güncellendi. Item: {inventoryItem.ItemName}, Icon: {inventoryItem.ItemIcon != null}");
                }
                else
                {
                    DebugLogWarning($"Slot {targetSlot} için InventorySlotUI bileşeni bulunamadı!");
                }
            }
            else
            {
                DebugLogWarning($"_slotObjects null veya slot {targetSlot} mevcut değil! Count: {(_slotObjects != null ? _slotObjects.Count.ToString() : "null")}");
            }
            
            addedItemCount++;
            DebugLog($"Başlangıç itemi eklendi: {item.itemName} (ID: {item.itemId})");
            
            // Ekipman olarak giydir (eğer isteniyorsa)
            if (equipItem)
            {
                EquipmentManager equipmentManager = FindObjectOfType<EquipmentManager>();
                if (equipmentManager != null)
                {
                    bool equipped = equipmentManager.EquipItem(inventoryItem, slotType, false);
                    if (equipped)
                    {
                        DebugLog($"Başlangıç itemi ekipman olarak giydirildi: {item.itemName}");
                    }
                    else
                    {
                        DebugLogWarning($"{item.itemName} ekipman olarak giydirilirken hata oluştu");
                    }
                }
            }
        }
        else
        {
            // Eğer ID ile bulunamazsa, tip ile dene
            DebugLogWarning($"Item (ID: {itemId}) bulunamadı, {slotType} tipindeki itemları deniyorum...");
            
            var itemsOfType = ItemDatabase.Instance.GetItemsByType(slotType);
            if (itemsOfType != null && itemsOfType.Count > 0)
            {
                DebugLog($"Alternatif item bulundu: {itemsOfType[0].itemName} (ID: {itemsOfType[0].itemId})");
                
                // İlk uygun itemi ekle
                var inventoryItem = new InventoryItem(itemsOfType[0], 1);
                
                // Önce _items Dictionary'sine manuel olarak ekleyelim
                _items[targetSlot] = inventoryItem;
                
                // UI'ı manuel olarak güncelleyelim
                if (_slotObjects != null && _slotObjects.Count > targetSlot)
                {
                    var slotUI = _slotObjects[targetSlot].GetComponent<InventorySlotUI>();
                    if (slotUI != null)
                    {
                        slotUI.UpdateUI(inventoryItem);
                        DebugLog($"Slot {targetSlot} UI'ı manuel olarak güncellendi. Item: {inventoryItem.ItemName}");
                    }
                    else
                    {
                        DebugLogWarning($"Slot {targetSlot} için InventorySlotUI bileşeni bulunamadı!");
                    }
                }
                else
                {
                    DebugLogWarning($"_slotObjects null veya slot {targetSlot} mevcut değil! Count: {(_slotObjects != null ? _slotObjects.Count.ToString() : "null")}");
                }
                
                addedItemCount++;
                DebugLog($"Alternatif başlangıç itemi eklendi: {itemsOfType[0].itemName} (ID: {itemsOfType[0].itemId})");
                
                // Ekipman olarak giydir (eğer isteniyorsa)
                if (equipItem)
                {
                    EquipmentManager equipmentManager = FindObjectOfType<EquipmentManager>();
                    if (equipmentManager != null)
                    {
                        bool equipped = equipmentManager.EquipItem(inventoryItem, slotType, false);
                        if (equipped)
                        {
                            DebugLog($"Alternatif başlangıç itemi ekipman olarak giydirildi: {itemsOfType[0].itemName}");
                        }
                        else
                        {
                            DebugLogWarning($"Alternatif başlangıç itemi ekipman olarak giydirilirken hata oluştu");
                        }
                    }
                }
            }
            else
            {
                DebugLogError($"Hiçbir {slotType} tipi item bulunamadı! ItemDatabase doğru şekilde başlatıldı mı?");
            }
        }
    }

    private bool LoadInventoryFromLocalStorage()
    {
        try
        {
            string path = GetLocalStoragePath();
            if (!File.Exists(path))
            {
                return false;
            }

            string json = File.ReadAllText(path);
            
            // JSON formatını kontrol et ve uygun şekilde çözümle
            var inventoryData = new InventoryData();
            
            try 
            {
                // Önce JSON'ı JObject olarak çözümle
                JObject jsonObj = JObject.Parse(json);
                
                // items alanının tipini kontrol et
                if (jsonObj["items"] != null)
                {
                    // Eğer items bir dizi ise, Dictionary'ye dönüştür
                    if (jsonObj["items"].Type == JTokenType.Array)
                    {
                        DebugLog("JSON'daki items bir dizi olarak algılandı, Dictionary'ye dönüştürülüyor...");
                        
                        var fixedItemsDict = new Dictionary<string, InventoryItemData>();
                        int index = 0;
                        
                        foreach (var item in jsonObj["items"])
                        {
                            if (item != null)
                            {
                                // item yapısını çözümle
                                string itemId = item["itemId"] != null ? item["itemId"].ToString() : "";
                                int amount = item["amount"] != null ? (int)item["amount"] : 1;
                                
                                if (!string.IsNullOrEmpty(itemId))
                                {
                                    fixedItemsDict[index.ToString()] = new InventoryItemData 
                                    { 
                                        itemId = itemId, 
                                        amount = amount 
                                    };
                                    index++;
                                }
                            }
                        }
                        
                        // Yeni veri yapısı oluştur
                        inventoryData = new InventoryData
                        {
                            version = jsonObj["version"] != null ? (int)jsonObj["version"] : 0,
                            timestamp = jsonObj["timestamp"] != null ? (long)jsonObj["timestamp"] : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            items = fixedItemsDict
                        };
                        
                        // Düzeltilmiş veriyi kaydet
                        SaveInventoryToLocalStorage();
                    }
                    else
                    {
                        // Normal şekilde deserialize et
                        inventoryData = JsonConvert.DeserializeObject<InventoryData>(json);
                    }
                }
                else
                {
                    // items alanı yoksa boş bir InventoryData oluştur
                    inventoryData = new InventoryData
                    {
                        version = jsonObj["version"] != null ? (int)jsonObj["version"] : 0,
                        timestamp = jsonObj["timestamp"] != null ? (long)jsonObj["timestamp"] : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        items = new Dictionary<string, InventoryItemData>()
                    };
                }
            }
            catch (Exception ex)
            {
                DebugLogError($"JSON çözümleme hatası: {ex.Message}");
                return false;
            }

            if (inventoryData == null || inventoryData.items == null)
            {
                return false;
            }

            // Mevcut envanteri temizle
            _items.Clear();
            foreach (var slot in _slotObjects)
            {
                var slotUI = slot.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.UpdateUI(null);
                }
            }

            // Verileri yükle
            int loadedCount = 0;
            foreach (var kvp in inventoryData.items)
            {
                if (int.TryParse(kvp.Key, out int slotId) && slotId >= 0 && slotId < inventorySize)
                {
                    var itemData = kvp.Value;
                    var itemDb = ItemDatabase.Instance.GetItemById(itemData.itemId);
                    
                    if (itemDb != null)
                    {
                        var newItem = new InventoryItem(itemDb, itemData.amount);
                        _items[slotId] = newItem;
                        
                        UpdateSlotUI(slotId, newItem);
                        loadedCount++;
                    }
                }
            }

            _version = inventoryData.version;
            DebugLog($"Yerel depolamadan {loadedCount} item yüklendi (Versiyon: {_version})");
            
            return loadedCount > 0;
        }
        catch (System.Exception e)
        {
            DebugLogError($"Yerel depolamadan yükleme hatası: {e.Message}");
            return false;
        }
    }

    private void SaveInventoryToLocalStorage()
    {
        try
        {
            var inventoryData = new InventoryData
            {
                version = _version,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                items = new Dictionary<string, InventoryItemData>()
            };

            // Itemları ekle
            foreach (var kvp in _items)
            {
                if (kvp.Value != null)
                {
                    inventoryData.items.Add(kvp.Key.ToString(), new InventoryItemData
                    {
                        itemId = kvp.Value.ItemId,
                        amount = kvp.Value.Amount
                    });
                }
            }

            string json = JsonConvert.SerializeObject(inventoryData);
            File.WriteAllText(GetLocalStoragePath(), json);
            
            DebugLog($"Envanter yerel depolamaya kaydedildi ({inventoryData.items.Count} item)");
        }
        catch (System.Exception e)
        {
            DebugLogError($"Yerel depolamaya kayıt hatası: {e.Message}");
        }
    }

    private string GetLocalStoragePath()
    {
        return Path.Combine(Application.persistentDataPath, $"inventory_{_userId}.json");
    }

    /// <summary>
    /// Tüm slotlardaki item icon'ları zorla aktif eder
    /// </summary>
    private void ForceActivateItemIcons()
    {
        DebugLog("Tüm item icon'ları zorla aktif ediliyor...");
        
        if (_slotObjects == null || _slotObjects.Count == 0)
        {
            DebugLogWarning("Slot objeleri bulunamadı!");
            return;
        }
        
        foreach (var slotObj in _slotObjects)
        {
            if (slotObj == null) continue;
            
            var slotUI = slotObj.GetComponent<InventorySlotUI>();
            if (slotUI != null && slotUI.CurrentItem != null)
            {
                // Item icon referansını al
                var iconTransform = slotObj.transform.Find("ItemIcon");
                if (iconTransform != null)
                {
                    iconTransform.gameObject.SetActive(true);
                    DebugLog($"Slot {slotUI.SlotIndex} için item icon zorla aktif edildi");
                }
            }
        }
    }

    /// <summary>
    /// Tüm slotlardaki item icon'ları zorla aktif eder (public metot)
    /// </summary>
    public void ForceActivateAllItemIcons()
    {
        ForceActivateItemIcons();
    }

    #endregion

    #region Debug Methods

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Inventory] {message}");
        }
    }

    private void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[Inventory] {message}");
        }
    }

    private void DebugLogError(string message)
    {
        Debug.LogError($"[Inventory] {message}");
    }

    #endregion
}

/// <summary>
/// Envanter verilerini tutmak için sınıf
/// </summary>
[System.Serializable]
public class InventoryData
{
    public int version;
    public long timestamp;
    public Dictionary<string, InventoryItemData> items;
}

/// <summary>
/// Item verilerini tutmak için sınıf
/// </summary>
[System.Serializable]
public class InventoryItemData
{
    public string itemId;
    public int amount;
} 
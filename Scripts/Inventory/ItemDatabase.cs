using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Item veritabanı - Tüm itemları içeren ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/Item Database")]
public class ItemDatabase : ScriptableObject
{
    #region Singleton

    private static ItemDatabase _instance;
    public static ItemDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ItemDatabaseInitializer>()?.Database;
                
                if (_instance == null)
                {
                    Debug.LogError("ItemDatabase bulunamadı! Lütfen bir ItemDatabaseInitializer ekleyin.");
                }
            }
            return _instance;
        }
    }

    #endregion

    [Header("Elle Eklenecek Itemlar")]
    [SerializeField] private List<ItemData> items = new List<ItemData>();
    
    [Header("Ayarlar")]
    [SerializeField] private bool logItemsOnLoad = true;
    
    private Dictionary<string, ItemData> _itemsById = new Dictionary<string, ItemData>();
    private Dictionary<SlotType, List<ItemData>> _itemsByType = new Dictionary<SlotType, List<ItemData>>();
    private bool _isInitialized = false;

    private void OnEnable()
    {
        InitializeDatabase();
    }

    public void InitializeDatabase()
    {
        if (_isInitialized)
        {
            // Debug.Log("ItemDatabase zaten başlatılmış, tekrar başlatılmıyor."); // Bu logu azaltabiliriz.
            return;
        }
        
        Debug.Log("ItemDatabase başlatılıyor...");
        
        _itemsById.Clear();
        _itemsByType.Clear();
        
        // Kullanılacak tüm itemları toplamak için geçici bir liste
        List<ItemData> allProcessableItems = new List<ItemData>();

        // 1. Inspector'dan eklenen itemları al (items listesi çalışma zamanında değişmeyecek)
        if (items != null)
        {
            Debug.Log($"Inspector'dan {items.Count} item işleniyor.");
            allProcessableItems.AddRange(items);
        }

        // 2. Resources klasöründen tüm ItemData varlıklarını yükle
        ItemData[] resourceItems = Resources.LoadAll<ItemData>("Items"); // "Items/Weapons" yerine "Items" genel klasörünü kullanalım.
        if (resourceItems != null && resourceItems.Length > 0)
        {
            Debug.Log($"Resources/Items klasöründen {resourceItems.Length} item yüklendi.");
            allProcessableItems.AddRange(resourceItems);
        }
        else
        {
            Debug.LogWarning("Resources/Items klasöründen hiç item yüklenemedi!");
        }
        
        // Ek olarak "Items/Weapons" klasörünü de kontrol edelim (eski yapıya uyumluluk için)
        ItemData[] weaponItems = Resources.LoadAll<ItemData>("Items/Weapons");
        if (weaponItems != null && weaponItems.Length > 0)
        {
            Debug.Log($"Resources/Items/Weapons klasöründen {weaponItems.Length} item yüklendi.");
            allProcessableItems.AddRange(weaponItems);
        }
        // Gerekirse diğer alt klasörler için de benzer bloklar eklenebilir.

        // Tüm itemları (Inspector + Resources) ID'lerine göre tekilleştirerek dictionary'lere ekle
        HashSet<string> processedItemIds = new HashSet<string>();

        foreach (var item in allProcessableItems)
        {
            if (item != null)
            {
                if (string.IsNullOrEmpty(item.itemId))
                {
                    Debug.LogError($"Hata: {item.name} item'ının ID'si boş! Bu item atlanacak.");
                    continue;
                }

                if (processedItemIds.Contains(item.itemId))
                {
                    // Bu ID zaten işlendi, muhtemelen Inspector ve Resources'da aynı item var.
                    // Veya Resources alt klasörlerinde duplicate ID'li itemlar var.
                    // Debug.LogWarning($"Duplicate Item ID: {item.itemId} ({item.name}). Önceden yüklenmiş olan kullanılacak.");
                    continue;
                }
                
                // Debug.Log($"Item yükleniyor: ID={item.itemId}, Name={item.itemName}, Type={item.itemType}");
                
                _itemsById[item.itemId] = item;
                processedItemIds.Add(item.itemId);
                
                SlotType itemType = item.itemType;
                if (!_itemsByType.ContainsKey(itemType))
                {
                    _itemsByType[itemType] = new List<ItemData>();
                }
                _itemsByType[itemType].Add(item);
            }
            else
            {
                Debug.LogWarning("Null item referansı tespit edildi (allProcessableItems içinde)!");
            }
        }
        
        _isInitialized = true;
        
        if (logItemsOnLoad)
        {
            Debug.Log($"{_itemsById.Count} benzersiz item veritabanına yüklendi. ID'ler: {string.Join(", ", _itemsById.Keys)}");
        }
    }

    /// <summary>
    /// ID'ye göre item verisini getirir
    /// </summary>
    public ItemData GetItemById(string id)
    {
        if (!_isInitialized) 
        {
            Debug.Log($"ItemDatabase henüz başlatılmamış, başlatılıyor... (GetItemById: {id})");
            InitializeDatabase();
        }
        
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("GetItemById'ye boş ID gönderildi!");
            return null;
        }

        if (_itemsById.TryGetValue(id, out ItemData itemData))
        {
            // Debug.Log($"Item bulundu: ID={itemData.itemId}, Name={itemData.itemName}, Type={itemData.itemType}"); // Bu logu kaldırıyoruz veya yorum satırı yapıyoruz
            return itemData;
        }

        // Item bulunamadı, daha detaylı log
        Debug.LogWarning($"Item bulunamadı: {id}");
        Debug.LogWarning($"Mevcut itemlar: {string.Join(", ", _itemsById.Keys)}");
        
        // Resources klasöründen tekrar yüklemeyi dene
        ItemData resourceItem = Resources.Load<ItemData>($"Items/Weapons/Wooden_Sword");
        if (resourceItem != null && resourceItem.itemId == id)
        {
            Debug.Log($"Item Resources klasöründen yüklendi: ID={id}, Name={resourceItem.itemName}");
            
            // Veritabanına ekle
            _itemsById[id] = resourceItem;
            
            if (!_itemsByType.ContainsKey(resourceItem.itemType))
            {
                _itemsByType[resourceItem.itemType] = new List<ItemData>();
            }
            _itemsByType[resourceItem.itemType].Add(resourceItem);
            
            return resourceItem;
        }
        
        return null;
    }

    /// <summary>
    /// İsme göre item verisini getirir
    /// </summary>
    public ItemData GetItemByName(string itemName)
    {
        if (!_isInitialized) InitializeDatabase();
        
        if (string.IsNullOrEmpty(itemName))
        {
            return null;
        }

        foreach (var item in items)
        {
            if (item != null && item.itemName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Item türüne göre tüm itemları getirir
    /// </summary>
    public List<ItemData> GetItemsByType(SlotType type)
    {
        if (!_isInitialized) InitializeDatabase();
        
        if (_itemsByType.TryGetValue(type, out List<ItemData> typeItems))
        {
            return new List<ItemData>(typeItems);
        }
        
        return new List<ItemData>();
    }

    /// <summary>
    /// Rastgele bir item getirir
    /// </summary>
    public ItemData GetRandomItem(SlotType? type = null)
    {
        if (!_isInitialized) InitializeDatabase();
        
        List<ItemData> itemPool;
        
        if (type.HasValue && _itemsByType.TryGetValue(type.Value, out List<ItemData> typeItems))
        {
            itemPool = typeItems;
        }
        else
        {
            itemPool = new List<ItemData>(items);
        }
        
        if (itemPool.Count == 0)
        {
            return null;
        }
        
        int randomIndex = Random.Range(0, itemPool.Count);
        return itemPool[randomIndex];
    }

    /// <summary>
    /// Tüm itemları döndürür
    /// </summary>
    public List<ItemData> GetAllItems()
    {
        if (!_isInitialized) 
        {
            Debug.Log("ItemDatabase henüz başlatılmamış, GetAllItems için başlatılıyor...");
            InitializeDatabase();
        }
        
        return new List<ItemData>(_itemsById.Values); // Artık _itemsById.Values'dan alıyoruz.
    }

    // Editor'da düzenleme yapabilmek için
    #if UNITY_EDITOR
    public void AddItem(ItemData item)
    {
        if (item != null && !items.Contains(item))
        {
            items.Add(item);
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
    
    public void RemoveItem(ItemData item)
    {
        if (item != null && items.Contains(item))
        {
            items.Remove(item);
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
    #endif
} 
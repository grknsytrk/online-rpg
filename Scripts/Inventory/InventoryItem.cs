using System;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Envanterda yer alan bir öğeyi temsil eden sınıf.
/// Firebase ve Photon üzerinden serileştirilebilir.
/// </summary>
[Serializable]
public class InventoryItem
{
    #region Item Properties

    [SerializeField] private string itemId;
    [SerializeField] private string itemName;
    [SerializeField] private string description;
    [SerializeField] private int amount;
    [SerializeField] private int maxStackSize;
    [SerializeField] private SlotType itemType;
    [SerializeField] private bool isStackable;
    [SerializeField] private float itemValue;

    #endregion

    #region Constructor

    /// <summary>
    /// Boş bir item oluşturur (serileştirme için)
    /// </summary>
    public InventoryItem() 
    {
        itemId = string.Empty;
        itemName = string.Empty;
        description = string.Empty;
        amount = 0;
        maxStackSize = 1;
        itemType = SlotType.None;
        isStackable = false;
        itemValue = 0;
    }

    /// <summary>
    /// ItemData'dan bir item oluşturur
    /// </summary>
    /// <param name="data">Item verileri</param>
    /// <param name="amount">Başlangıç miktarı</param>
    public InventoryItem(ItemData data, int amount = 1)
    {
        if (data == null)
        {
            Debug.LogError("Null ItemData ile InventoryItem oluşturulamaz!");
            return;
        }

        this.itemId = data.ItemId;
        this.itemName = data.ItemName;
        this.description = data.Description;
        this.amount = amount;
        this.maxStackSize = data.MaxStackSize;
        this.itemType = data.ItemType;
        this.isStackable = data.MaxStackSize > 1; // MaxStackSize > 1 ise yığınlanabilir
        this.itemValue = data.Value; // Value özelliğini kullanıyoruz
    }

    /// <summary>
    /// Tüm özellikleri manuel olarak belirterek bir item oluşturur
    /// </summary>
    public InventoryItem(string itemId, string itemName, string description, int amount, 
                         int maxStackSize, SlotType itemType, bool isStackable, float itemValue)
    {
        this.itemId = itemId;
        this.itemName = itemName;
        this.description = description;
        this.amount = amount;
        this.maxStackSize = maxStackSize;
        this.itemType = itemType;
        this.isStackable = isStackable;
        this.itemValue = itemValue;
    }

    /// <summary>
    /// Mevcut bir item'ın kopyasını oluşturur
    /// </summary>
    /// <param name="original">Kopyalanacak item</param>
    public InventoryItem(InventoryItem original)
    {
        if (original == null)
        {
            Debug.LogError("Null InventoryItem kopyalanamaz!");
            return;
        }

        this.itemId = original.itemId;
        this.itemName = original.itemName;
        this.description = original.description;
        this.amount = original.amount;
        this.maxStackSize = original.maxStackSize;
        this.itemType = original.itemType;
        this.isStackable = original.isStackable;
        this.itemValue = original.itemValue;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Item'ın benzersiz ID'si
    /// </summary>
    public string ItemId => itemId;

    /// <summary>
    /// Item'ın gösterim adı
    /// </summary>
    public string ItemName => itemName;

    /// <summary>
    /// Item'ın açıklaması
    /// </summary>
    public string Description => description;

    /// <summary>
    /// Item'ın miktarı
    /// </summary>
    public int Amount 
    { 
        get => amount; 
        set => amount = Mathf.Clamp(value, 0, maxStackSize); 
    }

    /// <summary>
    /// Bir slotta bulunabilecek maksimum item sayısı
    /// </summary>
    public int MaxStackSize => maxStackSize;

    /// <summary>
    /// Item'ın tipi/kategorisi
    /// </summary>
    public SlotType ItemType => itemType;

    /// <summary>
    /// Item'ın yığınlanabilir olup olmadığı
    /// </summary>
    public bool IsStackable => isStackable;

    /// <summary>
    /// Item'ın değeri (satış fiyatı, ağırlık vb.)
    /// </summary>
    public float ItemValue => itemValue;

    /// <summary>
    /// Item'ın hasar değeri (ItemDatabase'den alınır)
    /// </summary>
    public int Damage 
    { 
        get 
        {
            var itemData = ItemDatabase.Instance?.GetItemById(itemId);
            if (itemData != null)
            {
                return itemData.Damage;
            }
            return 0;
        }
    }

    /// <summary>
    /// Item'ın savunma değeri (ItemDatabase'den alınır)
    /// </summary>
    public int Defense 
    { 
        get 
        {
            var itemData = ItemDatabase.Instance?.GetItemById(itemId);
            if (itemData != null)
            {
                return itemData.Defense;
            }
            return 0;
        }
    }

    /// <summary>
    /// Item'ın can yenileme miktarı (ItemDatabase'den alınır)
    /// </summary>
    public int HealAmount
    {
        get
        {
            var itemData = ItemDatabase.Instance?.GetItemById(itemId);
            if (itemData != null)
            {
                return itemData.HealAmount;
            }
            return 0;
        }
    }

    /// <summary>
    /// Item'ın gösterim ikonu (ItemDatabase'den alınır)
    /// </summary>
    public Sprite ItemIcon 
    { 
        get 
        {
            var itemData = ItemDatabase.Instance?.GetItemById(itemId);
            if (itemData != null && itemData.Icon != null)
            {
                return itemData.Icon;
            }
            else
            {
                // Eğer icon bulunamazsa, varsayılan bir sprite döndür
                Debug.LogWarning($"Item için icon bulunamadı: ID={itemId}, Name={itemName}");
                
                // Resources klasöründen varsayılan bir sprite yüklemeyi dene
                Sprite defaultIcon = Resources.Load<Sprite>("DefaultItemIcon");
                if (defaultIcon != null)
                {
                    return defaultIcon;
                }
                
                // Eğer varsayılan sprite de bulunamazsa, null döndür
                // UpdateUI metodu bunu ele alacak
                return null;
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Item yığınına belirtilen miktarda item ekler
    /// </summary>
    /// <param name="amountToAdd">Eklenecek miktar</param>
    /// <returns>Eklenemeyen fazla miktarı döndürür</returns>
    public int AddToStack(int amountToAdd)
    {
        if (!isStackable || amountToAdd <= 0)
        {
            return amountToAdd;
        }

        int currentAmount = amount;
        int totalAmount = currentAmount + amountToAdd;
        int overflow = Mathf.Max(0, totalAmount - maxStackSize);
        amount = Mathf.Min(totalAmount, maxStackSize);

        return overflow;
    }

    /// <summary>
    /// Item yığınından belirtilen miktarda item çıkarır
    /// </summary>
    /// <param name="amountToRemove">Çıkarılacak miktar</param>
    /// <returns>Gerçekte çıkarılan miktar</returns>
    public int RemoveFromStack(int amountToRemove)
    {
        if (amountToRemove <= 0)
        {
            return 0;
        }

        int actualAmount = Mathf.Min(amountToRemove, amount);
        amount -= actualAmount;
        return actualAmount;
    }

    /// <summary>
    /// İki item'ın aynı tür olup olmadığını kontrol eder
    /// </summary>
    /// <param name="other">Karşılaştırılacak item</param>
    /// <returns>İki item aynı türse true</returns>
    public bool IsSameItemType(InventoryItem other)
    {
        if (other == null)
        {
            return false;
        }

        return itemId == other.itemId;
    }

    /// <summary>
    /// Bu item'dan belirtilen miktarda yeni bir item oluşturur
    /// </summary>
    /// <param name="splitAmount">Ayrılacak miktar</param>
    /// <returns>Ayrılan miktar ile yeni bir item</returns>
    public InventoryItem SplitStack(int splitAmount)
    {
        if (splitAmount <= 0 || splitAmount >= amount)
        {
            Debug.LogWarning("Geçersiz ayırma miktarı!");
            return null;
        }

        InventoryItem newItem = new InventoryItem(this);
        newItem.amount = splitAmount;
        this.amount -= splitAmount;

        return newItem;
    }

    /// <summary>
    /// Item'ı Photon üzerinden serileştirmek için hazırlar
    /// </summary>
    /// <returns>Serileştirilmiş veri dizisi</returns>
    public object[] SerializeForNetwork()
    {
        return new object[]
        {
            itemId,
            itemName,
            description,
            amount,
            maxStackSize,
            (int)itemType,
            isStackable,
            itemValue
        };
    }

    /// <summary>
    /// Photon'dan gelen verilerle item'ı oluşturur
    /// </summary>
    /// <param name="data">Serileştirilmiş veri dizisi</param>
    public static InventoryItem DeserializeFromNetwork(object[] data)
    {
        if (data == null || data.Length < 8)
        {
            Debug.LogError("Eksik veri ile InventoryItem serileştirmesi yapılamaz!");
            return null;
        }

        try
        {
            string id = (string)data[0];
            string name = (string)data[1];
            string desc = (string)data[2];
            int amt = (int)data[3];
            int maxStack = (int)data[4];
            SlotType type = (SlotType)(int)data[5];
            bool stack = (bool)data[6];
            float val = Convert.ToSingle(data[7]);

            return new InventoryItem(id, name, desc, amt, maxStack, type, stack, val);
        }
        catch (Exception e)
        {
            Debug.LogError($"InventoryItem deserializasyon hatası: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Item'ın detaylı açıklamasını oluşturur
    /// </summary>
    /// <returns>İtem hakkında detaylı bilgi</returns>
    public string GetDetailedDescription()
    {
        string details = description;
        
        if (!string.IsNullOrEmpty(details))
        {
            details += "\n\n";
        }
        
        // Tip bilgisi
        details += $"<color=#A8A8A8>Tip: {GetItemTypeDisplayName(itemType)}</color>";
        
        // Para birimleri için özel bilgi
        if (itemType == SlotType.Currency)
        {
            // Para birimi itemları için özel açıklama
            if (itemId == CurrencyUtils.COPPER_COIN_ID)
            {
                details += $"\n<color=#B87333>Temel para birimi</color>";
                details += $"\n<color=#A8A8A8>99 Bakır = 1 Gümüş</color>";
            }
            else if (itemId == CurrencyUtils.SILVER_COIN_ID)
            {
                details += $"\n<color=#C0C0C0>Orta seviye para birimi</color>";
                details += $"\n<color=#A8A8A8>99 Gümüş = 1 Altın</color>";
            }
            else if (itemId == CurrencyUtils.GOLD_COIN_ID)
            {
                details += $"\n<color=#FFD700>En değerli para birimi</color>";
                details += $"\n<color=#A8A8A8>En yüksek değer</color>";
            }
        }
        else
        {
            // Normal itemlar için istatistikler
            if (Damage > 0)
            {
                details += $"\n<color=#FF6347>Hasar: {Damage}</color>"; // Kırmızımsı bir renk
            }
            if (Defense > 0)
            {
                details += $"\n<color=#1E90FF>Savunma: {Defense}</color>"; // Mavimsi bir renk
            }
            if (HealAmount > 0)
            {
                details += $"\n<color=#2E8B57>Can Yenileme: {HealAmount}</color>"; // Deniz yeşili tonu
            }
        }

        // Değer bilgisi (para birimleri dahil tüm itemlar için)
        if (itemValue > 0)
        {
            string formattedValue = CurrencyUtils.FormatCopperValue((int)itemValue);
            details += $"\n<color=#FFD700>Değer: {formattedValue}</color>";
        }
        
        return details;
    }

    /// <summary>
    /// Item tipinin görüntülenecek adını döndürür
    /// </summary>
    private string GetItemTypeDisplayName(SlotType type)
    {
        switch (type)
        {
            case SlotType.None: return "Genel";
            case SlotType.Sword: return "Kılıç";
            case SlotType.Helmet: return "Kask";
            case SlotType.Chestplate: return "Zırh";
            case SlotType.Leggings: return "Pantolon";
            case SlotType.Boots: return "Bot";
            case SlotType.Ring: return "Yüzük";
            case SlotType.Necklace: return "Kolye";
            case SlotType.Potion: return "İksir";
            case SlotType.Currency: return "Para Birimi";
            default: return "Bilinmeyen";
        }
    }

    #endregion
} 
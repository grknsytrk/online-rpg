using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Item verilerini tutan ScriptableObject sınıfı
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Temel Bilgiler")]
    /*[ReadOnly]*/ public string itemId;  // Benzersiz item ID'si (otomatik oluşturulur)
    public string itemName;  // Item adı
    [TextArea(3, 5)]
    public string description;  // Item açıklaması
    
    [Header("Görsel")]
    public Sprite icon;  // Item ikonu
    
    [Header("Özellikler")]
    public SlotType itemType = SlotType.None;  // Item tipi/ekipman slotu
    public int maxStackSize = 1;  // Maksimum stack büyüklüğü
    public bool isPurchasableByPlayer = true; // Oyuncu bu eşyayı tüccardan satın alabilir mi?
    public bool isSellableToMerchant = true; // Oyuncu bu eşyayı tüccara satabilir mi?
    
    [Header("İstatistikler")]
    public int damage;  // Saldırı gücü
    public int defense;  // Savunma gücü
    public int value;  // Altın değeri - BU ARTIK TEMEL BAKIR DEĞERİ OLACAK
    public int healAmount; // Can verme miktarı (Eklendi)

    // Kategori bazlı ID sayaçları
    private static Dictionary<SlotType, int> idCounters = new Dictionary<SlotType, int>();

    // Property'ler
    public string ItemId => itemId;
    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
    public SlotType ItemType => itemType;
    public int MaxStackSize => maxStackSize;
    public int Damage => damage;
    public int Defense => defense;
    public int Value => value; // Temel Bakır Değeri
    public int HealAmount => healAmount; // Eklendi
    
    private void OnValidate()
    {
        // Item ID'yi otomatik oluştur
        if (string.IsNullOrEmpty(itemId))
        {
            GenerateCategoryBasedId();
        }
    }
    
    /// <summary>
    /// Item tipine göre kategori bazlı ID oluşturur
    /// </summary>
    private void GenerateCategoryBasedId()
    {
        int categoryStart;
        
        // Tipine göre ID aralığı belirle
        switch (itemType)
        {
            case SlotType.Sword:
                categoryStart = 1;
                break;
            case SlotType.Helmet:
                categoryStart = 100;
                break;
            case SlotType.Chestplate:
                categoryStart = 200;
                break;
            case SlotType.Leggings:
                categoryStart = 300;
                break;
            case SlotType.Boots:
                categoryStart = 400;
                break;
            case SlotType.Ring:
                categoryStart = 500;
                break;
            case SlotType.Necklace:
                categoryStart = 600;
                break;
            case SlotType.Potion:
                categoryStart = 700;
                break;
            case SlotType.Currency:
                categoryStart = 800;
                break;
            default:
                categoryStart = 900;
                break;
        }

        // Sayaç değerini kontrol et, yoksa oluştur
        if (!idCounters.ContainsKey(itemType))
        {
            idCounters[itemType] = categoryStart;
        }
        
        // Bir sonraki kullanılabilir ID'yi al
        int nextId = idCounters[itemType];
        
        // Sayacı artır
        idCounters[itemType] = nextId + 1;
        
        // ID'yi tanımla (sadece sayı)
        itemId = nextId.ToString();
    }
} 
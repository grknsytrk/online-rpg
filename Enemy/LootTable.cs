using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Represents a single possible loot drop with its associated chance.
/// </summary>
[System.Serializable]
public class LootDrop
{
    public ItemData itemData; // The item that might drop
    // [Range(0f, 1f)] public float dropChance = 0.5f; // KALDIRILDI
    public int weight = 1; // YENİ: Eşyanın düşme ağırlığı (negatif olmamalı)
    public int minQuantity = 1; // YENİ: Bu eşyadan en az kaç adet düşeceği
    public int maxQuantity = 1; // YENİ: Bu eşyadan en fazla kaç adet düşeceği

    // Inspector'da değerlerin mantıklı kalmasını sağlamak için
    private void OnValidate()
    {
        if (weight < 0) weight = 0; // Ağırlık negatif olamaz
        if (minQuantity < 1) minQuantity = 1; // En az 1 düşmeli
        if (maxQuantity < minQuantity) maxQuantity = minQuantity;
    }
}

/// <summary>
/// ScriptableObject defining a list of possible loot drops for an enemy or container using weighted selection.
/// </summary>
[CreateAssetMenu(fileName = "New Loot Table", menuName = "Inventory/Loot Table")]
public class LootTable : ScriptableObject
{
    [Header("Loot Drops")]
    public List<LootDrop> possibleDrops = new List<LootDrop>(); // YENİ: public yapıldı

    [Header("Drop Settings")]
    [SerializeField, Tooltip("Weight assigned to the chance of getting nothing.")]
    private int nothingDropWeight = 10; // YENİ: Hiçbir şey düşmeme ağırlığı

    /// <summary>
    /// Calculates and returns a single item drop based on weighted chances.
    /// Returns null if nothing drops based on the weights.
    /// </summary>
    /// <returns>An InventoryItem instance for the dropped item, or null.</returns>
    public InventoryItem GetRandomDrop()
    {
        int totalWeight = 0;
        // 1. Toplam ağırlığı hesapla (eşyalar + hiçbir şey)
        foreach (var lootEntry in possibleDrops)
        {
            if (lootEntry.itemData != null && lootEntry.weight > 0)
            {
                totalWeight += lootEntry.weight;
            }
        }
        totalWeight += nothingDropWeight;

        // Eğer toplam ağırlık 0 veya daha azsa (mümkün değil ama kontrol edelim)
        if (totalWeight <= 0)
        {
            return null;
        }

        // 2. Rastgele bir değer üret (0 ile totalWeight-1 arasında)
        int randomValue = Random.Range(0, totalWeight);

        // 3. Hangi aralığa düştüğünü bul
        int currentWeightSum = 0;
        foreach (var lootEntry in possibleDrops)
        {
            if (lootEntry.itemData != null && lootEntry.weight > 0)
            {
                currentWeightSum += lootEntry.weight;

                // Eğer rastgele değer bu eşyanın ağırlık aralığına düşüyorsa
                if (randomValue < currentWeightSum)
                {
                    // Bu eşya seçildi
                    int quantityToDrop = lootEntry.minQuantity;
                    if (lootEntry.maxQuantity > lootEntry.minQuantity)
                    {
                        quantityToDrop = Random.Range(lootEntry.minQuantity, lootEntry.maxQuantity + 1);
                    }

                    if (quantityToDrop > 0)
                    {
                        InventoryItem newItemInstance = new InventoryItem(lootEntry.itemData, quantityToDrop);
                        // Düşen eşyayı logla
                        Debug.Log($"[LootTable] Dropped: {quantityToDrop}x {lootEntry.itemData.ItemName} (Weight: {lootEntry.weight}/{totalWeight}, Roll: {randomValue})");
                        return newItemInstance;
                    }
                    else
                    {
                        // Miktar 0 ise (beklenmez ama) bir şey düşürme
                        return null;
                    }
                }
            }
        }

        // Eğer döngü bitti ve hiçbir eşya seçilmediyse, "nothingDropWeight" aralığına denk gelmiştir.
        // Hiçbir şey düşmediğini logla
        Debug.Log($"[LootTable] Nothing Dropped (Weight: {nothingDropWeight}/{totalWeight}, Roll: {randomValue})");
        return null;
    }
} 
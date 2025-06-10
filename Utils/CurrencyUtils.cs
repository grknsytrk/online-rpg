using UnityEngine;
using TMPro; // TextMeshPro için gerekirse

public static class CurrencyUtils
{
    // Para Birimi Item ID'leri (ItemDatabase'deki ID'lerle eşleşmeli)
    public const string COPPER_COIN_ID = "800";
    public const string SILVER_COIN_ID = "801";
    public const string GOLD_COIN_ID = "802";

    // Sabit dönüşüm oranları (daha güvenilir)
    public const int COPPER_PER_SILVER = 99;
    public const int SILVER_PER_GOLD = 99;

    /// <summary>
    /// Toplam bakır değerini Altın, Gümüş, Bakır formatında string'e çevirir.
    /// </summary>
    /// <param name="totalCopperValue">Formatlanacak toplam bakır miktarı.</param>
    /// <returns>Formatlanmış string (örn: "1 Altın, 25 Gümüş, 50 Bakır")</returns>
    public static string FormatCopperValue(int totalCopperValue)
    {
        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("[CurrencyUtils] ItemDatabase.Instance null! Para formatlanamıyor.");
            return "Veritabanı Hatası";
        }

        ItemData goldData = ItemDatabase.Instance.GetItemById(GOLD_COIN_ID);
        ItemData silverData = ItemDatabase.Instance.GetItemById(SILVER_COIN_ID);
        ItemData copperData = ItemDatabase.Instance.GetItemById(COPPER_COIN_ID);

        // Para birimlerinin isimleri için varsayılan değerler kullan (ItemData null olsa bile)
        string goldName = (goldData != null && !string.IsNullOrEmpty(goldData.itemName)) ? goldData.itemName : "Altın";
        string silverName = (silverData != null && !string.IsNullOrEmpty(silverData.itemName)) ? silverData.itemName : "Gümüş";
        string copperName = (copperData != null && !string.IsNullOrEmpty(copperData.itemName)) ? copperData.itemName : "Bakır";

        // Sabit dönüşüm oranlarını kullan
        int copperPerSilverRate = COPPER_PER_SILVER;
        int silverPerGoldRate = SILVER_PER_GOLD;

        string result = "";
        int gold = 0;
        int silver = 0;
        int copper = totalCopperValue;

        if (copper >= copperPerSilverRate)
        {
            silver = copper / copperPerSilverRate;
            copper %= copperPerSilverRate;
        }
        if (silver >= silverPerGoldRate)
        {
            gold = silver / silverPerGoldRate;
            silver %= silverPerGoldRate;
        }

        bool added = false;
        if (gold > 0)
        {
            result += $"{gold} <color=#FFD700>{goldName}</color>"; // Altın rengi
            added = true;
        }
        if (silver > 0)
        {
            result += (added ? ", " : "") + $"{silver} <color=#C0C0C0>{silverName}</color>"; // Gümüş rengi
            added = true;
        }
        if (copper > 0)
        {
            result += (added ? ", " : "") + $"{copper} <color=#B87333>{copperName}</color>"; // Bakır rengi
            added = true;
        }
        
        if (!added && totalCopperValue == 0) // Hiçbir şey yoksa ve başlangıç değeri 0 ise
        {
            result = $"0 <color=#B87333>{copperName}</color>";
        }
        else if (!added && totalCopperValue > 0) // Bir şeyler olmalıydı ama stringe eklenmedi (örn: sadece 0 altın, 0 gümüş, 0 bakır)
        {
             Debug.LogWarning($"[CurrencyUtils] FormatCopperValue beklenmedik durum: totalCopperValue={totalCopperValue} ama sonuç boş. '0 {copperName}' döndürülüyor.");
             result = $"0 <color=#B87333>{copperName}</color>"; // Varsayılan olarak 0 bakır göster
        }
        else if (string.IsNullOrEmpty(result) && totalCopperValue != 0) // totalCopperValue 0 değil ama result boşsa, bu da bir hata olabilir.
        {
            Debug.LogWarning($"[CurrencyUtils] FormatCopperValue: Sonuç boş ama totalCopperValue = {totalCopperValue}. Bu bir hata olabilir.");
            result = $"{totalCopperValue} (Ham Bakır)"; // Hata durumunda ham değeri göster
        }

        return result;
    }

    /// <summary>
    /// Para formatı test metodları - debug amaçlı
    /// </summary>
    public static void TestCurrencyFormatting()
    {
        Debug.Log("=== Para Formatı Testleri ===");
        Debug.Log($"0 bakır: {FormatCopperValue(0)}");
        Debug.Log($"50 bakır: {FormatCopperValue(50)}");
        Debug.Log($"99 bakır: {FormatCopperValue(99)}");
        Debug.Log($"100 bakır: {FormatCopperValue(100)}");
        Debug.Log($"130 bakır: {FormatCopperValue(130)}");
        Debug.Log($"198 bakır: {FormatCopperValue(198)}");
        Debug.Log($"9801 bakır: {FormatCopperValue(9801)}"); // 1 altın
        Debug.Log($"9900 bakır: {FormatCopperValue(9900)}"); // 1 altın
        Debug.Log($"10025 bakır: {FormatCopperValue(10025)}"); // 1 altın, 1 gümüş, 26 bakır
        Debug.Log("=== Test Tamamlandı ===");
    }
} 
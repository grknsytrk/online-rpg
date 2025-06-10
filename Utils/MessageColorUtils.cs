using UnityEngine;

/// <summary>
/// Sistem mesajlarındaki özel elemanları renklendirmek için yardımcı sınıf
/// </summary>
public static class MessageColorUtils
{
    // --- Renk Tanımları ---
    public static readonly Color PlayerNameColor = new Color(0.4f, 0.8f, 1f);        // #66CCFF - Açık Mavi
    public static readonly Color ItemNameColor = new Color(1f, 1f, 0.2f);           // #FFFF33 - Parlak Sarı (daha belirgin)
    public static readonly Color EnemyNameColor = new Color(1f, 0.5f, 0.3f);         // #FF8033 - Turuncu Kırmızı
    public static readonly Color NumberColor = new Color(0.9f, 0.9f, 0.9f);          // #E6E6E6 - Açık Gri
    public static readonly Color CurrencyColor = new Color(0.3f, 1f, 0.5f);          // #4DFF80 - Açık Yeşil
    public static readonly Color RareItemColor = new Color(0.27f, 1f, 0.25f);        // rgb(69, 255, 63) - Parlak Yeşil
    public static readonly Color LevelColor = new Color(1f, 0.7f, 0.2f);             // #FFB333 - Altın Turuncu

    // --- Etiket Renkleri ---
    public static readonly Color ServerTagColor = new Color(0.3f, 1f, 0.3f);         // #4DFF4D - Yeşil
    public static readonly Color WarningTagColor = new Color(1f, 0.6f, 0.1f);        // #FF9933 - Turuncu
    public static readonly Color TipTagColor = new Color(0.4f, 0.9f, 1f);            // #66E6FF - Açık Mavi

    /// <summary>
    /// Oyuncu ismini renklendirir
    /// </summary>
    /// <param name="playerName">Oyuncu ismi</param>
    /// <returns>Renklendirilmiş oyuncu ismi</returns>
    public static string ColorizePlayerName(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return playerName;
        string colorHex = ColorUtility.ToHtmlStringRGB(PlayerNameColor);
        return $"<color=#{colorHex}>{playerName}</color>";
    }

    /// <summary>
    /// Normal item ismini renklendirir
    /// </summary>
    /// <param name="itemName">Item ismi</param>
    /// <returns>Renklendirilmiş item ismi</returns>
    public static string ColorizeItemName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return itemName;
        string colorHex = ColorUtility.ToHtmlStringRGB(ItemNameColor);
        return $"<color=#{colorHex}>{itemName}</color>";
    }

    /// <summary>
    /// Nadir item ismini renklendirir (daha parlak)
    /// </summary>
    /// <param name="itemName">Nadir item ismi</param>
    /// <returns>Renklendirilmiş nadir item ismi</returns>
    public static string ColorizeRareItemName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return itemName;
        string colorHex = ColorUtility.ToHtmlStringRGB(RareItemColor);
        return $"<color=#{colorHex}>{itemName}</color>";
    }

    /// <summary>
    /// Düşman ismini renklendirir
    /// </summary>
    /// <param name="enemyName">Düşman ismi</param>
    /// <returns>Renklendirilmiş düşman ismi</returns>
    public static string ColorizeEnemyName(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return enemyName;
        string colorHex = ColorUtility.ToHtmlStringRGB(EnemyNameColor);
        return $"<color=#{colorHex}>{enemyName}</color>";
    }

    /// <summary>
    /// Sayıları renklendirir (seviye, miktar vb.)
    /// </summary>
    /// <param name="number">Sayı</param>
    /// <returns>Renklendirilmiş sayı</returns>
    public static string ColorizeNumber(int number)
    {
        string colorHex = ColorUtility.ToHtmlStringRGB(NumberColor);
        return $"<color=#{colorHex}>{number}</color>";
    }

    /// <summary>
    /// Seviye numarasını özel renkle renklendirir
    /// </summary>
    /// <param name="level">Seviye</param>
    /// <returns>Renklendirilmiş seviye</returns>
    public static string ColorizeLevel(int level)
    {
        string colorHex = ColorUtility.ToHtmlStringRGB(LevelColor);
        return $"<color=#{colorHex}>{level}</color>";
    }

    /// <summary>
    /// Para miktarını renklendirir (CurrencyUtils ile uyumlu)
    /// </summary>
    /// <param name="formattedCurrency">Formatlanmış para string'i</param>
    /// <returns>Renklendirilmiş para miktarı</returns>
    public static string ColorizeCurrency(string formattedCurrency)
    {
        if (string.IsNullOrEmpty(formattedCurrency)) return formattedCurrency;
        string colorHex = ColorUtility.ToHtmlStringRGB(CurrencyColor);
        return $"<color=#{colorHex}>{formattedCurrency}</color>";
    }

    /// <summary>
    /// Etiket renklendirir
    /// </summary>
    /// <param name="tag">Etiket metni (köşeli parantez olmadan)</param>
    /// <param name="tagType">Etiket tipi</param>
    /// <returns>Renklendirilmiş etiket</returns>
    public static string ColorizeTag(string tag, TagType tagType = TagType.Server)
    {
        Color tagColor;
        switch (tagType)
        {
            case TagType.Warning:
                tagColor = WarningTagColor;
                break;
            case TagType.Tip:
                tagColor = TipTagColor;
                break;
            case TagType.Server:
            default:
                tagColor = ServerTagColor;
                break;
        }

        string colorHex = ColorUtility.ToHtmlStringRGB(tagColor);
        return $"<color=#{colorHex}>[{tag}]</color>";
    }

    // --- Kompozit Metodlar (Tüm mesaj için) ---

    /// <summary>
    /// PvP kill mesajını renklendirir
    /// </summary>
    /// <param name="killerName">Katil oyuncu</param>
    /// <param name="victimName">Ölen oyuncu</param>
    /// <returns>Tamamen renklendirilmiş mesaj</returns>
    public static string BuildPvPKillMessage(string killerName, string victimName)
    {
        return $"{ColorizeTag("SUNUCU")} {ColorizePlayerName(killerName)}, {ColorizePlayerName(victimName)} oyuncusunu öldürdü!";
    }

    /// <summary>
    /// Nadir item bulma mesajını renklendirir
    /// </summary>
    /// <param name="playerName">Oyuncu ismi</param>
    /// <param name="enemyName">Düşman ismi</param>
    /// <param name="itemName">Item ismi</param>
    /// <returns>Tamamen renklendirilmiş mesaj</returns>
    public static string BuildRareItemMessage(string playerName, string enemyName, string itemName)
    {
        return $"{ColorizeTag("SUNUCU")} {ColorizePlayerName(playerName)}, {ColorizeEnemyName(enemyName)}'dan nadir eşya buldu: {ColorizeRareItemName(itemName)}! Çok şanslı!";
    }

    /// <summary>
    /// Level up mesajını renklendirir
    /// </summary>
    /// <param name="playerName">Oyuncu ismi</param>
    /// <param name="level">Yeni seviye</param>
    /// <returns>Tamamen renklendirilmiş mesaj</returns>
    public static string BuildLevelUpMessage(string playerName, int level)
    {
        return $"{ColorizeTag("SUNUCU")} {ColorizePlayerName(playerName)}, {ColorizeLevel(level)}. seviyeye ulaştı! Tebrikler!";
    }

    /// <summary>
    /// Oyuncu giriş mesajını renklendirir
    /// </summary>
    /// <param name="playerName">Oyuncu ismi</param>
    /// <returns>Tamamen renklendirilmiş mesaj</returns>
    public static string BuildPlayerJoinMessage(string playerName)
    {
        return $"{ColorizeTag("SUNUCU")} {ColorizePlayerName(playerName)} oyuna katıldı! Hoş geldin!";
    }

    /// <summary>
    /// Oyuncu çıkış mesajını renklendirir
    /// </summary>
    /// <param name="playerName">Oyuncu ismi</param>
    /// <returns>Tamamen renklendirilmiş mesaj</returns>
    public static string BuildPlayerLeaveMessage(string playerName)
    {
        return $"{ColorizeTag("SUNUCU")} {ColorizePlayerName(playerName)} oyundan ayrıldı. Güle güle!";
    }

    /// <summary>
    /// Master client değişim mesajını renklendirir
    /// </summary>
    /// <param name="masterName">Yeni master ismi</param>
    /// <returns>Tamamen renklendirilmiş mesaj</returns>
    public static string BuildMasterChangeMessage(string masterName)
    {
        return $"{ColorizeTag("SUNUCU")} {ColorizePlayerName(masterName)} artık sunucu yöneticisi! Yeni lider!";
    }
}

/// <summary>
/// Etiket tipi enum'u
/// </summary>
public enum TagType
{
    Server,   // Yeşil - Genel sunucu mesajları
    Warning,  // Turuncu - Uyarılar
    Tip       // Açık Mavi - İpuçları
} 
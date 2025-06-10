/// <summary>
/// Envanterda kullanılacak slot tiplerini tanımlar.
/// Hem item tiplerini hem de ekipman tiplerini içerir.
/// </summary>
public enum SlotType
{
    None = 0,       // Genel slot tipi (standart envanter slotları)
    Sword = 1,      // Kılıç slotu
    Helmet = 2,     // Kask slotu
    Chestplate = 3, // Zırh slotu
    Leggings = 4,   // Pantolon slotu
    Boots = 5,      // Bot slotu
    Ring = 6,       // Yüzük slotu
    Necklace = 7,   // Kolye slotu
    Potion = 8,     // İksir/Tüketilebilir slotu
    Quest = 9,      // Görev eşyası slotu 
    Resource = 10,  // Kaynak slotu
    Currency = 11   // Para birimi slotu
} 
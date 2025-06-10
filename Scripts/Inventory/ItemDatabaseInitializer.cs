using UnityEngine;

/// <summary>
/// ItemDatabase'i başlatan ve erişilebilir hale getiren bileşen.
/// Sahneye yerleştirilmelidir.
/// </summary>
public class ItemDatabaseInitializer : MonoBehaviour
{
    [SerializeField] private ItemDatabase database;
    
    public ItemDatabase Database => database;
    
    private void Awake()
    {
        if (database == null)
        {
            Debug.LogError("ItemDatabase referansı atanmamış! Lütfen Inspector'dan bir ItemDatabase ScriptableObject atayın.");
            return;
        }
        
        // Nesnenin yok edilmemesini sağla
        DontDestroyOnLoad(gameObject);
        
        // Database'i başlatmadan önce _isInitialized bayrağını sıfırla
        ResetDatabaseInitialization();
        
        // Database'i başlat
        database.InitializeDatabase();
        
        // Wooden_Sword itemini kontrol et
        var woodenSword = database.GetItemById("1");
        if (woodenSword == null)
        {
            Debug.LogError("Kritik hata: Wooden_Sword (ID: 1) item'ı veritabanında bulunamadı! ItemDatabase asset'ine bu item eklenmiş mi?");
            Debug.LogError("Çözüm: 1) Wooden_Sword.asset dosyasını Resources/Items/Weapons klasörüne taşıyın.");
            Debug.LogError("       2) ItemDatabase.asset dosyasını Resources/Items klasörüne taşıyın.");
            Debug.LogError("       3) ItemDatabase asset'ini açıp Wooden_Sword referansını yeniden atayın.");
        }
        else
        {
            Debug.Log($"Wooden_Sword başarıyla yüklendi: ID={woodenSword.itemId}, Name={woodenSword.itemName}");
        }
        
        Debug.Log("ItemDatabase başarıyla başlatıldı.");
    }
    
    /// <summary>
    /// Database'in _isInitialized bayrağını sıfırlar, böylece her oyun başlatıldığında yeniden başlatılır
    /// </summary>
    private void ResetDatabaseInitialization()
    {
        // ItemDatabase sınıfındaki _isInitialized alanına yansıma (reflection) ile erişim
        var field = typeof(ItemDatabase).GetField("_isInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(database, false);
            Debug.Log("ItemDatabase _isInitialized bayrağı sıfırlandı.");
        }
    }
    
    private void OnApplicationQuit()
    {
        // Uygulama kapanırken _isInitialized bayrağını sıfırla
        ResetDatabaseInitialization();
    }
} 
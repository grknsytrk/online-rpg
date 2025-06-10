using UnityEngine;
using Photon.Pun;
using System; // Action için
using System.Collections; // IEnumerator için eklendi
using Firebase.Database; // Firebase için eklendi
using System.Threading.Tasks; // Task için eklendi
using Newtonsoft.Json; // Json için eklendi
using System.IO; // File I/O için eklendi
using System.Linq; // FirstOrDefault için eklendi

// Bu script Player nesnesine eklenecek
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(PlayerHealth))] // PlayerHealth'e erişim için
public class PlayerStats : MonoBehaviourPunCallbacks
{
    // --- Events ---
    public event Action OnStatsCalculated; // İstatistikler güncellendiğinde UI gibi yerleri bilgilendirmek için
    public event Action<int> OnLevelUp; // Seviye atlandığında tetiklenecek olay
    public event Action OnStatsUpdated; // XP veya Seviye güncellendiğinde
    public event Action<int> OnCopperChanged; // Bakır miktarı değiştiğinde UI'ı güncellemek için

    // --- Base Stats ---
    [Header("Base Stats")]
    [SerializeField] private int baseAttack = 5; // Örnek temel saldırı
    [SerializeField] private int baseDefense = 0; // Örnek temel savunma
    [SerializeField] private int baseMaxHealth = 100; // Temel Maksimum Can

    [Header("Defense Calculation Settings")] // Yeni başlık
    [SerializeField] private int baseArmorBonus = 2; // TemelZırhBonusu
    [SerializeField] private float tierFactorDefense = 1.7f; // KademeFaktörü_Z

    [Header("Health Gain Per Level Settings")]
    [SerializeField] private int normalLevelFixedBonus = 10; // Normal seviye sabit artış
    [SerializeField] private int normalLevelBaseBonus = 5; // Normal seviye taban artış
    [SerializeField] private float normalLevelDivisor = 20f;  // Normal seviye bölme bonusu çarpanı

    [SerializeField] private int milestoneLevelFixedBonus = 50; // Kilometre taşı sabit artış
    [SerializeField] private int milestoneLevelBaseBonus = 20; // Kilometre taşı taban artış
    [SerializeField] private float milestoneLevelDivisor = 10f;   // Kilometre taşı bölme bonusu çarpanı
    [SerializeField] private int milestoneLevelInterval = 10;   // Kilometre taşı aralığı

    // --- Calculated Stats ---
    public int TotalAttack { get; private set; }
    public int TotalDefense { get; private set; }
    public int TotalMaxHealth { get; private set; } // Hesaplanan Maksimum Can
    
    // --- Dynamic Base Stats (Seviye ile artan temel değerler, ekipman bonusu hariç) ---
    public int BaseAttackDynamic { get; private set; } // Temel saldırı (seviye ile artan)
    public int BaseDefenseDynamic { get; private set; } // Temel savunma (seviye ile artan)

    // --- XP & Level ---
    public int CurrentLevel { get; private set; } = 1;
    public int CurrentXP { get; private set; } = 0;
    public int XPToNextLevel { get; private set; } = 100; // Başlangıç değeri

    // --- Currency ---
    public int TotalCopper { get; private set; } = 0; // Başlangıçta 0 bakır

    [Header("Leveling Curve Settings")] // Inspector için yeni başlık
    [SerializeField] private float baseXPForLevel2 = 100f; // Seviye 2 için gereken temel XP
    [SerializeField] [Range(1.01f, 2f)] private float xpMultiplierPerLevel = 1.15f; // Her seviye için XP artış çarpanı (Min 1.01)

    // --- Private Fields ---
    private PhotonView pv;
    private PlayerHealth playerHealth; // PlayerHealth referansı eklendi
    private DatabaseReference _databaseRef;
    private string _userId;
    private bool _isInitialized = false;
    private int _version = 0; // Veri senkronizasyonu için versiyon numarası
    private const string STATS_FILE_PREFIX = "stats_"; // Yerel dosya adı için ön ek
    private bool _isLoading = false; // Yükleme işlemi devam ediyor mu?
    private string logPrefix = "[Stats] "; // Log prefix

    // Public Getters for Base Stats (Yeni Eklendi)
    public int GetBaseAttack() => baseAttack;
    public int GetBaseDefense() => baseDefense;

    void Awake()
    {
        pv = GetComponent<PhotonView>();
        playerHealth = GetComponent<PlayerHealth>(); // PlayerHealth referansını al

        // Sadece yerel oyuncu istatistikleri yönetir ve kaydeder
        if (!pv.IsMine)
        {
            enabled = false; // Diğer oyuncular için bu scripti devre dışı bırak
            return;
        }
         Debug.Log($"{logPrefix}Awake çağrıldı (IsMine=True).");

        // Awake içinde sadece temel değerleri ve ilk hesaplamayı yap
        TotalAttack = baseAttack;
        TotalDefense = baseDefense;
        XPToNextLevel = CalculateXPForLevel(2);
        CalculateStats(); // Başlangıç statlarını hesapla (TotalMaxHealth dahil)
        Debug.Log($"{logPrefix}Temel statlar ve ilk hesaplama: Attack={TotalAttack}, Defense={TotalDefense}, MaxHealth={TotalMaxHealth}, Level={CurrentLevel}, XP={CurrentXP}/{XPToNextLevel}");

        // Başlatma rutinini başlat
        Debug.Log($"{logPrefix}InitializeStatsRoutine başlatılıyor...");
        StartCoroutine(InitializeStatsRoutine());
    }

    private IEnumerator InitializeStatsRoutine()
    {
         Debug.Log($"{logPrefix}InitializeStatsRoutine başladı.");
        _isLoading = true; // Yükleme başladı

        // Firebase Auth yüklenme kontrolü
        while (FirebaseAuthManager.Instance == null)
        {
            Debug.Log($"{logPrefix}FirebaseAuthManager bekleniyor...");
            yield return null;
        }
         Debug.Log($"{logPrefix}FirebaseAuthManager bulundu.");

        // Kullanıcı ID'sini al
        _userId = FirebaseAuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(_userId))
        {
            Debug.LogError($"{logPrefix}Kullanıcı giriş yapmamış! İstatistikler yüklenemiyor.");
            _isLoading = false;
            yield break;
        }
        Debug.Log($"{logPrefix}UserID alındı: {_userId}");

        // Database referansını al
        while (FirebaseDatabase.DefaultInstance == null)
        {
             Debug.Log($"{logPrefix}FirebaseDatabase bekleniyor...");
             yield return null;
        }
        _databaseRef = FirebaseDatabase.DefaultInstance.RootReference;
        Debug.Log($"{logPrefix}Database referansı alındı.");

        // İstatistik verilerini yükle
        Debug.Log($"{logPrefix}LoadPlayerData çağrılıyor...");
        yield return LoadPlayerData(); // Bu async Task, Coroutine içinde await ile çağrılmalı
         // LoadPlayerData'yı doğrudan çağırmak yerine Task olarak alıp bekleyelim
         /* Task loadTask = LoadPlayerData();
         yield return new WaitUntil(() => loadTask.IsCompleted);
         if (loadTask.IsFaulted)
         {
             Debug.LogError($"{logPrefix}LoadPlayerData sırasında hata: {loadTask.Exception}");
         }*/
         Debug.Log($"{logPrefix}LoadPlayerData tamamlandı.");

        // Auth değişikliklerini dinle
        Debug.Log($"{logPrefix}FirebaseAuthManager.OnAuthStateChanged olayına abone olunuyor...");
        FirebaseAuthManager.Instance.OnAuthStateChanged += OnAuthChanged;

        // EquipmentManager olaylarına abone ol (artık güvenli)
        Debug.Log($"{logPrefix}SubscribeToEquipmentManager başlatılıyor...");
        StartCoroutine(SubscribeToEquipmentManager()); // Bu zaten vardı, yerini değiştirdik

        _isInitialized = true;
        _isLoading = false; // Yükleme bitti
        Debug.Log($"{logPrefix}İstatistik sistemi başlatıldı ve veriler yüklendi.");

        // Başlangıçta UI'ı güncellemek için olay tetikle
        Debug.Log($"{logPrefix}Başlangıç UI güncelleme olayları tetikleniyor...");
        OnStatsUpdated?.Invoke();
        OnStatsCalculated?.Invoke(); // Ekipman istatistikleri için
        OnCopperChanged?.Invoke(TotalCopper); // Bakır için de olay tetikle
    }


    public override void OnEnable()
    {
        base.OnEnable();
         if (pv.IsMine) Debug.Log($"{logPrefix}OnEnable çağrıldı.");
        // Abone olma işlemini InitializeStatsRoutine içine taşıdık.
    }

    // EquipmentManager singleton'ının hazır olmasını bekleyip abone olan korutin
    private IEnumerator SubscribeToEquipmentManager()
    {
         Debug.Log($"{logPrefix}SubscribeToEquipmentManager başladı.");
        // EquipmentManager hazır olana kadar bekle (veya zaman aşımı)
        float timer = 0f;
        float waitTime = 5f; // Daha uzun bekleme süresi
        while (EquipmentManager.Instance == null && timer < waitTime)
        {
             // Debug.Log($"{logPrefix}EquipmentManager bekleniyor... ({timer:F1}/{waitTime}s)");
            // if (!_isLoading) // Sadece yükleme bittiyse zamanlayıcıyı artır
            // { // isLoading kontrolü kaldırıldı, her durumda bekle
            //     timer += Time.deltaTime;
            // }
            timer += Time.deltaTime; // isLoading kontrolü kaldırıldı
            yield return null; // Bir sonraki frame'e geç
        }

        if (EquipmentManager.Instance != null)
        {
            Debug.Log($"{logPrefix}EquipmentManager bulundu, olaylara abone olunuyor ve ilk hesaplama yapılıyor.");
            EquipmentManager.Instance.OnItemEquipped += HandleEquipmentChanged;
            EquipmentManager.Instance.OnItemUnequipped += HandleEquipmentChanged;
            // Başlangıçta bir kez hesapla (LoadPlayerData sonrası olmalı)
             Debug.Log($"{logPrefix}Başlangıç stat hesaplaması yapılıyor...");
            CalculateStats();
        }
        else
        {
            Debug.LogWarning($"{logPrefix}{waitTime} saniye beklendi ama EquipmentManager bulunamadı. Ekipman istatistikleri güncellenmeyebilir.");
             // EquipmentManager null olsa bile temel statlarla devam etmeli
             Debug.Log($"{logPrefix}EquipmentManager bulunamadı, sadece temel statlar hesaplanıyor...");
             CalculateStats(); // Temel statları hesapla
        }
         Debug.Log($"{logPrefix}SubscribeToEquipmentManager tamamlandı.");
    }

    public override void OnDisable()
    {
        base.OnDisable();
         if (pv.IsMine) Debug.Log($"{logPrefix}OnDisable çağrıldı.");
        // Olay aboneliklerini kaldır
        if (pv.IsMine)
        {
            if (EquipmentManager.Instance != null)
            {
                 Debug.Log($"{logPrefix}EquipmentManager olay abonelikleri kaldırılıyor.");
                EquipmentManager.Instance.OnItemEquipped -= HandleEquipmentChanged;
                EquipmentManager.Instance.OnItemUnequipped -= HandleEquipmentChanged;
            }
            if (FirebaseAuthManager.Instance != null)
            {
                 Debug.Log($"{logPrefix}FirebaseAuthManager olay aboneliği kaldırılıyor.");
                FirebaseAuthManager.Instance.OnAuthStateChanged -= OnAuthChanged;
            }
        }
    }

    // --- Stat Calculation ---
    private void HandleEquipmentChanged(SlotType slotType, InventoryItem item)
    {
        Debug.Log($"{logPrefix}HandleEquipmentChanged tetiklendi. Slot: {slotType}, Item: {(item == null ? "Yok" : item.ItemName)}. Statlar yeniden hesaplanıyor...");
        CalculateStats();
    }

    // Tüm giyili eşyalardan istatistikleri hesapla
    public void CalculateStats()
    {
        if (!pv.IsMine) { return; }

        // --- Attack ve Defense hesaplaması ---
        // int currentAttack = baseAttack; // Eski temel saldırı kullanımı kaldırıldı
        // int currentDefense = baseDefense; // Eski temel savunma kullanımı kaldırıldı

        // Yeni OyuncuTemelHasarı hesaplaması
        int dynamicBaseAttack = 10 + Mathf.CeilToInt((float)CurrentLevel / 2f);
        int currentAttack = dynamicBaseAttack;

        // Yeni OyuncuTemelSavunması hesaplaması
        // Formül: OyuncuTemelSavunması = TemelZırhBonusu + (MevcutSeviye / KademeFaktörü_Z)
        int dynamicBaseDefense = baseArmorBonus + Mathf.CeilToInt((float)CurrentLevel / tierFactorDefense);
        int currentDefense = dynamicBaseDefense;

        int equipmentAttackBonus = 0;
        int equipmentDefenseBonus = 0;
        int equipmentHealthBonus = 0; // Ekipman can bonusu (varsa)

        if (EquipmentManager.Instance != null)
        {
            foreach (SlotType slotType in Enum.GetValues(typeof(SlotType)))
            {
                if (slotType == SlotType.None) continue;
                InventoryItem equippedItem = EquipmentManager.Instance.GetEquippedItem(slotType);
                if (equippedItem != null)
                {
                    equipmentAttackBonus += equippedItem.Damage; 
                    equipmentDefenseBonus += equippedItem.Defense;
                    // equipmentHealthBonus += equippedItem.HealthBonus; // Eğer itemlerde can bonusu varsa
                }
            }
            currentAttack += equipmentAttackBonus;
            currentDefense += equipmentDefenseBonus;
        }
        
        // --- Yeni Maksimum Can Hesaplama --- 
        int calculatedMaxHealth = baseMaxHealth;

        // Seviye 1'den başlayarak mevcut seviyeye kadar her seviye artışını hesapla ve ekle
        for (int levelBefore = 1; levelBefore < CurrentLevel; levelBefore++)
        {
            int levelReached = levelBefore + 1; // Bu adımda ulaşılan seviye

            int fixedBonus;
            int baseBonus;
            float divisor;

            // Ulaşılan seviyenin kilometre taşı olup olmadığını kontrol et
            if (levelReached % milestoneLevelInterval == 0)
            {
                fixedBonus = milestoneLevelFixedBonus;
                baseBonus = milestoneLevelBaseBonus;
                divisor = milestoneLevelDivisor;
            }
            else // Normal seviye artışı
            {
                fixedBonus = normalLevelFixedBonus;
                baseBonus = normalLevelBaseBonus;
                divisor = normalLevelDivisor;
            }

            // Bölme bonusunu hesapla (pseudo-code'daki gibi bir önceki seviyeye göre)
            // levelBefore 0 olamayacağı için ayrıca kontrol gerekmez (döngü 1'den başlar)
            float divisionBonus = divisor / levelBefore;
            int totalIncreaseForThisLevel = fixedBonus + baseBonus + Mathf.RoundToInt(divisionBonus);

            calculatedMaxHealth += totalIncreaseForThisLevel;
        }

        // Ekipmanlardan gelen can bonusunu ekle (eğer varsa)
        calculatedMaxHealth += equipmentHealthBonus;
        // --- Maksimum Can Hesaplama Sonu ---

        // Hesaplanan değerleri ata
        bool changed = (TotalAttack != currentAttack || TotalDefense != currentDefense || TotalMaxHealth != calculatedMaxHealth);
        TotalAttack = currentAttack;
        TotalDefense = currentDefense;
        TotalMaxHealth = Mathf.Max(1, calculatedMaxHealth); // Canın en az 1 olmasını sağla

        Debug.Log($"{logPrefix}Hesaplanan Statlar (Seviye {CurrentLevel}): Attack={TotalAttack}, Defense={TotalDefense}, MaxHealth={TotalMaxHealth} {(changed ? "(Değişti)" : "(Değişmedi)")}");

        // Temel değerleri property'lere ata (ekipman bonusu eklenmeden önce)
        BaseAttackDynamic = dynamicBaseAttack;
        BaseDefenseDynamic = dynamicBaseDefense;

        OnStatsCalculated?.Invoke();
        OnStatsUpdated?.Invoke(); 
    }

    // --- XP & Leveling ---

    [PunRPC]
    public void RPC_AddXP(int amount)
    {
        // --- DEBUG LOG EKLENDİ --- (Zaten vardı, prefix ekleyelim)
        Debug.LogWarning($"{logPrefix}RPC_AddXP ÇAĞRILDI! ViewID: {pv.ViewID}, Miktar: {amount}, IsMine: {pv.IsMine}");
        // -------------------------

        // Bu RPC sadece Owner client üzerinde çalışmalı. Zaten EnemyHealth'den RpcTarget.Owner ile çağrılıyor.
        if (!pv.IsMine) {
             Debug.LogWarning($"{logPrefix}RPC_AddXP: Yerel oyuncu değil, işlem yapılmadı.");
             return;
        }
        
        // =============== XP DAMAGE NUMBER GÖSTERİMİ ===============
        // XP kazandığında beyaz damage number göster
        if (DamageNumberManager.Instance != null && amount > 0)
        {
            Vector3 playerPosition = transform.position + Vector3.up * 0.8f; // Oyuncunun biraz üstünde
            DamageNumberManager.Instance.ShowDamageNumber(playerPosition, amount, Color.white);
            Debug.Log($"XP Number shown: +{amount} XP at position {playerPosition}");
        }
        // ====================================================
        
        AddXP(amount);
    }


    private void AddXP(int amount)
    {
        if (amount <= 0) {
            Debug.LogWarning($"{logPrefix}AddXP: Geçersiz XP miktarı ({amount}).");
            return;
        }

        CurrentXP += amount;
        Debug.Log($"{logPrefix}{amount} XP kazanıldı. Toplam XP: {CurrentXP}/{XPToNextLevel}");

        bool leveledUp = false;
        while (CurrentXP >= XPToNextLevel)
        {
            CurrentXP -= XPToNextLevel;
            CurrentLevel++;
            XPToNextLevel = CalculateXPForLevel(CurrentLevel + 1);
            Debug.Log($"{logPrefix}SEVİYE ATLANDI! Yeni Seviye: {CurrentLevel}. Sonraki seviye için {XPToNextLevel} XP gerekli. Kalan XP: {CurrentXP}");
            
            // SEVİYE ATLANDIĞINDA STATLARI YENİDEN HESAPLA
            CalculateStats(); 

            // Ses efektini çal
            if (CurrentLevel % milestoneLevelInterval == 0) // milestoneLevelInterval PlayerStats içindeki bir alan olmalı
            {
                SFXManager.Instance?.PlaySound(SFXNames.PlayerMilestoneLevelUp);
                Debug.Log($"{logPrefix}Kilometre taşı seviye atlama sesi çalındı: {SFXNames.PlayerMilestoneLevelUp}");
            }
            else
            {
                SFXManager.Instance?.PlaySound(SFXNames.PlayerLevelUp);
                Debug.Log($"{logPrefix}Normal seviye atlama sesi çalındı: {SFXNames.PlayerLevelUp}");
            }

            leveledUp = true; // Sadece olay tetikleme için kullanılıyor, döngü koşulu yeterli
            OnLevelUp?.Invoke(CurrentLevel); // Seviye atlama olayını CalculateStats'tan *sonra* tetikle

            // --- LEVEL ATLAMA MESAJI --- EKLENDİ
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowLevelUpMessage(CurrentLevel);
            }
            
            // --- LEVEL UP SİSTEM MESAJI --- EKLENDİ
            if (pv.IsMine) // Sadece kendi oyuncumuz için mesaj gönder
            {
                ChatManager chatManager = FindObjectOfType<ChatManager>();
                if (chatManager != null)
                {
                    string playerName = PhotonNetwork.NickName ?? "Oyuncu";
                    string levelUpMessage = MessageColorUtils.BuildLevelUpMessage(playerName, CurrentLevel);
                    chatManager.SendSystemMessage(levelUpMessage, SystemMessageType.LevelUp);
                    Debug.Log($"Level Up Message: {levelUpMessage}");
                }
                else
                {
                    Debug.LogWarning("ChatManager bulunamadı, level up mesajı gönderilemedi.");
                }
            }
            // -------------------------

            // --- SEVİYE SENKRONİZASYONU İÇİN RPC ÇAĞRISI --- EKLENDİ
            if (pv.IsMine)
            {
                pv.RPC("RPC_SetLevelNetwork", RpcTarget.OthersBuffered, CurrentLevel);
            }
            // -----------------------------------------
        }

        // İstatistiklerin güncellendiğini bildir
        // Debug.Log($"{logPrefix}OnStatsUpdated olayı tetikleniyor (AddXP sonrası).");
        OnStatsUpdated?.Invoke();

        // Verileri kaydet
        // Debug.Log($"{logPrefix}SavePlayerData çağrılıyor (AddXP sonrası).");
        SavePlayerData(); // Her XP kazancında kaydetmek iyi mi? Belki periyodik? Şimdilik böyle kalsın.
    }

    private int CalculateXPForLevel(int level)
    {
        // Örnek: Basit üstel büyüme (100, 150, 225, 337...)
        // return Mathf.FloorToInt(100 * Mathf.Pow(1.5f, level - 1));

        // Inspector'dan alınan değerleri kullanarak hesapla
        if (level <= 1) return 0; // Seviye 1 için XP gerekmez
        if (level == 2) return Mathf.Max(1, Mathf.FloorToInt(baseXPForLevel2)); // Seviye 2 için direkt baseXP kullanılır (en az 1)

        // Seviye 3 ve sonrası için formül:
        // baseXPForLevel2 * (multiplier ^ (level - 2))
        int requiredXP = Mathf.Max(1, Mathf.FloorToInt(baseXPForLevel2 * Mathf.Pow(xpMultiplierPerLevel, level - 2)));
        // Debug.Log($"{logPrefix}CalculateXPForLevel({level}): Gerekli XP = {requiredXP}");
        return requiredXP;
    }


    // --- Data Persistence ---

    private string GetLocalStoragePath()
    {
        if (string.IsNullOrEmpty(_userId)) return null;
        string path = Path.Combine(Application.persistentDataPath, $"{STATS_FILE_PREFIX}{_userId}.json");
        // Debug.Log($"{logPrefix}Yerel depolama yolu: {path}");
        return path;
    }

    public void SavePlayerData(bool async = true)
    {
        if (!_isInitialized || string.IsNullOrEmpty(_userId) || !pv.IsMine)
        {
            // Debug.LogWarning($"{logPrefix}SavePlayerData: Kayıt yapılamadı (Initialized={_isInitialized}, UserID='{_userId}', IsMine={pv.IsMine}).");
            return;
        }

        // Debug.Log($"{logPrefix}SavePlayerData çağrıldı (Async: {async})...");

        try
        {
            _version++; // Versiyonu artır
            var statsData = new PlayerStatsData
            {
                version = _version,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                currentLevel = CurrentLevel,
                currentXP = CurrentXP,
                totalCopper = TotalCopper // Bakırı kaydet
                // Gerekirse baseAttack, baseDefense gibi diğer kaydedilecek statlar eklenebilir.
            };
            // Debug.Log($"{logPrefix}Kaydedilecek Veri: Versiyon={statsData.version}, Seviye={statsData.currentLevel}, XP={statsData.currentXP}");

            string json = JsonConvert.SerializeObject(statsData);

            // 1. Yerel Kayıt
            string localPath = GetLocalStoragePath();
            if (localPath != null)
            {
                try
                {
                    // Debug.Log($"{logPrefix}Yerel depolamaya yazılıyor: {localPath}");
                    File.WriteAllText(localPath, json);
                    // Debug.Log($"{logPrefix}İstatistikler yerel depolamaya kaydedildi (Versiyon: {_version}) Path: {localPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"{logPrefix}Yerel istatistik kayıt hatası: {e.Message}");
                    if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
                }
            }
            else {
                 Debug.LogWarning($"{logPrefix}Yerel depolama yolu alınamadı, yerel kayıt yapılamadı.");
            }


            // 2. Firebase Kayıt
            if (_databaseRef == null) {
                 Debug.LogError($"{logPrefix}Firebase database referansı null, Firebase'e kayıt yapılamadı!");
                 return;
            }
            var statsRef = _databaseRef.Child("users").Child(_userId).Child("stats");
            // Debug.Log($"{logPrefix}Firebase'e yazılıyor... Ref: {statsRef.ToString()}");
            if (async)
            {
                statsRef.SetRawJsonValueAsync(json).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        // Debug.LogError($"{logPrefix}Firebase istatistik kayıt hatası (async): {task.Exception}");
                        string innerErrorMsg = task.Exception?.Flatten()?.InnerExceptions?.FirstOrDefault()?.Message;
                        string mainErrorMsg = task.Exception?.Message;
                        Debug.LogError($"{logPrefix}Firebase istatistik kayıt hatası (async): {(innerErrorMsg ?? mainErrorMsg)}");
                    }
                    else if (task.IsCompleted)
                    {
                        // Debug.Log($"{logPrefix}İstatistikler Firebase'e kaydedildi (Versiyon: {_version}) - Async tamamlandı.");
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext()); // Unity ana thread'ine dönmek için
            }
            else
            {
                // Senkron kaydetme (Genellikle OnDestroy gibi durumlarda kullanılır ama Task ile zor)
                // Acil durumlar için yerel kayıt yeterli olmalı. Firebase için asenkron kullanmak daha iyi.
                // Yine de asenkron başlat ama bekleme (Wait() engeller, bunu yapma)
                 Debug.Log($"{logPrefix}Firebase'e kayıt gönderiliyor (senkron değil)... Ref: {statsRef.ToString()}");
                statsRef.SetRawJsonValueAsync(json); // Asenkron başlat, bekleme
                 // Debug.Log($"{logPrefix}İstatistikler Firebase'e kaydedilmek üzere gönderildi (senkron değil).");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{logPrefix}İstatistik kayıt genel hata: {e.Message}");
            if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
        }
    }

    private async Task LoadPlayerData()
    {
        Debug.Log($"{logPrefix}LoadPlayerData başladı.");
        if (string.IsNullOrEmpty(_userId)) {
             Debug.LogError($"{logPrefix}LoadPlayerData: UserID null veya boş!");
             return;
        }
        if (_databaseRef == null) {
            Debug.LogError($"{logPrefix}LoadPlayerData: DatabaseRef null!");
            return;
        }

        // Debug.Log($"{logPrefix}Yerel veri yükleniyor...");
        PlayerStatsData localData = LoadFromLocalStorage();
        int localVersion = localData?.version ?? -1;
        // Debug.Log($"{logPrefix}Yerel veri sonucu: Versiyon={localVersion} (Data: {(localData == null ? "Yok" : "Var")})");

        // Firebase'den yükleme
        var statsRef = _databaseRef.Child("users").Child(_userId).Child("stats");
        Task<DataSnapshot> task = null;
        try
        {
            Debug.Log($"{logPrefix}Firebase'den veri çekiliyor... Ref: {statsRef.ToString()}");
            task = statsRef.GetValueAsync();
            await task; // Task'in tamamlanmasını bekle
        }
        catch (Exception ex) {
             Debug.LogError($"{logPrefix}Firebase GetValueAsync başlatılırken hata: {ex.Message}");
             task = null; // Hata oluştuysa task'i null yap
        }


        PlayerStatsData firebaseData = null;
        int firebaseVersion = -1;

        // Task null değilse ve tamamlandıysa devam et
        if (task != null && task.IsCompleted)
        {
             Debug.Log($"{logPrefix}Firebase Task durumu: IsCompleted={task.IsCompleted}, IsFaulted={task.IsFaulted}, IsCanceled={task.IsCanceled}");
            try
            {
                if (task.IsFaulted)
                {
                    // Debug.LogError($"{logPrefix}Firebase istatistik yükleme hatası: {task.Exception?.Flatten().InnerExceptions.FirstOrDefault()?.Message ?? task.Exception?.Message}");
                    string innerErrorMsg = task.Exception?.Flatten()?.InnerExceptions?.FirstOrDefault()?.Message;
                    string mainErrorMsg = task.Exception?.Message;
                    Debug.LogError($"{logPrefix}Firebase istatistik yükleme hatası: {(innerErrorMsg ?? mainErrorMsg)}");
                }
                else if (task.IsCompletedSuccessfully && task.Result.Exists)
                {
                     Debug.Log($"{logPrefix}Firebase'den veri alındı. Boyut: {task.Result.GetRawJsonValue()?.Length ?? 0} bytes");
                    string json = task.Result.GetRawJsonValue();
                    try
                    {
                        firebaseData = JsonConvert.DeserializeObject<PlayerStatsData>(json);
                        firebaseVersion = firebaseData?.version ?? -1;
                        Debug.Log($"{logPrefix}Firebase'den istatistikler yüklendi (Versiyon: {firebaseVersion}, Seviye: {firebaseData?.currentLevel}, XP: {firebaseData?.currentXP})");
                    }
                    catch (Exception ex)
                    {
                        string errorMessage = $"{logPrefix}Firebase JSON çözümleme hatası: {ex.Message}\nJSON: {json.Substring(0, Math.Min(json.Length, 200))}..."; // JSON'un başını göster
                        Debug.LogError(errorMessage);
                        // Hatalı veriyi silmeyi düşünebiliriz veya loglayıp devam edebiliriz.
                        // Şimdilik devam et, yerel veri varsa o kullanılır.
                    }
                }
                else
                {
                    Debug.Log($"{logPrefix}Firebase'de istatistik verisi bulunamadı veya Task başarılı olmadı.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{logPrefix}Firebase yükleme işlemi sırasında hata (Task sonrası): {e.Message}");
            }
        } else {
             Debug.LogWarning($"{logPrefix}Firebase GetValueAsync Task'i null veya tamamlanmadı.");
        }


        // Karşılaştırma ve Yükleme
        PlayerStatsData dataToLoad = null;
        bool loadedFromFirebase = false;
        bool isNewPlayer = false; // Yeni oyuncu bayrağı

        if (firebaseVersion > localVersion)
        {
            Debug.Log($"{logPrefix}Firebase verisi daha yeni (Firebase: v{firebaseVersion}, Yerel: v{localVersion}), Firebase'den yükleniyor.");
            dataToLoad = firebaseData;
            loadedFromFirebase = true;
        }
        else if (localVersion != -1) // localVersion -1 değilse (yani yerel dosya varsa) ve Firebase'den yeni değilse
        {
            Debug.Log($"{logPrefix}Yerel veri daha yeni veya aynı (Firebase: v{firebaseVersion}, Yerel: v{localVersion}), yerel depolamadan yükleniyor.");
            dataToLoad = localData;
        }
        else
        {
            Debug.Log($"{logPrefix}Ne yerel ne de Firebase'de geçerli veri bulunamadı. Varsayılan değerler kullanılacak ve kaydedilecek.");
            isNewPlayer = true; // Yeni oyuncu
            CurrentLevel = 1;
            CurrentXP = 0;
            XPToNextLevel = CalculateXPForLevel(2); 
            _version = 0; 
            // dataToLoad null kalacak, CalculateStats çağrılacak
        }

        if (dataToLoad != null)
        {
            _version = dataToLoad.version;
            CurrentLevel = dataToLoad.currentLevel;
            CurrentXP = dataToLoad.currentXP;
            XPToNextLevel = CalculateXPForLevel(CurrentLevel + 1); 
            Debug.Log($"{logPrefix}Veriler uygulandı: Seviye={CurrentLevel}, XP={CurrentXP}/{XPToNextLevel}, Versiyon={_version}");
            if (loadedFromFirebase) { SaveToLocalStorageOnly(dataToLoad); }
        }
        else if (localVersion == -1 && firebaseVersion == -1 && !isNewPlayer) // Hem veri yok hem yeni oyuncu değilse (beklenmez)
        { Debug.LogError($"{logPrefix}dataToLoad null kaldı, ancak beklenmedik bir durum. YerelV={localVersion}, FirebaseV={firebaseVersion}"); }
        
        // --- SON HESAPLAMA VE CAN AYARLAMA --- 
        // İster veri yüklensin, ister yeni oyuncu olsun, statları hesapla
        Debug.Log($"{logPrefix}Son stat hesaplaması yapılıyor...");
        CalculateStats();

        // PlayerHealth referansı kontrolü
        if (playerHealth == null)
        {
            Debug.LogError($"{logPrefix}PlayerHealth referansı null! Başlangıç canı ayarlanamıyor.");
        }
        else
        {
            // PlayerHealth'e başlangıç (veya yüklenen) canını ayarla
            Debug.Log($"{logPrefix}PlayerHealth.SetCurrentHealth({TotalMaxHealth}) çağrılıyor...");
            playerHealth.SetCurrentHealth(TotalMaxHealth);
        }

        // Yeni oyuncuysa ilk veriyi kaydet
        if (isNewPlayer)
        {
             Debug.Log($"{logPrefix}Yeni oyuncu için ilk kayıt yapılıyor...");
             SavePlayerData(); 
        }
        
        // UI güncelleme olayını tetikle
        OnStatsUpdated?.Invoke();
        Debug.Log($"{logPrefix}LoadPlayerData bitti.");

        // --- YENİ EKLENDİ: Veri yüklendikten sonra seviyeyi diğerlerine gönder ---
        if (pv.IsMine)
        {
            Debug.Log($"{logPrefix}LoadPlayerData ({gameObject.name}) tamamlandı, mevcut seviye ({CurrentLevel}) RPC_SetLevelNetwork ile diğerlerine gönderiliyor.");
            pv.RPC("RPC_SetLevelNetwork", RpcTarget.OthersBuffered, CurrentLevel);
        }
        // --------------------------------------------------------------------
    }

    private PlayerStatsData LoadFromLocalStorage()
    {
        string path = GetLocalStoragePath();
        if (path == null) {
             Debug.LogWarning($"{logPrefix}LoadFromLocalStorage: Yerel yol alınamadı.");
             return null;
        }
        if (!File.Exists(path)) {
             // Debug.Log($"{logPrefix}LoadFromLocalStorage: Yerel dosya bulunamadı: {path}");
             return null;
        }

        try
        {
             Debug.Log($"{logPrefix}Yerel dosya okunuyor: {path}");
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) {
                 Debug.LogWarning($"{logPrefix}Yerel istatistik dosyası boş. Siliniyor: {path}");
                 try { File.Delete(path); } catch { /* Ignore delete error */ }
                 return null;
            }
            var data = JsonConvert.DeserializeObject<PlayerStatsData>(json);
             if (data == null) {
                 Debug.LogError($"{logPrefix}Yerel JSON çözümlenemedi (DeserializeObject null döndürdü). Dosya siliniyor: {path}");
                 try { File.Delete(path); } catch { /* Ignore delete error */ }
                 return null;
             }
            Debug.Log($"{logPrefix}Yerel depolamadan istatistikler yüklendi (Versiyon: {data.version}, Seviye: {data.currentLevel}, XP: {data.currentXP})");
            return data;
        }
        catch (JsonException jsonEx) {
             Debug.LogError($"{logPrefix}Yerel JSON çözümleme hatası: {jsonEx.Message}. Dosya siliniyor: {path}");
             try { File.Delete(path); } catch { /* Ignore delete error */ }
             return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"{logPrefix}Yerel istatistik yükleme hatası (Genel): {e.Message}. Dosya siliniyor: {path}");
            try { File.Delete(path); } catch { /* Ignore delete error */ }
            return null;
        }
    }

    // Sadece yerel depolamaya kaydetme (versiyon artırmadan)
    private void SaveToLocalStorageOnly(PlayerStatsData data)
    {
         if (data == null) {
              Debug.LogError($"{logPrefix}SaveToLocalStorageOnly: Kaydedilecek veri null!");
              return;
         }
         if (string.IsNullOrEmpty(_userId)) {
             Debug.LogWarning($"{logPrefix}SaveToLocalStorageOnly: UserID null, kayıt yapılamadı.");
             return;
         }
         string path = GetLocalStoragePath();
         if (path == null) {
             Debug.LogWarning($"{logPrefix}SaveToLocalStorageOnly: Yerel yol alınamadı, kayıt yapılamadı.");
             return;
         }

         // Debug.Log($"{logPrefix}SaveToLocalStorageOnly çağrıldı. Versiyon={data.version}, Seviye={data.currentLevel}, XP={data.currentXP}, Path={path}");
         try
         {
             string json = JsonConvert.SerializeObject(data);
             File.WriteAllText(path, json);
             // Debug.Log($"{logPrefix}İstatistikler sadece yerel depolamaya kaydedildi (Versiyon: {data.version}) Path: {path}");
         }
         catch (Exception e)
         {
             Debug.LogError($"{logPrefix}Sadece yerel istatistik kayıt hatası: {e.Message}");
             if (e.StackTrace != null) Debug.LogError($"{logPrefix}Stack Trace:\n{e.StackTrace}");
         }
    }


    // --- Unity Lifecycle & Photon Callbacks for Saving ---

    private void OnApplicationPause(bool pauseStatus)
    {
         Debug.Log($"{logPrefix}OnApplicationPause çağrıldı. Pause: {pauseStatus}");
        if (pauseStatus && _isInitialized && pv.IsMine)
        {
             Debug.Log($"{logPrefix}Uygulama duraklatıldı, istatistikler kaydediliyor (async)...");
            SavePlayerData(true); // Arka plana geçerken asenkron kaydet
             // Debug.Log($"{logPrefix}Uygulama duraklatıldı, istatistikler kaydedildi.");
        }
    }

    private void OnApplicationQuit()
    {
         Debug.Log($"{logPrefix}OnApplicationQuit çağrıldı.");
        if (_isInitialized && pv.IsMine)
        {
             Debug.Log($"{logPrefix}Uygulama kapatılıyor, istatistikler kaydediliyor (non-async)...");
            SavePlayerData(false); // Oyundan çıkarken senkron kaydetmeye çalış (asenkron daha güvenli olabilir)
             // Debug.Log($"{logPrefix}Uygulama kapatılıyor, istatistikler kaydedildi.");
        }
    }

    // OnDestroy yerine OnDisable daha güvenli olabilir ama Photon objeleri için Destroy daha uygun
    private void OnDestroy()
    {
         Debug.Log($"{logPrefix}OnDestroy çağrıldı.");
         // Bu obje yok edildiğinde (örneğin sahne değişimi, bağlantı kopması vb.)
         // Eğer hala Initialize olmuşsa ve yerel oyuncuysa kaydetmeyi dene.
        if (_isInitialized && pv.IsMine)
        {
             Debug.Log($"{logPrefix}PlayerStats yok ediliyor, son durum yerel olarak kaydediliyor...");
             // Senkron kaydetme genellikle burada önerilmez.
             // Yerel kayıt yeterli olabilir, Firebase asenkron devam eder.
             SaveToLocalStorageOnly(new PlayerStatsData // Mevcut durumu alıp kaydet
             {
                 version = _version,
                 timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                 currentLevel = CurrentLevel,
                 currentXP = CurrentXP,
                 totalCopper = TotalCopper
             });
             // Debug.Log($"{logPrefix}PlayerStats yok ediliyor, son durum yerel olarak kaydedildi.");
             // Firebase'e de göndermeyi deneyebiliriz ama tamamlanmasını bekleyemeyiz.
             // SavePlayerData(true); // Asenkron gönder
        }
    }


    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
         Debug.Log($"{logPrefix}OnLeftRoom çağrıldı.");
        if (_isInitialized && pv.IsMine)
        {
             Debug.Log($"{logPrefix}Odadan çıkıldı, istatistikler kaydediliyor (async)...");
            SavePlayerData(true); // Odadan çıkarken asenkron kaydet
             // Debug.Log($"{logPrefix}Odadan çıkıldı, istatistikler kaydedildi.");
        }
    }

    // --- Auth Change Handling ---
    private async void OnAuthChanged(Firebase.Auth.FirebaseUser user)
    {
         string previousUserId = _userId;
         Debug.Log($"{logPrefix}OnAuthChanged tetiklendi. Önceki UserID: {previousUserId}, Yeni User: {(user == null ? "Yok" : user.UserId)}");
         if (!_isInitialized)
         {
             Debug.LogWarning($"{logPrefix}OnAuthChanged: PlayerStats henüz initialize olmadı, işlem atlanıyor.");
             return;
         }

        string newUserId = user?.UserId;

        // Aynı kullanıcı için tekrar tetiklenirse (bazen olur), işlem yapma
        if (previousUserId == newUserId && user != null) {
            Debug.Log($"{logPrefix}OnAuthChanged: Kullanıcı aynı ({newUserId}), işlem yapılmadı.");
            return;
        }

        // Kullanıcı değiştiyse veya çıkış yaptıysa
        if (user != null)
        {
            // Yeni kullanıcı giriş yaptı veya değişti
            _userId = newUserId;
            Debug.Log($"{logPrefix}Kullanıcı değişti veya giriş yaptı: {_userId}. Veriler yeniden yükleniyor...");
            _version = 0; // Versiyonu sıfırla
            _isLoading = true; // Yükleme flag'ini set et
            await LoadPlayerData(); // Yeni kullanıcı verilerini yükle
            _isLoading = false; // Yükleme bitti
             // UI güncellemesi için olay tetikle (LoadPlayerData içinde zaten yapılıyor)
             // OnStatsUpdated?.Invoke();
             Debug.Log($"{logPrefix}OnAuthChanged: Yeni kullanıcı ({_userId}) için işlemler tamamlandı.");
        }
        else
        {
            // Kullanıcı çıkış yaptı
            Debug.Log($"{logPrefix}Kullanıcı çıkış yaptı. İstatistikler sıfırlanıyor.");
            _userId = null;
            CurrentLevel = 1;
            CurrentXP = 0;
            XPToNextLevel = CalculateXPForLevel(2); // Seviye 2 hedefini hesapla
            TotalAttack = baseAttack; // Temel değerlere dön
            TotalDefense = baseDefense;
            _version = 0;
             // UI güncellemesi için olay tetikle
             Debug.Log($"{logPrefix}OnAuthChanged: Çıkış sonrası UI güncelleme olayları tetikleniyor...");
             OnStatsUpdated?.Invoke();
             OnStatsCalculated?.Invoke();
             Debug.Log($"{logPrefix}OnAuthChanged: Çıkış işlemleri tamamlandı.");
        }
    }

    // Network üzerinden gelen seviye değerini ayarlamak için metot
    [PunRPC] // RPC olarak işaretle
    public void RPC_SetLevelNetwork(int level, PhotonMessageInfo info) // PhotonMessageInfo info EKLENDİ
    {
        // Bu script yerel oyuncuya ait ise, level değişikliğini yapmıyoruz
        // Bu RPC diğer client'larda çalışacağı için pv.IsMine kontrolü burada gereksiz,
        // çünkü zaten RPC sadece OthersBuffered ile gönderiliyor.
        // Ancak, bir şekilde Owner'da da çağrılırsa diye bir güvenlik katmanı olarak kalabilir
        // ya da kaldırılabilir. Şimdilik bırakalım, ana mantığı etkilemiyor.
        if (pv.IsMine) 
        {
            // Debug.LogWarning($"{logPrefix}RPC_SetLevelNetwork: Bu benim karakterim, zaten seviye güncel.");
            return;
        }

        // Seviye geçerli mi kontrol et (0'dan büyük olmalı)
        if (level <= 0)
        {
            Debug.LogError($"{logPrefix}RPC_SetLevelNetwork ({gameObject.name} - ViewID {pv.ViewID}): Geçersiz seviye değeri ({level}). Çağıran: {info.Sender?.NickName ?? "Bilinmiyor"}");
            return;
        }

        Debug.Log($"{logPrefix}RPC_SetLevelNetwork (Alıcı Obj: {gameObject.name}, ViewID: {pv.ViewID}, Sahip: {pv.Owner?.NickName ?? "Yok"}, IsMine: {pv.IsMine}) | Eski Seviye: {CurrentLevel}, Gelen Seviye: {level} | Çağıran Oyuncu: {info.Sender?.NickName ?? "Bilinmiyor"}");
        
        // Seviye değerini ayarla
        CurrentLevel = level;
        XPToNextLevel = CalculateXPForLevel(CurrentLevel + 1);
        
        // İstatistikleri ve UI'ı güncelle
        // CalculateStats metodu OnStatsUpdated event'ini tetikleyecek,
        // PlayerNameTag bu event üzerinden UI'ını güncelleyecek.
        CalculateStats();
        
        // MANUEL OLARAK OLAYI TETİKLE
        // CalculateStats içinde tetikleniyor olsa da, bazı durumlarda tetiklenmeyebilir
        // Bu nedenle açıkça tekrar tetikliyoruz
        OnStatsUpdated?.Invoke();
    }

    // --------------- PARA YÖNETİMİ METOTLARI ---------------

    public void AddCopper(int amount)
    {
        if (!pv.IsMine) return;
        if (amount <= 0)
        {
            Debug.LogWarning($"{logPrefix}Geçersiz miktar ({amount}) AddCopper çağrısında.");
            return;
        }
        TotalCopper += amount;
        Debug.Log($"{logPrefix}{amount} bakır eklendi. Yeni bakiye: {TotalCopper}");
        OnCopperChanged?.Invoke(TotalCopper);
        SavePlayerData(); // Para değiştiğinde kaydet
    }

    public bool TrySpendCopper(int amount)
    {
        if (!pv.IsMine) return false;
        if (amount <= 0)
        {
            Debug.LogWarning($"{logPrefix}Geçersiz miktar ({amount}) TrySpendCopper çağrısında.");
            return false;
        }

        if (TotalCopper >= amount)
        {
            TotalCopper -= amount;
            Debug.Log($"{logPrefix}{amount} bakır harcandı. Yeni bakiye: {TotalCopper}");
            OnCopperChanged?.Invoke(TotalCopper);
            SavePlayerData(); // Para değiştiğinde kaydet
            return true;
        }
        else
        {
            Debug.LogWarning($"{logPrefix}Yetersiz bakiye. {amount} bakır harcanamadı. Mevcut: {TotalCopper}");
            return false;
        }
    }
}

/// <summary>
/// Oyuncu istatistik verilerini (kaydetmek için) tutan sınıf
/// </summary>
[System.Serializable]
public class PlayerStatsData
{
    public int version;
    public long timestamp;
    public int currentLevel;
    public int currentXP;
    public int totalCopper; // Bakır için alan eklendi
} 
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using System.Linq;

/// <summary>
/// Belirli aralıklarla otomatik sistem mesajları gönderen manager
/// </summary>
public class PeriodicMessageManager : MonoBehaviourPunCallbacks
{
    public static PeriodicMessageManager Instance { get; private set; }

    [Header("Mesaj Ayarları")]
    [SerializeField] private float messageIntervalMin = 0.1f; // Minimum dakika
    [SerializeField] private float messageIntervalMax = 0.2f; // Maksimum dakika
    [SerializeField] private bool enablePeriodicMessages = true;

    [Header("Mesaj Kategorileri")]
    [SerializeField] private bool enableTips = true;
    [SerializeField] private bool enableStats = true;
    [SerializeField] private bool enableCommunity = true;
    [SerializeField] private bool enableFun = true;

    private Coroutine messageCoroutine;
    private List<PeriodicMessage> allMessages = new List<PeriodicMessage>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMessages();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (enablePeriodicMessages)
        {
            StartPeriodicMessages();
        }
    }

    private void InitializeMessages()
    {
        // --- İPUÇLARI (Daha Fazla!) ---
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Ekipmanlarınızı düzenli olarak kontrol edin, daha güçlü olanları bulabilirsiniz!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Düşmanları öldürdükçe XP kazanırsınız. Seviye atlamak için sürekli savaşın!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Savunma ekipmanları hasarı azaltır. Kask, zırh ve bot giymeyi unutmayın!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Gereksiz eşyalarınızı çöpe atarak para kazanabilirsiniz!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} PvP'de diğer oyuncularla savaşabilirsiniz, ama dikkatli olun!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Yerdeki itemler belirli aralıklarla silinir. Gördüğünüz itemleri hemen toplayın!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Server temizliği öncesi uyarı mesajı gelir. O zaman acele edin!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Yüksek seviyeli ekipmanlar daha fazla stat bonusu verir!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Can regenerasyonu için hasarsız kalmanız gerekiyor!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Envanter dolu olduğunda yeni itemlar alamazsınız!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Para birimi olarak bakır, gümüş ve altın kullanılır!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Nadir itemlar düşük şansla düşer, şanslı olmanız gerek!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Savunma ne kadar yüksekse, o kadar az hasar alırsınız!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Her seviye atlayışınızda canınız tamamen dolar!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Shop'tan item satın alabilir, kendi itemlerinizi satabilirsiniz!"));

        // --- TOPLULUK & SOSYAL ---
        allMessages.Add(new PeriodicMessage(MessageCategory.Community, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Oyun topluluğumuza katılın! Birlikte daha güçlüyüz!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Community, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Diğer oyuncularla işbirliği yapın, birlikte düşmanları yenin!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Community, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Chat ile diğer oyuncularla konuşabilirsiniz. Sosyal olun!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Community, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Yeni başlayan oyunculara yardım edin!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Community, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Fair play kurallarına uyun, herkese saygılı olun!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Community, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Takım halinde oynamak daha eğlenceli ve verimli!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Community, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Deneyimlerinizi diğer oyuncularla paylaşın!"));

        // --- İSTATİSTİKLER & SERVER ---
        allMessages.Add(new PeriodicMessage(MessageCategory.Stats, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Sunucu durumu: Stabil • Ping: İyi • Oyuncular aktif!", true));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Stats, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Server performansı için yerdeki itemler düzenli temizlenir!", true));

        allMessages.Add(new PeriodicMessage(MessageCategory.Stats, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Oyun sunucuları 7/24 aktif ve güvenli!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Stats, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Veri kaybını önlemek için otomatik kaydetme aktif!"));


        // --- EĞLENCE & MOTİVASYON (Çok Daha Fazla!) ---
        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Şans her zaman güçlülerin yanındadır!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} En büyük hazine, deneyimdir. Her savaş sizi daha güçlü yapar!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Güç, cesaret, kararlılık ve biraz da şans ile her düşman yenilebilir!"));
        
        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Gecenin karanlığında bile umut ışığı söner mi? Savaşa devam!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Her düşürülen item bir fırsat! Ama çabuk olun, temizlik zamanı yaklaşıyor!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Bugün hangi maceralara atılacaksınız? Keşfe çıkın!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Her levelde yeni güçler sizi bekliyor!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Düşmanlar korkup kaçsın! Gücünüzü gösterin!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Efsanevi ekipmanlar sadece en cesur savaşçıların!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Bu dünyada en güçlü kim olacak? Belki de sizsiniz!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Arkadaşlarınızla beraber oynamak çok daha eğlenceli!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Sınırlarınızı aşın, daha da güçlü olun!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Her düşman, yeni bir deneyim ve XP demektir!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Bu dünya sizin maceranızı bekliyor!"));

        // --- OYUN MEKANİKLERİ & BİLGİLER ---
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Ölünce spawn noktasına dönüp yeniden doğarsınız!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Hasar aldıktan sonra kısa süre dokunulmazsınız!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Yüksek seviyeli oyuncular daha fazla HP'ye sahiptir!"));

        // --- OYUN KONTROLLERİ & TEMEL MEKANİK ---
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Tab tuşu ile envanteri açıp kapatabilirsiniz!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} T tuşu ile chat'i açıp kapatabilirsiniz!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} ESC tuşu ile menüleri kapatabilirsiniz!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Mouse ile itemlerin üzerine gelin, detaylarını görün!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} NPC'lere yaklaşıp E tuşuna basarak shop'ları açabilirsiniz!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Seviye ve XP bilginiz menüden takip edilebilir!"));

        // --- UI & ARAYÜZ İPUÇLARI ---
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} İtem tooltip'leri size detaylı bilgi verir!"));

        // --- ELİT DÜŞMANLAR & ZORLU İÇERİK ---
        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Elite düşmanlar normal düşmanlardan çok daha güçlüdür!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Elite düşmanlar daha fazla XP ve nadir loot verir!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Elite düşmanları isimlerinden tanıyabilirsiniz!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Elite düşmanlardan nadir eşyalar çıkma olasılığı daha yüksektir!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Elite düşman görürseniz şansınızı deneyin!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Tips, 
            $"{MessageColorUtils.ColorizeTag("İPUCU", TagType.Tip)} Elite düşmanlar nadir spawn olur, fırsatı kaçırmayın!"));

        allMessages.Add(new PeriodicMessage(MessageCategory.Fun, 
            $"{MessageColorUtils.ColorizeTag("SUNUCU")} Elite düşmanları avlamak büyük risk, büyük ödül demektir!"));

        Debug.Log($"PeriodicMessageManager: {allMessages.Count} mesaj yüklendi.");
    }

    public void StartPeriodicMessages()
    {
        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }
        messageCoroutine = StartCoroutine(MessageRoutine());
        Debug.Log("PeriodicMessageManager: Periyodik mesajlar başlatıldı.");
    }

    public void StopPeriodicMessages()
    {
        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
            messageCoroutine = null;
        }
        Debug.Log("PeriodicMessageManager: Periyodik mesajlar durduruldu.");
    }

    private IEnumerator MessageRoutine()
    {
        // İlk mesaj için biraz bekle (oyuncular bağlansın)
        yield return new WaitForSeconds(60f); // 1 dakika bekle

        while (enablePeriodicMessages)
        {
            // Sadece Master Client mesaj göndersin
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            {
                SendRandomPeriodicMessage();
            }

            // Rastgele aralıkta bekle
            float waitTime = Random.Range(messageIntervalMin, messageIntervalMax) * 60f; // Dakikayı saniyeye çevir
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void SendRandomPeriodicMessage()
    {
        // Aktif kategorilere göre mesajları filtrele
        List<PeriodicMessage> availableMessages = allMessages.Where(msg =>
        {
            switch (msg.category)
            {
                case MessageCategory.Tips: return enableTips;
                case MessageCategory.Stats: return enableStats;
                case MessageCategory.Community: return enableCommunity;
                case MessageCategory.Fun: return enableFun;
                default: return true;
            }
        }).ToList();

        if (availableMessages.Count == 0)
        {
            Debug.LogWarning("PeriodicMessageManager: Gönderilebilecek mesaj bulunamadı!");
            return;
        }

        // Rastgele mesaj seç
        PeriodicMessage selectedMessage = availableMessages[Random.Range(0, availableMessages.Count)];
        
        // Dinamik mesajlarsa güncelle
        if (selectedMessage.isDynamic)
        {
            selectedMessage = UpdateDynamicMessage(selectedMessage);
        }

        // ChatManager ile gönder
        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
        {
            SystemMessageType messageType = GetSystemMessageType(selectedMessage.category);
            chatManager.SendSystemMessage(selectedMessage.content, messageType);
            Debug.Log($"Periyodik mesaj gönderildi: {selectedMessage.content}");
        }
        else
        {
            Debug.LogWarning("PeriodicMessageManager: ChatManager bulunamadı!");
        }
    }

    private PeriodicMessage UpdateDynamicMessage(PeriodicMessage originalMessage)
    {
        string updatedContent = originalMessage.content;

        // Dinamik içerik güncellemeleri
        if (originalMessage.content.Contains("Toplam kayıtlı oyuncu"))
        {
            int playerCount = PhotonNetwork.CountOfPlayers;
            updatedContent = $"{MessageColorUtils.ColorizeTag("SUNUCU")} Şu anda {MessageColorUtils.ColorizeNumber(playerCount)} oyuncu çevrimiçi!";
        }
        else if (originalMessage.content.Contains("Sunucu durumu"))
        {
            int playerCount = PhotonNetwork.CountOfPlayers;
            string ping = PhotonNetwork.GetPing() < 100 ? "İyi" : "Orta";
            updatedContent = $"{MessageColorUtils.ColorizeTag("SUNUCU")} Sunucu: Stabil • Ping: {ping} • Çevrimiçi: {MessageColorUtils.ColorizeNumber(playerCount)}";
        }

        return new PeriodicMessage(originalMessage.category, updatedContent, originalMessage.isDynamic);
    }

    private SystemMessageType GetSystemMessageType(MessageCategory category)
    {
        switch (category)
        {
            case MessageCategory.Tips: return SystemMessageType.General;
            case MessageCategory.Stats: return SystemMessageType.General;
            case MessageCategory.Community: return SystemMessageType.General;
            case MessageCategory.Fun: return SystemMessageType.General;
            default: return SystemMessageType.General;
        }
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        // Yeni Master Client periyodik mesajları yönetecek
        if (PhotonNetwork.IsMasterClient && enablePeriodicMessages)
        {
            Debug.Log("PeriodicMessageManager: Yeni Master Client olarak periyodik mesajlar başlatılıyor.");
            StartPeriodicMessages();
        }
        else if (messageCoroutine != null)
        {
            Debug.Log("PeriodicMessageManager: Master Client değilim, periyodik mesajlar durduruluyor.");
            StopCoroutine(messageCoroutine);
            messageCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

/// <summary>
/// Periyodik mesaj kategorileri
/// </summary>
public enum MessageCategory
{
    Tips,       // İpuçları
    Stats,      // İstatistikler  
    Community,  // Topluluk
    Fun         // Eğlence/Motivasyon
}

/// <summary>
/// Periyodik mesaj verisi
/// </summary>
[System.Serializable]
public class PeriodicMessage
{
    public MessageCategory category;
    public string content;
    public bool isDynamic; // Dinamik olarak güncellenen mesaj mı?

    public PeriodicMessage(MessageCategory cat, string msg, bool dynamic = false)
    {
        category = cat;
        content = msg;
        isDynamic = dynamic;
    }
} 
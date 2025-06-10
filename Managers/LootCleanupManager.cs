using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

/// <summary>
/// Yerdeki loot itemlarını belirli aralıklarla temizleyen manager
/// Server performansını korumak ve oyun deneyimini iyileştirmek için
/// </summary>
public class LootCleanupManager : MonoBehaviourPunCallbacks
{
    public static LootCleanupManager Instance { get; private set; }

    [Header("Temizlik Ayarları")]
    [SerializeField] private float cleanupIntervalMinutes = 8f; // Temizlik aralığı (dakika)
    [SerializeField] private float warningTimeSeconds = 30f; // Uyarı süresi (saniye)
    [SerializeField] private bool enableLootCleanup = true; // Temizlik sistemini açma/kapama

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Coroutine cleanupCoroutine;
    private bool isCleanupActive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (enableLootCleanup)
        {
            StartLootCleanup();
        }
    }

    /// <summary>
    /// Loot temizlik sistemini başlat
    /// </summary>
    public void StartLootCleanup()
    {
        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
        }
        
        cleanupCoroutine = StartCoroutine(LootCleanupRoutine());
        
        if (showDebugLogs)
        {
            Debug.Log($"LootCleanupManager: Temizlik sistemi başlatıldı. Aralık: {cleanupIntervalMinutes} dakika");
        }
    }

    /// <summary>
    /// Loot temizlik sistemini durdur
    /// </summary>
    public void StopLootCleanup()
    {
        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
            cleanupCoroutine = null;
        }

        isCleanupActive = false;
        
        if (showDebugLogs)
        {
            Debug.Log("LootCleanupManager: Temizlik sistemi durduruldu.");
        }
    }

    /// <summary>
    /// Ana temizlik döngüsü
    /// </summary>
    private IEnumerator LootCleanupRoutine()
    {
        // İlk temizlik için bekle (oyuncular toplanmaya fırsat versin)
        yield return new WaitForSeconds(cleanupIntervalMinutes * 60f);

        while (enableLootCleanup)
        {
            // Sadece Master Client temizlik yapabilir
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            {
                yield return StartCoroutine(PerformCleanupSequence());
            }

            // Bir sonraki temizlik için bekle
            yield return new WaitForSeconds(cleanupIntervalMinutes * 60f);
        }
    }

    /// <summary>
    /// Temizlik sırasını yönet (uyarı → temizlik → bilgi)
    /// </summary>
    private IEnumerator PerformCleanupSequence()
    {
        if (isCleanupActive) yield break; // Eğer zaten temizlik yapılıyorsa atla

        isCleanupActive = true;

        // 1. Önce kaç item olduğunu kontrol et
        LootItem[] currentLootItems = FindObjectsOfType<LootItem>();
        int itemCount = currentLootItems.Length;

        if (itemCount == 0)
        {
            if (showDebugLogs)
            {
                Debug.Log("LootCleanupManager: Temizlenecek item yok, temizlik atlandı.");
            }
            isCleanupActive = false;
            yield break;
        }

        if (showDebugLogs)
        {
            Debug.Log($"LootCleanupManager: {itemCount} item bulundu, temizlik sırası başlıyor.");
        }

        // 2. Uyarı mesajı gönder
        SendWarningMessage(itemCount);

        // 3. Uyarı süresi kadar bekle
        yield return new WaitForSeconds(warningTimeSeconds);

        // 4. Temizliği gerçekleştir
        int cleanedCount = PerformActualCleanup();

        // 5. Temizlik tamamlandı mesajı gönder
        if (cleanedCount > 0)
        {
            SendCleanupCompleteMessage(cleanedCount);
        }

        isCleanupActive = false;
    }

    /// <summary>
    /// Uyarı mesajını gönder
    /// </summary>
    private void SendWarningMessage(int itemCount)
    {
        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
        {
            string itemCountColored = MessageColorUtils.ColorizeNumber(itemCount);
            string timeColored = MessageColorUtils.ColorizeNumber((int)warningTimeSeconds);
            
            string warningMessage = $"{MessageColorUtils.ColorizeTag("UYARI", TagType.Warning)} Dikkat! {timeColored} saniye içinde yerdeki {itemCountColored} item silinecektir! Toplamak istediğiniz varsa acele edin!";
            
            chatManager.SendSystemMessage(warningMessage, SystemMessageType.General);
            
            if (showDebugLogs)
            {
                Debug.Log($"LootCleanupManager: Uyarı mesajı gönderildi - {itemCount} item");
            }
        }
    }

    /// <summary>
    /// Gerçek temizlik işlemini yap
    /// </summary>
    private int PerformActualCleanup()
    {
        if (!PhotonNetwork.IsMasterClient) return 0;

        // Tüm LootItem'ları bul
        LootItem[] lootItems = FindObjectsOfType<LootItem>();
        int cleanedCount = 0;

        foreach (LootItem lootItem in lootItems)
        {
            if (lootItem != null && lootItem.gameObject != null)
            {
                PhotonView lootPV = lootItem.GetComponent<PhotonView>();
                if (lootPV != null)
                {
                    try
                    {
                        // Network üzerinden yok et
                        PhotonNetwork.Destroy(lootItem.gameObject);
                        cleanedCount++;
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"LootCleanupManager: Item temizlendi (ViewID: {lootPV.ViewID})");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"LootCleanupManager: Item silinirken hata (ViewID: {lootPV.ViewID}): {e.Message}");
                    }
                }
                else
                {
                    // PhotonView yoksa yerel olarak sil
                    Destroy(lootItem.gameObject);
                    cleanedCount++;
                    
                    if (showDebugLogs)
                    {
                        Debug.LogWarning("LootCleanupManager: PhotonView olmayan item yerel olarak silindi");
                    }
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"LootCleanupManager: Temizlik tamamlandı. {cleanedCount} item silindi.");
        }

        return cleanedCount;
    }

    /// <summary>
    /// Temizlik tamamlandı mesajını gönder
    /// </summary>
    private void SendCleanupCompleteMessage(int cleanedCount)
    {
        ChatManager chatManager = FindObjectOfType<ChatManager>();
        if (chatManager != null)
        {
            string cleanedCountColored = MessageColorUtils.ColorizeNumber(cleanedCount);
            string nextCleanupColored = MessageColorUtils.ColorizeNumber((int)cleanupIntervalMinutes);
            
            string completeMessage = $"{MessageColorUtils.ColorizeTag("SUNUCU")} Server temizliği tamamlandı! {cleanedCountColored} item silindi. Bir sonraki temizlik: {nextCleanupColored} dakika sonra.";
            
            chatManager.SendSystemMessage(completeMessage, SystemMessageType.General);
            
            if (showDebugLogs)
            {
                Debug.Log($"LootCleanupManager: Temizlik tamamlandı mesajı gönderildi - {cleanedCount} item silindi");
            }
        }
    }

    /// <summary>
    /// Manuel temizlik komutu (debug/admin için)
    /// </summary>
    [ContextMenu("Manuel Temizlik Yap")]
    public void ManualCleanup()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("LootCleanupManager: Sadece Master Client manuel temizlik yapabilir!");
            return;
        }

        if (isCleanupActive)
        {
            Debug.LogWarning("LootCleanupManager: Temizlik zaten devam ediyor!");
            return;
        }

        StartCoroutine(PerformCleanupSequence());
    }

    /// <summary>
    /// Master Client değiştiğinde çağrılır
    /// </summary>
    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient && enableLootCleanup)
        {
            Debug.Log("LootCleanupManager: Yeni Master Client olarak temizlik sistemi başlatılıyor.");
            StartLootCleanup();
        }
        else if (cleanupCoroutine != null)
        {
            Debug.Log("LootCleanupManager: Master Client değilim, temizlik sistemi durduruluyor.");
            StopCoroutine(cleanupCoroutine);
            cleanupCoroutine = null;
            isCleanupActive = false;
        }
    }

    /// <summary>
    /// Temizlik ayarlarını runtime'da değiştir
    /// </summary>
    public void UpdateCleanupSettings(float newIntervalMinutes, float newWarningSeconds)
    {
        cleanupIntervalMinutes = Mathf.Max(1f, newIntervalMinutes); // En az 1 dakika
        warningTimeSeconds = Mathf.Max(5f, newWarningSeconds); // En az 5 saniye

        if (showDebugLogs)
        {
            Debug.Log($"LootCleanupManager: Ayarlar güncellendi - Aralık: {cleanupIntervalMinutes}dk, Uyarı: {warningTimeSeconds}s");
        }

        // Eğer sistem çalışıyorsa yeniden başlat
        if (enableLootCleanup && cleanupCoroutine != null)
        {
            StartLootCleanup();
        }
    }

    /// <summary>
    /// Temizlik sistemini açma/kapama
    /// </summary>
    public void SetCleanupEnabled(bool enabled)
    {
        enableLootCleanup = enabled;

        if (enabled)
        {
            StartLootCleanup();
        }
        else
        {
            StopLootCleanup();
        }

        if (showDebugLogs)
        {
            Debug.Log($"LootCleanupManager: Sistem {(enabled ? "açıldı" : "kapatıldı")}");
        }
    }

    /// <summary>
    /// Şu anki loot sayısını al
    /// </summary>
    public int GetCurrentLootCount()
    {
        LootItem[] lootItems = FindObjectsOfType<LootItem>();
        return lootItems.Length;
    }

    private void OnDestroy()
    {
        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Debug için Inspector'da bilgi göster
    private void OnValidate()
    {
        cleanupIntervalMinutes = Mathf.Max(1f, cleanupIntervalMinutes);
        warningTimeSeconds = Mathf.Max(5f, warningTimeSeconds);
    }
} 
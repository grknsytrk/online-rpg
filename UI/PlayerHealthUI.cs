using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Efekt türleri enum'u
public enum EffectType
{
    Healing,        // Sürekli can yenilme
    Shield          // Kalkan/Dokunulmazlık
}

// Efekt verisi sınıfı
[System.Serializable]
public class EffectData
{
    public EffectType effectType;
    public float duration;
    public int stackCount = 1;
    public Color effectColor = Color.white;
    public Sprite effectIcon;
}

public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas healthCanvas;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private TextMeshProUGUI healthValueText;
    
    [Header("Effect System")]
    [SerializeField] private Transform effectLayoutGroup; // Effect Layout Group referansı
    [SerializeField] private GameObject effectPrefab; // Effects prefabı (sadece Image componenti olan basit prefab)
    [SerializeField] private int maxEffectsShown = 10; // Maksimum gösterilecek efekt sayısı
    
    [Header("Effect Icons")]
    [SerializeField] private Sprite healingIcon; // Sürekli can yenilme icon'u
    [SerializeField] private Sprite shieldIcon; // Kalkan icon'u
    
    [Header("Default Values")]
    [SerializeField] private int defaultMaxHealth = 100; // Varsayılan maksimum can

    private PlayerHealth playerHealth;
    private Camera mainCamera;
    
    // Efekt sistemi için değişkenler
    private Dictionary<EffectType, GameObject> activeEffectObjects = new Dictionary<EffectType, GameObject>();
    private Dictionary<EffectType, EffectData> activeEffects = new Dictionary<EffectType, EffectData>();
    private List<EffectType> effectQueue = new List<EffectType>(); // Efekt sırası

    private void Awake()
    {
        mainCamera = Camera.main;
        
        // Tag this canvas for the PlayerHealth script to find
        if (healthCanvas != null)
        {
            healthCanvas.gameObject.tag = "HealthCanvas";
        }
        
        // Başlangıçta varsayılan değerleri kullan
        SetDefaultUIValues();
    }
    
    private void SetDefaultUIValues()
    {
        // Başlangıçta varsayılan değerleri kullan
        if (healthSlider != null)
        {
            healthSlider.maxValue = defaultMaxHealth;
            healthSlider.value = defaultMaxHealth;
        }
        
        if (healthValueText != null)
        {
            healthValueText.text = $"{defaultMaxHealth}/{defaultMaxHealth}";
        }
        
        // Efekt sistemi başlangıç kontrolü
        ValidateEffectSystem();
    }
    
    private void ValidateEffectSystem()
    {
        if (effectLayoutGroup == null)
        {
            Debug.LogWarning("PlayerHealthUI: Effect Layout Group atanmamış!");
        }
        
        if (effectPrefab == null)
        {
            Debug.LogWarning("PlayerHealthUI: Effect Prefab atanmamış!");
        }
    }

    private void Start()
    {
        // Oyuncuyu coroutine ile bulmaya çalış (birkaç deneme yaparak)
        StartCoroutine(FindPlayerRoutine());
    }
    
    private IEnumerator FindPlayerRoutine()
    {
        int maxAttempts = 20;  // Maksimum 20 deneme
        int attempts = 0;
        
        while (playerHealth == null && attempts < maxAttempts)
        {
            // Yerel oyuncuyu bul
            PlayerController[] players = FindObjectsOfType<PlayerController>();
            foreach (var player in players)
            {
                if (player != null && player.PV != null && player.PV.IsMine)
                {
                    Debug.Log("PlayerHealthUI: Yerel oyuncu bulundu!");
                    playerHealth = player.GetComponent<PlayerHealth>();
                    
                    if (playerHealth == null)
                    {
                        // Eğer oyuncuda sağlık bileşeni yoksa ekle
                        playerHealth = player.gameObject.AddComponent<PlayerHealth>();
                    }
                    
                    // Oyuncu bulunduğunda UI'ı hemen güncelle
                    UpdateHealthUI();
                    break;
                }
            }
            
            if (playerHealth == null)
            {
                // Biraz bekle ve tekrar dene
                yield return new WaitForSeconds(0.5f);
                attempts++;
                Debug.Log($"PlayerHealthUI: Oyuncu aranıyor... Deneme {attempts}/{maxAttempts}");
            }
        }
        
        if (playerHealth == null)
        {
            Debug.LogWarning($"PlayerHealthUI: {maxAttempts} denemeden sonra oyuncu bulunamadı!");
        }
        else
        {
            // Oyuncu bulunduğunda UI'ı bir kez daha güncelle
            UpdateHealthUI();
        }
    }

    private void Update()
    {
        if (playerHealth != null)
        {
            UpdateHealthUI();
        }
    }

    private void UpdateHealthUI()
    {
        int currentHealth = playerHealth.GetCurrentHealth();
        int maxHealth = playerHealth.GetMaxHealth();
        
        // Slider'ı güncelle
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth; // Dinamik maksimum değeri kullan
            healthSlider.value = currentHealth;
        }
        
        // Metni güncelle
        if (healthValueText != null)
        {
            healthValueText.text = $"{currentHealth}/{maxHealth}";
        }
        
        // Efektleri güncelle
        UpdateEffectDurations();
    }
    
    #region Effect System Methods
    
    /// <summary>
    /// Yeni bir efekt ekler veya mevcut efekti günceller
    /// </summary>
    public void AddEffect(EffectType effectType, float duration, int stackCount = 1, Sprite customIcon = null)
    {
        if (effectLayoutGroup == null || effectPrefab == null)
        {
            Debug.LogWarning("PlayerHealthUI: Efekt sistemi düzgün kurulmamış!");
            return;
        }
        
        // Eğer efekt zaten varsa, sadece süreyi güncelle
        if (activeEffects.ContainsKey(effectType))
        {
            activeEffects[effectType].duration = duration;
            Debug.Log($"Efekt süresi güncellendi: {effectType} - Yeni süre: {duration}s");
            return;
        }
        
        // Yeni efekt oluştur
        EffectData newEffect = new EffectData
        {
            effectType = effectType,
            duration = duration,
            stackCount = stackCount,
            effectIcon = customIcon != null ? customIcon : GetEffectIcon(effectType)
        };
        
        CreateNewEffect(effectType, newEffect);
        Debug.Log($"Efekt eklendi: {effectType} - Süre: {duration}s");
    }
    

    
    /// <summary>
    /// Yeni efekt objesi oluşturur (basit versiyon)
    /// </summary>
    private void CreateNewEffect(EffectType effectType, EffectData effectData)
    {
        // Maksimum efekt sayısını kontrol et
        if (activeEffects.Count >= maxEffectsShown)
        {
            RemoveOldestEffect();
        }
        
        // Yeni efekt objesi oluştur (sadece Image component'i olan basit prefab)
        GameObject newEffectObj = Instantiate(effectPrefab, effectLayoutGroup);
        activeEffectObjects[effectType] = newEffectObj;
        activeEffects[effectType] = effectData;
        effectQueue.Add(effectType);
        
        // Icon'u ayarla
        SetupEffectVisual(newEffectObj, effectData);
    }
    
    /// <summary>
    /// Efekt görselini ayarlar (basit versiyon - sadece icon)
    /// </summary>
    private void SetupEffectVisual(GameObject effectObj, EffectData effectData)
    {
        // Efekt ikonunu ayarla
        Image iconImage = effectObj.GetComponent<Image>();
        if (iconImage != null && effectData.effectIcon != null)
        {
            iconImage.sprite = effectData.effectIcon;
            iconImage.color = Color.white; // Icon'un orijinal rengini koru
        }
        
        // Tooltip bilgisi ekle (opsiyonel)
        EffectTooltip tooltip = effectObj.GetComponent<EffectTooltip>();
        if (tooltip != null)
        {
            tooltip.SetEffectInfo(effectData.effectType, effectData.duration, effectData.stackCount);
        }
    }
    

    
    /// <summary>
    /// Efekt türüne göre icon döndürür
    /// </summary>
    private Sprite GetEffectIcon(EffectType effectType)
    {
        return effectType switch
        {
            EffectType.Healing => healingIcon,
            EffectType.Shield => shieldIcon,
            _ => null
        };
    }
    
    /// <summary>
    /// Belirtilen efekti kaldırır
    /// </summary>
    public void RemoveEffect(EffectType effectType)
    {
        if (activeEffects.ContainsKey(effectType))
        {
            // Efekt objesini yok et
            if (activeEffectObjects.ContainsKey(effectType))
            {
                Destroy(activeEffectObjects[effectType]);
                activeEffectObjects.Remove(effectType);
            }
            
            // Efekt verisini kaldır
            activeEffects.Remove(effectType);
            effectQueue.Remove(effectType);
            
            Debug.Log($"Efekt kaldırıldı: {effectType}");
        }
    }
    
    /// <summary>
    /// Tüm efektleri temizler
    /// </summary>
    public void ClearAllEffects()
    {
        foreach (var effectObj in activeEffectObjects.Values)
        {
            if (effectObj != null)
            {
                Destroy(effectObj);
            }
        }
        
        activeEffectObjects.Clear();
        activeEffects.Clear();
        effectQueue.Clear();
        
        Debug.Log("Tüm efektler temizlendi");
    }
    
    /// <summary>
    /// En eski efekti kaldırır (maksimum sayı aşıldığında)
    /// </summary>
    private void RemoveOldestEffect()
    {
        if (effectQueue.Count > 0)
        {
            EffectType oldestEffect = effectQueue[0];
            RemoveEffect(oldestEffect);
        }
    }
    
    /// <summary>
    /// Efekt sürelerini günceller ve süresi dolan efektleri kaldırır
    /// </summary>
    private void UpdateEffectDurations()
    {
        List<EffectType> expiredEffects = new List<EffectType>();
        
        foreach (var kvp in activeEffects)
        {
            EffectType effectType = kvp.Key;
            EffectData effectData = kvp.Value;
            
            // Süreyi azalt
            effectData.duration -= Time.deltaTime;
            
            // Süre dolmuşsa kaldırılacaklar listesine ekle
            if (effectData.duration <= 0)
            {
                expiredEffects.Add(effectType);
            }
            else
            {
                // Görsel güncelleme (progress bar vs. için)
                UpdateEffectProgress(effectType, effectData);
            }
        }
        
        // Süresi dolan efektleri kaldır
        foreach (EffectType expiredEffect in expiredEffects)
        {
            RemoveEffect(expiredEffect);
        }
    }
    
    /// <summary>
    /// Efekt ilerlemesini günceller (progress bar vs.)
    /// </summary>
    private void UpdateEffectProgress(EffectType effectType, EffectData effectData)
    {
        if (activeEffectObjects.ContainsKey(effectType))
        {
            GameObject effectObj = activeEffectObjects[effectType];
            
            // Progress bar varsa güncelle
            Slider progressBar = effectObj.GetComponentInChildren<Slider>();
            if (progressBar != null)
            {
                // Başlangıç süresini bilmediğimiz için bu kısım opsiyonel
                // İhtiyaca göre başlangıç süresi de kaydedilebilir
            }
        }
    }
    
    /// <summary>
    /// Belirtilen efektin aktif olup olmadığını kontrol eder
    /// </summary>
    public bool HasEffect(EffectType effectType)
    {
        return activeEffects.ContainsKey(effectType);
    }
    
    /// <summary>
    /// Belirtilen efektin kalan süresini döndürür
    /// </summary>
    public float GetEffectRemainingTime(EffectType effectType)
    {
        if (activeEffects.ContainsKey(effectType))
        {
            return activeEffects[effectType].duration;
        }
        return 0f;
    }
    
    /// <summary>
    /// Aktif efekt sayısını döndürür
    /// </summary>
    public int GetActiveEffectCount()
    {
        return activeEffects.Count;
    }
    
    #endregion
    
    #region Public Methods for Testing
    
    /// <summary>
    /// Test için efekt ekleme metodları
    /// </summary>
    [ContextMenu("Test Healing Effect")]
    public void TestHealingEffect()
    {
        AddEffect(EffectType.Healing, 5f, 1);
    }
    
    [ContextMenu("Test Multiple Effects")]
    public void TestMultipleEffects()
    {
        AddEffect(EffectType.Healing, 5f, 1);
        AddEffect(EffectType.Shield, 15f, 1);
    }
    
    [ContextMenu("Clear All Effects")]
    public void TestClearEffects()
    {
        ClearAllEffects();
    }
    
    #endregion
} 
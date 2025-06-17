using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Basit efekt icon gösterme sistemi - sadece manuel show/hide
/// Süre takibi yok, mevcut sistemler icon'ları manuel kontrol eder
/// </summary>
public class EffectManager : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private bool debugMode = false; // Debug modu
    
    // Singleton pattern
    public static EffectManager Instance { get; private set; }
    
    // Aktif görsel efektler (sadece hangi iconların gösterildiğini tutar)
    private HashSet<EffectType> activeVisualEffects = new HashSet<EffectType>();
    
    // Component referansları
    private PlayerHealthUI playerHealthUI;
    
    private void Awake()
    {
        // Singleton kontrolü
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        // PlayerHealthUI'ı bul
        StartCoroutine(FindPlayerHealthUI());
    }
    
    private System.Collections.IEnumerator FindPlayerHealthUI()
    {
        int attempts = 0;
        int maxAttempts = 20;
        
        while (playerHealthUI == null && attempts < maxAttempts)
        {
            playerHealthUI = FindObjectOfType<PlayerHealthUI>();
            if (playerHealthUI == null)
            {
                yield return new WaitForSeconds(0.5f);
                attempts++;
            }
        }
        
        if (playerHealthUI == null)
        {
            Debug.LogWarning("EffectManager: PlayerHealthUI bulunamadı!");
        }
        else
        {
            Debug.Log("EffectManager: PlayerHealthUI bulundu ve bağlandı.");
        }
    }
    
    /// <summary>
    /// Efekt iconunu göster (Can yenilme başladığında çağır)
    /// </summary>
    public void ShowEffect(EffectType effectType)
    {
        if (playerHealthUI == null)
        {
            Debug.LogWarning("EffectManager: PlayerHealthUI bulunamadı, efekt gösterilemiyor!");
            return;
        }
        
        // Zaten gösteriliyorsa tekrar gösterme
        if (activeVisualEffects.Contains(effectType))
        {
            if (debugMode)
            {
                Debug.Log($"Efekt zaten gösterilmekte: {effectType}");
            }
            return;
        }
        
        // Efekti aktif listeye ekle
        activeVisualEffects.Add(effectType);
        
        // UI'da icon'u göster (süresiz)
        playerHealthUI.AddEffect(effectType, 999f); // Çok uzun süre (manual gizlenene kadar)
        
        if (debugMode)
        {
            Debug.Log($"Efekt ikonu gösterildi: {effectType}");
        }
    }
    
    /// <summary>
    /// Efekt iconunu gizle (Can dolunca veya yenilme kesilince çağır)
    /// </summary>
    public void HideEffect(EffectType effectType)
    {
        if (!activeVisualEffects.Contains(effectType))
        {
            if (debugMode)
            {
                Debug.Log($"Efekt zaten gizlenmişti: {effectType}");
            }
            return;
        }
        
        // Efekti aktif listeden kaldır
        activeVisualEffects.Remove(effectType);
        
        // UI'dan icon'u kaldır
        if (playerHealthUI != null)
        {
            playerHealthUI.RemoveEffect(effectType);
        }
        
        if (debugMode)
        {
            Debug.Log($"Efekt ikonu gizlendi: {effectType}");
        }
    }
    
    /// <summary>
    /// Belirtilen efektin gösterilip gösterilmediğini kontrol eder
    /// </summary>
    public bool IsEffectShown(EffectType effectType)
    {
        return activeVisualEffects.Contains(effectType);
    }
    
    /// <summary>
    /// Tüm efekt iconlarını gizler
    /// </summary>
    public void HideAllEffects()
    {
        List<EffectType> effectsToHide = new List<EffectType>(activeVisualEffects);
        
        foreach (EffectType effectType in effectsToHide)
        {
            HideEffect(effectType);
        }
        
        if (debugMode)
        {
            Debug.Log("Tüm efekt iconları gizlendi");
        }
    }
    
    #region Convenience Methods - Basit kullanım için hazır metodlar
    
    /// <summary>
    /// Sürekli can yenilme başladığında çağır
    /// </summary>
    public void ShowHealingEffect()
    {
        ShowEffect(EffectType.Healing);
    }
    
    /// <summary>
    /// Sürekli can yenilme bittiğinde çağır
    /// </summary>
    public void HideHealingEffect()
    {
        HideEffect(EffectType.Healing);
    }
    
    /// <summary>
    /// Dokunulmazlık/Kalkan efekti başladığında çağır (yeniden doğduktan sonra)
    /// </summary>
    public void ShowShieldEffect()
    {
        ShowEffect(EffectType.Shield);
    }
    
    /// <summary>
    /// Dokunulmazlık/Kalkan efekti bittiğinde çağır
    /// </summary>
    public void HideShieldEffect()
    {
        HideEffect(EffectType.Shield);
    }
    
    #endregion
    
    #region Test Methods
    
    [ContextMenu("Test Show Healing")]
    public void TestShowHealing()
    {
        ShowHealingEffect();
    }
    
    [ContextMenu("Test Hide Healing")]
    public void TestHideHealing()
    {
        HideHealingEffect();
    }
    
    [ContextMenu("Test Multiple Effects")]
    public void TestMultipleEffects()
    {
        ShowHealingEffect();
        ShowShieldEffect();
    }
    
    [ContextMenu("Hide All Effects")]
    public void TestHideAllEffects()
    {
        HideAllEffects();
    }
    
    #endregion
} 
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Efekt tooltip sistemi - efekt üzerine mouse geldiğinde bilgi gösterir
/// </summary>
public class EffectTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip Settings")]
    [SerializeField] private GameObject tooltipPanel; // Tooltip paneli
    [SerializeField] private TextMeshProUGUI tooltipText; // Tooltip yazısı
    [SerializeField] private float showDelay = 0.5f; // Gösterim gecikmesi
    [SerializeField] private Vector2 tooltipOffset = new Vector2(10, 10); // Tooltip konumu offset
    
    // Efekt bilgileri
    private EffectType effectType;
    private float remainingDuration;
    private int stackCount;
    private string effectDescription;
    
    // Tooltip kontrol değişkenleri
    private bool isHovering = false;
    private Coroutine showTooltipCoroutine;
    private Canvas parentCanvas;
    private RectTransform tooltipRect;
    
    private void Awake()
    {
        // Parent canvas'ı bul
        parentCanvas = GetComponentInParent<Canvas>();
        
        // Tooltip paneli başlangıçta kapalı
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        }
        
        // Eğer tooltip paneli inspector'da atanmamışsa, otomatik oluştur
        if (tooltipPanel == null)
        {
            CreateTooltipPanel();
        }
    }
    
    /// <summary>
    /// Tooltip panelini otomatik oluşturur
    /// </summary>
    private void CreateTooltipPanel()
    {
        // Ana tooltip container oluştur
        GameObject tooltipGO = new GameObject("TooltipPanel");
        tooltipGO.transform.SetParent(transform);
        
        // RectTransform ekle
        tooltipRect = tooltipGO.AddComponent<RectTransform>();
        tooltipRect.sizeDelta = new Vector2(200, 80);
        
        // Image component ekle (arkaplan)
        Image bgImage = tooltipGO.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Koyu yarı şeffaf
        
        // Text componenti oluştur
        GameObject textGO = new GameObject("TooltipText");
        textGO.transform.SetParent(tooltipGO.transform);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        tooltipText = textGO.AddComponent<TextMeshProUGUI>();
        tooltipText.text = "Tooltip Text";
        tooltipText.fontSize = 14;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.Center;
        
        tooltipPanel = tooltipGO;
        tooltipPanel.SetActive(false);
    }
    
    /// <summary>
    /// Efekt bilgilerini ayarlar
    /// </summary>
    public void SetEffectInfo(EffectType type, float duration, int stacks)
    {
        effectType = type;
        remainingDuration = duration;
        stackCount = stacks;
        effectDescription = GetEffectDescription(type);
    }
    
    /// <summary>
    /// Efekt türüne göre açıklama döndürür
    /// </summary>
    private string GetEffectDescription(EffectType type)
    {
        return type switch
        {
            EffectType.Healing => "Sürekli can yeniler",
            EffectType.Shield => "Dokunulmazlık/Hasarı engeller",
            _ => "Bilinmeyen efekt"
        };
    }
    
    /// <summary>
    /// Tooltip metnini günceller
    /// </summary>
    private void UpdateTooltipText()
    {
        if (tooltipText != null)
        {
            string tooltipContent = $"<b>{GetEffectDisplayName()}</b>\n";
            tooltipContent += $"{effectDescription}\n";
            tooltipContent += $"Süre: {remainingDuration:F1}s";
            
            if (stackCount > 1)
            {
                tooltipContent += $"\nStack: {stackCount}";
            }
            
            tooltipText.text = tooltipContent;
        }
    }
    
    /// <summary>
    /// Efekt türünün görünen adını döndürür
    /// </summary>
    private string GetEffectDisplayName()
    {
        return effectType switch
        {
            EffectType.Healing => "İyileşme",
            EffectType.Shield => "Kalkan",
            _ => "Bilinmeyen"
        };
    }
    
    /// <summary>
    /// Mouse efekt üzerine geldiğinde çalışır
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        
        // Önceki coroutine'i durdur
        if (showTooltipCoroutine != null)
        {
            StopCoroutine(showTooltipCoroutine);
        }
        
        // Gecikmeli tooltip gösterimi başlat
        showTooltipCoroutine = StartCoroutine(ShowTooltipWithDelay());
    }
    
    /// <summary>
    /// Mouse efektten ayrıldığında çalışır
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        
        // Tooltip'i gizle
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
        
        // Coroutine'i durdur
        if (showTooltipCoroutine != null)
        {
            StopCoroutine(showTooltipCoroutine);
            showTooltipCoroutine = null;
        }
    }
    
    /// <summary>
    /// Gecikmeli tooltip gösterimi
    /// </summary>
    private IEnumerator ShowTooltipWithDelay()
    {
        yield return new WaitForSeconds(showDelay);
        
        // Hala hover durumundaysa tooltip'i göster
        if (isHovering && tooltipPanel != null)
        {
            UpdateTooltipText();
            PositionTooltip();
            tooltipPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Tooltip pozisyonunu ayarlar
    /// </summary>
    private void PositionTooltip()
    {
        if (tooltipRect == null || parentCanvas == null) return;
        
        // Mouse pozisyonunu al
        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            Input.mousePosition,
            parentCanvas.worldCamera,
            out mousePos
        );
        
        // Tooltip pozisyonunu ayarla
        Vector2 tooltipPos = mousePos + tooltipOffset;
        
        // Ekran sınırlarını kontrol et
        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        Vector2 canvasSize = canvasRect.sizeDelta;
        
        // Sağ kenar kontrolü
        if (tooltipPos.x + tooltipRect.sizeDelta.x > canvasSize.x / 2)
        {
            tooltipPos.x = mousePos.x - tooltipOffset.x - tooltipRect.sizeDelta.x;
        }
        
        // Üst kenar kontrolü
        if (tooltipPos.y + tooltipRect.sizeDelta.y > canvasSize.y / 2)
        {
            tooltipPos.y = mousePos.y - tooltipOffset.y - tooltipRect.sizeDelta.y;
        }
        
        tooltipRect.anchoredPosition = tooltipPos;
    }
    
    /// <summary>
    /// Kalan süreyi günceller (PlayerHealthUI tarafından çağrılabilir)
    /// </summary>
    public void UpdateRemainingTime(float newDuration)
    {
        remainingDuration = newDuration;
        
        // Eğer tooltip açıksa metni güncelle
        if (tooltipPanel != null && tooltipPanel.activeInHierarchy)
        {
            UpdateTooltipText();
        }
    }
    
    /// <summary>
    /// Stack sayısını günceller
    /// </summary>
    public void UpdateStackCount(int newStackCount)
    {
        stackCount = newStackCount;
        
        // Eğer tooltip açıksa metni güncelle
        if (tooltipPanel != null && tooltipPanel.activeInHierarchy)
        {
            UpdateTooltipText();
        }
    }
} 
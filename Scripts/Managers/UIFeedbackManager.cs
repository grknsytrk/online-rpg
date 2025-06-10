using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIFeedbackManager : MonoBehaviour
{
    public static UIFeedbackManager Instance { get; private set; }

    [Header("Tooltip Settings")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private float tooltipDuration = 2f;
    
    [Header("Item Popup")]
    [SerializeField] private GameObject itemPopupPanel;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI itemDescription;
    
    private Coroutine tooltipCoroutine;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initial setup
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
            if (itemPopupPanel != null) itemPopupPanel.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void ShowTooltip(string message)
    {
        if (tooltipPanel == null || tooltipText == null)
        {
            Debug.LogWarning("Tooltip UI elements are not assigned!");
            return;
        }
        
        if (tooltipCoroutine != null)
        {
            StopCoroutine(tooltipCoroutine);
        }
        
        tooltipCoroutine = StartCoroutine(ShowTooltipCoroutine(message));
    }
    
    private IEnumerator ShowTooltipCoroutine(string message)
    {
        tooltipText.text = message;
        tooltipPanel.SetActive(true);
        
        yield return new WaitForSeconds(tooltipDuration);
        
        tooltipPanel.SetActive(false);
        tooltipCoroutine = null;
    }
    
    public void ShowItemPopup(InventoryItem item, Vector2 screenPosition)
    {
        if (itemPopupPanel == null || item == null)
        {
            return;
        }

        RectTransform popupRT = itemPopupPanel.GetComponent<RectTransform>();
        if (popupRT == null)
        {
            Debug.LogError("Item Popup Panel does not have a RectTransform component!");
            return;
        }
        
        // Sadece metin bilgilerini ayarla
        if (itemName != null) 
        {
            itemName.text = item.ItemName;
            // Renk örneği, isterseniz değiştirebilirsiniz veya InventoryItem'dan alabilirsiniz
            itemName.color = new Color(1f, 0.84f, 0f); // Canlı Sarı (RGB: 255, 215, 0)
        }
        if (itemDescription != null) itemDescription.text = item.GetDetailedDescription();
        
        // Paneli aktif et ve boyutlarının güncellenmesi için canvas'ı zorla
        // Bu, doğru genişlik değerini almak için önemlidir.
        itemPopupPanel.SetActive(true);
        Canvas.ForceUpdateCanvases(); 

        float popupWidth = popupRT.rect.width;
        // Ekran genişliğini al
        float screenWidth = Screen.width;

        // Varsayılan pivot (sol üst)
        Vector2 newPivot = new Vector2(0, 1f); // Y pivotunu mevcut ayara göre (1f) koruyalım

        // Panel, varsayılan pivot (sol üst) ile screenPosition'a yerleştirildiğinde
        // sağ kenarının ekranı aşıp aşmadığını kontrol et.
        // screenPosition.x, panelin sol kenarı olur.
        if (screenPosition.x + popupWidth > screenWidth)
        {
            // Eğer taşıyorsa, pivotu sağ üste al (1, 1)
            newPivot = new Vector2(1, 1f);
        }

        popupRT.pivot = newPivot;
        // Pivot ayarlandıktan sonra pozisyonu ata.
        // Bu, screenPosition noktasının yeni pivot noktasına göre hizalanmasını sağlar.
        popupRT.position = screenPosition; 
        
        // Panelin tekrar aktif edildiğinden emin ol (Canvas.ForceUpdateCanvases sonrası gerekebilir)
        // itemPopupPanel.SetActive(true); // Zaten yukarıda yapıldı
    }
    
    public void HideItemPopup()
    {
        if (itemPopupPanel != null)
        {
            itemPopupPanel.SetActive(false);
        }
    }
} 
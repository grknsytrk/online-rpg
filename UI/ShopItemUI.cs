using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemQuantityText; // Satış sekmesinde envanterdeki miktarı göstermek için
    [SerializeField] private Button itemButton;

    private ItemData currentItemData;
    private InventoryItem currentInventoryItem; // Satış sekmesindeki itemlar için
    private bool isBuyTabItem; // Bu item satın alma sekmesinde mi, satış sekmesinde mi?
    private ShopUIManager shopUIManager;
    private const float SELL_PERCENTAGE = 0.50f;

    public void Setup(ItemData itemData, bool isBuyItem, ShopUIManager manager, int quantity = 1)
    {
        currentItemData = itemData;
        isBuyTabItem = isBuyItem;
        shopUIManager = manager;

        if (itemIcon != null) 
        { 
            itemIcon.sprite = itemData.Icon;
            itemIcon.gameObject.SetActive(itemData.Icon != null);
        }

        if (itemQuantityText != null)
        {
            if (!isBuyItem && quantity > 1) // Sadece satışta ve miktarı 1'den fazlaysa göster
            {
                itemQuantityText.text = "x" + quantity.ToString();
                itemQuantityText.gameObject.SetActive(true);
            }
            else
            {
                itemQuantityText.gameObject.SetActive(false);
            }
        }
        
        // Eğer satış için bir InventoryItem olarak ayarlanıyorsa (ShopUIManager.PopulateSellTab içinden)
        // ve SelectShopInventoryItem metoduna uygun hale getirmek için
        if (!isBuyItem && quantity > 0) // quantity > 0 kontrolü eklendi, çünkü bu Setup itemData ile çağrılıyor
        {
            currentInventoryItem = new InventoryItem(itemData, quantity);
        }

        itemButton?.onClick.RemoveAllListeners(); // Önceki listenerları temizle
        itemButton?.onClick.AddListener(OnItemClicked);
    }

    void OnItemClicked()
    {
        if (shopUIManager != null && currentItemData != null)
        {
            if (isBuyTabItem)
            {
                shopUIManager.SelectShopItem(currentItemData, isBuyTabItem);
            }
            else
            {
                // Satış sekmesinde, eğer stack durumu varsa InventoryItem olarak gönder
                if (currentInventoryItem != null) // Bu, Setup sırasında quantity > 0 ile oluşturulmuş olacak
                {
                    shopUIManager.SelectShopInventoryItem(currentInventoryItem, isBuyTabItem);
                }
                else // Nadir bir durum, ama sadece ItemData varsa (örn: miktar 1 veya bilinmiyor)
                {
                    shopUIManager.SelectShopItem(currentItemData, isBuyTabItem, 1); // Miktarı 1 olarak gönder
                }
            }
        }
    }
} 
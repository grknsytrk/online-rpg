using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Envanter slot'larının UI davranışlarını yöneten sınıf
/// </summary>
public class InventorySlotUI : MonoBehaviour, 
    IBeginDragHandler, IDragHandler, IEndDragHandler, 
    IDropHandler, IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler
{
    #region Inspector References

    [Header("UI References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image selectedImage;
    
    [Header("Slot Settings")]
    [SerializeField] private SlotType slotType = SlotType.None;
    [SerializeField] private bool allowDrops = true;
    [SerializeField] private bool isTrashSlot = false;

    #endregion

    #region Private Fields

    private InventoryItem _currentItem;
    private static InventorySlotUI _draggedFrom;
    private static GameObject _draggedItemObj;
    private static Canvas _canvas;
    private Vector2 _originalItemPosition;
    private RectTransform _rectTransform;
    
    public static InventorySlotUI currentlySelectedSlot;

    #endregion

    #region Properties

    /// <summary>
    /// Slot'taki mevcut item
    /// </summary>
    public InventoryItem CurrentItem => _currentItem;
    
    /// <summary>
    /// Slot'un tipi
    /// </summary>
    public SlotType SlotType => slotType;
    
    /// <summary>
    /// Slot'un envanterdeki indeksi
    /// </summary>
    public int SlotIndex { get; set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        
        // Canvas referansını al
        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        // Başlangıçta UI'ı ayarla
        InitializeUI();
    }

    private void Start()
    {
        // Oyun başladığında, eğer bir item varsa icon'u aktif et
        if (_currentItem != null)
        {
            Debug.Log($"Start: Slot={SlotIndex} için item bulundu, icon aktif ediliyor");
            if (itemIcon != null)
            {
                itemIcon.gameObject.SetActive(true);
                Debug.Log($"Start: Slot={SlotIndex} için item icon aktif edildi");
            }
        }
        // Ensure selectedImage is initially hidden
        if (selectedImage != null)
        {
            selectedImage.gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        // Inspector'da değişiklikler yapıldığında UI'ı güncelle
        if (slotBackground != null)
        {
            UpdateSlotAppearance();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Slot UI'ını günceller
    /// </summary>
    /// <param name="item">Gösterilecek item (null ise slot boş)</param>
    public void UpdateUI(InventoryItem item)
    {
        _currentItem = item;

        if (item != null)
        {
            Debug.Log($"UpdateUI çağrıldı: Slot={SlotIndex}, Item={item.ItemName}, Icon={item.ItemIcon != null}");
            
            // Item icon'unu göster
            if (itemIcon != null)
            {
                // Her durumda itemIcon'u aktif yap
                itemIcon.gameObject.SetActive(true);
                Debug.Log($"Slot={SlotIndex} için item icon aktif edildi");
                
                // Sprite'ı ayarla (eğer varsa)
                if (item.ItemIcon != null)
                {
                    itemIcon.sprite = item.ItemIcon;
                    Debug.Log($"Item icon ayarlandı: {item.ItemName}, Sprite={item.ItemIcon.name}");
                }
            }
            else
            {
                Debug.LogWarning("itemIcon referansı null!");
            }

            // Miktar gösterimini güncelle
            if (amountText != null)
            {
                bool isStackable = item.IsStackable;
                bool isPotion = item.ItemType == SlotType.Potion;
                bool shouldShowAmount = isPotion || isStackable; // Miktar gösterilmeli mi?

                // Önce GameObject'in aktifliğini ayarla
                if (amountText.gameObject.activeSelf != shouldShowAmount) // Check current state first
                {
                    amountText.gameObject.SetActive(shouldShowAmount); // Only change if needed
                    Debug.Log($"UpdateUI: Slot={SlotIndex}, Item={(item != null ? item.ItemName : "null")}, AmountText Active changed to: {shouldShowAmount}"); // Added Log
                }

                // Eğer gösterilecekse, metni güncelle
                if (shouldShowAmount)
                {
                    amountText.text = item.Amount.ToString();
                    // Debug.Log($"UpdateUI: Slot={SlotIndex}, Item={item.ItemName}, AmountText activated and set to {item.Amount}"); // İsteğe bağlı detaylı log
                }
                /*else
                {
                     // Debug.Log($"UpdateUI: Slot={SlotIndex}, Item={item.ItemName}, AmountText deactivated."); // İsteğe bağlı detaylı log
                }*/
            }
            
            // Not: SlotBackground her zaman görünür kalacak
        }
        else
        {
            Debug.Log($"UpdateUI çağrıldı: Slot={SlotIndex}, Item=null (boş slot)");
            
            // Item yoksa UI'ı temizle
            if (itemIcon != null)
            {
                itemIcon.gameObject.SetActive(false);
            }

            // Miktar gösterimini gizle
            if (amountText != null)
            {
                // Ensure it's deactivated when slot is empty
                if (amountText.gameObject.activeSelf) // Only deactivate if currently active
                {
                     amountText.gameObject.SetActive(false); // Deactivate it
                     Debug.Log($"UpdateUI: Slot={SlotIndex}, Item=null, AmountText Active changed to: false"); // Added Log
                }
                amountText.text = ""; // Deaktif olsa bile text'i temizle
                // Debug.Log($"UpdateUI: Slot={SlotIndex} is empty, AmountText deactivated."); // İsteğe bağlı detaylı log
            }
            
            // Not: SlotBackground her zaman görünür kalacak // BU YORUM ARTIK TAM GEÇERLİ DEĞİL
        }

        // Slot Background görünürlük kontrolü
        if (slotBackground != null)
        {
            bool shouldBeActive;
            if (_currentItem != null && slotType != SlotType.None) // Özel slot VE içinde item var
            {
                shouldBeActive = false; // Arka planı gizle
            }
            else // Normal slot (itemli veya itemsiz) VEYA Özel slot ama boş
            {
                shouldBeActive = true; // Arka planı göster
            }

            if (slotBackground.gameObject.activeSelf != shouldBeActive)
            {
                slotBackground.gameObject.SetActive(shouldBeActive);
                Debug.Log($"UpdateUI: Slot={SlotIndex}, Item={(_currentItem != null ? _currentItem.ItemName : "null")}, SlotType={slotType}, SlotBackground Active changed to: {shouldBeActive}");
            }
        }
    }

    /// <summary>
    /// Item'ı slot'a ekleyip UI'ı günceller
    /// </summary>
    /// <param name="item">Eklenecek item</param>
    public bool AddItem(InventoryItem item)
    {
        if (!CanAcceptItem(item))
        {
            return false;
        }

        UpdateUI(item);
        return true;
    }

    /// <summary>
    /// Item'ı slot'tan kaldırır ve UI'ı günceller
    /// </summary>
    public InventoryItem RemoveItem()
    {
        var item = _currentItem;
        _currentItem = null;
        UpdateUI(null);
        return item;
    }

    /// <summary>
    /// Belirtilen tipin bu slot'a yerleştirilebilir olup olmadığını kontrol eder
    /// </summary>
    /// <param name="item">Kontrol edilecek item</param>
    /// <returns>Item kabul edilebilir mi?</returns>
    public bool CanAcceptItem(InventoryItem item)
    {
        if (item == null)
        {
            return true; // Boş item her zaman kabul edilir (slotu temizlemek için)
        }

        // Slot tipi None ise tüm itemları kabul et (normal envanter slotları)
        if (slotType == SlotType.None)
        {
            return true;
        }

        // Ekipman slotları için item tipinin slot tipine uygun olup olmadığını kontrol et
        return item.ItemType == slotType;
    }

    /// <summary>
    /// Slot'un tipini ayarlar
    /// </summary>
    /// <param name="type">Slot tipi</param>
    public void SetSlotType(SlotType type)
    {
        slotType = type;
        UpdateSlotAppearance();
    }

    /// <summary>
    /// Slot'un belirli bir ekipman tipi için olup olmadığını kontrol eder (None olmayan her slot ekipman slotudur).
    /// </summary>
    public bool IsDesignatedEquipmentSlot() 
    {
        return slotType != SlotType.None;
    }

    #endregion

    #region Drag & Drop Interface

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_currentItem == null || !allowDrops)
        {
            return;
        }

        // If this slot is currently selected, deselect it when starting a drag
        if (currentlySelectedSlot == this)
        {
            DeselectCurrentSlot(); // This will deselect and set currentlySelectedSlot to null
        }

        _draggedFrom = this;

        // Sürüklenen item için yeni bir obje oluştur
        _draggedItemObj = new GameObject("DraggedItem");
        _draggedItemObj.transform.SetParent(_canvas.transform);
        
        var image = _draggedItemObj.AddComponent<Image>();
        image.sprite = _currentItem.ItemIcon;
        image.raycastTarget = false;
        
        // RectTransform ayarları
        var rt = _draggedItemObj.GetComponent<RectTransform>();
        rt.sizeDelta = _rectTransform.sizeDelta;
        
        // İlk pozisyonu kaydet
        _originalItemPosition = itemIcon.transform.position;
        
        // Sürüklenen item'ı fare pozisyonuna taşı
        _draggedItemObj.transform.position = eventData.position;
        
        // Orijinal icon'u yarı saydam yap
        if (itemIcon != null)
        {
            itemIcon.color = new Color(1, 1, 1, 0.5f);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_draggedItemObj != null)
        {
            _draggedItemObj.transform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_draggedItemObj != null)
        {
            Destroy(_draggedItemObj);
        }

        if (itemIcon != null)
        {
            itemIcon.color = Color.white;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_draggedFrom == null)
        {
            Debug.LogWarning("Sürüklenen slot bulunamadı!");
            return;
        }
        
        if (_draggedFrom == this)
        {
            Debug.LogWarning("Kendine sürükleme yapılamaz!");
            return;
        }
        
        if (!allowDrops)
        {
            Debug.LogWarning("Bu slota sürükleme yapılamaz!");
            return;
        }

        // Slot bilgilerini detaylı olarak logla
        Debug.Log($"OnDrop: FromSlot={_draggedFrom.SlotIndex} (GameObject={_draggedFrom.gameObject.name}), ToSlot={SlotIndex} (GameObject={gameObject.name})");
        
        // Eğer hedef slot bir alt bileşense, ana slot bileşenini bul
        InventorySlotUI targetSlot = this;
        if (gameObject.name == "SlotBG" || gameObject.name == "ItemIcon" || gameObject.name == "AmountText")
        {
            targetSlot = transform.parent.GetComponent<InventorySlotUI>();
            if (targetSlot != null)
            {
                Debug.Log($"Alt bileşene düşürüldü, ana slot bulundu: {targetSlot.SlotIndex} (GameObject={targetSlot.gameObject.name})");
                
                // Ana slot ile işlemi gerçekleştir
                targetSlot.HandleItemDrop(_draggedFrom);
            }
            else
            {
                Debug.LogError("Ana slot bulunamadı!");
            }
        }
        else
        {
            // Doğru hedef slotu kullanarak işlemi gerçekleştir
            HandleItemDrop(_draggedFrom);
        }
        
        // İşlem tamamlandıktan sonra _draggedFrom değerini null'a ayarla
        _draggedFrom = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_currentItem == null)
        {
            return;
        }

        // Sadece tooltip göster, renklendirme yapma
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Tooltip'i gizle
        HideTooltip();
    }

    // Yeni Eklendi: IPointerClickHandler implementasyonu
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount == 1)
        {
                Debug.Log($"Slot {SlotIndex} üzerine tek tıklandı.");
            if (currentlySelectedSlot != null && currentlySelectedSlot != this)
            {
                currentlySelectedSlot.Deselect();
            }
            Select();
            currentlySelectedSlot = this;
            }
            else if (eventData.clickCount == 2 && _currentItem != null)
            {
                Debug.Log($"Slot {SlotIndex} üzerine çift tıklandı. Item: {(_currentItem != null ? _currentItem.ItemName : "NULL")}");
                 InventoryManager.Instance?.AttemptCoinConversion(SlotIndex, _currentItem);
            }
        }
    }

    #endregion

    #region Helper Methods

    private void InitializeUI()
    {
        // Item ikon'unu başlangıçta gizle
        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(false);
        }

        // Miktar metnini başlangıçta gizle
        if (amountText != null)
        {
            amountText.gameObject.SetActive(true);
        }
        
        // SlotBackground'ı başlangıçta göster
        if (slotBackground != null)
        {
            slotBackground.gameObject.SetActive(true);
        }
        // Ensure selectedImage is hidden on init as well
        if (selectedImage != null)
        {
            selectedImage.gameObject.SetActive(false);
        }
    }

    private void UpdateSlotAppearance()
    {
        // Slot görünümü artık sadece item varlığına göre UpdateUI metodunda kontrol ediliyor
    }

    private void HandleItemDrop(InventorySlotUI fromSlot)
    {
        // YENİ BAŞLANGIÇ: Çöp Kutusu Kontrolü
        if (isTrashSlot)
        {
            InventoryItem itemToTrash = fromSlot.CurrentItem;
            if (itemToTrash != null)
            {
                // Debug.Log($"Item {itemToTrash.ItemName} çöp kutusuna atıldı. Değer: {itemToTrash.Value}");
                if (itemToTrash.ItemValue <= 0)
                {
                    UIFeedbackManager.Instance?.ShowTooltip($"{itemToTrash.ItemName} satılamaz (değeri yok).");
                    // Sürüklemeyi iptal etmek için _draggedFrom'u null yapabiliriz veya OnEndDrag'in halletmesini bekleyebiliriz.
                    // Item'ı fromSlot'a geri döndürmek için özel bir mantık gerekebilir, 
                    // ancak şimdilik sadece tooltip gösterip bırakıyoruz.
                    // OnEndDrag, sürüklenen görseli yok edecek, item datası fromSlot'ta kalacak.
                }
                else
                {
                    TrashManager.Instance?.ShowTrashConfirmationPanel(fromSlot, itemToTrash);
                }
            }
            else
            {
                Debug.LogWarning("Çöp kutusuna boş bir item atılmaya çalışıldı.");
            }
            return; // Çöp kutusu işlemi sonrası diğer drop mantıklarını çalıştırma
        }
        // ÇÖP KUTUSU KONTROLÜ SONU

        bool isThisEquipment = IsDesignatedEquipmentSlot();
        bool isFromEquipment = fromSlot.IsDesignatedEquipmentSlot();
        
        Debug.Log($"Drop işleniyor: {(isFromEquipment ? "Ekipman" : "Envanter")} -> {(isThisEquipment ? "Ekipman" : "Envanter")}");
        Debug.Log($"FromSlot: {fromSlot.SlotIndex}, ToSlot: {SlotIndex}, FromType: {fromSlot.SlotType}, ToType: {slotType}");

        // İşlem tipine göre ilgili metodu çağır
        if (isThisEquipment && isFromEquipment)
        {
            // İki ekipman slotu arasında taşıma
            Debug.Log("İki ekipman slotu arasında taşıma yapılıyor");
            SwapWithEquipmentSlot(fromSlot);
        }
        else if (isThisEquipment && !isFromEquipment)
        {
            // Envanterden ekipmana taşıma
            Debug.Log("Envanterden ekipmana taşıma yapılıyor");
            EquipFromInventory(fromSlot);
        }
        else if (!isThisEquipment && isFromEquipment)
        {
            // Ekipmandan envantere taşıma
            Debug.Log("Ekipmandan envantere taşıma yapılıyor");
            UnequipToInventory(fromSlot);
        }
        else
        {
            // İki envanter slotu arasında taşıma
            Debug.Log("İki envanter slotu arasında taşıma yapılıyor");
            // Eğer hedef slot özel bir slot tipi ise (None değilse) ve item bu slota uygun değilse,
            // SwapWithInventorySlot içinde kontrol edilecek
            SwapWithInventorySlot(fromSlot);
        }
    }

    private bool IsEquipmentSlot()
    {
        // Ekipman slotları, EquipmentManager tarafından yönetilen slotlardır
        // Bu slotlar genellikle EquipmentManager'ın inspector'ında atanmış olur
        // Envanter slotları ise InventoryManager tarafından yönetilir
        
        // Eğer bu slot bir EquipmentManager slotu ise true döndür
        if (EquipmentManager.Instance != null)
        {
            // EquipmentManager.IsEquipmentSlot metodu doğru çalışmıyor olabilir
            // Bu nedenle, slotType'a göre kontrol yapalım
            // Ancak bu geçici bir çözüm, asıl sorun EquipmentManager.IsEquipmentSlot metodunda olabilir
            
            // Slot tipi None ise (normal envanter slotu), false döndür
            if (slotType == SlotType.None)
            {
                // return false; // Eski hali
                return SlotIndex == -1; // Envanter dışı bir genel slot ise true olabilir
            }
            
            // Slot tipi None değilse ve SlotIndex -1 ise (InventoryManager tarafından yönetilmeyen slot),
            // bu muhtemelen bir ekipman slotudur
            // if (SlotIndex == -1) // Bu kontrol zaten yukarıdaki ile birleşti
            // {
            //     return true;
            // }
            
            // Son çare olarak EquipmentManager.IsEquipmentSlot metodunu kullan
            // return EquipmentManager.Instance.IsEquipmentSlot(this); // Bu satır da yorumlu kalabilir.

            return true; // Eğer slotType None değilse, kesinlikle bir ekipman slotudur.
        }
        
        return false;
    }

    private void SwapWithEquipmentSlot(InventorySlotUI fromSlot)
    {
        // Ekipman Manager kontrolü
        if (EquipmentManager.Instance == null)
        {
            Debug.LogWarning("EquipmentManager bulunamadı!");
            return;
        }
        
        // İtemları geçici olarak sakla
        InventoryItem fromItem = fromSlot.CurrentItem;
        InventoryItem thisItem = CurrentItem;
        
        // Slotları kontrol et
        if (fromItem != null && !CanAcceptItem(fromItem))
        {
            UIFeedbackManager.Instance?.ShowTooltip($"Bu ekipman tipi ({fromItem.ItemType}) bu slota ({slotType}) yerleştirilemez!");
            return;
        }
        
        if (thisItem != null && !fromSlot.CanAcceptItem(thisItem))
        {
            UIFeedbackManager.Instance?.ShowTooltip($"Bu ekipman tipi ({thisItem.ItemType}) diğer slota ({fromSlot.SlotType}) yerleştirilemez!");
            return;
        }
        
        // Ekipman değiştirme işlemi
        if (thisItem != null)
        {
            EquipmentManager.Instance.UnequipItem(slotType, false);
        }
        
        if (fromItem != null)
        {
            EquipmentManager.Instance.UnequipItem(fromSlot.SlotType, false);
        }
        
        // Yeni ekipmanları giydir
        if (fromItem != null)
        {
            EquipmentManager.Instance.EquipItem(fromItem, slotType, false);
        }
        
        if (thisItem != null)
        {
            EquipmentManager.Instance.EquipItem(thisItem, fromSlot.SlotType, false);
        }
        
        // Takas sonrası Firebase'e kaydet
        EquipmentManager.Instance.SaveEquipmentManual();
    }

    private void EquipFromInventory(InventorySlotUI fromSlot)
    {
        // Ekipman Manager kontrolü
        if (EquipmentManager.Instance == null || InventoryManager.Instance == null)
        {
            Debug.LogWarning("EquipmentManager veya InventoryManager bulunamadı!");
            return;
        }
        
        InventoryItem itemToEquip = fromSlot.CurrentItem;
        if (itemToEquip == null)
        {
            return;
        }
        
        // Item bu slota uygun mu kontrol et
        if (!CanAcceptItem(itemToEquip))
        {
            UIFeedbackManager.Instance?.ShowTooltip($"Bu item ({itemToEquip.ItemType}) bu ekipman slotuna ({slotType}) yerleştirilemez!");
            return;
        }
        
        // Mevcut ekipmanı sakla
        InventoryItem currentEquipment = CurrentItem;
        
        // Önce itemi envanterden kaldır
        InventoryManager.Instance.RemoveItem(fromSlot.SlotIndex, false);
        
        // Ekipmanı giydir
        bool success = EquipmentManager.Instance.EquipItem(itemToEquip, slotType, false);
        
        if (success)
        {
            Debug.Log($"Item başarıyla ekipman olarak giyildi: {itemToEquip.ItemName}");
            
            // Eğer mevcut bir ekipman varsa, envantere geri koy
            if (currentEquipment != null)
            {
                InventoryManager.Instance.AddItem(currentEquipment, fromSlot.SlotIndex, false);
            }
            
            // Firebase'e kaydet
            EquipmentManager.Instance.SaveEquipmentManual();
            InventoryManager.Instance.ManualSave();
        }
        else
        {
            Debug.LogError($"Item ekipman olarak giyilemedi: {itemToEquip.ItemName}");
            
            // Başarısız olursa, itemi geri koy
            InventoryManager.Instance.AddItem(itemToEquip, fromSlot.SlotIndex, true);
        }
    }

    private void UnequipToInventory(InventorySlotUI fromSlot)
    {
        // Ekipman Manager kontrolü
        if (EquipmentManager.Instance == null || InventoryManager.Instance == null)
        {
            Debug.LogWarning("EquipmentManager veya InventoryManager bulunamadı!");
            return;
        }
        
        InventoryItem equipmentItem = fromSlot.CurrentItem;
        if (equipmentItem == null)
        {
            return;
        }
        
        // Eğer bu slotta bir item varsa, takas yapılacak
        InventoryItem inventoryItem = CurrentItem;
        
        // Ekipmanı çıkar
        EquipmentManager.Instance.UnequipItem(fromSlot.SlotType, false);
        
        // Mevcut slottaki itemi kaldır ve ekipman olarak giydir
        if (inventoryItem != null)
        {
            InventoryManager.Instance.RemoveItem(SlotIndex, false);
            
            if (fromSlot.CanAcceptItem(inventoryItem))
            {
                EquipmentManager.Instance.EquipItem(inventoryItem, fromSlot.SlotType, false);
            }
            else
            {
                // Uyumsuz item, geri koy
                InventoryManager.Instance.AddItem(inventoryItem, SlotIndex, false);
                UIFeedbackManager.Instance?.ShowTooltip($"Bu item ({inventoryItem.ItemType}) ekipman slotuna ({fromSlot.SlotType}) yerleştirilemez!");
            }
        }
        
        // Ekipmanı envantere ekle
        InventoryManager.Instance.AddItem(equipmentItem, SlotIndex, false);
        
        // Firebase'e kaydet
        EquipmentManager.Instance.SaveEquipmentManual();
        InventoryManager.Instance.ManualSave();
    }

    private void SwapWithInventorySlot(InventorySlotUI fromSlot)
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("InventoryManager bulunamadı!");
            return;
        }
        
        // Slot indekslerini al
        int fromIndex = fromSlot.SlotIndex;
        int toIndex = SlotIndex;
        
        Debug.Log($"SwapWithInventorySlot: FromSlot={fromIndex} (GameObject={fromSlot.gameObject.name}), ToSlot={toIndex} (GameObject={gameObject.name})");
        
        // Aynı slotsa takas yapma
        if (fromIndex == toIndex)
        {
            Debug.LogWarning("Aynı slot, takas yapılmıyor!");
            return;
        }
        
        // Hedef slot özel bir slot tipi ise (None değilse) ve item bu slota uygun değilse işlemi engelle
        if (slotType != SlotType.None && fromSlot.CurrentItem != null && !CanAcceptItem(fromSlot.CurrentItem))
        {
            Debug.LogWarning($"Bu item ({fromSlot.CurrentItem.ItemName}, Tip={fromSlot.CurrentItem.ItemType}) bu slot tipine ({slotType}) yerleştirilemez!");
            UIFeedbackManager.Instance?.ShowTooltip($"Bu item bu slot tipine yerleştirilemez!");
            return;
        }
        
        // Kaynak slot özel bir slot tipi ise ve hedef slottaki item bu slota uygun değilse işlemi engelle
        if (fromSlot.SlotType != SlotType.None && CurrentItem != null && !fromSlot.CanAcceptItem(CurrentItem))
        {
            Debug.LogWarning($"Bu item ({CurrentItem.ItemName}, Tip={CurrentItem.ItemType}) kaynak slot tipine ({fromSlot.SlotType}) yerleştirilemez!");
            UIFeedbackManager.Instance?.ShowTooltip($"Bu item kaynak slot tipine yerleştirilemez!");
            return;
        }
        
        Debug.Log($"Slotlar arasında takas yapılıyor: {fromIndex} -> {toIndex}");
        
        // Slotlar arasında takas yap
        bool success = InventoryManager.Instance.SwapItems(fromIndex, toIndex, true);
        Debug.Log($"Takas sonucu: {(success ? "Başarılı" : "Başarısız")}");
    }

    private void ShowTooltip()
    {
        if (_currentItem == null || UIFeedbackManager.Instance == null)
        {
            return;
        }
        
        // Tooltip pozisyonunu hesapla
        // Slot'un merkez pozisyonunu al
        Vector2 position = transform.position;
        
        // Popup'ın sol üst köşesi slot'un merkezine gelecek şekilde ayarla
        // Bu şekilde popup tamamen slotun sağ alt kısmında görünecek
        
        UIFeedbackManager.Instance.ShowItemPopup(_currentItem, position);
    }

    private void HideTooltip()
    {
        if (UIFeedbackManager.Instance == null)
        {
            return;
        }
        
        UIFeedbackManager.Instance.HideItemPopup();
    }

    public void Select()
    {
        if (selectedImage != null)
        {
            selectedImage.gameObject.SetActive(true);
            Debug.Log($"Slot {SlotIndex} selected.");
        }
    }

    public void Deselect()
    {
        if (selectedImage != null)
        {
            selectedImage.gameObject.SetActive(false);
            Debug.Log($"Slot {SlotIndex} deselected.");
        }
    }

    // Public static method to allow external deselection
    public static void DeselectCurrentSlot()
    {
        if (currentlySelectedSlot != null)
        {
            currentlySelectedSlot.Deselect();
            currentlySelectedSlot = null;
        }
    }

    #endregion
} 
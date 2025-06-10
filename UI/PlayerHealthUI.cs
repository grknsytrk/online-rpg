using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas healthCanvas;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private TextMeshProUGUI healthValueText;
    
    [Header("Default Values")]
    [SerializeField] private int defaultMaxHealth = 100; // Varsayılan maksimum can

    private PlayerHealth playerHealth;
    private Camera mainCamera;

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
    }
} 
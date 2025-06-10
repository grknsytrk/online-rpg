using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }
    
    [Header("Damage Number Settings")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform canvas;
    [SerializeField] private float lifetime = 1.2f; // Ekranda kalma süresi
    [SerializeField] private float moveSpeed = 100f; // Yukarı hareket hızı (artırıldı)
    [SerializeField] private float maxHeight = 80f; // Maksimum yükseklik (pixel cinsinden)
    [SerializeField] private bool useEasing = true; // Yumuşak hareket kullan mı?
    
    [Header("Object Pooling")]
    [SerializeField] private int poolSize = 20; // Kaç tane obje tutulsun
    
    // Object Pool
    private Queue<GameObject> damageNumberPool = new Queue<GameObject>();
    private List<GameObject> allPoolObjects = new List<GameObject>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Object pool'unu başlat - başlangıçta objeler oluştur
    /// </summary>
    private void InitializePool()
    {
        if (damageNumberPrefab == null || canvas == null)
        {
            Debug.LogWarning("DamageNumberManager: Prefab veya Canvas atanmamış!");
            return;
        }
        
        for (int i = 0; i < poolSize; i++)
        {
            GameObject poolObj = Instantiate(damageNumberPrefab, canvas);
            
            // CanvasGroup yoksa ekle
            if (poolObj.GetComponent<CanvasGroup>() == null)
            {
                poolObj.AddComponent<CanvasGroup>();
            }
            
            poolObj.SetActive(false); // Başlangıçta görünmez
            damageNumberPool.Enqueue(poolObj);
            allPoolObjects.Add(poolObj);
        }
        
        Debug.Log($"DamageNumberManager: {poolSize} obje ile pool oluşturuldu.");
    }
    
    /// <summary>
    /// Pool'dan bir obje al
    /// </summary>
    private GameObject GetPooledObject()
    {
        if (damageNumberPool.Count > 0)
        {
            return damageNumberPool.Dequeue();
        }
        
        // Pool boşsa yeni obje oluştur (emergency)
        Debug.LogWarning("DamageNumberManager: Pool boş! Yeni obje oluşturuluyor.");
        GameObject newObj = Instantiate(damageNumberPrefab, canvas);
        if (newObj.GetComponent<CanvasGroup>() == null)
        {
            newObj.AddComponent<CanvasGroup>();
        }
        allPoolObjects.Add(newObj);
        return newObj;
    }
    
    /// <summary>
    /// Objeyi pool'a geri döndür
    /// </summary>
    private void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        damageNumberPool.Enqueue(obj);
    }
    
    public void ShowDamageNumber(Vector3 worldPosition, int damage, Color color)
    {
        GameObject numberObj = GetPooledObject(); // Pool'dan al
        if (numberObj == null) return;
        
        TextMeshProUGUI text = numberObj.GetComponent<TextMeshProUGUI>();
        CanvasGroup canvasGroup = numberObj.GetComponent<CanvasGroup>();
        
        if (text != null)
        {
            text.text = damage.ToString();
            text.color = color;
            
            // World position'ı screen position'a çevir
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
            numberObj.transform.position = screenPos;
            
            // Objevi aktif et ve alpha'yı sıfırla
            canvasGroup.alpha = 1f;
            numberObj.SetActive(true);
            
            StartCoroutine(AnimateDamageNumber(numberObj));
        }
    }
    
    /// <summary>
    /// İyileşme sayısı göster - yeşil renkte ve "+" işareti ile
    /// </summary>
    public void ShowHealingNumber(Vector3 worldPosition, int healAmount)
    {
        GameObject numberObj = GetPooledObject(); // Pool'dan al
        if (numberObj == null) return;
        
        TextMeshProUGUI text = numberObj.GetComponent<TextMeshProUGUI>();
        CanvasGroup canvasGroup = numberObj.GetComponent<CanvasGroup>();
        
        if (text != null)
        {
            text.text = healAmount.ToString();
            text.color = Color.green; // Healing yeşil renk
            
            // World position'ı screen position'a çevir
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
            numberObj.transform.position = screenPos;
            
            // Objevi aktif et ve alpha'yı sıfırla
            canvasGroup.alpha = 1f;
            numberObj.SetActive(true);
            
            StartCoroutine(AnimateDamageNumber(numberObj));
        }
    }
    
    private IEnumerator AnimateDamageNumber(GameObject numberObj)
    {
        float timer = 0f;
        Vector3 startPos = numberObj.transform.position;
        CanvasGroup canvasGroup = numberObj.GetComponent<CanvasGroup>();
        
        // Alpha'yı 1 yapıp tam görünür hale getir
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        
        // Belirtilen süre boyunca yukarı hareket et
        while (timer < lifetime)
        {
            timer += Time.deltaTime;
            float progress = timer / lifetime; // 0'dan 1'e
            
            // Hareket mesafesini hesapla
            float moveDistance;
            if (useEasing)
            {
                // Easing Out Quart - hızlı başlar, yavaş biter
                float easedProgress = 1f - Mathf.Pow(1f - progress, 4f);
                moveDistance = easedProgress * maxHeight;
            }
            else
            {
                // Linear hareket - sabit hız
                moveDistance = Mathf.Min(moveSpeed * timer, maxHeight);
            }
            
            Vector3 newPos = startPos + Vector3.up * moveDistance;
            numberObj.transform.position = newPos;
            
            // Alpha sabit kalıyor (fade out yok)
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            
            yield return null;
        }
        
        // Süre bitince ANIDEN yok ol (fade yok)
        ReturnToPool(numberObj);
    }
    
    /// <summary>
    /// Pool istatistiklerini göster (Debug için)
    /// </summary>
    public void ShowPoolStats()
    {
        int activeCount = allPoolObjects.FindAll(obj => obj.activeSelf).Count;
        int pooledCount = damageNumberPool.Count;
        Debug.Log($"Pool Stats - Aktif: {activeCount}, Pool'da: {pooledCount}, Toplam: {allPoolObjects.Count}");
    }
} 
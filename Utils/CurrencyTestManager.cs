using UnityEngine;

/// <summary>
/// Para sistemi formatını test etmek için kullanılır
/// Editörde Test Currency Formatting butonuna basarak çalıştırılabilir
/// </summary>
public class CurrencyTestManager : MonoBehaviour
{
    [Header("Test Controls")]
    [SerializeField] private bool runTestOnStart = false;
    
    [Header("Test Values")]
    [SerializeField] private int[] testValues = { 0, 50, 99, 100, 130, 198, 1000, 9801, 9900, 10025 };

    void Start()
    {
        if (runTestOnStart)
        {
            TestCurrencyFormatting();
        }
    }

    [ContextMenu("Test Currency Formatting")]
    public void TestCurrencyFormatting()
    {
        Debug.Log("=== Para Formatı Testleri Başladı ===");
        
        foreach (int value in testValues)
        {
            string formatted = CurrencyUtils.FormatCopperValue(value);
            Debug.Log($"{value} bakır → {formatted}");
        }
        
        Debug.Log("=== Para Formatı Testleri Tamamlandı ===");
    }

    [ContextMenu("Test Specific Value")]
    public void TestSpecificValue()
    {
        int testValue = 130; // 1 gümüş 31 bakır olmalı
        string result = CurrencyUtils.FormatCopperValue(testValue);
        Debug.Log($"Test: {testValue} bakır → {result}");
    }
} 
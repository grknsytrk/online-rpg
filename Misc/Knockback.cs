using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding; // AIPath için eklendi

public class Knockback : MonoBehaviour
{
    public bool gettingKnockbacked { get; private set; }

    public float knockbackTime = 0.2f;

    private Rigidbody2D rb;
    private Coroutine knockbackCoroutine; // Çalışan coroutine referansı
    private AIPath aiPath; // AIPath referansı eklendi

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        aiPath = GetComponent<AIPath>(); // AIPath bileşenini al
    }

    public void GetKnockbacked(Transform damageSource, float knockbackPower)
    {
        // AIPath varsa ve aktifse, hareketini durdur
        if (aiPath != null && aiPath.enabled)
        {
            aiPath.canMove = false;
        }

        gettingKnockbacked = true;

        // Eğer zaten bir knockback coroutine çalışıyorsa, onu durdur
        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
        }

        // Vector2 difference = (transform.position - damageSource.position).normalized * knockbackPower;
        Vector2 rawDifference = transform.position - damageSource.position;
        float distance = rawDifference.magnitude;
        Vector2 direction = Vector2.zero; // Başlangıç değeri
        if (distance > 0.001f) // Çok yakınsa normalleştirmeyi atla (sıfıra bölme hatasını önle)
        {
            direction = rawDifference.normalized;
        }
        else
        {
            // Eğer çok yakınsa veya üst üsteyse, rastgele bir yön ver (veya varsayılan)
            direction = Random.insideUnitCircle.normalized;
            Debug.LogWarning("Knockback source and target are too close. Using random direction.");
        }
        
        Vector2 forceVector = direction * knockbackPower;
        
        // Knockback gücünü logla (daha detaylı)
        // Debug.Log($"Knockback received! Source: {damageSource.name}, Power: {knockbackPower}, Initial Force Direction: {difference.normalized}, Magnitude: {difference.magnitude}");
        Debug.Log($"Knockback received! Source: {damageSource.name}, Power: {knockbackPower}, Distance: {distance:F4}, Direction: {direction:F2}, Calculated Force Magnitude: {forceVector.magnitude:F4}");
        
        rb.velocity = Vector2.zero; // Kuvvet uygulamadan önce hızı sıfırla
        rb.AddForce(forceVector, ForceMode2D.Impulse);
        knockbackCoroutine = StartCoroutine(KnockRoutine()); // Yeni coroutine'i başlat ve referansını sakla
    }

    IEnumerator KnockRoutine()
    {
        // yield return new WaitForSeconds(knockbackTime); // Eski bekleme yerine dampen mantığı

        float elapsedTime = 0f;
        float dampenTime = knockbackTime; // Dampen süresi knockback süresiyle aynı olsun
        Vector2 startVelocity = rb.velocity;

        while (elapsedTime < dampenTime && gettingKnockbacked)
        {
            // Hızı yavaşça sıfırla
            rb.velocity = Vector2.Lerp(startVelocity, Vector2.zero, elapsedTime / dampenTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Knockback süresi bittiğinde veya dışarıdan durdurulduğunda
        if (gettingKnockbacked)
        {
             rb.velocity = Vector2.zero; // Hızı tam sıfırla
             gettingKnockbacked = false;

             // Knockback bitti, AIPath'in hareketini tekrar etkinleştir (varsa ve aktifse)
             if (aiPath != null && aiPath.enabled)
             {
                 aiPath.canMove = true;
                 // Opsiyonel: AIPath'in hemen yeni bir yol aramasını tetikleyebiliriz
                 // aiPath.SearchPath(); 
             }

             knockbackCoroutine = null; // Coroutine bittiğinde referansı temizle
        }
        else
        {
            // Eğer gettingKnockbacked başka bir yerden false yapıldıysa (örn. yeni knockback geldi), 
            // aiPath'i etkinleştirmememiz gerekebilir. Şimdilik bu durumu göz ardı ediyoruz.
             if (aiPath != null && aiPath.enabled)
             {
                  // Eğer knockback kesildiyse, yine de harekete izin verelim?
                  aiPath.canMove = true; 
             }
        }
    }
}

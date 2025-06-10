using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ses yöneticisi sınıfı - oyundaki müzik ve ses efektlerini kontrol eder
public class AudioManager : MonoBehaviour
{
    [Header("Audio Source")]
    public AudioSource musicSource; // Müzik çalacak ses kaynağı
    public AudioSource SFXSource; // Ses efektleri için ses kaynağı

    [Header("AudioClip")]
    public AudioClip background; // Arkaplan müziği dosyası
    public AudioClip button; // Buton ses efekti dosyası

    // Oyun başladığında çalışır
    private void Start()
    {
        // 2 saniye bekledikten sonra müziği çalmaya başla
        StartCoroutine(PlayMusicWithDelay(2f)); 
    }

    // Belirtilen süre kadar bekleyip müziği çalan fonksiyon
    private IEnumerator PlayMusicWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay); // Verilen süre kadar bekle
        musicSource.clip = background; // Müzik kaynağına arkaplan müziğini ata
        musicSource.Play(); // Müziği çalmaya başla
    }

    // Ses efekti çalan genel fonksiyon - dışarıdan çağrılabilir
    public void PlaySFX(AudioClip clip)
    {
        SFXSource.PlayOneShot(clip); // Verilen ses efektini bir kez çal
    }
}

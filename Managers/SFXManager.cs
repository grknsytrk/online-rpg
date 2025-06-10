using UnityEngine;
using System.Collections.Generic;

// Ses efektleri yöneticisi - oyundaki tüm ses efektlerini kontrol eder
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; } // Singleton yapısı

    // Bu AudioSource artık varsayılan olabilir veya hiç kullanılmayabilir,
    // çünkü her sesi kendi geçici AudioSource'unda çalacağız.
    // Yine de referans olarak tutabiliriz veya kaldırabiliriz.
    [SerializeField] private AudioSource mainAudioSource; // Ana ses kaynağı

    // Ses efekti sınıfı - her ses için ayarları tutar
    [System.Serializable]
    public class SoundEffect
    {
        public string name; // Ses ismi
        public AudioClip clip; // Ses dosyası
        [Range(0f, 1f)]
        public float volume = 1.0f; // Ses seviyesi
        [Range(0.1f, 3f)]
        public float minPitch = 1.0f; // Minimum pitch değeri
        [Range(0.1f, 3f)]
        public float maxPitch = 1.0f; // Maksimum pitch değeri
    }
    [SerializeField] private List<SoundEffect> soundEffects = new List<SoundEffect>(); // Ses efektleri listesi
    private Dictionary<string, SoundEffect> soundEffectMap; // Hızlı erişim için ses haritası

    // Obje oluşturulduğunda çalışır
    private void Awake()
    {
        // Singleton kontrolü - sadece bir tane SFXManager olmasını sağlar
        if (Instance == null)
        {
            Instance = this; // Bu objeyi Instance yap
            DontDestroyOnLoad(gameObject); // Sahne değişiminde yok olmasın

            // Ana AudioSource'u al veya ekle (opsiyonel, yeni sistemde daha az kritik)
            if (mainAudioSource == null)
            {
                mainAudioSource = GetComponent<AudioSource>();
                if (mainAudioSource == null)
                {
                    mainAudioSource = gameObject.AddComponent<AudioSource>();
                    mainAudioSource.playOnAwake = false; // Emin olmak için
                }
            }

            // Ses haritasını oluştur
            soundEffectMap = new Dictionary<string, SoundEffect>();
            foreach (var sfx in soundEffects)
            {
                // Geçerli ses kontrolü
                if (!string.IsNullOrEmpty(sfx.name) && sfx.clip != null)
                {
                    // Aynı isimde ses var mı kontrol et
                    if (!soundEffectMap.ContainsKey(sfx.name))
                    {
                        // Pitch değerlerini kontrol et
                        if (sfx.minPitch > sfx.maxPitch)
                        {
                            Debug.LogWarning($"SFXManager: Sound '{sfx.name}' has minPitch ({sfx.minPitch}) > maxPitch ({sfx.maxPitch}). Clamping maxPitch.");
                            sfx.maxPitch = sfx.minPitch;
                        }
                        soundEffectMap.Add(sfx.name, sfx); // Haritaya ekle
                    }
                    else
                    {
                        Debug.LogWarning($"SFXManager: Duplicate sound name '{sfx.name}'. Ignoring duplicate.");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("SFXManager: Another instance already exists. Destroying this one.");
            Destroy(gameObject); // Fazla objeyi yok et
        }
    }

    // Bu metod artık geçici bir AudioSource oluşturup sesi çalacak
    public void PlaySound(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
    {
        // Null kontrolü
        if (clip == null)
        {
            Debug.LogWarning("SFXManager: AudioClip is null. Cannot play sound.");
            return;
        }

        // Geçici bir GameObject ve AudioSource oluştur
        GameObject soundGameObject = new GameObject("TempAudio");
        AudioSource audioSource = soundGameObject.AddComponent<AudioSource>();

        // AudioSource ayarlarını yap
        audioSource.clip = clip; // Ses dosyasını ata
        audioSource.volume = volume; // Ses seviyesini ayarla
        audioSource.pitch = pitch; // Pitch'i ayarla
        audioSource.playOnAwake = false; // Otomatik çalmasın
        // İsteğe bağlı: 2D sesler için (varsayılanı zaten 0 yani 2D'dir çoğu durumda)
        // audioSource.spatialBlend = 0f; 

        // Sesi çal
        audioSource.Play();

        // Ses bittikten sonra GameObject'i yok et
        // Klip uzunluğu 0 veya daha küçükse (geçersiz klip), kısa bir süre sonra yok et
        float destroyDelay = clip.length > 0 ? clip.length : 1.0f;
        Destroy(soundGameObject, destroyDelay); // Belirli süre sonra yok et
    }

    // İsimle ses çalma fonksiyonu
    public void PlaySound(string soundName)
    {
        // Ses haritasında bu isimde bir ses var mı kontrol et
        if (soundEffectMap.TryGetValue(soundName, out SoundEffect sfxEntry))
        {
            // Rastgele pitch değeri hesapla
            float randomPitch = Random.Range(sfxEntry.minPitch, sfxEntry.maxPitch);
            Debug.Log($"Playing sound: {soundName} with MinPitch: {sfxEntry.minPitch}, MaxPitch: {sfxEntry.maxPitch}, ChosenPitch: {randomPitch}, Volume: {sfxEntry.volume}");
            PlaySound(sfxEntry.clip, sfxEntry.volume, randomPitch); // Sesi çal
        }
        else
        {
            Debug.LogWarning($"SFXManager: Sound with name '{soundName}' not found.");
        }
    }

    // Example of how you might add specific sound playing methods
    // public void PlayLootPickupSound()
    // {
    //     PlaySound("LootPickup"); // Assuming "LootPickup" is defined in the Inspector
    // }

    // public void PlayPlayerHitSound()
    // {
    //     PlaySound("PlayerHit");
    // }
} 
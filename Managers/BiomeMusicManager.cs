using System.Collections;
using UnityEngine;

public class BiomeMusicManager : MonoBehaviour
{
    public static BiomeMusicManager Instance { get; private set; }

    [System.Serializable]
    public class BiomeMusic
    {
        public string biomeName;
        public AudioClip musicClip;
    }

    [Header("Biome Settings")]
    public BiomeMusic[] biomeMusics;
    public float fadeTime = 1.5f;

    private AudioSource[] audioSources = new AudioSource[2];
    private int currentSourceIndex = 0;
    [Range(0, 1)] [SerializeField] private float currentVolume;
    private Coroutine fadeCoroutine;
    private string currentBiome;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            PreloadBiomeMusics();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudioSources()
    {
        for (int i = 0; i < 2; i++)
        {
            audioSources[i] = gameObject.AddComponent<AudioSource>();
            audioSources[i].loop = true;
            audioSources[i].playOnAwake = false;
            audioSources[i].volume = currentVolume;
        }
    }

    private void PreloadBiomeMusics()
    {
        if (biomeMusics != null)
        {
            foreach (var biomeMusic in biomeMusics)
            {
                if (biomeMusic.musicClip != null)
                {
                    StartCoroutine(PreloadAudioClip(biomeMusic));
                }
            }
        }
    }

    private IEnumerator PreloadAudioClip(BiomeMusic biomeMusic)
    {
        if (biomeMusic.musicClip.loadState != AudioDataLoadState.Loaded)
        {
            biomeMusic.musicClip.LoadAudioData();
            while (biomeMusic.musicClip.loadState != AudioDataLoadState.Loaded)
            {
                yield return null;
            }
        }
    }

    public void ChangeBiomeMusic(string biomeName)
    {
        if (currentBiome == biomeName) return;

        BiomeMusic biomeMusic = System.Array.Find(biomeMusics, x => x.biomeName == biomeName);
        if (biomeMusic == null)
        {
            Debug.LogWarning($"No music found for biome: {biomeName}");
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(CrossFadeMusic(biomeMusic));
        currentBiome = biomeName;
    }

    public void SetVolume(float volume)
    {
        currentVolume = volume;
        
        // Tüm ses kaynaklarının volume'unu güncelle
        foreach (var source in audioSources)
        {
            if (source != null && source.isPlaying)
            {
                source.volume = currentVolume;
            }
        }
    }

    private IEnumerator CrossFadeMusic(BiomeMusic biomeMusic)
    {
        int nextSourceIndex = (currentSourceIndex + 1) % 2;
        AudioSource currentSource = audioSources[currentSourceIndex];
        AudioSource nextSource = audioSources[nextSourceIndex];

        if (nextSource.clip == biomeMusic.musicClip && nextSource.isPlaying)
        {
            yield break;
        }

        nextSource.clip = biomeMusic.musicClip;
        nextSource.volume = 0;
        nextSource.Play();

        float timer = 0;
        float startVolume = currentSource.isPlaying ? currentSource.volume : 0;

        WaitForEndOfFrame waitForFrame = new WaitForEndOfFrame();
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            float t = timer / fadeTime;
            float smoothT = t * t * (3f - 2f * t);

            if (currentSource.isPlaying)
                currentSource.volume = Mathf.Lerp(startVolume, 0, smoothT);
            
            nextSource.volume = Mathf.Lerp(0, currentVolume, smoothT);

            yield return waitForFrame;
        }

        if (currentSource.isPlaying)
        {
            currentSource.Stop();
            currentSource.clip = null;
        }
        
        currentSourceIndex = nextSourceIndex;
    }
}

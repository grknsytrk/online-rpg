using UnityEngine;
using TMPro;

// Metin titreşim efekti sağlayan sınıf - TextMeshPro ile çalışır
public class ShakyText : MonoBehaviour
{
    public float shakeMagnitude = 0.5f; // Titreşim genliği
    public float shakeSpeed = 3.0f; // Titreşim hızı

    // Gerekli bileşenler ve değişkenler
    private TMP_Text tmpText; // TextMeshPro text bileşeni
    private string originalText; // Orijinal metin içeriği
    private Mesh mesh; // Metnin mesh verisi
    private Vector3[] originalVertices; // Orijinal vertex pozisyonları
    private Vector3[] modifiedVertices; // Değiştirilmiş vertex pozisyonları
    private bool isShaking = false; // Titreşim başlatıldı mı?

    // Obje oluşturulduğunda çalışır
    void Awake()
    {
        tmpText = GetComponent<TMP_Text>(); // TextMeshPro bileşenini al
        originalText = tmpText.text; // Orijinal metni sakla
    }

    // Oyun başladığında çalışır
    void Start()
    {
        tmpText.ForceMeshUpdate(); // Mesh'i güncelle
        mesh = tmpText.mesh; // Mesh referansını al
        // Vertex dizilerini başlat
        originalVertices = new Vector3[mesh.vertices.Length];
        modifiedVertices = new Vector3[mesh.vertices.Length];
    }

    // Her frame'de çalışır
    void Update()
    {
        if (!isShaking) return; // Eğer titreşim yoksa çık

        tmpText.ForceMeshUpdate(); // Mesh verilerini güncelle
        mesh = tmpText.mesh; // Güncel mesh'i al
        int characterCount = tmpText.textInfo.characterCount; // Karakter sayısını al

        // Zaman tabanlı titreşim hesaplaması
        float timeOffset = Time.time * shakeSpeed;

        // Her karakter için titreşim uygula
        for (int i = 0; i < characterCount; i++)
        {
            TMP_CharacterInfo charInfo = tmpText.textInfo.characterInfo[i]; // Karakter bilgisini al
            if (!charInfo.isVisible) // Görünür değilse atla
                continue;

            // Her karakterin 4 vertex'ini işle
            for (int j = 0; j < 4; j++)
            {
                int vertexIndex = charInfo.vertexIndex + j; // Vertex indeksini hesapla
                // Orijinal vertex pozisyonunu al
                originalVertices[vertexIndex] = tmpText.textInfo.meshInfo[charInfo.materialReferenceIndex].vertices[vertexIndex];

                // Rastgele titreşim vektörü oluştur
                Vector3 offset = Random.insideUnitSphere * shakeMagnitude;
                offset.z = 0; // TMP çalışması için z'yi 0 yapın

                // Sinüs fonksiyonu ile yumuşak titreşim uygula
                modifiedVertices[vertexIndex] = originalVertices[vertexIndex] + offset * Mathf.Sin(timeOffset);
            }
        }

        // Değiştirilmiş vertex'leri mesh'e uygula
        mesh.vertices = modifiedVertices;
        tmpText.canvasRenderer.SetMesh(mesh); // Yeni mesh'i renderer'a ver
    }

    // Titreşimi başlat
    public void StartShaking()
    {
        isShaking = true; // Titreşim bayrağını aç
    }

    // Titreşimi durdur
    public void StopShaking()
    {
        isShaking = false; // Titreşim bayrağını kapat
    }
}
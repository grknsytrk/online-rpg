using UnityEngine;
using Firebase;
using Firebase.Database;
using System.Threading.Tasks;
using System.Collections.Generic;

public class FirebaseManager : MonoBehaviour
{
    private static FirebaseManager instance;
    public static FirebaseManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<FirebaseManager>();
            return instance;
        }
    }

    private DatabaseReference dbReference;
    private string playerId;
    private FirebaseApp app;

    private async void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        await InitializeFirebase();
    }

    private async Task InitializeFirebase()
    {
        try
        {
            Debug.Log("Firebase başlatılıyor...");
            
            if (FirebaseApp.DefaultInstance == null)
            {
                Debug.Log("Firebase App başlatılıyor...");
                var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
                
                if (dependencyStatus == DependencyStatus.Available)
                {
                    app = FirebaseApp.DefaultInstance;
                    Debug.Log("Firebase App başlatıldı!");
                }
                else
                {
                    Debug.LogError($"Firebase bağımlılıkları yüklenemedi! Durum: {dependencyStatus}");
                    return;
                }
            }
            else
            {
                app = FirebaseApp.DefaultInstance;
                Debug.Log("Mevcut Firebase App kullanılıyor.");
            }

            try
            {
                dbReference = FirebaseDatabase.DefaultInstance.RootReference;
                Debug.Log("Veritabanı referansı alındı!");
                
                // Test amaçlı debug mesajı
                Debug.Log("<color=green>Firebase bağlantısı başarılı!</color>");

                /* NOT: Player status tracking şimdilik devre dışı bırakıldı
                // Gereksiz veri birikimini önlemek için
                playerId = SystemInfo.deviceUniqueIdentifier;
                await UpdateOnlineStatus(true);
                */
            }
            catch (System.Exception dbEx)
            {
                Debug.LogError($"Veritabanı işlemleri sırasında hata: {dbEx.Message}\nStack Trace: {dbEx.StackTrace}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Firebase başlatma hatası: {e.Message}\nStack Trace: {e.StackTrace}");
            
            if (e.Message.Contains("DllNotFoundException"))
            {
                Debug.LogError("Firebase DLL'leri bulunamadı! Lütfen Firebase SDK'yı yeniden import edin ve build settings'i kontrol edin.");
            }
        }
    }

    /* NOT: Online status tracking şimdilik devre dışı bırakıldı
    private async Task UpdateOnlineStatus(bool isOnline)
    {
        if (dbReference != null && !string.IsNullOrEmpty(playerId))
        {
            try
            {
                var onlineData = new Dictionary<string, object>
                {
                    { "last_seen", System.DateTime.Now.ToString() },
                    { "is_online", isOnline },
                    { "device_id", SystemInfo.deviceUniqueIdentifier },
                    { "platform", Application.platform.ToString() }
                };

                await dbReference.Child("players_status").Child(playerId).UpdateChildrenAsync(onlineData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Online durum güncellenirken hata: {e.Message}");
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (dbReference != null && !string.IsNullOrEmpty(playerId))
        {
            UpdateOnlineStatus(false).ContinueWith(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"Offline durumu güncellenirken hata: {task.Exception}");
            });
        }
    }
    */

    // Veritabanına veri kaydetme
    public async Task SaveData(string path, object data)
    {
        try
        {
            if (dbReference == null)
            {
                Debug.LogError("Veritabanı referansı null! Firebase başlatılmamış olabilir.");
                return;
            }

            string json = JsonUtility.ToJson(data);
            await dbReference.Child(path).SetRawJsonValueAsync(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Veri kaydedilirken hata: {e.Message}");
        }
    }

    // Veritabanından veri okuma
    public async Task<DataSnapshot> LoadData(string path)
    {
        try
        {
            if (dbReference == null)
            {
                Debug.LogError("Veritabanı referansı null! Firebase başlatılmamış olabilir.");
                return null;
            }

            return await dbReference.Child(path).GetValueAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Veri yüklenirken hata: {e.Message}");
            return null;
        }
    }

    // Realtime veritabanı değişikliklerini dinleme
    public void ListenForChanges(string path, System.Action<DataSnapshot> callback)
    {
        try
        {
            if (dbReference == null)
            {
                Debug.LogError("Veritabanı referansı null! Firebase başlatılmamış olabilir.");
                return;
            }

            dbReference.Child(path).ValueChanged += (object sender, ValueChangedEventArgs args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogError($"Veritabanı dinleme hatası: {args.DatabaseError.Message}");
                    return;
                }

                callback(args.Snapshot);
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Değişiklik dinleyicisi eklenirken hata: {e.Message}");
        }
    }
} 
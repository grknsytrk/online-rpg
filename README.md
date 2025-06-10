# 🎮 OnlineRPG Scripts

<div align="center">
  <img src="https://img.shields.io/badge/Unity-2022.3+-000000?style=for-the-badge&logo=unity&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-.NET-239120?style=for-the-badge&logo=c-sharp&logoColor=white" />
  <img src="https://img.shields.io/badge/Photon-PUN2-00C853?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Firebase-Enabled-FFA000?style=for-the-badge&logo=firebase&logoColor=white" />
</div>


---

## ✨ Özellikler

🕹️ **Gelişmiş Oyuncu Sistemi**
- Player controller ve hareket kontrolleri (PlayerController.cs)
- Sağlık ve can yönetimi (PlayerHealth.cs, PlayerHealthUI.cs)
- Oyuncu istatistikleri ve seviye sistemi (PlayerStats.cs)
- Emote sistemi ve sosyal etkileşimler (PlayerEmoteSystem.cs)
- Karakter görünümü ve isim etiketleri (PlayerNameTag.cs)

⚔️ **Akıllı Düşman Sistemi**
- Gelişmiş AI ve pathfinding teknolojisi (EnemyAI.cs)
- Sağlık sistemi ve hasarlanma (EnemyHealth.cs) 
- Dinamik düşman isim etiketleri (EnemyNameTag.cs)
- Elite düşman sistemi ve özel görsel efektler
- Loot sistemi ve drop mekanikleri (LootItem.cs)

🎒 **Kapsamlı Envanter Sistemi**
- Tam özellikli envanter yönetimi (InventoryManager.cs)
- Drag & drop interface (InventorySlotUI.cs)
- Ekipman sistemi ve otomatik stat bonusları (EquipmentManager.cs)
- Item veritabanı ve veri yönetimi (ItemDatabase)
- Firebase ile bulut senkronizasyonu

🛒 **Ticaret ve Shop Sistemi**
- Tüccar NPC'leri ve etkileşim (Merchant.cs)
- Gelişmiş shop UI (ShopUIManager.cs, ShopItemUI.cs)
- Para birimi sistemi (CurrencyUtils.cs)
- Alım-satım mekanikleri

🌐 **Multiplayer Alt Yapısı**
- Photon PUN2 ile real-time multiplayer
- Sunucu yönetimi (PhotonServerManager.cs)
- Oda yönetimi ve oyuncu senkronizasyonu
- Network optimizasyonları ve RPC sistemleri

🔥 **Firebase Cloud Sistemi**
- Kullanıcı kimlik doğrulama
- Cloud Firestore veri yönetimi
- Real-time database senkronizasyonu
- Oyuncu verilerinin otomatik kaydedilmesi

🎨 **Gelişmiş UI Sistemi**
- Ana UI yönetimi (UIManager.cs)
- Modern shop arayüzü
- Chat sistemi (ChatManager.cs)
- Feedback ve tooltip sistemleri (UIFeedbackManager)
- Dinamik sağlık barları

🎵 **Audio Yönetimi**
- Ses efektleri kontrolü (AudioManager.cs)
- SFX sistemleri (SFXManager, SFXNames.cs)
- Dinamik ses yönetimi

🧪 **Test ve Yardımcı Sistemler**
- Test araçları ve debugging
- Yardımcı sınıflar (Utils klasörü)
- Main thread dispatcher (UnityMainThreadDispatcher.cs)
- Mesaj renklendirme (MessageColorUtils.cs)

## 📁 Detaylı Klasör Yapısı

```
📦 Scripts
├── 🎮 Player/                   # Oyuncu Sistemleri
│   ├── PlayerController.cs     # Ana oyuncu kontrolü ve hareket
│   ├── PlayerHealth.cs         # Sağlık sistemi ve hasarlanma
│   ├── PlayerStats.cs          # Seviye, XP ve stat yönetimi
│   ├── PlayerNameTag.cs        # Oyuncu isim etiketi
│   ├── Player Controls.cs      # Input sistemi
│   └── Sword/                  # Kılıç sistemi
│       ├── Sword.cs           # Kılıç kontrolü
│       ├── SlashAnim.cs       # Saldırı animasyonu
│       └── PlayerDamage.cs    # Hasar sistemi
│
├── 👹 Enemy/                   # Düşman Sistemleri
│   ├── EnemyAI.cs             # Düşman yapay zekası
│   ├── EnemyHealth.cs         # Düşman sağlık sistemi
│   └── EnemyNameTag.cs        # Düşman isim etiketi
│
├── 👥 NPCs/                    # NPC Sistemleri
│   └── Merchant.cs            # Tüccar NPC ve etkileşim
│
├── 🎒 Items/                   # Item ve Loot Sistemleri
│   └── LootItem.cs            # Loot toplama mekanikleri
│
├── 📦 Scripts/                 # Ana Sistemler
│   ├── Inventory/             # Envanter Alt Sistemi
│   │   ├── InventoryManager.cs      # Envanter yönetimi
│   │   └── InventorySlotUI.cs       # Slot UI kontrolü
│   └── Managers/              # Oyun Yöneticileri
│       ├── EquipmentManager.cs      # Ekipman sistemi
│       ├── PhotonServerManager.cs   # Photon sunucu yönetimi
│       ├── ChatManager.cs          # Chat sistemi
│       └── SFXNames.cs             # Ses efekti isimleri
│
├── 🖥️ UI/                      # Kullanıcı Arayüzü
│   ├── UIManager.cs           # Ana UI kontrolü
│   ├── PlayerHealthUI.cs      # Sağlık bar UI
│   ├── ShopUIManager.cs       # Shop arayüzü
│   └── ShopItemUI.cs          # Shop item UI
│
├── 🔧 Utils/                   # Yardımcı Araçlar
│   ├── UnityMainThreadDispatcher.cs  # Thread yönetimi
│   ├── MessageColorUtils.cs          # Mesaj renklendirme
│   ├── CurrencyUtils.cs              # Para birimi araçları
│   └── CurrencyTestManager.cs        # Para test sistemi
│
├── 🧪 Tests/                   # Test Sistemleri
│   └── [Test dosyaları]       # Debug ve test araçları
│
├── 🌍 Environment/             # Çevre Sistemleri
│   └── [Çevre objeleri]       # Dünya etkileşim objeleri
│
├── 🔊 Audio/                   # Ses Sistemleri
│   ├── AudioManager.cs        # Ana ses yöneticisi
│   └── ShakyText.cs          # Titreşimli text efekti
│
└── 📱 MainMenu/               # Ana Menü
    └── [Menü sistemleri]      # Ana menü ve UI
```

## 🛠️ Teknolojiler

<table>
<tr>
<td align="center">
  <img src="https://img.shields.io/badge/Unity-000000?style=for-the-badge&logo=unity&logoColor=white" /><br />
  <b>Unity 2022.3+</b>
</td>
<td align="center">
  <img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white" /><br />
  <b>C# (.NET)</b>
</td>
<td align="center">
  <img src="https://img.shields.io/badge/Photon-00C853?style=for-the-badge" /><br />
  <b>PUN2 Multiplayer</b>
</td>
</tr>
<tr>
<td align="center">
  <img src="https://img.shields.io/badge/Firebase-FFA000?style=for-the-badge&logo=firebase&logoColor=white" /><br />
  <b>Firebase</b>
</td>
<td align="center">
  <img src="https://img.shields.io/badge/A*-Pathfinding-FF6B6B?style=for-the-badge" /><br />
  <b>A* Pathfinding</b>
</td>
<td align="center">
  <img src="https://img.shields.io/badge/JSON-Database-4ECDC4?style=for-the-badge" /><br />
  <b>JSON Data</b>
</td>
</tr>
</table>

## 🚀 Kurulum

### Gereksinimler
- Unity 2022.3 veya daha yeni sürüm
- .NET Framework 4.7.1+
- Git (version control için)

### Adım Adım Kurulum

1. **📥 Repository'yi klonlayın**
   ```bash
   git clone https://github.com/grknsytrk/online-rpg.git
   ```

2. **📁 Unity projenizde Assets/Scripts klasörüne kopyalayın**

3. **📦 Gerekli paketleri import edin**
   - Photon PUN2 (Multiplayer)
   - Firebase SDK
   - A* Pathfinding Project
   - TextMeshPro

4. **⚙️ Scene'leri ayarlayın ve prefab'ları bağlayın**

5. **🔧 Firebase konfigürasyonunu yapın**
   - `google-services.json` dosyasını ekleyin
   - Authentication ve Firestore'u aktifleştirin

## 🎯 Kullanım

### Temel Oyun Döngüsü
```csharp
// Oyuncu oluşturma
var player = PhotonNetwork.Instantiate("Player", spawnPoint, Quaternion.identity);

// Envanter yönetimi
InventoryManager.Instance.AddItem(newItem);

// Düşman spawn
EnemySpawner.Instance.SpawnEnemy(enemyType, position);
```

### Kod Örnekleri

#### Envanter Sistemi
```csharp
// Item ekleme
InventoryItem newItem = new InventoryItem(itemData, quantity);
bool success = InventoryManager.Instance.AddItem(newItem);

// Item kaldırma
InventoryManager.Instance.RemoveItem(slotIndex);

// Para ekleme/çıkarma
InventoryManager.Instance.TryAddCurrency(amount);
InventoryManager.Instance.TryRemoveCurrency(amount);
```

#### Ekipman Sistemi
```csharp
// Ekipman giydirme
bool equipped = EquipmentManager.Instance.EquipItem(item, slotType);

// Ekipman çıkarma
InventoryManager.Instance.UnequipItem(slotType);
```

#### Chat Sistemi
```csharp
// Mesaj gönderme
ChatManager.Instance.SendMessage(message);

// Sistem mesajı
ChatManager.Instance.SendSystemMessage(message, messageType);
```

#### Ses Sistemi
```csharp
// Ses efekti çalma
SFXManager.Instance?.PlaySound(SFXNames.LootPickup);
AudioManager.Instance?.PlaySFX(soundClip);
```

## 🤝 Katkıda Bulunma

1. Bu repository'yi fork edin
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Değişikliklerinizi commit edin (`git commit -m 'Add amazing feature'`)
4. Branch'inizi push edin (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun

## 📸 Screenshots

*Yakında eklenecek...*

## 📝 Değişiklik Günlüğü

### v1.0.0
- ✅ Temel oyuncu sistemi
- ✅ Multiplayer desteği
- ✅ Firebase entegrasyonu
- ✅ Envanter sistemi

## 📞 İletişim

- **GitHub**: [@grknsytrk](https://github.com/grknsytrk)
- **Email**: [İletişim için GitHub üzerinden mesaj gönderin]

## 📄 Lisans

Bu proje **eğitim amaçlı** geliştirilmiştir. Ticari kullanım için izin alınmalıdır.

---

<div align="center">
  <b>⭐ Bu projeyi beğendiyseniz star vermeyi unutmayın! ⭐</b>
</div>

# 🎮 OnlineRPG Scripts

<div align="center">
  <img src="https://img.shields.io/badge/Unity-2022.3+-000000?style=for-the-badge&logo=unity&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-.NET-239120?style=for-the-badge&logo=c-sharp&logoColor=white" />
  <img src="https://img.shields.io/badge/Photon-PUN2-00C853?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Firebase-Enabled-FFA000?style=for-the-badge&logo=firebase&logoColor=white" />
</div>

<p align="center">
  Unity tabanlı OnlineRPG oyunumun script dosyalarını içeren repository
</p>

---

## ✨ Özellikler

🕹️ **Oyuncu Sistemi**
- Player controller ve hareket kontrolleri
- Sağlık sistemi ve karakter yönetimi
- Skill ve yetenek sistemleri

⚔️ **Düşman Sistemi**
- Gelişmiş AI ve pathfinding
- Loot sistemi ve drop mekanikleri
- Çeşitli düşman türleri

🎒 **Envanter Sistemi**
- Item yönetimi ve database entegrasyonu
- Drag & drop interface
- Ekipman ve kullanım itemleri

🌐 **Multiplayer Desteği**
- Photon PUN2 ile real-time multiplayer
- Oda yönetimi ve oyuncu senkronizasyonu
- Network optimizasyonları

🔥 **Firebase Entegrasyonu**
- Kullanıcı kimlik doğrulama
- Cloud Firestore veri yönetimi
- Real-time database

🎨 **UI Sistemi**
- Modern shop arayüzü
- Chat sistemi
- Feedback ve bildirim sistemleri

🎵 **Audio Yönetimi**
- Ses efektleri kontrolü
- Müzik sistemleri
- Dynamic audio mixing

## 📁 Klasör Yapısı

```
📦 Scripts
├── 🎮 Player/          # Oyuncu kontrolü ve yetenekleri
├── 👹 Enemy/           # Düşman AI ve davranışları
├── ⚙️ Managers/        # Oyun yöneticisi scriptleri
├── 🎒 Items/           # Item ve envanter sistemi
├── 🖥️ UI/              # Kullanıcı arayüzü
├── 🔧 Utils/           # Yardımcı araçlar ve utilities
├── 🗣️ Chat/            # Chat sistemi
├── 🌍 Environment/     # Çevre ve dünya objeleri
├── 👥 NPCs/            # NPC sistemleri
└── 🧪 Tests/           # Test scriptleri
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

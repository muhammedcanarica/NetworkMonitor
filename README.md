# NetScope Network Monitor

[Türkçe](#türkçe) | [English](#english)

## Türkçe

NetScope; sürekli erişilebilirlik takibi, salt okunur SNMP görünürlüğü, olay ve bildirim akışları, operasyon araçları ve yapılandırma geçmişini tek bir responsive web uygulamasında birleştiren, üreticiden bağımsız bir ağ izleme ve operasyon portfolyo projesidir. Üreticiye özel davranışlar genişletilebilir bileşenlerin arkasında izole edilmiştir. Mevcut yapılandırma yedekleme özelliği Cisco IOS/IOS-XE cihazlarını desteklese de NetScope yalnızca Cisco ürünlerine yönelik değildir.

> NetScope'u yalnızca sahibi olduğunuz veya test etmek için açıkça yetkilendirildiğiniz sistem ve ağlarda kullanın. Bu depo gerçek üretim IP adresleri, SNMP community bilgileri, SSH kimlik bilgileri veya SMTP kimlik bilgileri içermez.

### Mevcut özellikler

Aşağıdaki özellikler mevcut API, servisler, arayüz rotaları ve testlerle doğrulanmıştır:

- SQLite kalıcılığı ile cihaz yönetimi
- Arka planda ICMP izleme, izleme geçmişi ve 24 saatlik özetler
- REST yenileme alternatifi ile SignalR tabanlı gerçek zamanlı izleme güncellemeleri
- Reverse DNS sorgusu destekli, sınırlandırılmış IPv4 CIDR IP tarayıcısı
- Sistem bilgileri, ağ arayüzleri, GET ve WALK işlemleri için salt okunur SNMP v2c Explorer
- Kaydedilmiş ağ arayüzleri için trafik izleme ve bant genişliği geçmiş grafikleri
- Gelen/giden bant genişliği eşikleri ve uyarıları
- Doğrulanmış Interface Down olayı oluşturma ve düzelme takibi
- Okundu/okunmadı işlemleriyle Olay Takibi ve Bildirim Merkezi
- Yapılandırılabilir e-posta bildirimleri ve test e-postası gönderme işlemi
- İstek üzerine LLDP topoloji keşfi
- Sınırlandırılmış TCP Port Tarayıcısı ve Wake-on-LAN aracı
- SSH üzerinden istek üzerine yapılandırma yedekleme
- Yapılandırma geçmişi, içerik tekilleştirme ve satır bazlı fark karşılaştırması
- Ortam değişkenleriyle ilk yöneticiyi oluşturan cookie tabanlı kimlik doğrulama
- Şifrelenmiş kayıtlı SNMP ve SSH ağ kimlik bilgileri
- Cihaz Detayı ekranında Device Intelligence panelleri

### Teknolojiler

**Backend:** .NET 10, ASP.NET Core Web API, ASP.NET Core Identity, Entity Framework Core 10, SQLite, SignalR, ASP.NET Core Data Protection, SharpSnmpLib, SSH.NET, MailKit ve xUnit.

**Frontend:** React 19, TypeScript 6, Vite 8, React Router 7, Recharts, Lucide React, Vitest 4, React Testing Library, jest-dom, user-event, jsdom ve oxlint.

### Yerel kurulum

#### Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 24 LTS (mevcut Vite sürümü Node 22.12 ve üzeriyle de uyumludur)
- npm
- Aşağıdaki örnekler için PowerShell; diğer kabuklarda eşdeğer ortam değişkeni söz dizimi kullanılabilir

#### 1. Backend bağımlılıklarını yükleyin ve veritabanını güncelleyin

Depo kök dizininde:

```powershell
dotnet tool restore
dotnet restore
dotnet ef database update --project backend/NetworkMonitor.Api --startup-project backend/NetworkMonitor.Api
```

Varsayılan SQLite veritabanı, API çalışma dizininde `networkmonitor.db` adıyla oluşturulur.

#### 2. İlk yönetici hesabını ve anahtar dizinini yapılandırın

Başlangıç yönetici hesabı yalnızca belirtilen kullanıcı adı daha önce oluşturulmamışsa eklenir. ASP.NET Core Identity en az 10 karakter uzunluğunda parola zorunluluğu uygular.

```powershell
$env:NETSCOPE_ADMIN_USERNAME = "local-admin"
$env:NETSCOPE_ADMIN_PASSWORD = "benzersiz-ve-uzun-bir-parola-kullanin"
$env:NETSCOPE_KEY_RING_PATH = "C:\secure\netscope-keys"
```

Yerel geliştirmede `NETSCOPE_KEY_RING_PATH` tanımlanmazsa anahtarlar Git tarafından yok sayılan `backend/NetworkMonitor.Api/.keys` dizininde saklanır. Üretim ortamında dağıtım klasörünün dışında, kalıcı, erişimi denetlenen ve yedeklenen bir dizin kullanın.

#### 3. Backend'i başlatın

```powershell
dotnet run --project backend/NetworkMonitor.Api --launch-profile http
```

Geliştirme API'si `http://localhost:5107` adresinde çalışır.

#### 4. Frontend'i başlatın

İkinci bir terminalde:

```powershell
cd frontend/network-monitor-ui
npm ci
npm run dev
```

Tarayıcıda `http://localhost:5173` adresini açın. Yalnızca API adresini değiştirmek gerekiyorsa `.env.example` dosyasını `.env` adıyla kopyalayın.

### Güvenlik modeli

- Kullanıcı parolaları ASP.NET Core Identity tarafından hash'lenir ve yönetilir; düz metin olarak saklanmaz.
- Kayıtlı ağ kimlik bilgileri ve SMTP parolası ASP.NET Core Data Protection ile şifrelenir.
- Saklanan kimlik bilgilerinin çözülebilmesi için Data Protection anahtarları gereklidir. Anahtar dizini kaybedilirse bu bilgiler kurtarılamaz.
- Veritabanı ve anahtar dizini ayrı ayrı yedeklenmeli ve korunmalıdır. İkisinin birlikte açığa çıkması kayıtlı bilgilerin çözülebilmesine neden olabilir.
- Gerçek cihaz, SNMP, SSH veya SMTP kimlik bilgilerini asla depoya eklemeyin. Yerel ortam değişkenleri ve yalnızca test amaçlı sahte değerler kullanın.
- Üretim ortamlarında HTTPS sonlandırması yapılmalı; veritabanı ve anahtar dizini için dosya sistemi erişimi sınırlandırılmalıdır.

Operasyon kontrol listesi için [docs/SECURITY.md](docs/SECURITY.md) belgesine bakın.

### Testler ve kalite kontrolleri

Backend:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test NetworkMonitor.slnx --no-restore
```

Frontend:

```powershell
cd frontend/network-monitor-ui
npm ci
npm run lint
npm run test:run
npm run build
```

`npm test`, yerel geliştirme için Vitest'i izleme modunda başlatır. CI ortamında kullanılan `npm run test:run` ise testleri bir kez çalıştırıp kapanır. Testler stub/mock kullanır; fiziksel cihaz, SMTP sunucusu veya çalışan bir backend gerektirmez.

### Ekran görüntüleri

Henüz gerçek ekran görüntüleri depoya eklenmemiştir; üretilmiş veya sahte ürün görselleri kullanılmamaktadır. Aşağıdaki ekranlar yalnızca örnek verilerle kaydedilmelidir:

- **Genel Bakış** — dashboard durumu ve yakın tarihli izleme özeti
- **Cihaz Detayı** — Device Intelligence panelleri
- **Topoloji** — yetkili laboratuvar cihazlarından oluşturulan LLDP grafiği
- **Olaylar** — açık ve çözülmüş olay listesi
- **Arayüz Trafiği** — başlangıç ölçümü, grafik ve eşik durumu
- **Bildirim Merkezi** — okunmamış bildirim rozeti ve bildirim paneli

Güvenli ekran görüntüsü hazırlama yönergeleri için [docs/screenshots/README.md](docs/screenshots/README.md) belgesine bakın.

### Mimari

API; kalıcılık, kimlik doğrulama, izleme görevleri ve harici protokol işlemlerinden sorumludur. React istemcisi kimliği doğrulanmış REST endpoint'lerini çağırır ve izleme olaylarını SignalR üzerinden alır. Yapılandırma yedekleme akışı aşağıdaki genişletilebilir yapıyı kullanır:

```text
ConfigBackupService
  -> ConfigBackupProviderResolver
      -> CiscoIosConfigBackupProvider (uygulandı)
      -> Fortinet provider (uygulanmadı)
      -> gelecekteki üretici/platform sağlayıcıları
  -> ISshCommandTransport
  -> ConfigBackupStorageService (geçmiş ve fark karşılaştırması)
```

Ana veri akışları için [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) belgesine bakın.

### Bilinen sınırlamalar

- SNMP desteği salt okunur v2c ile sınırlıdır. SNMPv3 uygulanmamıştır ve v2c community bilgileri ağ üzerinde şifrelenmez.
- Yapılandırma alma özelliğinde şu anda yalnızca Cisco IOS/IOS-XE sağlayıcısı vardır. Fortinet seçildiğinde açık bir “uygulanmadı” sonucu döner ve tahmini bir komut gönderilmez.
- Yapılandırma yedekleri kullanıcı tarafından başlatılır; zamanlanmış yedekleme uygulanmamıştır.
- LLDP topoloji keşfi istek üzerine çalışır ve erişilebilir SNMP/LLDP verilerine bağlıdır. Sürekli uzlaştırılan bir topoloji veritabanı değildir.
- SQLite ve süreç içi arka plan servisleri, yatay ölçeklenen çok düğümlü bir yapı yerine tek instance'lı portfolyo dağıtımını hedefler.
- Güncel erişilebilirlik durum sayaçları süreç belleğinde tutulur ve API yeniden başladığında sıfırlanır; kalıcı kontrol geçmişi korunur.
- E-posta gönderimi kullanıcı tarafından sağlanan SMTP ayarlarını gerektirir. Gönderimin başarısı seçilen SMTP sağlayıcısına bağlıdır ve CI tarafından gerçek bir sunucuyla test edilmez.
- Uygulamada tek bir kimliği doğrulanmış kullanıcı yetki seviyesi vardır. RBAC, çoklu kiracılık ve bakım pencereleri mevcut kapsamın dışındadır.
- ICMP, SNMP, SSH, port tarama, Wake-on-LAN ve reverse DNS davranışları işletim sistemi izinlerine, yönlendirmeye, güvenlik duvarlarına ve cihaz politikalarına göre değişebilir.

### Gerçek cihazlarda sorumlu test

Şirket veya gerçek ağ cihazlarını sorumlu ağ sahibinin açık izni olmadan test etmeyin. Tek bir onaylı laboratuvar cihazı, salt okunur erişim ve ölçülü sorgulama aralıklarıyla başlayın. Port Tarayıcısı, Wake-on-LAN, Yapılandırma Yedekleme, geniş ağ keşfi veya durum değiştirebilecek herhangi bir işlemi ayrıca izin almadan çalıştırmayın. Fortinet'e özel yönergeler [docs/FORTINET_TEST_PLAN.md](docs/FORTINET_TEST_PLAN.md) belgesindedir.

---

## English

NetScope is a vendor-neutral network monitoring and operations portfolio project. It combines continuous reachability monitoring, read-only SNMP visibility, incident and notification workflows, operational tools, and configuration history in one responsive web application. Vendor-specific behavior is isolated behind extension points; the current configuration-backup implementation supports Cisco IOS/IOS-XE, but NetScope itself is not a Cisco-only product.

> Use NetScope only on systems and networks you own or are explicitly authorized to test. The repository contains no production IP addresses, communities, SSH credentials, or SMTP credentials.

### Current features

The following capabilities are confirmed by the current API, services, UI routes, and tests:

- Device management with SQLite persistence
- Background ICMP monitoring, monitoring history, and 24-hour summaries
- SignalR realtime monitoring updates with REST refresh fallback
- Bounded IPv4 CIDR IP Scanner with reverse-DNS lookup
- Read-only SNMP v2c Explorer for system information, interfaces, GET, and WALK
- Saved-interface traffic monitoring and bandwidth history charts
- Inbound/outbound bandwidth thresholds and alerts
- Confirmed Interface Down incident creation and recovery tracking
- Incident Tracking and Notification Center with read/unread actions
- Configurable email notifications and test-email action
- On-demand LLDP topology discovery
- Bounded TCP Port Scanner and Wake-on-LAN tool
- On-demand Configuration Backup over SSH
- Configuration History, content deduplication, and line diff
- Cookie-based authentication with an environment-bootstrapped admin account
- Encrypted saved SNMP and SSH network credentials
- Device Intelligence panels on the Device Detail screen

### Technology

**Backend:** .NET 10, ASP.NET Core Web API, ASP.NET Core Identity, Entity Framework Core 10, SQLite, SignalR, ASP.NET Core Data Protection, SharpSnmpLib, SSH.NET, MailKit, and xUnit.

**Frontend:** React 19, TypeScript 6, Vite 8, React Router 7, Recharts, Lucide React, Vitest 4, React Testing Library, jest-dom, user-event, jsdom, and oxlint.

### Local setup

#### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 24 LTS (Node 22.12 or later is also compatible with the current Vite version)
- npm
- PowerShell for the examples below; equivalent environment-variable syntax works in other shells

#### 1. Restore and migrate the backend

From the repository root:

```powershell
dotnet tool restore
dotnet restore
dotnet ef database update --project backend/NetworkMonitor.Api --startup-project backend/NetworkMonitor.Api
```

The default SQLite database is created as `networkmonitor.db` in the API working directory.

#### 2. Create the first admin account and configure the key ring

The bootstrap account is created only when the username does not already exist. ASP.NET Core Identity enforces a minimum 10-character password.

```powershell
$env:NETSCOPE_ADMIN_USERNAME = "local-admin"
$env:NETSCOPE_ADMIN_PASSWORD = "replace-with-a-unique-long-password"
$env:NETSCOPE_KEY_RING_PATH = "C:\secure\netscope-keys"
```

For local development, omitting `NETSCOPE_KEY_RING_PATH` stores keys under `backend/NetworkMonitor.Api/.keys`, which is git-ignored. Use a persistent, access-controlled, backed-up directory outside the deployment folder in production.

#### 3. Start the backend

```powershell
dotnet run --project backend/NetworkMonitor.Api --launch-profile http
```

The development API listens at `http://localhost:5107`.

#### 4. Start the frontend

In a second terminal:

```powershell
cd frontend/network-monitor-ui
npm ci
npm run dev
```

Open `http://localhost:5173`. Copy `.env.example` to `.env` only if the API URL must be overridden.

### Security model

- User passwords are hashed and managed by ASP.NET Core Identity; they are not stored in plain text.
- Saved network credentials and the SMTP password are encrypted with ASP.NET Core Data Protection.
- Data Protection keys are required to decrypt stored credentials. Losing the key ring makes those secrets unrecoverable.
- The database and key ring must be backed up and protected separately. Disclosure of both can allow secret decryption.
- Never commit real device, SNMP, SSH, or SMTP credentials. Use local environment variables and test-only fake values.
- Production deployments should terminate HTTPS and restrict database/key-ring filesystem access.

See [docs/SECURITY.md](docs/SECURITY.md) for the operational checklist.

### Tests and quality checks

Backend:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test NetworkMonitor.slnx --no-restore
```

Frontend:

```powershell
cd frontend/network-monitor-ui
npm ci
npm run lint
npm run test:run
npm run build
```

`npm test` starts Vitest in watch mode for local development. CI uses `npm run test:run`, so it exits after one run. The tests use stubs/mocks and do not require a device, SMTP server, or running backend.

### Screenshots

Real screenshots have not been committed yet; no generated or fake product images are used. Capture the following views with sample-only data:

- **Overview** — dashboard status and recent monitoring summary
- **Device Detail** — Device Intelligence panels
- **Topology** — LLDP graph built from authorized lab devices
- **Incidents** — open and resolved incident list
- **Interface Traffic** — baseline, chart, and threshold state
- **Notification Center** — unread badge and notification drawer

See [docs/screenshots/README.md](docs/screenshots/README.md) for safe capture guidance.

### Architecture

The API owns persistence, authentication, monitoring jobs, and external protocol operations. The React client calls authenticated REST endpoints and receives monitoring events over SignalR. Configuration backup follows this extension path:

```text
ConfigBackupService
  -> ConfigBackupProviderResolver
      -> CiscoIosConfigBackupProvider (implemented)
      -> Fortinet provider (not implemented)
      -> future vendor/platform providers
  -> ISshCommandTransport
  -> ConfigBackupStorageService (history and diff)
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the main data flows.

### Known limitations

- SNMP support is read-only v2c; SNMPv3 is not implemented, and v2c community data is not encrypted on the wire.
- Configuration retrieval currently has only a Cisco IOS/IOS-XE provider. Selecting Fortinet returns a clear not-implemented result and sends no guessed command.
- Configuration backups are user-triggered; scheduled backup is not implemented.
- LLDP topology discovery is on-demand and depends on accessible SNMP/LLDP data. It is not a continuously reconciled topology database.
- SQLite and in-process background workers target a single-instance portfolio deployment, not horizontal multi-node operation.
- Current reachability status counters are held in process memory and reset when the API restarts; persisted check history remains available.
- Email delivery requires user-supplied SMTP settings. Delivery depends on the chosen SMTP provider and is not exercised against a real server by CI.
- The application has one authenticated-user access level; RBAC, multi-tenancy, and maintenance windows are outside the current scope.
- ICMP, SNMP, SSH, port scanning, Wake-on-LAN, and reverse DNS behavior can vary with operating-system permissions, routing, firewalls, and device policy.

### Responsible real-device testing

Do not test against company or real network devices without explicit authorization from the responsible network owner. Begin with one approved lab device, read-only access, and conservative polling. Never run Port Scanner, Wake-on-LAN, Configuration Backup, broad network discovery, or any state-changing action without separate permission. Fortinet-specific guidance is in [docs/FORTINET_TEST_PLAN.md](docs/FORTINET_TEST_PLAN.md).

# 02 · Arsitektur

## 1. Gambaran besar

```
                                ┌────────────────────────────┐
           OIDC Code + PKCE     │ Keycloak (realm "nomax")   │◀── Admin API (client nomax-api,
        ┌──────────────────────▶│ SSO, password, peran       │     service account) dari modul Identity
        │                       └────────────────────────────┘
┌───────┴──────────┐  HTTPS+JWT  ┌────────────────────┐     ┌──────────────────────────────────────┐
│ React SPA (PWA)  │────────────▶│ Gateway (YARP)     │────▶│ Nomax.Api (host modular monolith)    │
│ folder web/      │◀── SignalR ─│ /api /hubs /ical   │     │  ├─ Identity   (schema identity)     │
└──────────────────┘             │ rate limit, JWT    │     │  ├─ Content    (schema content)  ────┼─▶ Penyimpanan file (disk → S3)
                                 └────────────────────┘     │  ├─ Classroom  (schema classroom) ───┼─▶ SMTP, Web Push
                                                            │  ├─ Practice   (schema practice)     │
                                                            │  └─ Insight    (schema insight)  ────┼─▶ Redis (leaderboard, backplane)
                                                            └──────────────────┬───────────────────┘
                                                                               ▼
                                                     PostgreSQL: 1 database "nomax", 1 schema per modul
```

## 2. Topologi: modular monolith yang siap dipecah (D-01 ✅, dikonfirmasi lewat Q-02)

v1 menyebut ".NET dengan beberapa API service" sekaligus memberi opsi mulai sebagai modular monolith dengan batas modul yang sama. Default v2:

- **Lima modul = lima "service" logis.** Masing-masing punya API sendiri (`/api/{modul}`), dokumen OpenAPI sendiri, schema database sendiri, dan kontrak publik sendiri.
- **Dijalankan dalam satu proses** (`Nomax.Api`) di belakang Gateway YARP. Frontend tidak tahu dan tidak peduli apakah modul berjalan di satu proses atau banyak.
- **Batasnya dijaga ketat** (tes arsitektur, §6.1) sehingga satu modul bisa dipecah menjadi service sendiri tanpa mengubah frontend (§17).

Alasan: satu proses jauh lebih mudah dibangun, di-debug, dan di-vibe-code oleh satu developer, sementara batas modul tetap sama seperti desain service di v1. **Kalau pemilik proyek memilih microservices penuh sejak awal, ganti D-01 dan sesuaikan §4, §6, dan §13 sebelum Fase 0.**

## 3. Modul & tanggung jawab

| Modul (kode) | Service di v1 | Schema DB | Prefix rute | Tanggung jawab |
|---|---|---|---|---|
| **Identity** | Identity/Admin Svc | `identity` | `/api/identity` | Profil & preferensi, manajemen akun lewat Keycloak Admin API, impor CSV, reset password, nonaktifkan |
| **Content** | Content Svc | `content` | `/api/content` | Level/Buku/Bab, materi, file (upload tus + unduhan bertanda tangan), data referensi (kana, kanji, kosakata, tata bahasa, kalimat) |
| **Classroom** ✏️ | Class Svc | `classroom` | `/api/classroom`, `/ical` | Kelas, anggota, unlock bab, jadwal, feed ICS, notifikasi & pengingat, langganan web push |
| **Practice** | Practice Svc | `practice` | `/api/practice` | Bank soal, generator, drilling, mastery & kelemahan, kuis/ujian, tugas, penilaian, nilai kelas, komentar |
| **Insight** | Insight Svc | `insight` | `/api/insight`, `/hubs/leaderboard` | Log aktivitas, poin, season, leaderboard live, laporan |
| **Gateway** | Gateway (YARP) | – | `/` | Satu pintu masuk, validasi JWT, rate limit, routing; di produksi juga menyajikan file SPA |

✏️ Service "Class" dinamai **Classroom** di kode agar namespace `Nomax.Modules.Classroom` tidak bentrok dengan entity `Class` (C# tidak boleh punya namespace dan tipe bernama sama di jalur yang sama). Nama entity dan tabel tetap `Class` / `classes`.

## 4. Struktur repo (target setelah Fase 0)

```
Nomax_JP_Learn/
├── AGENTS.md · CLAUDE.md · README.md · .editorconfig · .gitattributes · .gitignore
├── global.json                       # ➕ versi SDK .NET + runner tes Microsoft.Testing.Platform
├── Nomax.slnx                        # solution .NET 10 (format slnx)
├── Directory.Build.props             # net10.0, nullable, warnings-as-errors, analyzer
├── Directory.Packages.props          # central package management: versi paket di satu tempat
├── docs/                             # blueprint v2 (sumber kebenaran)
├── design/                           # tokens.css, tailwind-theme.css, preview.html, brand/
├── scripts/dev-live.mjs             # ➕ supervisor sistem live dev (API, Gateway, Vite; pulih otomatis)
├── infra/
│   ├── README.md                     # ➕ cara menjalankan, port, kredensial dev
│   ├── docker-compose.yml            # postgres, valkey (pengganti redis, D-29), keycloak, mailpit
│   ├── .env.example                  # contoh variabel; .env asli tidak di-commit
│   ├── keycloak/realm-nomax.json     # realm export: client, role, user dev
│   └── postgres/init.sql             # membuat database keycloak
├── src/
│   ├── Nomax.Gateway/                # YARP
│   ├── Nomax.Api/                    # host: composition root, memuat semua modul
│   ├── BuildingBlocks/
│   │   ├── Nomax.SharedKernel/       # tipe bersama murni: TextBlock, RichText, Roles, Result, PagedResult, IntegrationEvent
│   │   └── Nomax.BuildingBlocks/     # infrastruktur bersama: konvensi EF, outbox/inbox, auth, ProblemDetails, OpenAPI, logging, signed URL
│   └── Modules/
│       ├── Identity/   Nomax.Modules.Identity/   + Nomax.Modules.Identity.Contracts/
│       ├── Content/    Nomax.Modules.Content/    + Nomax.Modules.Content.Contracts/
│       ├── Classroom/  Nomax.Modules.Classroom/  + Nomax.Modules.Classroom.Contracts/
│       ├── Practice/   Nomax.Modules.Practice/   + Nomax.Modules.Practice.Contracts/
│       └── Insight/    Nomax.Modules.Insight/    + Nomax.Modules.Insight.Contracts/
├── tests/
│   ├── Directory.Build.props         # ➕ pengaturan bersama proyek tes (xUnit v3, Shouldly)
│   ├── Nomax.BuildingBlocks.Tests/   # ➕ unit test SharedKernel + BuildingBlocks
│   ├── Nomax.Modules.{Modul}.Tests/  # unit test per modul (Domain + handler)
│   ├── Nomax.IntegrationTests/       # pipeline API & Gateway, API + PostgreSQL (Testcontainers), tes otorisasi
│   └── Nomax.ArchitectureTests/      # aturan dependency antar-modul
└── web/                              # React SPA
    ├── src/
    │   ├── app/                      # router, providers, layout per peran (murid/sensei/admin)
    │   ├── features/                 # identity, content, classroom, practice, insight
    │   │   └── practice/{api,components,routes,hooks}/
    │   ├── components/ui/            # komponen shadcn/ui (diberi token NomaX)
    │   ├── components/japanese/      # JapaneseText, ScriptToggle, RomajiLine
    │   ├── i18n/                     # tb(), parser furigana, romaji, labels/*
    │   ├── lib/                      # api client, auth (OIDC), signalr, upload, utils
    │   └── styles/globals.css        # mengimpor ../../../design/tailwind-theme.css
    ├── public/                       # logo, favicon, ikon PWA, manifest
    └── e2e/                          # Playwright
```

## 5. Struktur internal modul (backend)

Pola **vertical slice**: satu use case = satu file berisi request, validator, handler, dan pemetaan endpoint.

```
Nomax.Modules.Practice/
├── PracticeModule.cs                 # AddPracticeModule(services, config) dan MapPracticeEndpoints(app)
├── Domain/                           # entity, value object, aturan bisnis MURNI (tanpa I/O, tanpa EF)
│   ├── Drilling/MasteryCalculator.cs # BR-MASTERY-01
│   └── Grading/GradeCalculator.cs    # BR-NILAI-01…04
├── Features/
│   ├── Drill/StartDrillSession.cs
│   ├── Drill/SubmitDrillAnswer.cs
│   └── Quizzes/…
├── Infrastructure/
│   ├── PracticeDbContext.cs          # HasDefaultSchema("practice")
│   ├── Configurations/               # IEntityTypeConfiguration<T> per entity
│   └── Migrations/
├── IntegrationEvents/                # handler untuk event dari modul lain
└── Public/                           # implementasi interface milik Nomax.Modules.Practice.Contracts
```

- Endpoint memakai **Minimal API** dengan `MapGroup("/api/practice")` per modul (D-06 🟡).
- Handler adalah class biasa yang di-inject lewat DI. **Tidak memakai MediatR atau AutoMapper** (lisensi komersial + menambah lapisan). Mapping ditulis manual.
- Aturan bisnis yang punya `BR-*` hidup di `Domain/` sebagai fungsi/class murni agar mudah dites unit.

## 6. Komunikasi antar-modul

### 6.1 Aturan dependency (dijaga `Nomax.ArchitectureTests`)

| Proyek | Boleh mereferensikan |
|---|---|
| `Nomax.Modules.X` | `Nomax.SharedKernel`, `Nomax.BuildingBlocks`, `Nomax.Modules.Y.Contracts` (Y ≠ X) |
| `Nomax.Modules.X.Contracts` | `Nomax.SharedKernel` saja |
| `Nomax.Api` | semua modul (composition root) |
| `Nomax.Gateway` ✏️ | `Nomax.SharedKernel`, `Nomax.BuildingBlocks` (JWT, Problem Details, dan log yang sama dengan API); tidak mereferensikan modul maupun `Nomax.Api` |
| `Nomax.BuildingBlocks` | `Nomax.SharedKernel` saja |
| `Nomax.SharedKernel` | tidak ada (hanya .NET; tanpa ASP.NET Core/EF Core) |

Aturan ini dicek di dua level: `ProjectReference` di setiap `.csproj` di bawah `src/`, dan dependency IL dengan NetArchTest (kemurnian SharedKernel, Contracts, dan folder `Domain/`). Jenis proyek baru di `src/` wajib ditambahkan ke tabel ini dan ke tes arsitektur lebih dulu.

Dilarang: mereferensikan proyek internal modul lain, memakai DbContext modul lain, query ke schema modul lain, foreign key lintas schema, atau berbagi tabel.

### 6.2 Query sinkron lewat Contracts

| Contracts | Interface | Dipakai oleh | Fungsi utama |
|---|---|---|---|
| Identity | `IUserDirectory` | Classroom, Practice, Insight | Profil ringkas berdasarkan daftar id: nama tampilan, peran, aktif, zona waktu, locale, email (khusus notifikasi) |
| Content | `IContentCatalog` | Classroom, Practice | Validasi dan info Level/Buku/Bab; level dari sebuah bab; urutan bab dalam buku |
| Content | `IReferenceDataReader` | Practice (generator) | Daftar kana/kanji/kosakata/tata bahasa/kalimat per level dan bab |
| Content | `IStoredFileService` | Practice (tugas) | Validasi file (status, pengunggah, purpose) dan pembuatan URL unduhan bertanda tangan |
| Classroom | `IClassAccessReader` | Content, Practice, Insight | `IsTeacherOf`, `IsStudentOf`, `CanTeacherSeeStudent`, `IsChapterAccessible`, `GetAccessibleChapterIds`, `GetStudentClassIds`, `GetTeacherClassIds`, `GetClassInfo`, `GetClassStudentIds` |

Interface baru wajib dicatat di tabel ini sebelum dipakai.

Signature yang sudah diimplementasikan:

```csharp
// Nomax.Modules.Identity.Contracts (Fase 0.2)
Task<IReadOnlyDictionary<Guid, UserSummary>> IUserDirectory.GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
record UserSummary(Guid Id, string Username, string DisplayName, string Role, bool IsActive, string Timezone, string UiLocale, string? Email);
```

### 6.3 Integration event lewat outbox (D-03 🟡)

1. Producer menyimpan perubahan dan baris `outbox_messages` dalam **satu transaksi** DbContext modulnya.
2. `OutboxProcessor` (BackgroundService per modul, interval 1 detik) mengambil pesan dengan `FOR UPDATE SKIP LOCKED`, lalu memanggil semua `IIntegrationEventHandler<T>` yang terdaftar (in-process).
3. Consumer mencatat `inbox_messages(message_id, handler)` di schema-nya sendiri dalam transaksi yang sama dengan efek handler, sehingga handler **idempoten**.
4. Gagal: `attempts` bertambah, backoff eksponensial; setelah 10 kali ditandai gagal dan dicatat sebagai error.
5. Pesan tanpa handler langsung ditandai terproses.
6. Daftar event dan payload: [05](05-api-dan-event.md) §8. Event baru wajib didokumentasikan di sana sebelum dipakai.

Outbox mulai dibangun di Fase 1. Consumer pertama (Insight) baru ada di Fase 4, sehingga log aktivitas dan poin dimulai dari Fase 4 (tanpa backfill).

## 7. Data & persistensi

- **PostgreSQL**: satu database `nomax`, lima schema. Keycloak memakai database terpisah `keycloak` di instance yang sama (dev).
- **EF Core 10 + Npgsql**: satu DbContext per modul, konvensi snake_case, tabel riwayat migrasi per schema (`__ef_migrations_history`).
- Migrasi: di dev otomatis saat startup (`Database:MigrateOnStartup = true`); di produksi lewat langkah eksplisit (migration bundle) 🟡.
- **Redis**: leaderboard (sorted set), cache nama tampilan, backplane SignalR saat lebih dari satu instance. **Redis bukan sumber kebenaran**; semua isinya bisa dibangun ulang dari PostgreSQL.
- Detail skema: [03](03-skema-data.md).

## 8. Alur autentikasi & otorisasi

1. SPA mengarahkan pengguna ke Keycloak (Authorization Code + PKCE). Token disimpan di memori/sessionStorage oleh `oidc-client-ts`, **tidak di localStorage**.
2. SPA memanggil `/api/...` lewat Gateway dengan header `Authorization: Bearer <access_token>`.
3. Gateway memvalidasi JWT dan menerapkan rate limit per pengguna (`sub`) atau per IP untuk rute publik.
4. API memvalidasi JWT lagi (Authority `…/realms/nomax`, audience `nomax-api`), memetakan `realm_access.roles` menjadi role claim, mengecek profil aktif, lalu policy peran (lapis 1) dan kepemilikan resource (lapis 2). Detail: [01](01-peran-dan-akses.md) §5.
5. SignalR: token dikirim lewat query `access_token`, **hanya diterima pada path `/hubs/*`**.

## 9. Real-time

- Hub `/hubs/leaderboard` di modul Insight. Grup per season (`season:{seasonId}`); pesan pribadi lewat `Clients.User(sub)`.
- Broadcast papan peringkat dibatasi maksimal 1 kali per detik per season (BR-SEASON-04).
- Backplane Redis diaktifkan lewat konfigurasi saat API dijalankan lebih dari satu instance.
- Kontrak hub: [05](05-api-dan-event.md) §9.

## 10. File & upload

- Upload resumable dengan protokol **tus** (`tusdotnet`) di `/api/content/uploads`. Maksimal 1 GiB per file, tipe file bebas (BR-FILE-01, 02).
- Selama upload, file berada di folder sementara. Saat selesai: dipindah ke `IFileStorage`, SHA-256 dihitung, status menjadi `ready`.
- Penyimpanan default **disk lokal** di balik abstraksi `IFileStorage`; implementasi S3-compatible bisa ditambahkan tanpa mengubah modul lain (D-05 🟡).
- Unduhan **selalu lewat endpoint resource pemilik** (materi, submission) yang mengecek akses, lalu mengarahkan ke URL bertanda tangan berumur 5 menit (BR-FILE-04).

## 11. Background job (D-04 🟡)

Memakai `BackgroundService` + `PeriodicTimer`, aman untuk beberapa instance lewat `FOR UPDATE SKIP LOCKED` atau advisory lock. Hangfire/Quartz tidak dipakai kecuali kebutuhan bertambah.

| Job | Modul | Interval | Fungsi |
|---|---|---|---|
| `OutboxProcessor` | semua | 1 detik | Mengirim integration event |
| `NotificationDispatcher` | Classroom | 30 detik | Mengirim notifikasi jatuh tempo (email, web push) |
| `UploadCleanup` | Content | 1 jam | Menghapus upload tus kedaluwarsa (> 24 jam) dan file yatim (> 7 hari) |
| `QuizAttemptExpirer` | Practice | 1 menit | Auto-submit attempt yang melewati `expires_at` |
| `GradeRecomputer` | Practice | 1 jam | Menghitung ulang nilai kelas untuk kuis/tugas yang baru lewat tenggat |
| `DrillSessionCloser` | Practice | 10 menit | Menutup sesi drilling yang idle > 30 menit |

## 12. Arsitektur frontend (ringkas; detail di 06 dan 07)

| Kebutuhan | Pilihan (D-02 🟡) |
|---|---|
| Build & dev server | Vite |
| UI | React + TypeScript (strict) |
| Routing | React Router (mode data, SPA) dengan layout per peran |
| Data server | TanStack Query (semua panggilan API lewat hook query/mutation) |
| Klien API | Tipe digenerate dari OpenAPI (`openapi-typescript`) + `openapi-fetch` |
| Auth | `oidc-client-ts` + `react-oidc-context` |
| Styling | Tailwind CSS v4 + shadcn/ui + design tokens NomaX |
| Form | react-hook-form + zod |
| Jepang | WanaKana + komponen `JapaneseText` buatan sendiri |
| Real-time | `@microsoft/signalr` |
| Upload | `tus-js-client` |
| PWA & push | `vite-plugin-pwa` (manifest + service worker) |
| Ikon | `lucide-react` |
| Font | `@fontsource` (Plus Jakarta Sans, Noto Sans JP, Klee One) |
| Tes | Vitest + Testing Library, Playwright + axe |

State global tambahan (Redux, Zustand, dll.) tidak dipakai tanpa izin. Preferensi pengguna disimpan di context dan disinkronkan ke API.

## 13. Infrastruktur pengembangan (D-25 🟡)

Infrastruktur berjalan di Docker; aplikasi .NET dan web berjalan langsung di mesin developer. Ini menghindari masalah *issuer mismatch* token Keycloak (issuer di token harus sama dengan yang dilihat API) dan mempercepat hot reload.

| Layanan | Cara jalan | Port host |
|---|---|---|
| PostgreSQL | Docker (`postgres:18`) | 5432 |
| Redis | Docker (`valkey/valkey:9.2`, kompatibel protokol Redis, BSD-3; D-29) | 6379 |
| Keycloak | Docker (`quay.io/keycloak/keycloak:26.8`, `start-dev --import-realm`) | 8081 |
| Mailpit | Docker (`axllent/mailpit:v1.31`, penangkap email dev) | 8025 (UI), 1025 (SMTP) |
| Gateway | `dotnet run --project src/Nomax.Gateway` | 8080 |
| Nomax.Api | `dotnet run --project src/Nomax.Api` | 5080 |
| Web | `npm --prefix web run dev` (proxy `/api`, `/hubs`, `/ical` ke 8080) | 5173 |

Prasyarat mesin: .NET 10 SDK, Node.js LTS (22 atau 24), Docker Desktop, Git.

➕ Selama pengembangan, `node scripts/dev-live.mjs start` menjalankan Gateway, API, dan web sekaligus: layanan yang mati dinaikkan lagi, perubahan di `src/` di-build dan di-restart otomatis, dan aplikasi .NET berjalan dari salinan di `.live/` agar tidak mengunci output build (`infra/README.md`).

Port Docker hanya dibuka di `127.0.0.1`. Bila 5432/6379 sudah dipakai proyek lain, port host PostgreSQL dan Valkey bisa diganti lewat `POSTGRES_PORT`/`VALKEY_PORT` di `infra/.env` (langkahnya di `infra/README.md`).

## 14. Konfigurasi & rahasia

- `appsettings.json` berisi default non-rahasia. Rahasia dev memakai `dotnet user-secrets`; produksi memakai environment variable.
- `infra/.env` (tidak di-commit) untuk Docker; `infra/.env.example` di-commit.
- Seksi konfigurasi utama: `ConnectionStrings` (Postgres, Redis), `Keycloak` (Authority, Audience, Realm, AdminClientId, AdminClientSecret), `Storage` (Provider, RootPath, SigningKey), `Upload` (MaxFileBytes), `Email` (SmtpHost, SmtpPort, From), `WebPush` (PublicKey, PrivateKey, Subject), `Practice` (Mastery, Drill), `Grading`, `Leaderboard`.
- Semua angka aturan bisnis yang disebut "konfigurabel" di [04](04-aturan-bisnis.md) dibaca dari konfigurasi lewat Options pattern, dengan default sesuai dokumen.

## 15. Observability

- Serilog dengan output JSON ke console; enrich `TraceId`, `UserId` (`sub`), `Module`. **Dilarang** mencatat token, password, isi file, atau email.
- Health check: `/health/live` dan `/health/ready` (PostgreSQL, Redis, Keycloak).
- OpenAPI per modul di `/openapi/{modul}.json` dan UI Scalar di `/scalar`, keduanya hanya di environment Development.
- OpenTelemetry dan Aspire Dashboard: opsional.

## 16. Target deployment (Q-08 ❓)

Usulan 🟡: satu VPS (2–4 vCPU, 8 GB RAM) dengan Docker Compose produksi, reverse proxy Caddy (HTTPS otomatis), Gateway menyajikan file SPA, backup harian (pg_dump, folder file, export realm Keycloak) ke penyimpanan di luar server. Checklist rilis ada di [09](09-kualitas-dan-keamanan.md) §8.

## 17. Cara memecah modul menjadi service (nanti)

1. Buat host baru `src/Hosts/Nomax.{Modul}.Host` yang memuat modul tersebut.
2. Modul lain tetap memakai interface Contracts yang sama; implementasinya diganti klien HTTP.
3. Dispatcher event in-process diganti RabbitMQ (publisher + consumer). Outbox dan inbox tetap.
4. Rute Gateway `/api/{modul}/**` diarahkan ke host baru.
5. Database boleh dipisah karena data sudah terisolasi per schema.

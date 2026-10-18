# 08 · Roadmap & Fase

## 1. Cara memakai dokumen ini

- Kerjakan **fase berurutan**. Satu sesi AI mengerjakan **satu sub-langkah** (mis. 0.2) atau satu fase kecil. **Jangan mencampur dua fase dalam satu sesi** (D-23 🟡).
- Setiap fase punya **brief cold-start**: AI yang baru mulai tanpa konteks tetap bisa bekerja hanya dengan membaca brief + dokumen yang disebut.
- Alur per sesi: `/fase <n>` → AI menulis RENCANA → pemilik proyek menyetujui → AI mengerjakan → `/cek-aturan` → laporan akhir. Template ada di `AGENTS.md` §3.
- Sub-langkah selesai hanya bila **semua kriteria penerimaannya** terpenuhi. Kalau sub-langkah terlalu besar, pecah (§9).

## 2. Ringkasan fase

| Fase | Isi | Demo (v1) | Sub-langkah |
|---|---|---|---|
| 0 | Fondasi: skeleton, Keycloak, akun oleh admin, i18n + JapaneseText | Login 3 peran, toggle bahasa dan tampilan Jepang | 0.1 Repo & infra · 0.2 Identity · 0.3 Frontend shell |
| 1 | Konten dan kelas: Level/Buku/Bab, upload, kelas, unlock bab, jadwal, ICS, pengingat | Sensei upload materi dan buka bab, murid lihat kalender | 1.1 Outbox & struktur konten · 1.2 File & upload · 1.3 Kelas & unlock · 1.4 Materi · 1.5 Jadwal & notifikasi · 1.6 UI |
| 2 | Latihan mandiri: bank soal, drilling, mastery, kelemahan, progres sensei | Murid drilling, sensei lihat progres dan kelemahan | 2.1 Bank soal · 2.2 Generator · 2.3 Mesin drilling · 2.4 Kelemahan & progres · 2.5 UI |
| 3 | Kuis, ujian, tugas, nilai 0–4, komentar | Penilaian lengkap | 3.1 Kuis & attempt · 3.2 Tugas · 3.3 Nilai kelas · 3.4 UI |
| 4 | Leaderboard live dan season | Papan peringkat real-time per level | 4.1 Insight & poin · 4.2 Redis & SignalR · 4.3 UI |
| 5 | Tulis tangan, impor PDF, Google Calendar OAuth, persiapan produksi | Fitur lanjutan | 5.1 Tulis tangan · 5.2 Impor PDF · 5.3 Google Calendar · 5.4 Hardening & rilis |

Rilis beta ke komunitas: usulan setelah Fase 2 (Q-07 ❓).

## 3. Fase 0 — Fondasi

**Tujuan.** Repo yang bisa dijalankan dari nol, login 3 peran lewat Keycloak, admin bisa mengelola akun, dan UI bilingual dengan pendamping Jepang berfungsi.

**Brief cold-start.** Repo baru berisi dokumen (`docs/`), aturan AI (`AGENTS.md`), dan token desain (`design/`). Belum ada kode. Topologi: modular monolith service-ready (D-01 ✅, dikonfirmasi lewat Q-02). Stack: .NET 10, EF Core + PostgreSQL, Redis, YARP, Keycloak, React + TypeScript (Vite, Tailwind v4, shadcn/ui). Infrastruktur di Docker, aplikasi di host (D-25).

**Baca dulu.** `AGENTS.md` · [01](01-peran-dan-akses.md) §5–9 · [02](02-arsitektur.md) seluruhnya · [03](03-skema-data.md) §1, §3, §4 · [05](05-api-dan-event.md) §1–4 · [06](06-teks-multibahasa.md) seluruhnya · [07](07-desain-ui-ux.md) §3–5, §10, §12, §15 · [09](09-kualitas-dan-keamanan.md) §1–2.

**Keputusan yang dicek di rencana.** Q-02 (topologi), D-02 (stack frontend), Q-01 (nama produk; boleh tetap nama kerja).

### 0.1 Repo & infrastruktur
- `git init` (bila belum), `.gitignore` (dotnet, node, `.env`, `storage/`), `Nomax.slnx`, `Directory.Build.props` (net10.0, nullable, implicit usings, warnings-as-errors), `Directory.Packages.props`.
- `infra/docker-compose.yml` (PostgreSQL, Redis, Keycloak `start-dev --import-realm` di port 8081, Mailpit) dengan healthcheck; `infra/postgres/init.sql`; `infra/.env.example`; `infra/README.md` (cara jalan + kredensial dev).
- `infra/keycloak/realm-nomax.json` sesuai [01](01-peran-dan-akses.md) §8, termasuk user dev (1 admin, 1 sensei, 2 murid) dengan **id tetap** agar profilnya bisa di-seed dengan id yang sama.
- `Nomax.Gateway` (YARP: `/api/**`, `/hubs/**`, `/ical/**`, `/openapi/**`, `/scalar/**` → API; validasi JWT; rate limit; health).
- `Nomax.Api` (Serilog, Problem Details + katalog `code`, JWT Keycloak + pemetaan role + policy, middleware profil aktif, OpenAPI per modul + Scalar, health check, registrasi modul).
- `Nomax.SharedKernel` (Roles, TextBlock/JaSegment/RichText, Result/Error, PagedResult, helper UUIDv7, converter enum snake_case) dan `Nomax.BuildingBlocks` (konvensi EF: snake_case, interceptor `created_at`/`updated_at`, tabel migrasi per schema; auth; error handling; filter validasi).
- `Nomax.ArchitectureTests` (aturan dependency [02](02-arsitektur.md) §6.1).
- Perbarui bagian "Perintah proyek" di `CLAUDE.md`.

### 0.2 Modul Identity
- Tabel `identity.user_profiles` + migrasi + seed profil user dev (id = id di realm export).
- Endpoint [05](05-api-dan-event.md) §4. Klien Keycloak Admin API (typed `HttpClient`, client credentials `nomax-api`, cache token).
- BR-AKUN-01…07 dengan unit test; tes integrasi otorisasi memakai JWT uji (kunci lokal) dan Keycloak Admin API yang di-mock lewat interface.

### 0.3 Frontend shell
- `web/`: Vite + React + TS strict, ESLint + Prettier, Vitest, Playwright + axe.
- Tailwind v4 + shadcn/ui; `globals.css` mengimpor `design/tailwind-theme.css`; font `@fontsource`; skrip `lint:tokens` ([07](07-desain-ui-ux.md) §15).
- OIDC (login, callback, refresh, logout), guard rute per peran, layout murid/sensei/admin, TopBar (logo, `ScriptToggle`, `LanguageSwitcher`, menu profil), garis merek merah.
- Klien API (`gen:api` untuk Identity) + TanStack Query + pemetaan error ke label `errors.*`.
- i18n lengkap sesuai [06](06-teks-multibahasa.md): `tb()`, parser furigana, romaji (uji dengan kasus §5), label `common`/`nav`/`errors`/`identity`, `PrefsProvider`, `JapaneseText`, `Label`, `useT`, `ScriptToggle`, dialog onboarding.
- Halaman: sambutan, callback, beranda per peran (empty state), profil (preferensi), admin pengguna (daftar, buat, impor CSV + dry run, reset password, nonaktif/aktif).
- Turunan logo (favicon, ikon PWA) dari file master + `manifest.webmanifest` (service worker menyusul di Fase 1).

### Kriteria penerimaan Fase 0
- [ ] `docker compose -f infra/docker-compose.yml up -d` → semua layanan sehat, realm `nomax` ter-import.
- [ ] `dotnet build` tanpa warning; `dotnet test` hijau (unit, arsitektur, integrasi).
- [ ] Gateway & API berjalan; `/health/ready` OK; Scalar menampilkan OpenAPI Identity.
- [ ] Login sebagai admin, sensei, murid → masing-masing ke layout perannya; rute peran lain → halaman 403.
- [ ] Admin membuat akun murid → password sementara tampil sekali → murid login → dipaksa ganti password → dialog onboarding.
- [ ] Impor CSV: baris salah dilaporkan tanpa membuat akun; file valid membuat akun + file password sementara.
- [ ] Reset password dan nonaktifkan berfungsi; akun nonaktif tidak bisa login dan API membalas 403 `account_inactive`.
- [ ] Ganti bahasa ID/EN dan 5 mode tampilan Jepang langsung mengubah label dan tetap setelah reload.
- [ ] Tes furigana/romaji lulus; tidak ada teks UI hardcode; `lint:tokens` lulus; axe tanpa pelanggaran serius.

**Risiko.** Issuer/audience token tidak cocok (mitigasi D-25 + audience mapper), pemetaan `realm_access.roles`, refresh token di SPA, line ending Windows (`.gitattributes`).

## 4. Fase 1 — Konten & kelas

**Tujuan.** Sensei mengunggah materi dan membuka bab per kelas; murid melihat materi bab yang terbuka dan jadwal kelas di kalender.

**Brief cold-start.** Fase 0 selesai: solution, Gateway, host API, modul Identity, frontend shell dengan login, layout per peran, dan sistem label. Fase ini menambah modul **Content** dan **Classroom**, infrastruktur outbox, upload tus, dan notifikasi.

**Baca dulu.** [01](01-peran-dan-akses.md) · [02](02-arsitektur.md) §6–7, §10–11 · [03](03-skema-data.md) §5, §6, §10 · [04](04-aturan-bisnis.md) §2–4 · [05](05-api-dan-event.md) §5, §6, §8, §10, §12, §13 · [07](07-desain-ui-ux.md) §10–12.

### Sub-langkah
1. **1.1 Outbox & struktur konten**: outbox/inbox + `OutboxProcessor` di BuildingBlocks; Content: Level/Buku/Bab (admin), data referensi kana + impor (source/license wajib), `IContentCatalog`, `IReferenceDataReader`; event Identity (`UserAccountCreated`, dst.) mulai diterbitkan.
2. **1.2 File & upload**: `stored_files`, endpoint tus, `IFileStorage` (disk), unduhan bertanda tangan, job `UploadCleanup`, `IStoredFileService`.
3. **1.3 Kelas & unlock**: kelas, anggota, pengajar, unlock (event `ChapterUnlocked`/`ChapterLocked`), `IClassAccessReader`.
4. **1.4 Materi**: CRUD materi, gating BR-KONTEN-01 lewat `IClassAccessReader`, unduhan lewat resource.
5. **1.5 Jadwal & notifikasi**: sesi, token & feed ICS, antrean notifikasi + `NotificationDispatcher` (email lewat Mailpit di dev, web push VAPID), pengingat BR-JADWAL-06, langganan push, service worker (`vite-plugin-pwa`).
6. **1.6 UI**: admin konten & data referensi; sensei: kelas (anggota, bab, materi, jadwal); murid: kelas saya, daftar bab (terbuka/terkunci), penampil materi, jadwal + "Tambah ke Google Calendar" + langganan ICS + "Aktifkan pengingat".

### Kriteria penerimaan Fase 1
- [ ] Admin membuat level/buku/bab dan mengimpor data kana dengan sumber + lisensi.
- [ ] Sensei membuat kelas, menambah murid, membuka Bab 1. Murid melihat Bab 1 terbuka dan Bab 2 terkunci; API membalas 403 `chapter_locked` untuk Bab 2.
- [ ] Upload file besar bisa dijeda dan dilanjutkan; file > 1 GiB ditolak; unduhan hanya untuk yang berhak; URL bertanda tangan kedaluwarsa.
- [ ] Membuat sesi → email terlihat di Mailpit dan push muncul di browser; pengingat terjadwal sesuai kasus uji BR-JADWAL-06.
- [ ] Feed ICS terbaca di Google Calendar (uji manual); perubahan menaikkan `SEQUENCE`.
- [ ] Event tersimpan di outbox dan terproses (belum ada konsumen).
- [ ] Unit test BR-AKSES-01, BR-JADWAL-06, BR-FILE-*; tes integrasi otorisasi untuk setiap endpoint baru.

**Risiko.** Upload 1 GB di jaringan lambat (tus + potongan 8 MB), web push di iOS hanya untuk PWA yang dipasang ke layar utama (iOS 16.4+), Google Calendar lambat menyegarkan feed ICS (pengingat utama tetap email/push).

## 5. Fase 2 — Latihan mandiri

**Tujuan.** Murid drilling tanpa batas dari bab yang terbuka; sistem mencatat penguasaan dan kelemahan; sensei melihat progres dan memberi komentar.

**Brief cold-start.** Fase 1 selesai: konten, file, kelas, unlock, jadwal, outbox. Fase ini membangun modul **Practice** bagian bank soal dan drilling. Insight belum ada, jadi "terakhir aktif" di halaman progres memakai data drilling dari Practice.

**Baca dulu.** [03](03-skema-data.md) §7.1–7.5, §8 · [04](04-aturan-bisnis.md) §5–6 · [05](05-api-dan-event.md) §7 (bank soal, drilling), §8 · [06](06-teks-multibahasa.md) §7–9 · [07](07-desain-ui-ux.md) §8, §10, §11 (alur 3 dan 7).

### Sub-langkah
1. **2.1 Bank soal**: sumber soal, CRUD soal + validasi payload per `kind`, alur status, scope, aturan BR-SOAL-04, `preview-answer`; UI bank soal staf (filter, editor per `kind` dengan `FuriganaInput` dan pratinjau versi murid).
2. **2.2 Generator**: template BR-SOAL-05, review dan terbit massal oleh admin.
3. **2.3 Mesin drilling**: sesi, pool BR-DRILL-02, mode normal & fokus lemah, batch, evaluasi BR-DRILL-05/06, jawaban idempoten, mastery BR-MASTERY-01, event `AnswerSubmitted`/`DrillSessionEnded`, job `DrillSessionCloser`.
4. **2.4 Kelemahan & progres**: `me/weaknesses`, `me/mastery`, progres kelas, detail murid, komentar.
5. **2.5 UI**: Latihan (pilih kategori/bab/mode), layar drilling (pilihan, tap, ketik dengan konversi romaji→kana, cocokkan, flashcard), shortcut keyboard, umpan balik ○/×, ringkasan + stempel hanko, Kelemahanku, mode uji coba staf.

### Kriteria penerimaan Fase 2
- [ ] Semua kasus uji BR-DRILL-06, BR-MASTERY-01/02 lulus.
- [ ] Kunci jawaban tidak pernah ada di respons sebelum murid menjawab (tes otomatis).
- [ ] Kirim ulang jawaban dengan id yang sama tidak menggandakan data.
- [ ] Mode fokus lemah memilih item ber-mastery rendah lebih sering (tes statistik sederhana dengan seed).
- [ ] Sensei hanya melihat progres murid di kelasnya; murid hanya melihat miliknya (tes otorisasi).
- [ ] Respons kirim jawaban p95 < 300 ms di mesin dev.
- [ ] Layar drilling bisa dipakai penuh dengan keyboard dan lolos axe.

## 6. Fase 3 — Kuis, ujian, tugas, nilai

**Tujuan.** Penilaian lengkap: kuis dan ujian dari bank soal, tugas dengan file, nilai kelas 0–4.

**Brief cold-start.** Fase 2 selesai: bank soal, evaluasi jawaban (dipakai ulang untuk kuis), drilling, mastery. Fase ini melengkapi modul **Practice** dengan kuis, tugas, dan nilai.

**Baca dulu.** [03](03-skema-data.md) §7.6–7.14 · [04](04-aturan-bisnis.md) §7–9 · [05](05-api-dan-event.md) §7 (kuis, tugas, nilai), §8 · [07](07-desain-ui-ux.md) §10, §11 (alur 4, 5, 8).

### Sub-langkah
1. **3.1 Kuis & attempt**: CRUD kuis, susunan soal, terbit, attempt (timer server, acak stabil, autosave, submit), penilaian otomatis, penilaian manual `long_answer`, pembahasan BR-KUIS-09, event `QuizGraded`, job `QuizAttemptExpirer`.
2. **3.2 Tugas**: CRUD tugas, submission (teks + file tus), kirim ulang, terlambat, penilaian + feedback, event `SubmissionGraded`.
3. **3.3 Nilai kelas**: `GradeCalculator` murni (BR-NILAI-01…04) + skema nilai (BR-NILAI-06) + penghitungan ulang (event + job `GradeRecomputer`).
4. **3.4 UI**: sensei (penyusun kuis, tugas, antrean penilaian, tabel nilai, skema nilai); murid (mengerjakan kuis, mengirim tugas, nilai saya).

### Kriteria penerimaan Fase 3
- [ ] Semua kasus uji BR-KUIS-03 dan BR-NILAI lulus; nama tes memuat ID aturan.
- [ ] Timer ditegakkan di server: jawaban setelah kedaluwarsa ditolak 409 `attempt_expired`; attempt kedaluwarsa ter-submit otomatis.
- [ ] Murid tidak bisa melihat kunci sebelum waktunya (BR-KUIS-09) — tes otomatis.
- [ ] Batas percobaan, jendela waktu, dan aturan edit kuis (BR-KUIS-10) ditegakkan.
- [ ] Tugas dengan file 1 GB bisa dikirim; hanya murid pengirim, pengajar kelas, dan admin yang bisa mengunduh.
- [ ] Nilai 0–4 muncul untuk murid dan sensei dengan rincian per komponen.

## 7. Fase 4 — Leaderboard live

**Tujuan.** Papan peringkat real-time per level dengan aturan anti-farming dan season.

**Brief cold-start.** Fase 3 selesai. Practice sudah menerbitkan `AnswerSubmitted`, `DrillSessionEnded`, `QuizGraded`, `SubmissionGraded`; Classroom dan Identity juga menerbitkan event. Fase ini membangun modul **Insight** sebagai konsumen pertama. Poin dan log aktivitas dimulai sejak Fase 4 berjalan (tanpa backfill).

**Baca dulu.** [03](03-skema-data.md) §9, §11 · [04](04-aturan-bisnis.md) §10–11 · [05](05-api-dan-event.md) §7a, §8, §9, §11 · [07](07-desain-ui-ux.md) §10 (LeaderboardRow/Podium), §11 (alur 11).

### Sub-langkah
1. **4.1 Insight & poin**: konsumen event (log aktivitas), mesin aturan poin murni (BR-POIN) + tes, `point_events`, seed season per level, tutup season + arsip peringkat.
2. **4.2 Redis & SignalR**: sorted set per season, hub `/hubs/leaderboard` (`entryKey`, broadcast maks 1/detik, `PointsAwarded`, `MyRankUpdated`), konfigurasi backplane Redis.
3. **4.3 UI**: halaman leaderboard live (podium, baris saya menempel, indikator Live, arsip), halaman season admin, "terakhir aktif" dari Insight di progres sensei, animasi "+1" di layar drilling.

### Kriteria penerimaan Fase 4
- [ ] Semua kasus uji BR-POIN dan BR-SEASON-03 lulus.
- [ ] Poin muncul di papan < 2 detik setelah jawaban benar (dev).
- [ ] Event yang sama diproses dua kali tidak menggandakan poin.
- [ ] Menutup season mengarsipkan peringkat dan membuat papan baru dari nol.
- [ ] Payload hub tidak memuat id pengguna lain (hanya `entryKey`, nama tampilan, poin).
- [ ] Redis bisa dikosongkan lalu dibangun ulang dari PostgreSQL.

## 8. Fase 5 — Lanjutan

**Tujuan.** Fitur lanjutan dan persiapan produksi. **Untuk setiap sub-langkah, tulis rencana dan risiko teknis dulu** (v1).

**Brief cold-start.** Fase 0–4 selesai; platform utuh. Fitur di fase ini berisiko tinggi atau opsional.

### Sub-langkah
1. **5.1 Tulis tangan**: canvas (mouse/sentuh) untuk kana dengan pencocokan stroke berbasis data KanjiVG (lisensi CC BY-SA 3.0, atribusi wajib); lalu kanji dengan opsi cek mandiri bila akurasi belum memadai.
2. **5.2 Impor PDF**: upload → ekstraksi teks (OCR bila perlu) → parser → draf soal dengan sumber + lisensi wajib → review admin → terbit (BR-SOAL-06).
3. **5.3 Google Calendar OAuth** (opsional): `google_calendar_links` dengan refresh token terenkripsi.
4. **5.4 Hardening & rilis produksi**: review keamanan, uji beban ringan, backup & restore teruji, deployment sesuai Q-08, checklist [09](09-kualitas-dan-keamanan.md) §8.

## 9. Protokol perubahan rencana

| Situasi | Tindakan |
|---|---|
| Sub-langkah terlalu besar untuk satu sesi | Pecah menjadi a/b dengan kriteria penerimaan masing-masing; catat di dokumen ini |
| Ditemukan prasyarat yang belum ada | Sisipkan langkah baru sebelum langkah yang membutuhkannya; catat alasannya |
| Fitur ditunda atau dibatalkan | Hanya dengan persetujuan pemilik proyek; catat di [11](11-keputusan.md) |
| Kriteria penerimaan tidak bisa dipenuhi | Berhenti, laporkan penyebabnya, usulkan opsi; jangan menurunkan kriteria diam-diam |

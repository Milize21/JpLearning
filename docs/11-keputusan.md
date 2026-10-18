# 11 · Keputusan & Pertanyaan Terbuka

## Cara pakai

- ✅ final · 🟡 usulan/default yang **berlaku** sampai dikonfirmasi · ❓ **wajib dijawab** sebelum fase terkait.
- Pemilik proyek mengonfirmasi dengan mengubah status (🟡 → ✅ atau ❓ → ✅) dan mengisi catatan. **AI tidak boleh mengubah status sendiri**; AI hanya boleh mengusulkan.
- Setiap RENCANA AI yang menyentuh item 🟡/❓ wajib menyebut ID-nya. Item ❓ yang dibutuhkan fase berjalan harus dijawab dulu.
- Keputusan baru ditambahkan di tabel yang sesuai dan di riwayat (§4).

## 1. Pertanyaan terbuka ❓

| ID | Pertanyaan | Default sementara | Dibutuhkan sebelum |
|---|---|---|---|
| Q-01 | Nama resmi produk? | "NomaX JP Learn" | Rilis beta |
| Q-02 ✅ | Konfirmasi topologi D-01 (modular monolith yang siap dipecah) atau microservices penuh sejak awal? | **Dijawab: modular monolith** (pemilik proyek, 2026-10-18, saat menyetujui RENCANA Fase 0.1) | Fase 0 |
| Q-03 | Buku Dasar kana: satu buku (v1) atau dua buku (Hiragana dan Katakana)? | satu buku, bab per baris | Fase 1 |
| Q-04 | Perlu materi khusus satu kelas? (v1: materi melekat ke bab dan terlihat oleh semua kelas yang membuka bab itu) | tidak | Fase 1 |
| Q-05 | Apakah jawaban kuis/ujian ikut memperbarui mastery dan kelemahan? | tidak, hanya drilling (sesuai v1) | Fase 3 |
| Q-06 | Murid boleh melihat leaderboard level di luar kelasnya? | tidak, hanya level kelasnya | Fase 4 |
| Q-07 | Kapan rilis beta ke komunitas? | setelah Fase 2 | Akhir Fase 2 |
| Q-08 | Hosting produksi dan domain? | 1 VPS + Docker Compose + Caddy | Rilis beta |
| Q-09 | Alamat email pengirim dan layanan SMTP produksi? | – | Rilis beta |
| Q-10 | Perlu mode tampilan "kanji tanpa furigana" khusus untuk level lanjut? | tidak (pakai mode `off`) | Fase 2 |
| Q-11 | Perlu notifikasi in-app selain email dan web push? | tidak | Fase 1 |
| Q-12 | Kebijakan retensi data ([03](03-skema-data.md) §14) disetujui? | usulan di 03 §14 | Rilis beta |
| Q-13 | Perlu kuota penyimpanan per pengguna atau total? | tanpa kuota; ada rate limit upload | Fase 1 |
| Q-14 | Ada murid di bawah 18 tahun? (butuh persetujuan orang tua/wali menurut UU PDP) | belum diketahui | Rilis beta |

## 2. Register keputusan

| ID | Keputusan | Status | Alasan / sumber | Dokumen |
|---|---|---|---|---|
| D-01 | Topologi modular monolith yang siap dipecah (5 modul = 5 service logis) | ✅ | Catatan v1 §4; jauh lebih sederhana untuk vibe coding. Dikonfirmasi lewat Q-02 | [02](02-arsitektur.md) §2 |
| D-02 | Frontend: Vite, React Router, TanStack Query, Tailwind v4, shadcn/ui, react-hook-form + zod, oidc-client-ts, WanaKana | 🟡 | A8 (React + TS) + pustaka yang sangat dikenal AI | [02](02-arsitektur.md) §12 |
| D-03 | Event antar-modul: outbox + dispatcher in-process; RabbitMQ saat modul dipecah | 🟡 | v1 "RabbitMQ atau outbox + worker" | [02](02-arsitektur.md) §6.3 |
| D-04 | Background job: BackgroundService + tabel antrean, tanpa Hangfire/Quartz dulu | 🟡 | v1 "Hangfire atau Quartz"; dependency lebih sedikit | [02](02-arsitektur.md) §11 |
| D-05 | File: disk lokal di balik `IFileStorage`; upload tus (`tusdotnet`) | 🟡 | v1 "S3-compatible atau disk" | [02](02-arsitektur.md) §10 |
| D-06 | API: Minimal API + vertical slice; Problem Details + katalog `code`; tanpa MediatR/AutoMapper | 🟡 | Lisensi komersial pustaka tersebut; kesederhanaan | [02](02-arsitektur.md) §5, [05](05-api-dan-event.md) §2 |
| D-07 | Id: UUIDv7 dari aplikasi, kecuali `user_profiles.id` = `sub` Keycloak | 🟡 | v1 "semua id UUID"; v7 lebih ramah index | [03](03-skema-data.md) §1 |
| D-08 | Enum disimpan `text` + CHECK dengan nilai snake_case | 🟡 | Migrasi lebih mudah daripada enum PostgreSQL | [03](03-skema-data.md) §1 |
| D-09 | `order` → `sort_order`; `item_ref` → `item_type` + `item_id` | 🟡 | `order` kata kunci SQL; konsistensi dengan item_masteries | [03](03-skema-data.md) §2 |
| D-10 | Bahasa: kode, komentar kode, commit dalam bahasa Inggris; dokumen dan komunikasi dalam Bahasa Indonesia | 🟡 | Konvensi umum | `AGENTS.md` §16 |
| D-11 | Warna: palet shiro/aka/sumi dari logo; aksi = `aka-700`, merek = `aka-900` (merah logo) | 🟡 | Permintaan putih–merah–hitam; kontras AA | [07](07-desain-ui-ux.md) §4 |
| D-12 | Font: Plus Jakarta Sans + Noto Sans JP + Klee One | 🟡 | Keterbacaan dan pedagogi bentuk huruf | [07](07-desain-ui-ux.md) §5 |
| D-13 | Romaji gaya wāpuro (`renshuu`) | 🟡 | Sesuai contoh v1 | [06](06-teks-multibahasa.md) §5 |
| D-14 | Sapaan UI "kamu" | 🟡 | Nuansa komunitas | [07](07-desain-ui-ux.md) §13 |
| D-15 | Mastery EWMA α 0,3; lemah < 0,6 (≥ 3 percobaan); dikuasai ≥ 0,85 + streak 3 | 🟡 | v1 belum menentukan rumus | [04](04-aturan-bisnis.md) §6 |
| D-16 | Nilai: bobot default kuis 30 / ujian 40 / tugas 30; percobaan terbaik; tidak dikerjakan setelah tutup = 0; bobot dinormalisasi bila komponen kosong | 🟡 | v1 belum menentukan | [04](04-aturan-bisnis.md) §9 |
| D-17 | Poin: hari = Asia/Jakarta; faktor kuis 1,0 dan ujian 2,0; percobaan ulang hanya menghitung kenaikan | 🟡 | v1 "poin sebanding skor" | [04](04-aturan-bisnis.md) §10 |
| D-18 | Menutup season: admin saja | 🟡 | A5 menyebut admin atau sensei, tetapi season berlaku lintas kelas dan prompt Fase 4 v1 menyebut halaman admin | [04](04-aturan-bisnis.md) §11 |
| D-19 | Soal buatan sensei `private` secara default; bank bawaan (`builtin`) dikelola admin | 🟡 | v1 "buat soal sendiri (milik sendiri)" | [04](04-aturan-bisnis.md) §5 |
| D-20 | Tugas terlambat diterima dan ditandai; kirim ulang hanya sebelum dinilai | 🟡 | v1 belum menentukan | [04](04-aturan-bisnis.md) §8 |
| D-21 | Nama tampilan hanya diubah admin | 🟡 | Mencegah nama tidak pantas di leaderboard | [04](04-aturan-bisnis.md) §1 |
| D-22 | Mode uji coba staf tidak menyimpan data | 🟡 | v1 "uji coba" | [01](01-peran-dan-akses.md) §7 |
| D-23 | Satu sub-langkah per sesi; satu fase boleh beberapa sesi, tanpa mencampur fase | 🟡 | v1 "satu fase per sesi"; fase 0–3 terlalu besar untuk satu sesi AI | [08](08-roadmap-fase.md) §1 |
| D-24 | Palet warna bawaan Tailwind dimatikan | 🟡 | Memaksa pemakaian token | [07](07-desain-ui-ux.md) §15 |
| D-25 | Dev: infrastruktur di Docker, aplikasi .NET dan web di host | 🟡 | Menghindari issuer mismatch Keycloak; hot reload cepat | [02](02-arsitektur.md) §13 |
| D-26 | Materi melekat ke bab dan terlihat oleh semua kelas yang membuka bab itu | ✅ | Skema v1 (`Material.chapter_id`) | [03](03-skema-data.md) §5.4, Q-04 |
| D-27 | Kalender: feed ICS dulu, Google OAuth di Fase 5; pengingat email + web push | 🟡 | A9 | [04](04-aturan-bisnis.md) §4 |
| D-28 | Penilaian diri (flashcard, cek mandiri tulis tangan) tidak memberi poin | 🟡 | Anti-farming (A10) | [04](04-aturan-bisnis.md) §10 |
| D-29 | Server Redis memakai image Valkey (`valkey/valkey`), bukan `redis` | ✅ | Redis 7.4+ berlisensi RSAL/SSPL (8.x: + AGPL), di luar daftar `AGENTS.md` §12; Valkey BSD-3 dan kompatibel protokol. Disetujui di RENCANA 0.1 | [02](02-arsitektur.md) §13 |
| D-30 | Atribut profil Keycloak (email, nama depan/belakang) opsional dan hanya diubah admin | 🟡 | Email opsional (BR-AKUN-02); salinan di `user_profiles` tidak boleh menyimpang; mencegah VERIFY_PROFILE Keycloak 24+ | [01](01-peran-dan-akses.md) §8 |
| D-31 | Gateway mereferensikan `Nomax.BuildingBlocks` (bukan hanya SharedKernel) | 🟡 | Validasi JWT, Problem Details, dan penyamaran log dipakai bersama API, tidak diduplikasi; Gateway tetap tidak mereferensikan modul | [02](02-arsitektur.md) §6.1 |

## 3. Asumsi v1 (A1–A10)

| ID | Asumsi v1 | Status | Catatan v2 |
|---|---|---|---|
| A1 | Murid yang di-assign ke sensei = anggota kelas sensei; satu murid boleh ikut lebih dari satu kelas | 🟡 | Dipakai di [01](01-peran-dan-akses.md) §2 dan BR-AKSES-01 (akses gabungan) |
| A2 | Level Dasar (kana), N5, N4, N3; kana menjadi Buku Dasar dengan bab per baris | 🟡 | Struktur buku: Q-03 |
| A3 | Latihan mandiri tidak bernilai; sensei hanya melihat dan berkomentar; kuis, ujian, tugas bernilai | 🟡 | BR-DRILL-01 |
| A4 | Nilai 0–4 per kelas dari bobot kuis, ujian, tugas (bukan IPK kumulatif) | 🟡 | BR-NILAI |
| A5 | Reset leaderboard saat season level ditutup admin atau sensei; riwayat season disimpan | 🟡 ✏️ | Penutup: admin saja (D-18); riwayat di `season_standings` |
| A6 | Batas 1 GB berlaku per file | 🟡 | 1 GiB = 1.073.741.824 byte (BR-FILE-01) |
| A7 | Admin punya semua hak sensei; sensei hanya melihat kelasnya | 🟡 | [01](01-peran-dan-akses.md) §3 |
| A8 | Frontend React + TypeScript | 🟡 | D-02 |
| A9 | Kalender: ICS dulu, OAuth Google di fase akhir; pengingat email dan web push | 🟡 | D-27 |
| A10 | Poin leaderboard memakai aturan anti-farming | 🟡 | BR-POIN, D-28 |

## 4. Riwayat

| Tanggal | ID | Perubahan | Oleh |
|---|---|---|---|
| 2026-10-18 | semua | Register dibuat dari blueprint v1 + usulan v2 | AI, menunggu konfirmasi pemilik proyek |
| 2026-10-18 | Q-02, D-01 | Q-02 dijawab "modular monolith"; D-01 menjadi ✅ | Pemilik proyek (persetujuan RENCANA Fase 0.1), dicatat AI |
| 2026-10-18 | D-29 | Valkey menggantikan image Redis (lisensi) | Pemilik proyek (persetujuan RENCANA Fase 0.1), dicatat AI |
| 2026-10-18 | D-30, D-31 | Usulan baru dari implementasi Fase 0.1 | AI, menunggu konfirmasi pemilik proyek |

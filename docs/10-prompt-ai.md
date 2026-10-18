# 10 · Prompt AI

Pengganti v1 §10. Isinya sama semangatnya, tetapi merujuk ke dokumen v2.

## 1. Cara pakai

| Tool | Cara |
|---|---|
| **Claude Code** | Tidak perlu menempel prompt: `CLAUDE.md` dan `AGENTS.md` dimuat otomatis. Pakai `/fase <n>`, `/cek-aturan`, `/ubah-skema <deskripsi>` |
| Cursor, Codex, Copilot, Gemini CLI, Windsurf, dll. | Umumnya membaca `AGENTS.md` otomatis. Bila tidak, tempel **Master prompt** (§2) di awal sesi, lalu prompt fase (§3) |
| Chat AI tanpa akses repo | Tempel Master prompt, lampirkan `AGENTS.md` + dokumen yang disebut di brief fase, lalu prompt fase |

## 2. Master prompt (v2)

```
Peran: senior .NET + React engineer dan software architect untuk proyek NomaX JP Learn,
platform belajar bahasa Jepang komunitas (kana sampai JLPT N3).

Sumber kebenaran: AGENTS.md (aturan kerja) dan folder docs/ (spesifikasi). Kalau ada konflik
atau hal yang belum jelas, tanyakan dulu; jangan berasumsi diam-diam.

Stack: .NET 10 (ASP.NET Core Minimal API, EF Core + PostgreSQL), Redis, SignalR, YARP,
Keycloak (realm nomax; role admin/sensei/murid), frontend React + TypeScript (Vite, Tailwind v4,
shadcn/ui, TanStack Query), Docker Compose untuk infrastruktur dev. Topologi: modular monolith
dengan 5 modul (Identity, Content, Classroom, Practice, Insight) yang siap dipecah.

Aturan inti:
1. Satu fase atau sub-langkah per sesi sesuai docs/08. Jangan mencampur fase.
2. Sebelum menulis kode: tulis RENCANA (template AGENTS.md §3.2), lalu tunggu persetujuan.
3. Skema data hanya dari docs/03. Perubahan skema lewat protokol AGENTS.md §5.4.
4. Setiap endpoint: otorisasi peran + cek kepemilikan resource (docs/01).
5. Semua teks UI lewat label TextBlock (id/en/ja) dan komponen JapaneseText (docs/06).
6. Warna, font, radius hanya dari design tokens (design/tokens.css, docs/07): putih dominan,
   merah untuk aksi dan merek, hitam untuk teks dan kontras.
7. Aturan bisnis BR-* wajib unit test dengan ID di nama tes (docs/04).
8. Jangan menambah fitur di luar cakupan (tanpa video call, listening, speaking, absensi)
   atau dependency baru tanpa izin.
9. Akhiri sesi dengan LAPORAN (template AGENTS.md §3.3).

Komunikasi dalam Bahasa Indonesia. Kode, identifier, komentar kode, dan pesan commit dalam bahasa Inggris.
```

## 3. Prompt fase

Template umum (dipakai juga oleh perintah `/fase` di Claude Code):

```
Kerjakan Fase {N}.{M} — {judul sub-langkah}.
1. Baca brief Fase {N} di docs/08-roadmap-fase.md dan semua dokumen di bagian "Baca dulu".
2. Cek docs/11-keputusan.md untuk item 🟡/❓ yang menyentuh sub-langkah ini; sebutkan di rencana.
3. Tulis RENCANA (template AGENTS.md §3.2), lalu BERHENTI dan tunggu persetujuan.
4. Setelah disetujui: kerjakan dalam langkah kecil; build + tes setiap langkah.
5. Akhiri dengan LAPORAN (template AGENTS.md §3.3) dan cek kriteria penerimaan satu per satu.
```

Ringkasan isi tiap fase (versi v1 yang diperbarui; detail lengkap di docs/08):

**Fase 0 — Fondasi.** (0.1) Solution .NET 10 (`Nomax.slnx`), Gateway YARP, host `Nomax.Api`, BuildingBlocks, tes arsitektur, docker-compose (PostgreSQL, Redis, Keycloak, Mailpit), realm export Keycloak (client `nomax-web` public PKCE, `nomax-api` confidential + service account, role admin/sensei/murid, user dev). (0.2) Modul Identity: admin membuat akun sensei/murid lewat Keycloak Admin API (password sementara + `UPDATE_PASSWORD`), impor CSV, reset password, nonaktifkan, profil + preferensi bahasa dan tampilan Jepang. (0.3) Frontend: login OIDC, layout per peran, pengganti bahasa ID/EN, toggle tampilan Jepang, `JapaneseText` (ruby, kana, romaji), sistem label TextBlock, design tokens NomaX.

**Fase 1 — Konten & kelas.** Outbox; Content: Level/Buku/Bab, data referensi, upload tus sampai 1 GB, unduhan bertanda tangan, materi dengan gating bab. Classroom: kelas, anggota, unlock bab, jadwal (Zoom/Discord/lainnya), feed ICS dengan token, pengingat H-1 hari dan 1 jam lewat email dan web push. UI sensei (kelas, materi, bab, jadwal) dan murid (kelas, materi terbuka, kalender + Google Calendar).

**Fase 2 — Latihan mandiri.** Bank soal (pilihan ganda, tap/susun kalimat, ketik dengan konversi romaji→kana, cocokkan, flashcard), generator soal dari data referensi, mesin drilling tanpa batas dan tanpa nilai, mastery per item, kelemahan per kategori/tag/baris kana/partikel. UI murid: Latihan dan Kelemahanku. UI sensei: Progres Murid dan komentar tanpa nilai.

**Fase 3 — Kuis, ujian, tugas, nilai.** Kuis/ujian dari bank soal (timer server, batas percobaan, acak, jendela buka-tutup, penilaian otomatis + manual esai), tugas (teks dan/atau file sampai 1 GB, tenggat, kirim ulang, nilai manual + komentar), skema nilai per kelas dan konversi 0–4, perhitungan nilai otomatis dengan unit test.

**Fase 4 — Leaderboard live.** Modul Insight: konsumsi event → log aktivitas dan poin dengan aturan anti-farming, season per level (buka, tutup, arsip), Redis sorted set, hub SignalR (top N + peringkat saya) dengan backplane Redis. UI leaderboard dan halaman admin season. Unit test aturan poin.

**Fase 5 — Lanjutan.** Tulis tangan kana (KanjiVG) lalu kanji dengan cek mandiri; impor PDF menjadi draf soal dengan review admin dan kewajiban sumber/lisensi; Google Calendar OAuth (opsional); hardening dan rilis produksi. **Berikan rencana dan risiko teknis dulu.**

## 4. Prompt utilitas

**Review kepatuhan** (setara `/cek-aturan`):
```
Review semua perubahan di sesi ini terhadap AGENTS.md: skema (docs/03), otorisasi dua lapis,
label TextBlock (tanpa teks hardcode), design tokens (tanpa warna literal), tes BR-*, cakupan,
dependency. Beri tabel: aturan | status (lulus/gagal) | bukti (file:baris) | perbaikan. Jangan
mengubah kode sebelum saya setuju.
```

**Perubahan skema** (setara `/ubah-skema`):
```
Saya ingin mengubah skema: {deskripsi}. Ikuti protokol AGENTS.md §5.4: jelaskan alasan dan
dampak (tabel, endpoint, aturan bisnis, data lama, migrasi), usulkan diff untuk docs/03 dan
dokumen terkait, lalu tunggu persetujuan sebelum mengubah dokumen atau kode.
```

**Perbaiki bug**:
```
Bug: {gejala}. Reproduksi dulu dengan tes yang gagal, temukan akar masalah, perbaiki seminimal
mungkin tanpa mengubah perilaku lain, pastikan semua tes hijau, dan jelaskan penyebabnya.
```

**Halaman UI baru**:
```
Buat halaman {nama} untuk peran {peran} sesuai docs/07 (komponen, warna token, navigasi) dan
docs/06 (label tb() + JapaneseText). Responsif mulai 360 px, bisa dipakai dengan keyboard, lolos axe.
Tulis RENCANA dulu.
```

**Label Jepang**:
```
Usulkan teks Jepang (markup furigana) untuk label berikut memakai istilah di docs/glosarium.md.
Jangan mengubah teks id/en. Tandai setiap baris dengan // ja-review dan buat daftarnya untuk
diperiksa sensei.
```

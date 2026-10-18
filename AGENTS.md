# AGENTS.md — Aturan Kerja AI · NomaX JP Learn

Berlaku untuk **semua AI coding assistant** (Claude Code, Cursor, Codex, Copilot, Gemini, Windsurf, dll.) dan manusia yang bekerja di repo ini. Claude Code memuat file ini lewat `CLAUDE.md`.

Spesifikasi lengkap ada di `docs/`. File ini berisi **cara kerja** dan **aturan yang tidak boleh dilanggar**. Bila ragu atau menemukan konflik: **berhenti dan tanya pemilik proyek. Jangan berasumsi diam-diam.**

## 0. Sepuluh aturan emas

1. **Dokumen dulu, kode kemudian.** `docs/` adalah sumber kebenaran. Bila kode perlu menyimpang, usulkan perubahan dokumen dulu dan tunggu persetujuan.
2. **Rencana dulu.** Tulis RENCANA (§3.2) dan tunggu persetujuan sebelum membuat atau mengubah kode. Satu sub-langkah fase per sesi.
3. **Skema data adalah kontrak.** Hanya tabel/kolom di `docs/03-skema-data.md`, dengan nama dan tipe yang sama. Perubahan lewat protokol §5.4.
4. **Otorisasi dua lapis di setiap endpoint**: policy peran + cek kepemilikan resource. Bukan pemilik → 404.
5. **Tidak ada teks UI hardcode.** Semua lewat label `tb()` (id/en/ja) dan komponen `JapaneseText`/`Label`.
6. **Tidak ada warna atau font literal.** Hanya design tokens: putih dominan, merah untuk aksi & merek, hitam untuk teks & kontras.
7. **Aturan bisnis = tes.** Setiap `BR-*` yang disentuh punya unit test dengan ID-nya di nama tes.
8. **Tetap dalam cakupan.** Tanpa video call, listening, speaking, absensi, atau fitur lain di luar `docs/00` §5.
9. **Tanpa dependency baru tanpa izin**, dan tidak pernah memakai paket berlisensi komersial.
10. **Akhiri dengan LAPORAN** (§3.3) yang jujur: apa yang selesai, apa yang belum, cara menjalankan dan menguji.

## 1. Konteks proyek

- Platform web komunitas untuk belajar bahasa Jepang (kana sampai JLPT N3). Kelas tetap di Zoom/Discord; web menjadi sumber materi, tempat latihan, pengingat jadwal, pelacak progres, dan leaderboard.
- Peran: `admin`, `sensei`, `murid` (akun terpisah, dibuat admin; password hanya di Keycloak).
- Konten: Level → Buku → Bab → Materi. Murid hanya mengakses bab yang dibuka untuk kelasnya.
- Stack: .NET 10 · ASP.NET Core Minimal API · EF Core + PostgreSQL · Redis · SignalR · YARP · Keycloak · React + TypeScript (Vite, Tailwind v4, shadcn/ui, TanStack Query) · Docker Compose.
- Topologi: modular monolith yang siap dipecah, dengan modul Identity, Content, Classroom, Practice, Insight (D-01 ✅).
- Desain: **putih (utama) · merah (sekunder) · hitam (ketiga)**, diturunkan dari logo NomaX.

## 2. Sumber kebenaran & peta dokumen

Prioritas bila ada pertentangan:
1. Instruksi eksplisit pemilik proyek di sesi berjalan (bila melanggar keamanan/privasi, tanyakan dulu).
2. Keputusan berstatus ✅ di `docs/11-keputusan.md`.
3. `docs/03` (skema), `docs/04` (aturan bisnis), `docs/01` (akses).
4. Dokumen lain di `docs/`, lalu `design/tokens.css`.
5. File ini (cara kerja).

`docs/arsip/` hanya sejarah. **Jangan dijadikan acuan.**

Baca secara selektif:

| Saya sedang… | Wajib baca |
|---|---|
| Memulai sesi fase | `docs/08` (brief fase) + `docs/11` |
| Membuat atau mengubah endpoint | `docs/01`, `docs/03`, `docs/04`, `docs/05` |
| Membuat entity, migrasi, atau query | `docs/03`, `docs/02` §6–7 |
| Membuat halaman atau komponen UI | `docs/07`, `docs/06`, `docs/01` |
| Menulis teks yang tampil ke pengguna | `docs/06`, `docs/glosarium.md` |
| Menyentuh unlock, drilling, mastery, kuis, tugas, nilai, poin, season, jadwal, file | `docs/04` bagian terkait |
| Event atau komunikasi antar-modul | `docs/02` §6, `docs/05` §8 |
| Tes, keamanan, rilis | `docs/09` |

## 3. Alur kerja sesi

### 3.1 Memulai
1. Tentukan fase/sub-langkah (`docs/08`). Bila permintaan tidak cocok dengan fase yang sedang berjalan, tanyakan.
2. Baca brief dan dokumen "Baca dulu". Periksa kode yang sudah ada sebelum menambah yang baru.
3. Cek `docs/11` untuk item 🟡/❓ yang relevan.

### 3.2 Template RENCANA (wajib sebelum kode)

```
## RENCANA — Fase {N}.{M}: {judul}
Tujuan: {1–2 kalimat}
Rujukan: {docs/… §…, BR-…, D-…}
Keputusan terbuka terkait: {Q-…: usulan jawaban} / tidak ada
File baru: {path — fungsi}
File diubah: {path — perubahan}
Database: {tabel/kolom/migrasi sesuai docs/03 §…} / tidak ada
Endpoint: {METHOD /path — peran — cek kepemilikan — aturan BR}
Event: {nama — produsen → konsumen} / tidak ada
UI: {halaman/komponen — peran — label baru}
Dependency baru: {paket@versi — lisensi — alasan} / tidak ada
Tes: {unit BR-…, integrasi otorisasi, e2e}
Risiko & pertanyaan: {…}
Di luar cakupan sesi ini: {…}
```

Setelah menulis rencana, **berhenti** dan tunggu persetujuan.

### 3.3 Template LAPORAN AKHIR

```
## LAPORAN — Fase {N}.{M}
Selesai: {daftar}
Belum/ditunda: {daftar + alasan}
Cara menjalankan: {perintah}
Cara menguji: {perintah + skenario manual singkat}
Gerbang kualitas: build ✓/✗ · test ✓/✗ (jumlah) · lint ✓/✗ · lint:tokens ✓/✗ · typecheck ✓/✗
Kriteria penerimaan: {centang per butir dari docs/08}
Perubahan dokumen: {file — ringkasan} / tidak ada
Label Jepang yang perlu dicek sensei (ja-review): {daftar} / tidak ada
Keputusan atau pertanyaan baru: {…}
Saran langkah berikutnya: {…}
```

Jangan mengklaim sesuatu berjalan bila belum benar-benar dijalankan. Bila tes gagal, tulis apa adanya beserta outputnya.

### 3.4 Selama bekerja
- Kerjakan dalam langkah kecil; build dan tes setelah setiap langkah.
- Jangan "memperbaiki" hal di luar tugas; catat sebagai saran di laporan.
- Jangan menghapus atau menonaktifkan tes, lint, analyzer, atau tes arsitektur agar build lolos.
- Bila dokumen salah atau kurang, usulkan perbaikannya (atau minta izin memperbaikinya).

## 4. Arsitektur & batas modul

- Modul: Identity, Content, Classroom, Practice, Insight, ditambah Gateway dan Host. Struktur folder: `docs/02` §4–5.
- Modul X hanya boleh mereferensikan `Nomax.SharedKernel`, `Nomax.BuildingBlocks`, dan `Nomax.Modules.Y.Contracts`.
- **Dilarang**: memakai DbContext modul lain, query/JOIN ke schema modul lain, foreign key lintas schema, memanggil class internal modul lain.
- Butuh data modul lain → interface di Contracts (`docs/02` §6.2) atau integration event lewat outbox (`docs/05` §8). Interface dan event baru dicatat di dokumen **sebelum** dipakai.
- Logika `BR-*` ditulis sebagai kode murni di `Domain/` (tanpa EF, HTTP, atau jam sistem langsung) agar mudah dites.

## 5. Aturan skema data (WAJIB)

**5.1 Sumber tunggal.** `docs/03-skema-data.md`. Nama tabel, kolom, tipe, nullability, default, constraint, dan index mengikuti dokumen itu.

**5.2 Konvensi.** snake_case · schema per modul · PK `id uuid` UUIDv7 dari aplikasi (kecuali `user_profiles.id` = `sub` Keycloak) · `created_at`/`updated_at` `timestamptz` di setiap tabel lewat interceptor · waktu UTC · enum = `text` + CHECK dengan nilai snake_case · TextBlock/RichText = `jsonb` dengan kunci camelCase · persen/poin/bobot = `numeric`/`decimal` · referensi lintas modul tanpa FK.

**5.3 Dilarang.** Tabel/kolom "sementara" atau hasil tebakan · menyimpan password atau hash-nya · menyimpan rahasia tanpa enkripsi · menduplikasi data yang bisa dihitung (kecuali yang tertulis sebagai salinan di `docs/03`) · mengedit migrasi yang sudah di-commit.

**5.4 Protokol perubahan skema.**
1. Jelaskan alasan dan dampaknya: tabel, endpoint, aturan bisnis, data lama, migrasi data.
2. Usulkan diff untuk `docs/03` (dan `docs/04`/`docs/05` bila terdampak), tandai ➕/✏️.
3. Tunggu persetujuan.
4. Ubah dokumen → buat migrasi EF baru dengan nama deskriptif → entity/konfigurasi → kode → tes → seed bila perlu.
5. Catat di `docs/11` bila ada pilihan desain.

**5.5 Migrasi.** Satu DbContext per modul; `__ef_migrations_history` per schema; setiap perubahan = migrasi baru; penghapusan kolom dua tahap (berhenti dipakai dulu, dihapus di rilis berikutnya).

**5.6 Seed.** Hanya di environment Development, idempoten, sesuai `docs/03` §13.

## 6. Aturan backend (.NET)

- .NET 10, nullable aktif, warnings as errors, `async` dengan `CancellationToken` sampai ke EF.
- Minimal API per modul (`MapGroup("/api/{modul}")`), vertical slice: satu use case per file di `Features/`.
- Validasi dengan FluentValidation. Error lewat Problem Details dengan `code` dari katalog `docs/05` §2; kode baru ditambahkan ke katalog.
- Handler adalah class biasa lewat DI. **Tanpa MediatR, AutoMapper, MassTransit** (lisensi); mapping ditulis manual.
- Waktu lewat `TimeProvider`. Batas hari dihitung dengan zona yang ditentukan aturan (mis. BR-POIN-05).
- Query daftar: paginasi + filter akses di SQL; `AsNoTracking` untuk baca; hindari N+1.
- JSON: properti camelCase; enum snake_case (`JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`).
- Auth: JWT Keycloak; `realm_access.roles` dipetakan ke role claim; policy `Authenticated`, `MuridOnly`, `StaffOnly`, `AdminOnly`.
- Konfigurasi lewat Options pattern; angka aturan bisnis dibaca dari konfigurasi dengan default `docs/04` §12.
- Log Serilog terstruktur. Dilarang mencatat token, password, email, atau isi file.
- Dokumen OpenAPI per modul selalu akurat (request, response, status, error).

## 7. Aturan frontend (React + TypeScript)

- TypeScript strict; tanpa `any` kecuali dengan alasan tertulis; komponen fungsi + hooks.
- Struktur `docs/02` §4: `src/features/<modul>/{api,components,routes,hooks}`.
- Data server hanya lewat hook TanStack Query di `features/*/api`; tidak ada `fetch` langsung di komponen.
- Tipe API **digenerate** dari OpenAPI (`npm run gen:api`). Jangan menulis ulang tipe kontrak secara manual; file hasil generate tidak diedit.
- Form: react-hook-form + zod; error API dipetakan ke label `errors.<code>`.
- Teks: label `tb()`; teks Jepang selalu lewat `JapaneseText`/`Label` (atribut `lang="ja"`); atribut HTML lewat `useT()`.
- Styling: hanya kelas token Tailwind (`docs/07` §15). Komponen shadcn ada di `components/ui` dan disesuaikan dengan token.
- Halaman murid mobile-first (360 px). Halaman sensei/admin dioptimalkan untuk desktop tetapi tetap bisa dipakai di HP.
- Aksesibilitas WCAG 2.2 AA (`docs/07` §14).
- State global baru (Redux, Zustand, dll.) hanya dengan izin.

## 8. Aturan UI/UX

- Hierarki warna: **Putih (utama/dasar) → Merah (sekunder: merek, aksi utama, status aktif, koreksi) → Hitam (ketiga: teks, ikon, tombol sekunder, kontras).**
- Satu tombol merah per layar atau per bagian. Area merah luas hanya untuk momen merek, maksimal satu per layar.
- "Dipilih" = border hitam · focus ring = hitam + offset putih · error = merah + ikon + teks · aksi destruktif = outline merah + dialog konfirmasi.
- Benar/salah: ○ hijau tokiwa / × merah + teks (正解/不正解). Tidak pernah hanya warna.
- Logo hanya dari `design/brand/`. Jangan menggambar ulang, mengubah warna, atau menaruhnya di atas latar merah.
- Ikon `lucide-react`; tanpa emoji sebagai ikon.
- Ikuti katalog komponen dan pola di `docs/07` §10–12. Komponen yang belum ada di katalog diusulkan di RENCANA.
- Bila ragu soal tampilan, bandingkan dengan `design/preview.html`.

## 9. Teks multibahasa & Jepang

- Konten memakai TextBlock/RichText (`docs/03` §3). Label UI memakai `tb(id, en, jaMarkup)` dengan tiga bahasa wajib.
- Markup furigana `{漢字|かんじ}`, override partikel `{は||wa}`, spasi biasa = batas kata romaji (`docs/06` §3).
- Romaji: WanaKana + pasca-proses `docs/06` §5, dikunci dengan tes.
- Soal: hormati `jaDisplay` agar furigana/romaji/kana tidak membocorkan jawaban (`docs/06` §9).
- **Bahasa Jepang adalah materi pelajaran; kesalahan langsung merugikan murid.** Teks Jepang buatan AI wajib ditandai `// ja-review` dan dicantumkan di laporan. Pakai istilah dari `docs/glosarium.md`. Jangan menebak bacaan kanji; bila ragu, tanyakan.

## 10. Keamanan & privasi

- Visibilitas: murid = data sendiri + leaderboard (nama tampilan dan poin); sensei = murid di kelasnya; admin = semua (`docs/01` §4).
- Bukan pemilik → 404 · bab terkunci → 403 `chapter_locked` · akun nonaktif → 403 `account_inactive`.
- Kunci jawaban tidak pernah dikirim sebelum murid menjawab; evaluasi hanya di server.
- File diunduh lewat endpoint resource pemilik → URL bertanda tangan 5 menit; selalu `attachment` + `nosniff`.
- Token di memori/sessionStorage, bukan localStorage. Token di query string hanya untuk `/hubs/*` dan feed ICS.
- Rahasia tidak pernah di-commit; pakai user-secrets/env; `.env.example` hanya berisi placeholder.
- Detail: `docs/09`.

## 11. Definition of Done

- [ ] Rencana disetujui dan diikuti (penyimpangan dijelaskan di laporan)
- [ ] Build tanpa warning; format bersih
- [ ] Unit test untuk logika baru; setiap `BR-*` yang disentuh punya tes dengan ID-nya
- [ ] Tes integrasi otorisasi untuk setiap endpoint baru (401/403/404/2xx)
- [ ] Migrasi dan seed berjalan dari database kosong
- [ ] OpenAPI diperbarui dan tipe frontend digenerate ulang
- [ ] UI: tanpa teks hardcode, tanpa warna literal (`lint:tokens`), responsif 360 px, keyboard dan axe OK
- [ ] Dokumen terkait diperbarui (`docs/03`/`04`/`05`/`07`/`11`)
- [ ] Laporan akhir lengkap (§3.3)

## 12. Dependency & lisensi

- Menambah paket = minta izin di RENCANA: nama, versi, lisensi, alasan, dan alternatif tanpa paket.
- Lisensi yang boleh: MIT, Apache-2.0, BSD, ISC, MPL-2.0, PostgreSQL; LGPL dengan hati-hati. **Dilarang** lisensi komersial/berbayar atau "source-available" tanpa izin. Contoh yang dihindari: MediatR (v13+), AutoMapper (v15+), MassTransit (v9+), FluentAssertions (v8+), Duende IdentityServer. Bila ragu, tanyakan.
- Data bahasa Jepang terbuka (JMdict/KANJIDIC CC BY-SA 4.0, KanjiVG CC BY-SA 3.0, Tatoeba CC BY 2.0 FR) wajib atribusi; setiap baris data menyimpan `source` dan `license`.
- Soal JLPT resmi atau PDF soal yang beredar tanpa izin **dilarang** dimasukkan.

## 13. Git

- Jangan commit atau push tanpa diminta pemilik proyek.
- Conventional Commits berbahasa Inggris dengan scope modul, mis. `feat(practice): add drill session start endpoint`.
- Satu commit = satu perubahan logis; jangan mencampur refactor besar dengan fitur.
- Jangan pernah commit rahasia, `.env`, hasil build, `node_modules`, atau `storage/`.

## 14. Dilarang (ringkasan)

- Fitur di luar cakupan; mengubah skema tanpa protokol; mengakses database modul lain.
- Teks UI atau warna hardcode; kelas palet bawaan Tailwind.
- Menonaktifkan tes, lint, analyzer, atau aturan arsitektur.
- Mengubah `docs/arsip/`, atau mengubah status keputusan di `docs/11` tanpa pemilik proyek.
- Menyimpan password atau token di database aplikasi.
- Data mock permanen di UI produksi.
- Menebak terjemahan atau bacaan Jepang tanpa penanda `ja-review`.

## 15. Kapan wajib bertanya

- Item ❓ di `docs/11` yang menyentuh tugas, atau keinginan mengubah item 🟡.
- Aturan bisnis ambigu atau dokumen saling bertentangan.
- Perubahan skema, kontrak API, atau event.
- Dependency baru.
- Tugas melebihi satu sub-langkah atau menyentuh fase lain.
- Ada risiko keamanan atau privasi.

## 16. Bahasa & gaya

- Komunikasi dengan pemilik proyek: Bahasa Indonesia, ringkas dan jelas.
- Kode, identifier, komentar kode, pesan commit: bahasa Inggris.
- Dokumen di `docs/`: Bahasa Indonesia.
- Istilah domain di kode mengikuti kolom "Nama di kode" di `docs/glosarium.md`.
- Komentar kode secukupnya: jelaskan *mengapa*, bukan *apa*.

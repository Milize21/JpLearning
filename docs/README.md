# Dokumentasi NomaX JP Learn — Blueprint v2

Folder ini adalah **sumber kebenaran** proyek. Semua kode, baik ditulis manusia maupun AI, mengikuti dokumen di sini. Aturan kerja untuk AI ada di [`../AGENTS.md`](../AGENTS.md).

> Prinsip: **dokumen dulu, kode kemudian.** Kalau implementasi perlu menyimpang dari dokumen, ubah dokumennya dulu (dengan persetujuan pemilik proyek), baru kodenya.

## Cara membaca

- **Baru bergabung?** Baca berurutan 00 → 02, lalu 07.
- **Mau mengerjakan fase?** Buka brief fasenya di [08](08-roadmap-fase.md), lalu dokumen yang disebut di brief itu.
- **Ragu atau menemukan konflik?** Cek [11](11-keputusan.md). Kalau belum ada jawabannya, tanyakan ke pemilik proyek. Jangan menebak.

## Peta dokumen

| No | Dokumen | Isi | Wajib dibaca saat |
|---|---|---|---|
| 00 | [Visi & cakupan](00-visi-dan-cakupan.md) | Tujuan produk, persona, yang termasuk dan tidak termasuk cakupan | Memulai proyek, menilai permintaan fitur baru |
| 01 | [Peran & hak akses](01-peran-dan-akses.md) | Admin/Sensei/Murid, matriks hak, aturan kepemilikan, Keycloak | Membuat endpoint, halaman, atau query apa pun |
| 02 | [Arsitektur](02-arsitektur.md) | Modul, topologi, struktur repo, komunikasi antar-modul, infrastruktur | Membuat proyek/modul baru, menyentuh lebih dari satu modul |
| 03 | [Skema data](03-skema-data.md) | **Kontrak data**: semua tabel, kolom, tipe, constraint, index, payload JSON | Entity, migrasi, query, DTO |
| 04 | [Aturan bisnis](04-aturan-bisnis.md) | Aturan `BR-*` lengkap dengan rumus dan kasus uji | Logika unlock, drilling, mastery, kuis, nilai, poin, season, jadwal, file |
| 05 | [API & event](05-api-dan-event.md) | Konvensi REST, format error, katalog endpoint, event, SignalR, ICS, tus | Endpoint baru, integrasi frontend–backend |
| 06 | [Teks multibahasa & Jepang](06-teks-multibahasa.md) | TextBlock, RichText, markup furigana, romaji, label UI, komponen JapaneseText | Teks apa pun yang tampil ke pengguna |
| 07 | [Desain UI/UX](07-desain-ui-ux.md) | Sistem desain putih–merah–hitam, logo, tipografi, komponen, pola UX, navigasi | Halaman atau komponen UI apa pun |
| 08 | [Roadmap & fase](08-roadmap-fase.md) | Fase 0–5, brief per fase, kriteria penerimaan | Awal setiap sesi vibe coding |
| 09 | [Kualitas & keamanan](09-kualitas-dan-keamanan.md) | Strategi tes, keamanan, privasi, performa, checklist rilis | Sebelum menyatakan tugas selesai |
| 10 | [Prompt AI](10-prompt-ai.md) | Master prompt, prompt per fase, prompt utilitas | Memakai AI tool selain Claude Code |
| 11 | [Keputusan & pertanyaan](11-keputusan.md) | Register keputusan (D-), pertanyaan terbuka (Q-), asumsi v1 (A) | Saat menyentuh hal yang statusnya 🟡/❓ |
| — | [Glosarium](glosarium.md) | Istilah ID/EN/kode/Jepang | Menamai sesuatu, menulis label |
| — | [`../design/`](../design/) | `tokens.css`, `tailwind-theme.css`, `preview.html`, logo | Implementasi UI |
| — | [Arsip](arsip/) | Blueprint v1 (sejarah, **bukan acuan**) | Tidak perlu dibaca untuk implementasi |

## Penanda dalam dokumen

| Penanda | Arti |
|---|---|
| ✅ | Final: dari v1 atau sudah dikonfirmasi pemilik proyek |
| 🟡 | Usulan/default: **berlaku** sampai pemilik proyek mengonfirmasi atau mengganti |
| ❓ | Pertanyaan terbuka: **wajib dijawab** sebelum fase terkait dikerjakan |
| ➕ | Tambahan di v2 (tidak ada di v1) |
| ✏️ | Diubah dari v1 (alasannya dicatat di tempat yang sama) |
| `BR-…` | ID aturan bisnis ([04](04-aturan-bisnis.md)). Dipakai sebagai nama tes |
| `D-…` `Q-…` `A…` | Keputusan, pertanyaan terbuka, asumsi v1 ([11](11-keputusan.md)) |

## Riwayat versi

| Versi | Tanggal | Ringkasan |
|---|---|---|
| v1 | 18 Okt 2026 | Draf awal dari diskusi bagian A–G ([arsip](arsip/blueprint-v1.md)) |
| v2 | 18 Okt 2026 | Dipecah per topik; skema, aturan bisnis, dan API dirinci; aturan AI (AGENTS.md); sistem desain putih–merah–hitam dari logo NomaX |

## Cara memperbarui dokumen

1. Perubahan spesifikasi: ubah dokumen dulu, baru kode. Perubahan skema mengikuti protokol di `AGENTS.md` §5.4.
2. Keputusan baru atau perubahan keputusan: catat di [11](11-keputusan.md).
3. Perubahan besar: tambahkan baris di tabel riwayat versi.
4. Isi `arsip/` tidak boleh diubah.

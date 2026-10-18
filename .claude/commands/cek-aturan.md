---
description: Review kepatuhan perubahan sesi ini terhadap AGENTS.md dan docs/ sebelum menyatakan tugas selesai
---

Lakukan review kepatuhan untuk semua perubahan di sesi ini (pakai `git status` dan `git diff` bila repo sudah memakai git; bila belum, daftar file yang kamu buat atau ubah).

Periksa satu per satu, dengan bukti `file:baris`:

1. **Skema**: entity, konfigurasi EF, dan migrasi sama persis dengan `docs/03` (nama, tipe, null, default, constraint, index). Tidak ada tabel/kolom di luar dokumen.
2. **Batas modul**: tidak ada referensi ke proyek internal modul lain, DbContext modul lain, query lintas schema, atau FK lintas schema.
3. **Otorisasi**: setiap endpoint baru punya policy peran + cek kepemilikan, dan ada tes 401/403/404/2xx.
4. **Aturan bisnis**: setiap `BR-*` yang disentuh punya unit test dengan ID-nya; kasus uji di `docs/04` tercakup.
5. **UI**: tanpa teks hardcode (semua lewat `tb()`/`Label`/`useT()`); tanpa warna literal atau kelas palet bawaan Tailwind (`lint:tokens`); teks Jepang lewat `JapaneseText` dengan `lang="ja"`; label Jepang baru bertanda `// ja-review`.
6. **Aksesibilitas dasar**: bisa dipakai dengan keyboard, fokus terlihat, label form ada, kontras sesuai `docs/07` §4.5.
7. **Cakupan**: tidak ada fitur di luar `docs/00` §5 maupun di luar sub-langkah ini.
8. **Dependency**: paket baru sudah disetujui dan lisensinya diizinkan (`AGENTS.md` §12).
9. **Keamanan**: tidak ada rahasia di repo, tidak ada token/PII di log, kunci jawaban tidak bocor ke murid.
10. **Dokumen**: `docs/` diperbarui bila spesifikasi berubah; `CLAUDE.md` diperbarui bila ada perintah baru.
11. **Gerbang kualitas** (`docs/09` §2): jalankan perintah yang sudah tersedia dan laporkan hasil aslinya.

Keluarkan tabel `No | Aturan | Status (lulus / gagal / tidak relevan) | Bukti | Perbaikan yang diusulkan`. Jangan mengubah kode sebelum pemilik proyek menyetujui perbaikannya.

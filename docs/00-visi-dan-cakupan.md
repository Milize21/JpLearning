# 00 · Visi & Cakupan

## 1. Ringkasan

**NomaX JP Learn** (nama kerja, lihat Q-01) adalah platform web komunitas untuk belajar bahasa Jepang dari **kana sampai JLPT N3**. Kelas tatap muka tetap berjalan di **Zoom atau Discord**. Web ini berperan sebagai:

1. **Sumber materi**: Level → Buku → Bab → Materi (file, tautan, bacaan).
2. **Tempat latihan**: drilling tanpa batas dan tanpa nilai; kuis, ujian, dan tugas yang dinilai dengan skala 0–4.
3. **Pengingat jadwal**: kalender kelas, feed ICS, email, dan web push.
4. **Pelacak progres**: penguasaan per item, kelemahan, nilai, dan leaderboard per level.
5. **Media belajar itu sendiri**: antarmuka dalam Bahasa Indonesia atau Inggris dengan pendamping Jepang (kanji, furigana, kana, romaji) yang bisa diatur pengguna.

## 2. Masalah yang diselesaikan

| Masalah di komunitas | Jawaban platform |
|---|---|
| Materi tersebar di grup chat dan drive | Materi tersusun per Level/Buku/Bab; murid hanya melihat bab yang sudah dibuka sensei |
| Latihan tidak terukur, murid tidak tahu kelemahannya | Drilling mencatat setiap jawaban, menghitung penguasaan per item, dan menampilkan kelemahan |
| Jadwal kelas mudah terlewat | Kalender per pengguna, feed ICS, pengingat H-1 hari dan 1 jam sebelum kelas |
| Sensei sulit memantau siapa yang tertinggal | Halaman progres per kelas: terakhir aktif, akurasi, item lemah |
| Motivasi naik-turun | Leaderboard live per level dengan aturan anti-farming dan season |
| Istilah Jepang jarang terpapar di luar kelas | Seluruh UI menampilkan pendamping Jepang sesuai preferensi |

## 3. Pengguna & persona

### Murid (生徒, せいと, seito)
- Pemula sampai N3, kebanyakan memakai **HP**, belajar di sela waktu (5–15 menit).
- Butuh: tahu apa yang harus dipelajari minggu ini, latihan cepat, tahu kelemahannya, tidak ketinggalan jadwal, merasa ada kemajuan.

### Sensei (先生, せんせい, sensei)
- Pengajar komunitas (sering relawan), memakai **laptop atau tablet**.
- Butuh: membuka bab sesuai ritme kelas, mengunggah materi, menyusun kuis cepat dari bank soal, menilai tugas, melihat murid yang tertinggal.

### Admin (管理者, かんりしゃ, kanrisha)
- Pengelola komunitas.
- Butuh: membuat akun massal, mengelola struktur konten dan bank soal bawaan, mengatur kelas, menutup season leaderboard.

## 4. Prinsip produk

1. **Web adalah media belajar.** Setiap layar memberi paparan bahasa Jepang (bisa dimatikan).
2. **Latihan tanpa tekanan, penilaian yang jelas.** Drilling tidak bernilai; kuis, ujian, dan tugas bernilai dengan aturan transparan.
3. **Sensei memegang ritme kelas.** Bab dibuka per kelas; murid tidak melompat terlalu jauh.
4. **Privasi sebagai default.** Murid hanya melihat datanya sendiri dan leaderboard (nama tampilan dan poin).
5. **Sederhana dulu.** Fitur sedikit tetapi matang, lalu bertambah per fase.

## 5. Cakupan (in scope) ✅

| Area | Isi | Fase |
|---|---|---|
| Akun & akses | Login SSO (Keycloak), 3 peran terpisah, akun dibuat admin (termasuk impor CSV), reset password, nonaktifkan akun, preferensi bahasa dan tampilan Jepang | 0 |
| Konten | Level/Buku/Bab, materi (file, tautan, rich text), upload resumable sampai 1 GB per file, data referensi (kana, kanji, kosakata, tata bahasa, kalimat) | 1 |
| Kelas & jadwal | Kelas, anggota, buka/tutup bab per kelas, jadwal pertemuan dengan link Zoom/Discord, feed ICS, pengingat email dan web push | 1 |
| Latihan mandiri | Bank soal, generator soal dari data referensi, drilling tanpa batas, mastery per item, kelemahan, progres di sisi sensei, komentar tanpa nilai | 2 |
| Penilaian | Kuis, ujian, tugas (teks dan/atau file), penilaian otomatis dan manual, skema nilai per kelas, nilai akhir 0–4 | 3 |
| Leaderboard | Poin dari drilling, kuis, dan ujian dengan anti-farming; season per level; update live | 4 |
| Lanjutan | Tulis tangan kana lalu kanji, impor PDF menjadi draf soal, integrasi Google Calendar (opsional) | 5 |

## 6. Di luar cakupan

### Tidak dibangun (keputusan v1) ✅
- Video call atau kelas live di web (kelas tetap di Zoom/Discord).
- Listening (audio) dan speaking.
- Absensi atau kehadiran.

### Belum direncanakan (butuh keputusan baru sebelum dikerjakan)
- Forum atau chat komunitas.
- Registrasi mandiri (akun selalu dibuat admin).
- Pembayaran atau langganan.
- Aplikasi native (cukup web responsif + PWA).
- Mode gelap.
- Level N2 dan N1.
- Notifikasi in-app (Q-11).
- Streak harian dan lencana (badge).

> Permintaan fitur dari daftar ini wajib dibahas dan dicatat di [11](11-keputusan.md) sebelum dikerjakan. AI dilarang menambahkannya atas inisiatif sendiri.

## 7. Level & struktur konten

```
Level (Dasar, N5, N4, N3)
└── Buku (mis. Buku Dasar Kana, buku teks N5)
    └── Bab (nomor + judul, dibuka per kelas)
        ├── Materi (file / tautan / rich text)
        ├── Data referensi (kana, kanji, kosakata, tata bahasa, kalimat)
        └── Soal (bank soal; dipakai drilling, kuis, ujian)
```

- Level awal: **Dasar (kana), N5, N4, N3** (asumsi A2).
- Kana menjadi Buku Dasar dengan bab per baris (a, ka, sa, dst.). Pembagian final hiragana/katakana: Q-03.

## 8. Alur utama end-to-end

1. Admin membuat akun sensei dan murid (satu per satu atau impor CSV).
2. Sensei (atau admin) membuat kelas: pilih level dan buku, tambahkan murid.
3. Sensei membuka Bab 1, mengunggah materi, membuat jadwal pertemuan dengan link Zoom/Discord.
4. Murid login → melihat Bab 1 terbuka → membaca materi → drilling → melihat kelemahan.
5. Murid menambahkan kalender kelas ke Google Calendar (ICS) dan menerima pengingat.
6. Sensei menyusun kuis dari bank soal → murid mengerjakan → nilai otomatis → nilai kelas 0–4.
7. Poin drilling dan kuis masuk leaderboard live per level → admin menutup season → papan baru mulai dari nol.

## 9. Indikator keberhasilan 🟡

| Indikator | Target awal |
|---|---|
| Murid aktif drilling minimal 3 hari per minggu | ≥ 60% murid aktif |
| Rata-rata mastery item pada bab yang sudah terbuka ≥ 2 minggu | ≥ 0,7 |
| Kuis dikerjakan sebelum ditutup | ≥ 80% |
| Waktu sensei menyusun kuis 10 soal dari bank soal | < 5 menit |
| Murid yang mengaktifkan pengingat (ICS atau push) | ≥ 70% |

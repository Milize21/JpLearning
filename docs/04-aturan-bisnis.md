# 04 · Aturan Bisnis

Setiap aturan punya ID `BR-<AREA>-<NN>`. Aturan yang diimplementasikan **wajib** punya unit test yang menyebut ID-nya di nama tes, misalnya `BR_NILAI_02_renormalizes_weights_when_component_missing`. Tabel "Kasus uji" adalah data tes minimum, bukan satu-satunya.

Semua angka bertanda *(konfig)* dibaca dari konfigurasi dengan default di §12. Dasar: v1 §7, ditambah rincian v2.

## 1. Akun — BR-AKUN

**BR-AKUN-01 · Hanya admin membuat akun.** Tidak ada registrasi mandiri. Admin bisa membuat akun `admin`, `sensei`, `murid`. Impor CSV hanya untuk `sensei` dan `murid`.

**BR-AKUN-02 · Langkah pembuatan akun.**
1. Validasi: username 3–64 karakter `[a-z0-9._-]`, unik; email (opsional) valid dan unik; peran valid; nama tampilan 1–80 karakter.
2. Keycloak: buat user (`enabled = true`), set password sementara (`temporary = true`, 12 karakter acak tanpa karakter ambigu), required action `UPDATE_PASSWORD`, tetapkan realm role.
3. Buat `user_profiles` dengan `id` = id user Keycloak.
4. Bila langkah 3 gagal: hapus user Keycloak (kompensasi), kembalikan error.
5. Password sementara dikembalikan **sekali** di respons, tidak pernah disimpan.
6. Terbitkan event `UserAccountCreated` (mulai Fase 1).

**BR-AKUN-03 · Impor CSV.**
- Format UTF-8 dengan header: `username,email,display_name,role,timezone,ui_locale`. Kolom `email`, `timezone` (default `Asia/Jakarta`), `ui_locale` (default `id`) opsional. Maksimal 500 baris.
- **Validasi semua baris dulu** (format, duplikat di file, username/email yang sudah ada). Bila ada error, tidak ada akun yang dibuat; respons berisi daftar error per baris (`row`, `column`, `code`).
- Bila valid, akun dibuat berurutan. Bila Keycloak gagal di tengah jalan, proses berhenti dan respons melaporkan baris yang sudah dibuat. Impor ulang file yang sama melewati username yang sudah ada.
- Hasil: file CSV `username,temporary_password` yang bisa diunduh **sekali**.
- ✏️ Rincian (Fase 0.2): urutan kolom bebas; kolom wajib `username`, `display_name`, `role`; kolom tak dikenal = error. Pemisah `,` atau `;` (CSV dari spreadsheet berlokal Indonesia memakai `;`), BOM UTF-8 diterima, baris kosong diabaikan. Baris yang **username-nya sudah ada dilewati** (dilaporkan sebagai `skipped`, bukan error) sehingga impor ulang aman; email yang sudah dipakai akun lain = error `email_taken`. Kode error per sel ada di [05](05-api-dan-event.md) §4.

**BR-AKUN-04 · Reset password.** Password sementara baru + `UPDATE_PASSWORD` + logout semua sesi pengguna. Password ditampilkan sekali.

**BR-AKUN-05 · Nonaktifkan dan aktifkan.**
- Nonaktif: Keycloak `enabled = false`, logout semua sesi, `is_active = false`, event `UserDeactivated`. Akibat: tidak bisa login, API membalas 403 `account_inactive`, hilang dari leaderboard, notifikasi pending dibatalkan. Keanggotaan kelas tetap (ditandai nonaktif di daftar).
- Aktif kembali: kebalikannya, event `UserReactivated`.
- Admin tidak bisa menonaktifkan dirinya sendiri. Harus selalu ada minimal satu admin aktif.

**BR-AKUN-06 · Nama tampilan** diubah oleh admin saja (D-21 🟡), 1–80 karakter, di-trim, boleh tidak unik. Perubahan menerbitkan `UserDisplayNameChanged`.

**BR-AKUN-07 · Preferensi** (`ui_locale`, `script_pref`, `timezone`) bisa diubah pengguna kapan saja dan langsung berlaku di UI. `timezone` harus nama zona IANA yang valid.

## 2. Akses — BR-AKSES

**BR-AKSES-01 · Akses bab (unlock) ✅.** Murid M boleh mengakses bab C (materi, data referensi, dan soal drilling bab itu) jika ada kelas K dengan: M anggota `active` di K, K berstatus `active` atau `archived`, dan ada `class_unlocks(K, C)`. Akses adalah **gabungan** lintas kelas. Kelas `archived` bersifat baca saja (materi dan drilling tetap, tanpa kuis/tugas baru). Kelas `draft` tidak memberi akses.

Kasus uji:

| # | Kondisi | Hasil |
|---|---|---|
| 1 | M aktif di K1; K1 membuka C | boleh |
| 2 | M aktif di K1 (tidak membuka C); K2 membuka C, M bukan anggota K2 | ditolak, 403 `chapter_locked` |
| 3 | M aktif di K1 (tidak membuka C) dan K2 (membuka C) | boleh (gabungan) |
| 4 | M berstatus `removed` di K2 yang membuka C | ditolak |
| 5 | K2 `archived`, membuka C, M aktif | boleh (baca saja) |
| 6 | K2 `draft`, membuka C, M aktif | ditolak |

**BR-AKSES-02 · Menutup bab.** Menghapus baris unlock dan menerbitkan `ChapterLocked`. Untuk murid yang tidak punya akses lain: materi langsung tersembunyi, batch drilling berikutnya tidak memuat soal bab itu, riwayat jawaban dan mastery tetap tersimpan dan tetap terlihat di progres. Kuis tidak terpengaruh (diatur BR-KUIS-01).

**BR-AKSES-03 · Lingkup sensei.** Jika `Mengajar(S, K)`, sensei S boleh mengelola anggota, unlock, jadwal, kuis, tugas, penilaian, dan skema nilai kelas K, serta melihat data murid di K. Sensei yang membuat kelas otomatis menjadi pengajar utama. Menambah/menghapus pengajar lain: pengajar utama atau admin. Pengajar terakhir tidak boleh dihapus.

**BR-AKSES-04 · Visibilitas data** mengikuti tabel di [01](01-peran-dan-akses.md) §4. Resource milik orang lain dibalas 404.

**BR-AKSES-05 · Mode uji coba staf** 🟡. Admin dan sensei bisa mencoba drilling dan kuis. Jawaban dievaluasi dan umpan balik tampil, tetapi tidak ada data yang disimpan: tanpa sesi, jawaban, mastery, attempt, poin, atau log aktivitas.

## 3. Konten & file — BR-KONTEN, BR-FILE

**BR-KONTEN-01 · Materi tampil ke murid** jika BR-AKSES-01 terpenuhi **dan** (`publish_at` null atau `publish_at <= now`). Staf melihat semua materi.

**BR-KONTEN-02 · Ubah/hapus materi**: pembuat (`created_by`) atau admin. Menghapus materi `file` menandai file `deleted`; file fisik dihapus job pembersihan.

**BR-KONTEN-03 · Level/Buku/Bab** dikelola admin. Hapus hanya bila belum pernah dipakai: tanpa buku/bab/materi/data referensi di bawahnya, tanpa soal, kelas, atau unlock yang merujuk (cek lintas modul lewat Contracts; method baru dicatat di [02](02-arsitektur.md) §6.2). Bila dipakai: 409 `has_dependents`.

**BR-KONTEN-04 · Data referensi tanpa `chapter_id`** tidak dipakai generator soal sampai dipetakan ke bab.

**BR-KONTEN-05 · Materi tautan** hanya `https://`. Ditampilkan dengan nama domain dan ikon tautan luar, dibuka di tab baru dengan `rel="noopener noreferrer"`.

**BR-FILE-01 · Ukuran maksimal** 1 GiB per file *(konfig `Upload:MaxFileBytes` = 1073741824)*. Upload yang menyatakan ukuran lebih besar ditolak di awal (413 `file_too_large`). Data melebihi ukuran yang dinyatakan juga ditolak.

**BR-FILE-02 · Tipe file bebas, tidak pernah dirender inline.** Unduhan memakai `Content-Disposition: attachment` (nama file dengan `filename*` UTF-8), `X-Content-Type-Options: nosniff`, dan `Content-Type: application/octet-stream`. Pengecualian: sampul buku (`book_cover`) boleh tampil sebagai gambar bila lolos cek *magic bytes* PNG/JPEG/WebP dan ≤ 5 MB.

**BR-FILE-03 · Resumable.** Klien mengirim potongan 8 MB dan melanjutkan setelah koneksi putus. Upload yang tidak selesai dalam 24 jam *(konfig)* kedaluwarsa: status `failed`, file sementara dihapus.

**BR-FILE-04 · Hak unduh ditentukan resource pemilik.**

| Purpose | Yang boleh mengunduh |
|---|---|
| `material` | murid dengan akses bab (BR-KONTEN-01), staf |
| `submission` | murid pengirim, pengajar kelas tugas itu, admin |
| `book_cover` | semua pengguna login |
| `question_import` | admin |

Endpoint resource mengecek akses lalu mengarahkan (302) ke URL bertanda tangan HMAC berumur 5 menit *(konfig)*.

**BR-FILE-05 · File yatim**: file `ready` yang tidak dirujuk resource mana pun selama 7 hari *(konfig)* ditandai `deleted` lalu dihapus.

**BR-FILE-06 · Hak upload**: staf untuk `material`; admin untuk `book_cover` dan `question_import`; murid hanya `submission`. Batas 20 upload per jam per pengguna 🟡 *(konfig)*.

## 4. Jadwal & notifikasi — BR-JADWAL

**BR-JADWAL-01 · Validitas sesi**: `ends_at > starts_at`, durasi ≤ 8 jam, `meeting_url` hanya `https://`, kelas berstatus `active`.

**BR-JADWAL-02 · Link meeting** hanya terlihat oleh murid aktif kelas itu, pengajar kelas, dan admin. Respons lain (termasuk daftar sesi untuk pihak lain) tidak memuat link.

**BR-JADWAL-03 · Feed ICS** per pengguna memuat sesi dari semua kelas tempat ia murid aktif atau pengajar, jendela 30 hari ke belakang sampai 180 hari ke depan *(konfig)*. `UID` = `{sessionId}@nomax`; `SEQUENCE` = `sequence`; sesi batal → `STATUS:CANCELLED`; `SUMMARY` = `[nama kelas] topik`; `DESCRIPTION` memuat platform, link, catatan; waktu dalam UTC. Header: `Content-Type: text/calendar; charset=utf-8`, `Cache-Control: private, max-age=300`.

**BR-JADWAL-04 · Token ICS**: 32 byte acak (base64url), yang disimpan hanya hash SHA-256. Membuat/merotasi token mengembalikan URL lengkap **sekali**; rotasi membatalkan token lama. Token tidak valid → 404.

**BR-JADWAL-05 · Perubahan jadwal.** Membuat sesi → notifikasi `session_created` ke semua anggota. Mengubah waktu/link/topik → `sequence + 1` dan notifikasi `session_updated`. Membatalkan → status `cancelled`, `sequence + 1`, notifikasi `session_cancelled`, pengingat pending dibatalkan.

**BR-JADWAL-06 · Pengingat** untuk setiap anggota dan kanal: `starts_at − 24 jam` (`session_reminder_1d`) dan `starts_at − 1 jam` (`session_reminder_1h`) *(konfig)*. Pengingat hanya dibuat bila waktunya masih di masa depan. Saat sesi diubah, pengingat pending lama dibatalkan lalu dibuat ulang. `dedupe_key` mencegah duplikat.

Kasus uji (waktu WIB):

| # | Kondisi | Hasil |
|---|---|---|
| 1 | Senin 10:00 membuat sesi Rabu 19:00 | Pengingat Selasa 19:00 dan Rabu 18:00 |
| 2 | Rabu 08:00 membuat sesi Rabu 19:00 | Hanya pengingat Rabu 18:00 + notifikasi sesi baru |
| 3 | Rabu 18:30 membuat sesi Rabu 19:00 | Tanpa pengingat; hanya notifikasi sesi baru |
| 4 | Sesi Rabu 19:00 digeser ke Kamis 19:00 (dilakukan Senin) | Pengingat lama batal; baru: Rabu 19:00 dan Kamis 18:00; notifikasi perubahan |
| 5 | Sesi dibatalkan | Semua pengingat pending batal; notifikasi pembatalan |

**BR-JADWAL-07 · Kanal & bahasa.** Email dikirim bila profil punya email; web push ke setiap langganan perangkat. Pengguna nonaktif tidak menerima apa pun. Isi notifikasi memakai `ui_locale` penerima; waktu ditampilkan dalam zona waktu penerima beserta singkatannya (mis. WIB).

**BR-JADWAL-08 · Tampilan waktu** di UI selalu dalam zona waktu pengguna yang melihat.

## 5. Bank soal — BR-SOAL

**BR-SOAL-01 · Sumber dan lisensi wajib ✅.** `source_id` tidak boleh null. Soal buatan sensei/admin memakai sumber "Internal NomaX" (license `internal`). Soal ujian JLPT resmi atau PDF yang beredar tanpa izin **tidak boleh** dimasukkan.

**BR-SOAL-02 · Alur status.** `draft` ↔ `review` → `published` → `archived`.
- Soal `private`: pemilik boleh langsung menerbitkan (`draft` → `published`) untuk dipakai sendiri.
- Soal `builtin`: hanya admin yang boleh menerbitkan. Soal hasil generator dan impor selalu masuk `review` dulu.

**BR-SOAL-03 · Scope dan visibilitas.** `builtin` terlihat oleh semua staf. `private` terlihat oleh pemilik dan admin. Sensei menyusun kuis dari `builtin` + soal `private` miliknya sendiri.

**BR-SOAL-04 · Soal yang sudah dipakai tidak diubah isinya.** Bila soal sudah punya `drill_answers` atau `attempt_answers`, kolom `kind`, `payload`, `category`, dan `chapter_id` terkunci. Mengubah isi = membuat salinan baru (status `draft`) dan mengarsipkan yang lama. Kolom `prompt`, `explanation`, dan `tags` tetap boleh diperbaiki.

**BR-SOAL-05 · Generator soal** dari data referensi menghasilkan soal `builtin` berstatus `review` milik admin, tanpa duplikat (template + item yang sama dilewati). Pengecoh diambil dari item lain di level dan kategori yang sama, diutamakan dari bab yang sama atau berdekatan.

| Data | Template | `kind` | `jaDisplay` |
|---|---|---|---|
| kana | kana → pilih romaji | `mcq` | `no_romaji` |
| kana | romaji → pilih kana | `mcq` | `no_romaji` |
| kana | kana → ketik romaji | `type_answer` (`romaji`) | `no_romaji` |
| kosakata | kata → pilih arti | `mcq` | `user_pref` |
| kosakata | arti → pilih kata | `mcq` | `user_pref` |
| kosakata | kata berkanji → ketik bacaan | `type_answer` (`kana`) | `plain` |
| kanji | kanji → pilih bacaan | `mcq` | `plain` |
| kanji | kanji → pilih arti | `mcq` | `plain` |
| kalimat | terjemahan → susun kalimat | `tap_sentence` | `user_pref` |

**BR-SOAL-06 · Impor PDF (Fase 5)**: upload → ekstraksi teks (OCR bila perlu) → parser → draf soal dengan sumber dan lisensi → review admin → terbit. Tidak pernah terbit otomatis.

## 6. Drilling & mastery — BR-DRILL, BR-MASTERY

**BR-DRILL-01 · Tanpa batas dan tanpa nilai ✅.** Drilling tidak memengaruhi nilai kelas. Sensei boleh melihat dan memberi komentar tanpa nilai.

**BR-DRILL-02 · Pool soal** = soal yang `status = 'published'`, berada di bab yang bisa diakses murid (BR-AKSES-01) dan dipilih di konfigurasi sesi (default: semua bab yang bisa diakses), kategori cocok, `kind` bukan `long_answer`, `handwrite_*` hanya bila mode input tulis tangan (Fase 5), dan scope `builtin` **atau** `private` milik pengajar salah satu kelas aktif murid. Pool kosong → 409 `empty_pool`.

**BR-DRILL-03 · Mode normal**: urutan acak tanpa pengulangan sampai pool habis, lalu diacak ulang. Soal dikirim per batch 10 *(konfig)*.

**BR-DRILL-04 · Mode fokus lemah**: kandidat = item di pool yang sudah pernah dijawab (`attempts >= 1`). Bobot `w = (1 − mastery) + 0.1`, ditambah `0.2` bila `last_seen_at` lebih dari 7 hari lalu. Pengambilan acak berbobot tanpa pengembalian per batch. Bila kandidat < 5 *(konfig)*, kembali ke mode normal dan respons memberi tanda `fallback: "normal"`.

**BR-DRILL-05 · Evaluasi jawaban** (dipakai juga oleh kuis):

| `kind` | Benar jika |
|---|---|
| `mcq` | himpunan `selected` sama persis dengan `correct` |
| `tap_sentence` | `order` sama persis dengan salah satu urutan di `answers` |
| `type_answer` | jawaban ternormalisasi (BR-DRILL-06) sama dengan salah satu `accepted` ternormalisasi |
| `match` | drilling: semua pasangan benar (kuis: poin parsial, BR-KUIS-07) |
| `flashcard` | `remembered = true` (penilaian diri) |
| `handwrite_kana` | Fase 5: skor pencocokan stroke ≥ 0,75 *(konfig)*; cadangan: penilaian diri |
| `handwrite_kanji` | Fase 5: penilaian diri (default) |
| `long_answer` | tidak dipakai di drilling |

**BR-DRILL-06 · Normalisasi `type_answer`.**
1. Unicode NFKC, trim, spasi ganda menjadi satu.
2. Buang tanda baca di akhir: `。．.！!？?` dan spasi lebar penuh.
3. `answerScript = kana`: klien mengonversi ketikan romaji ke kana (WanaKana) dan mengirim `text` (kana) + `raw`; server memastikan `text` berupa kana.
4. `kanaInsensitive` (default `true` untuk kana): katakana diubah ke hiragana di kedua sisi.
5. `answerScript = romaji`: huruf kecil; makron dinormalisasi (ā→aa, ī→ii, ū→uu, ē→ee, ō→ou); apostrof, tanda hubung, dan spasi dibuang.

| # | `accepted` | Input | Hasil |
|---|---|---|---|
| 1 | たべる | `たべる ` | benar |
| 2 | たべる | `タベル` | benar (kanaInsensitive) |
| 3 | たべる | ketik `taberu` → `たべる` | benar |
| 4 | たべる | `たべる。` | benar |
| 5 | toukyou | `tōkyō` | benar (makron) |
| 6 | toukyou | `tokyo` | salah |
| 7 | ka | `KA` | benar |
| 8 | たべる | `たべた` | salah |

**BR-DRILL-07 · Idempoten.** Klien membuat id jawaban (UUIDv7). Mengirim ulang id yang sama mengembalikan hasil tersimpan tanpa menghitung dua kali. Waktu yang dipakai adalah waktu server.

**BR-DRILL-08 · Siklus sesi.** Sesi berakhir lewat endpoint `end` atau otomatis setelah idle 30 menit *(konfig)* (`ended_at` = waktu jawaban terakhir). Jawaban ke sesi yang sudah berakhir → 409 `session_ended`.

**BR-MASTERY-01 · Pembaruan mastery** setiap jawaban drilling untuk item (`item_type`, `item_id`), atau soal itu sendiri bila tanpa item:
- `attempts += 1`; `correct += 1` bila benar; `streak = benar ? streak + 1 : 0`; `last_seen_at = now`.
- `mastery = round(mastery + α × (x − mastery), 3)`, dengan `x = 1` (benar) atau `0` (salah), `α = 0.3` *(konfig)*, pembulatan *away from zero*.
- Kolom dimensi (`level_id`, `chapter_id`, `category`, `tags`) disalin dari soal.

| Urutan jawaban (mulai 0) | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ | ✓ |
|---|---|---|---|---|---|---|---|
| mastery | 0.300 | 0.510 | 0.357 | 0.550 | 0.685 | 0.780 | 0.846 |
| streak | 1 | 2 | 0 | 1 | 2 | 3 | 4 |

**BR-MASTERY-02 · Status item.** `belum dicoba` (attempts = 0); `lemah` (attempts ≥ 3 dan mastery < 0,6); `dikuasai` (mastery ≥ 0,85 dan streak ≥ 3); selain itu `sedang dipelajari`. Semua ambang *(konfig)*.

| attempts | mastery | streak | Status |
|---|---|---|---|
| 2 | 0.20 | 0 | sedang dipelajari (data belum cukup) |
| 5 | 0.55 | 1 | lemah |
| 6 | 0.86 | 3 | dikuasai |
| 6 | 0.86 | 2 | sedang dipelajari |

**BR-MASTERY-03 · Agregasi kelemahan** per kategori, tag (`kana_row:*`, `particle:*`, tag lain), dan bab, hanya dari item dengan attempts ≥ 1. Per kelompok: jumlah item, rata-rata mastery, akurasi (`Σcorrect / Σattempts`), jumlah item lemah. Urut dari rata-rata mastery terendah, lalu jumlah item lemah terbanyak. Tampilkan 5 kelompok teratas dan 10 item terlemah.

**BR-MASTERY-04 · Hanya jawaban drilling** yang memperbarui mastery (sesuai v1). Jawaban kuis/ujian tidak (Q-05).

## 7. Kuis & ujian — BR-KUIS

**BR-KUIS-01 · Ketersediaan.** Terlihat oleh murid aktif kelas bila `status = 'published'`. Bisa dimulai bila (`open_at` null atau `now >= open_at`) dan (`close_at` null atau `now < close_at`); di luar itu 409 `quiz_not_open`. Ketersediaan kuis tidak bergantung pada unlock bab.

**BR-KUIS-02 · Percobaan.** `attempt_no <= max_attempts`, selebihnya 409 `attempts_exhausted`. Maksimal satu attempt `in_progress`; memulai lagi mengembalikan attempt yang sedang berjalan.

**BR-KUIS-03 · Timer di server.** `expires_at = min(started_at + time_limit_min, close_at)` (null bila keduanya null). Jawaban setelah `expires_at` + 10 detik toleransi *(konfig)* → 409 `attempt_expired`. Attempt yang kedaluwarsa di-submit otomatis oleh job dengan jawaban yang sudah tersimpan.

| # | Mulai | Batas waktu | `close_at` | `expires_at` |
|---|---|---|---|---|
| 1 | 10:00 | 30 menit | – | 10:30 |
| 2 | 10:00 | 30 menit | 10:20 | 10:20 |
| 3 | 10:00 | – | 12:00 | 12:00 |
| 4 | 10:00 | – | – | – (tanpa batas) |

**BR-KUIS-04 · Urutan.** Bila `shuffle`, `question_order` adalah permutasi acak per attempt; pilihan, token, dan kolom kanan `match` diacak dengan seed dari id attempt (stabil saat dimuat ulang). Bila tidak, mengikuti `sort_order`.

**BR-KUIS-05 · Tanpa kunci jawaban** di respons selama attempt berjalan ([03](03-skema-data.md) §8.3).

**BR-KUIS-06 · Simpan otomatis.** Jawaban per soal boleh disimpan (PUT) berulang selama `in_progress` dan belum kedaluwarsa; simpanan terakhir yang berlaku.

**BR-KUIS-07 · Penilaian.** Otomatis untuk semua `kind` kecuali `long_answer` (manual oleh pengajar). Benar → poin penuh; salah → 0; `match` → `points × (pasangan benar / total pasangan)`, dibulatkan 2 desimal. Kuis/ujian **tidak boleh** memuat `flashcard` atau soal penilaian diri.

**BR-KUIS-08 · Skor.** Setelah semua jawaban dinilai: `score_pct = round(100 × Σpoin / Σpoin maksimal, 2)`, status `graded`, event `QuizGraded`. Selama masih ada `long_answer` yang belum dinilai, status tetap `submitted`.

**BR-KUIS-09 · Pembahasan.** Bila `show_review = true`, murid melihat kunci dan pembahasan setelah `close_at` lewat; bila `close_at` null, setelah murid tidak punya sisa percobaan. Bila `false`, murid hanya melihat skor.

**BR-KUIS-10 · Mengubah kuis yang sudah dikerjakan.** Setelah ada attempt pertama: soal, poin, `kind`, dan `time_limit_min` terkunci; yang boleh diubah hanya judul, `close_at` (diperpanjang), `show_review`, dan `max_attempts` (dinaikkan).

## 8. Tugas — BR-TUGAS

**BR-TUGAS-01 · Isi submission**: teks atau minimal satu file. File harus `ready`, diunggah murid itu sendiri, purpose `submission`, maksimal 10 file.

**BR-TUGAS-02 · Kirim ulang** hanya bila `allow_resubmit = true` **dan** belum dinilai; isi lama ditimpa dan `submitted_at` diperbarui. Tanpa izin kirim ulang → 409 `already_submitted`.

**BR-TUGAS-03 · Terlambat** 🟡 (D-20): submission setelah `due_at` tetap diterima dan ditandai "terlambat" (dihitung dari `submitted_at > due_at`).

**BR-TUGAS-04 · Penilaian** oleh pengajar kelas atau admin: `grade_pct` 0–100 (2 desimal) + feedback, event `SubmissionGraded`, nilai kelas dihitung ulang. Nilai boleh diubah kemudian (tercatat di log aktivitas).

## 9. Nilai kelas — BR-NILAI

**BR-NILAI-01 · Rata-rata komponen** per murid per kelas:
- **Kuis** (`kind = quiz`, `published`): per kuis, skor attempt `graded` terbaik; bila tidak ada attempt dan `close_at` sudah lewat → 0; selain itu tidak dihitung. Lalu dirata-rata.
- **Ujian** (`kind = exam`): sama seperti kuis.
- **Tugas** (`published`): sudah dinilai → `grade_pct`; tidak mengirim dan `due_at` sudah lewat → 0; sudah mengirim tapi belum dinilai, atau belum tenggat → tidak dihitung.

**BR-NILAI-02 · Nilai akhir** ✅: `final_pct = Σ(w_c × avg_c) / Σ(w_c)` hanya untuk komponen yang punya minimal satu item terhitung. Tidak ada komponen → `final_pct` null.

**BR-NILAI-03 · Konversi 0–4** ✅: urutkan `scale_map` dari `min` terbesar; ambil `gpa` pertama dengan `final_pct >= min`. Default v1: ≥80 = 4, 70–79 = 3, 60–69 = 2, 50–59 = 1, <50 = 0.

**BR-NILAI-04 · Pembulatan**: hitung dengan `decimal`; rata-rata dan nilai akhir dibulatkan 2 desimal (`MidpointRounding.AwayFromZero`) **sebelum** dibandingkan dengan tabel konversi.

**BR-NILAI-05 · Kapan dihitung ulang**: saat `QuizGraded`, `SubmissionGraded`, skema nilai berubah, keanggotaan berubah, dan oleh job per jam untuk kuis/tugas yang baru lewat tenggat. Hasil ditulis ke `class_grades` beserta `breakdown`.

**BR-NILAI-06 · Validasi skema**: setiap bobot ≥ 0 dan jumlahnya 100; `scale_map` punya `min` unik, memuat `min = 0`, `gpa` dalam 0–4. Mengubah skema menghitung ulang semua nilai di kelas itu.

| # | Data | `final_pct` | 0–4 |
|---|---|---|---|
| 1 | Bobot 30/40/30; kuis 85, ujian 72, tugas 90 | 81.30 | 4 |
| 2 | Bobot 30/40/30; kuis 70, ujian belum ada, tugas 80 | 75.00 | 3 |
| 3 | Kuis A: attempt 60 dan 85 (ambil 85); kuis B lewat tutup tanpa attempt (0) | rata-rata kuis 42.50 | – |
| 4 | Tugas sudah dikirim, belum dinilai | tidak dihitung | – |
| 5 | Nilai akhir mentah 79.995 | 80.00 | 4 |
| 6 | Nilai akhir mentah 79.994 | 79.99 | 3 |
| 7 | Belum ada komponen | null | null |
| 8 | `scale_map` kustom 85→4, 75→3.5, 65→3, 0→0; nilai akhir 78 | 78.00 | 3.5 |

## 10. Poin leaderboard — BR-POIN

**BR-POIN-01 · Hanya murid** yang mendapat poin. Mode uji coba staf tidak menghasilkan poin.

**BR-POIN-02 · Drilling**: jawaban benar = 1 poin *(konfig)*. Jawaban salah tidak menghasilkan `point_events`.

**BR-POIN-03 · Pengulangan** ✅: item yang sama (`item_type` + `item_id`, atau soal bila tanpa item) yang dijawab benar **lebih dari 3 kali** dalam hari yang sama bernilai **separuh** untuk jawaban benar ke-4 dan seterusnya *(konfig: ambang 3, pengali 0,5)*.

**BR-POIN-04 · Batas harian drilling** ✅: maksimal 200 poin per hari *(konfig)*. Poin yang diberikan = `min(poin_terhitung, max(0, 200 − total_drill_hari_ini))`.

**BR-POIN-05 · Definisi hari**: tanggal kalender di zona `Asia/Jakarta` *(konfig `Leaderboard:DayTimeZone`, D-17 🟡)*.

**BR-POIN-06 · Hanya jawaban benar dicatat**: setiap jawaban drilling yang benar menghasilkan satu `point_events`, termasuk yang bernilai 0 karena batas, dengan kolom `rule` yang menjelaskan alasannya.

**BR-POIN-07 · Penilaian diri bernilai 0**: jawaban `self_check` (flashcard, tulis tangan dengan cek mandiri) tidak menghasilkan poin (`rule = self_check_zero`), tetapi tetap memperbarui mastery. Alasan: tidak bisa diverifikasi, rawan farming.

**BR-POIN-08 · Kuis dan ujian** ✅: poin = `round(score_pct × faktor, 1)`, faktor kuis 1,0 dan ujian 2,0 *(konfig, D-17 🟡)*. Untuk percobaan berikutnya pada kuis yang sama, hanya **kenaikan** yang dihitung: `Δ = max(0, poin_baru − poin_terbaik_sebelumnya)` (`rule = quiz_improvement`).

**BR-POIN-09 · Season tujuan**: poin masuk ke season aktif milik level sumbernya (level soal untuk drilling, level kelas untuk kuis) pada saat kejadian.

**BR-POIN-10 · Idempoten**: `unique(source_type, source_id)` menjamin satu sumber hanya menghasilkan satu event poin.

| # | Situasi | Poin |
|---|---|---|
| 1 | 5 jawaban benar untuk item yang sama, hari yang sama | 1, 1, 1, 0.5, 0.5 (total 4.0) |
| 2 | Ada jawaban salah di antaranya | jawaban salah tidak dicatat dan tidak menambah hitungan pengulangan |
| 3 | Total drilling hari ini 199.5, lalu jawaban benar bernilai dasar 1 | 0.5, berikutnya 0 (`drill_daily_cap`) |
| 4 | Benar ke-4 untuk item X pukul 23:59 WIB, lalu benar lagi pukul 00:01 WIB | 0.5, lalu 1 (hari baru) |
| 5 | Flashcard dijawab "ingat" | 0 (`self_check_zero`) |
| 6 | Kuis: attempt 1 skor 85 | 85.0 |
| 7 | Kuis yang sama: attempt 2 skor 92 | +7.0 |
| 8 | Kuis yang sama: attempt 3 skor 80 | +0 |
| 9 | Ujian skor 70 (faktor 2,0) | 140.0 |
| 10 | Sensei dalam mode uji coba | tidak ada event |

## 11. Season & leaderboard — BR-SEASON

**BR-SEASON-01 · Satu season aktif per level**, selalu. Season awal dibuat oleh seed; season baru dibuat otomatis saat season lama ditutup.

**BR-SEASON-02 · Menutup season** (admin, D-18 🟡), dalam satu transaksi: jumlahkan poin per murid dari `point_events` season itu → tulis `season_standings` → status `closed`, `ends_at = now`, `closed_by` → buat season aktif baru "{level} · Musim {n+1}". Setelah commit: siapkan sorted set Redis kosong untuk season baru dan broadcast `SeasonClosed`.

**BR-SEASON-03 · Peringkat**: urut poin menurun; poin sama berbagi peringkat (*competition ranking*). Urutan tampil di dalam peringkat yang sama: nama tampilan A–Z. Peringkat live dihitung `1 + ZCOUNT(lb:{season}, "(" + skor, "+inf")`.

| Poin | 120 | 100 | 100 | 90 |
|---|---|---|---|---|
| Peringkat | 1 | 2 | 2 | 4 |

**BR-SEASON-04 · Update live**: setiap `point_events` menambah skor di Redis (`ZINCRBY`). Papan top N (20, *konfig*) di-broadcast ke grup season paling sering sekali per detik. Murid yang bersangkutan menerima `PointsAwarded` dan `MyRankUpdated`.

**BR-SEASON-05 · Tampilan**: nama tampilan, poin, dan peringkat saja. Baris milik sendiri ditandai. Pengguna nonaktif disembunyikan (dihapus dari sorted set saat `UserDeactivated`, dihitung ulang dari `point_events` saat aktif kembali).

## 12. Parameter konfigurabel (default)

| Kunci konfigurasi | Default | Aturan |
|---|---|---|
| `Upload:MaxFileBytes` | 1073741824 | BR-FILE-01 |
| `Upload:IncompleteExpiryHours` | 24 | BR-FILE-03 |
| `Upload:OrphanDays` | 7 | BR-FILE-05 |
| `Upload:MaxUploadsPerHour` | 20 | BR-FILE-06 |
| `Storage:SignedUrlTtlSeconds` | 300 | BR-FILE-04 |
| `Schedule:ReminderOffsets` | `24:00:00`, `01:00:00` | BR-JADWAL-06 |
| `Schedule:IcsPastDays` / `IcsFutureDays` | 30 / 180 | BR-JADWAL-03 |
| `Practice:Drill:BatchSize` | 10 | BR-DRILL-03 |
| `Practice:Drill:IdleMinutes` | 30 | BR-DRILL-08 |
| `Practice:Drill:WeakFocusMinCandidates` | 5 | BR-DRILL-04 |
| `Practice:Mastery:Alpha` | 0.3 | BR-MASTERY-01 |
| `Practice:Mastery:WeakThreshold` / `WeakMinAttempts` | 0.6 / 3 | BR-MASTERY-02 |
| `Practice:Mastery:MasteredThreshold` / `MasteredMinStreak` | 0.85 / 3 | BR-MASTERY-02 |
| `Practice:Quiz:GraceSeconds` | 10 | BR-KUIS-03 |
| `Practice:Handwriting:PassScore` | 0.75 | BR-DRILL-05 |
| `Grading:DefaultWeights` | kuis 30, ujian 40, tugas 30 | BR-NILAI-02 |
| `Grading:DefaultScaleMap` | ≥80=4, ≥70=3, ≥60=2, ≥50=1, else 0 | BR-NILAI-03 |
| `Leaderboard:DrillPointPerCorrect` | 1 | BR-POIN-02 |
| `Leaderboard:RepeatThreshold` / `RepeatMultiplier` | 3 / 0.5 | BR-POIN-03 |
| `Leaderboard:DailyDrillCap` | 200 | BR-POIN-04 |
| `Leaderboard:DayTimeZone` | Asia/Jakarta | BR-POIN-05 |
| `Leaderboard:QuizFactor` / `ExamFactor` | 1.0 / 2.0 | BR-POIN-08 |
| `Leaderboard:TopN` / `BroadcastIntervalMs` | 20 / 1000 | BR-SEASON-04 |

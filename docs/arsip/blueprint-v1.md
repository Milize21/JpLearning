# Blueprint Platform Belajar Bahasa Jepang (Komunitas)

Draf v1 · 18 Oktober 2026 · disusun dari jawaban diskusi bagian A sampai G

## 1. Ringkasan kebutuhan

Platform web untuk komunitas belajar bahasa Jepang, target sampai JLPT N3. Kelas tetap berjalan di Zoom atau Discord. Web ini menjadi sumber materi, tempat latihan, pengingat jadwal, dan pelacak progres. Tidak ada video call, listening, speaking, maupun absensi.

- **Peran:** Admin, Sensei, Murid (akun terpisah). Akun murid dibuat oleh admin.
- **Konten:** Level → Buku → Bab → Materi. Sensei membuka bab per kelas (fase). Murid hanya bisa mengakses bab yang sudah dibuka.
- **Latihan mandiri:** drilling tanpa batas ulang dan tanpa nilai (hafalan kana, kosakata, kalimat). Input bisa pilih, tap teks yang benar, ketik, atau tulis tangan. Sistem mencatat bagian mana yang masih lemah.
- **Kuis, ujian, tugas:** dinilai dengan skala 0–4. Tugas bisa upload file maksimal 1 GB.
- **Leaderboard:** live, gabungan poin latihan dan kuis, reset per level.
- **Privasi:** murid hanya melihat data sendiri dan leaderboard. Sensei melihat murid di kelasnya. Admin melihat semua.
- **UI sebagai media belajar:** teks tampil dalam Indonesia atau Inggris dengan pendamping Jepang (kanji, furigana, kana, romaji) yang bisa diatur.
- **Teknis:** .NET dengan beberapa API service, Keycloak untuk SSO, frontend terpisah, upload maksimal 1 GB per file.

## 2. Asumsi yang perlu dikonfirmasi

1. Murid yang di-assign ke sensei = anggota kelas sensei tersebut. Satu murid boleh ikut lebih dari satu kelas.
2. Level yang tersedia: Dasar (kana), N5, N4, N3. Kana menjadi Buku Dasar dengan bab per baris (a, ka, sa, dan seterusnya).
3. Latihan mandiri tidak bernilai. Sensei hanya bisa melihat dan memberi komentar. Kuis, ujian, dan tugas bernilai.
4. Nilai akhir 0–4 dihitung per kelas dari bobot kuis, ujian, dan tugas, bukan IPK kumulatif lintas level.
5. Reset leaderboard terjadi saat season level ditutup oleh admin atau sensei. Riwayat season disimpan.
6. Batas 1 GB berlaku per file.
7. Admin punya semua hak sensei. Sensei hanya melihat kelasnya sendiri.
8. Frontend: React + TypeScript (cocok untuk canvas tulis tangan dan konversi kana/romaji). Bisa diganti Vue atau Blazor.
9. Kalender: tahap awal berupa langganan ICS yang bisa ditambahkan ke Google Calendar. Integrasi OAuth Google di fase akhir. Pengingat lewat email dan web push.
10. Poin leaderboard memakai aturan anti-farming (bagian 7) supaya drilling tak terbatas tidak dipakai untuk menumpuk poin.

## 3. Peran dan hak akses

| Kemampuan | Admin | Sensei | Murid |
| --- | --- | --- | --- |
| Buat akun murid/sensei, reset password, nonaktifkan | ✓ | – | – |
| Kelola Level, Buku, Bab, bank soal bawaan, import | ✓ | – | – |
| Buat soal sendiri | ✓ | ✓ (milik sendiri) | – |
| Buat kelas, atur anggota | ✓ | kelas sendiri | – |
| Upload materi | ✓ | ✓ | – |
| Buka/tutup bab (fase kelas) | ✓ | ✓ | – |
| Jadwal pertemuan + link Zoom/Discord | ✓ | ✓ | lihat |
| Susun kuis, ujian, tugas dari bank soal | ✓ | ✓ | – |
| Nilai tugas, beri komentar | ✓ | ✓ | – |
| Lihat progres dan kelemahan murid | semua | murid di kelasnya | diri sendiri |
| Drilling, kuis, tugas | uji coba | uji coba | ✓ |
| Leaderboard | ✓ | ✓ | ✓ |

## 4. Arsitektur

```
[React SPA] --OIDC (Authorization Code + PKCE)--> [Keycloak]
     | JWT
     v
[API Gateway (YARP)]
   |-- Identity/Admin Svc --> Keycloak Admin API
   |-- Content Svc  --> PostgreSQL + penyimpanan file
   |-- Class Svc    --> PostgreSQL
   |-- Practice Svc --> PostgreSQL
   |-- Insight Svc  --> PostgreSQL + Redis + SignalR (live)

Event antar-service: AnswerSubmitted, QuizGraded, SubmissionGraded,
ChapterUnlocked, SessionCreated (RabbitMQ atau outbox + worker)
```

| Service | Tanggung jawab |
| --- | --- |
| Gateway (YARP) | Satu pintu masuk, validasi JWT, rate limit |
| Identity/Admin | Profil, preferensi, buat/nonaktifkan akun lewat Keycloak Admin API, import CSV |
| Content | Level/Buku/Bab, materi, file (upload resumable), data referensi (kana, kanji, kosakata, tata bahasa, kalimat) |
| Class | Kelas, anggota, unlock bab, jadwal, feed ICS, pengingat |
| Practice | Bank soal, drilling, kuis/ujian, tugas, penilaian, mastery |
| Insight | Log aktivitas, poin, leaderboard live, laporan progres |

Hal lintas service:

- **Keycloak:** satu realm, client web-app (public, PKCE), client api, realm role admin, sensei, murid. Password hanya disimpan di Keycloak.
- **Otorisasi dua lapis:** policy peran, lalu cek kepemilikan resource (sensei ke kelasnya, murid ke datanya sendiri).
- **Infrastruktur:** PostgreSQL (schema per service), Redis, penyimpanan file S3-compatible atau disk, Docker Compose, OpenAPI per service, health check, Serilog.
- **Catatan:** kalau mau cepat di awal, boleh mulai sebagai modular monolith dengan batas modul yang sama, lalu dipecah. Itu keputusanmu.

## 5. Teks multi-bahasa dan Jepang pendamping

Tujuannya: murid dan sensei ikut belajar Jepang saat memakai web.

- Semua teks konten disimpan sebagai **TextBlock**: versi id, en, dan ja berupa potongan teks beserta bacaan (furigana). Romaji dihasilkan otomatis dari bacaan, dengan override untuk partikel (は dibaca wa, を dibaca o, へ dibaca e).

```json
{
  "id": "Saya belajar bahasa Jepang.",
  "en": "I study Japanese.",
  "ja": [
    {"t": "私", "r": "わたし"}, {"t": "は", "ro": "wa"},
    {"t": "日本語", "r": "にほんご"}, {"t": "を", "ro": "o"},
    {"t": "勉強", "r": "べんきょう"}, {"t": "します"}
  ]
}
```

- Label antarmuka memakai struktur yang sama lewat kunci i18n. Contoh: nav.practice tampil sebagai Latihan / Practice / 練習 (れんしゅう, renshuu).
- Preferensi per pengguna: bahasa utama (ID atau EN) dan tampilan Jepang pendamping (mati, kanji + furigana, kana saja, romaji saja, atau kanji + furigana + romaji). Bisa diganti kapan saja lewat toggle.
- Frontend: komponen JapaneseText memakai elemen ruby dan font Noto Sans JP. Konversi kana dan romaji di sisi klien (mis. WanaKana). Kata bisa diketuk untuk melihat arti.

## 6. Skema data

Semua id UUID. Kolom TextBlock disimpan sebagai jsonb. Setiap tabel punya created\_at dan updated\_at.

**Identity/Admin**

- UserProfile(id = Keycloak sub, display\_name, role, ui\_locale, script\_pref, timezone, is\_active, created\_by)

**Content**

- Level(id, code, name, order), Book(id, level\_id, title, cover\_file\_id, order), Chapter(id, book\_id, number, title, order)
- Material(id, chapter\_id, kind: file, link, atau richtext, title, body, file\_id, url, publish\_at, created\_by)
- StoredFile(id, storage\_key, original\_name, mime, size\_bytes, sha256, uploaded\_by, status)
- Data referensi: KanaChar, Kanji, Vocab, GrammarPoint, Sentence. Masing-masing punya level\_id, chapter\_id (opsional), source, license.

**Class**

- Class(id, level\_id, book\_id, name, status, created\_by)
- ClassTeacher(class\_id, teacher\_id, is\_primary), ClassStudent(class\_id, student\_id, joined\_at, status)
- ClassUnlock(class\_id, chapter\_id, unlocked\_by, unlocked\_at)
- ClassSession(id, class\_id, chapter\_id?, topic, starts\_at, ends\_at, timezone, platform, meeting\_url, note)
- CalendarFeedToken(user\_id, token\_hash), Notification(id, user\_id, type, payload, channel, send\_at, sent\_at)
- GoogleCalendarLink(user\_id, refresh\_token\_enc), opsional di fase 5

**Practice**

- QuestionSource(id, name, url, license, imported\_file\_id, reviewed\_by, reviewed\_at)
- Question(id, kind, level\_id, chapter\_id, category, tags\[\], prompt, payload jsonb, explanation, item\_ref, status: draft, review, atau published, source\_id, owner\_id)
- Jenis soal (kind): mcq, tap\_sentence, type\_answer, match, flashcard, handwrite\_kana, handwrite\_kanji, long\_answer
- DrillSession(id, student\_id, class\_id, category, config jsonb, started\_at, ended\_at)
- DrillAnswer(id, session\_id, question\_id, input\_mode, answer jsonb, is\_correct, response\_ms)
- ItemMastery(student\_id, item\_type, item\_id, attempts, correct, streak, last\_seen\_at, mastery), primary key gabungan
- Quiz(id, class\_id, chapter\_id, kind: quiz atau exam, title, time\_limit\_min, max\_attempts, open\_at, close\_at, shuffle, show\_review)
- QuizQuestion(quiz\_id, question\_id, points, order), QuizAttempt(id, quiz\_id, student\_id, started\_at, submitted\_at, score\_pct, status), AttemptAnswer(attempt\_id, question\_id, answer jsonb, is\_correct, points)
- Assignment(id, class\_id, chapter\_id, title, instructions, due\_at, allow\_resubmit), Submission(id, assignment\_id, student\_id, text, file\_ids\[\], submitted\_at, grade\_pct, feedback, graded\_by, graded\_at)
- GradeScheme(class\_id, w\_quiz, w\_exam, w\_assignment, scale\_map jsonb), ClassGrade(class\_id, student\_id, final\_pct, gpa\_0\_4, computed\_at)
- Comment(id, student\_id, author\_id, target\_type, target\_id, text)

**Insight**

- ActivityLog(id, user\_id, type, ref, meta jsonb, ts)
- LeaderboardSeason(id, level\_id, name, starts\_at, ends\_at, status), PointEvent(id, student\_id, level\_id, season\_id, source\_type, source\_id, points, ts)
- Redis: sorted set lb:{season\_id} untuk peringkat

## 7. Aturan bisnis penting

- **Unlock bab:** murid di kelas K boleh mengakses materi, soal, dan pool drilling bab C hanya jika ada ClassUnlock(K, C). Bila murid ada di beberapa kelas, aksesnya gabungan. Admin dan sensei bisa membuka dan menutup bab.
- **Drilling:** tanpa batas ulang dan tanpa nilai. Murid memilih kategori dan pool dari bab yang terbuka. Tiap jawaban dicatat dan memperbarui ItemMastery. Mode fokus lemah memilih item bermastery rendah lebih sering.
- **Kelemahan:** agregasi ItemMastery per kategori, tag, baris kana, jenis partikel, dan bab. Tampil di murid dan di sensei (untuk murid di kelasnya).
- **Nilai 0–4:** persen tiap komponen (rata-rata kuis, ujian, tugas) dikali bobot menjadi final\_pct, lalu dikonversi. Usulan default: ≥80 = 4, 70–79 = 3, 60–69 = 2, 50–59 = 1, di bawah 50 = 0. Bobot dan tabel konversi bisa diatur per kelas.
- **Poin leaderboard (usulan):** drilling 1 poin per jawaban benar. Item yang sama dijawab benar lebih dari 3 kali dalam sehari bernilai separuh. Batas 200 poin per hari dari drilling. Kuis dan ujian: poin sebanding skor. Semua angka dibuat konfigurabel.
- **Season:** satu season aktif per level. Menutup season mengarsipkan peringkat akhir dan memulai papan baru dari nol. Update peringkat live lewat SignalR.
- **Jadwal:** link Zoom/Discord hanya terlihat anggota kelas. Feed ICS menggabungkan semua kelas milik pengguna. Perubahan jadwal memperbarui feed dan memicu pemberitahuan. Pengingat H-1 hari dan 1 jam.
- **Upload:** resumable, maksimal 1 GB per file, tipe file bebas. Unduhan hanya untuk yang berhak.
- **Visibilitas:** murid hanya data sendiri dan leaderboard. Sensei murid di kelasnya. Admin semua.

## 8. Bank soal dan import

- Sumber soal: (1) buatan sensei, (2) dihasilkan otomatis dari data referensi (kana, kosakata dan kanji per level, kalimat contoh), (3) impor dokumen atau PDF.
- Setiap soal wajib punya source dan license. Alur status: draft, review, published. Soal bawaan hanya dipublikasikan admin.
- Soal ujian JLPT resmi tahun-tahun sebelumnya: setahu saya yang dirilis resmi hanya contoh soal terbatas, dan PDF soal lama yang beredar di internet berisiko melanggar hak cipta. Sebaiknya bukan sumber utama. Gunakan dataset berlisensi terbuka (mis. JMdict, KANJIDIC, KanjiVG, daftar kosakata JLPT komunitas, kalimat Tatoeba), soal buatan sendiri, dan konten yang izinnya jelas. Cek lisensi tiap sumber sebelum dimasukkan karena sebagian mewajibkan atribusi atau share-alike.
- Pipeline impor PDF (fase 5): upload, ekstrak teks (OCR bila perlu), parser menjadi draft soal, review admin, lalu publish. Format soal kanonik adalah kolom payload di tabel Question.
- Tulis tangan: kana dulu dengan pencocokan stroke berbasis KanjiVG. Kanji tulis tangan adalah fitur paling berisiko, sediakan cek mandiri (murid menilai sendiri) sebagai cadangan.

## 9. Roadmap

| Fase | Isi | Hasil yang bisa didemokan |
| --- | --- | --- |
| 0 | Fondasi: skeleton, Keycloak, akun oleh admin, i18n + JapaneseText | Login 3 peran, toggle bahasa dan tampilan Jepang |
| 1 | Konten dan kelas: Level/Buku/Bab, upload materi, kelas, unlock bab, jadwal, ICS, pengingat | Sensei upload materi dan buka bab, murid lihat kalender |
| 2 | Latihan mandiri: bank soal, drilling, mastery, kelemahan, progres di sisi sensei | Murid drilling, sensei lihat progres dan kelemahan |
| 3 | Kuis, ujian, tugas, nilai 0–4, komentar | Penilaian lengkap |
| 4 | Leaderboard live dan season | Papan peringkat real-time per level |
| 5 | Tulis tangan, impor PDF, Google Calendar OAuth | Fitur lanjutan |

## 10. Prompt untuk AI coding assistant

Cara pakai: tempel Master prompt di awal setiap sesi dan lampirkan dokumen ini. Lalu kirim satu prompt fase per sesi, sesuai urutan.

### Master prompt

```
Peran: senior .NET engineer dan software architect.
Tugas: bantu saya membangun platform belajar bahasa Jepang untuk komunitas (target JLPT N5 sampai N3). Dokumen blueprint terlampir adalah sumber kebenaran. Kalau ada konflik atau hal yang belum jelas, tanyakan dulu, jangan berasumsi diam-diam.
Stack: .NET LTS terbaru, ASP.NET Core Web API, EF Core + PostgreSQL, Redis, SignalR, YARP gateway, Keycloak (OIDC, realm role admin/sensei/murid), frontend React + TypeScript terpisah, Docker Compose untuk dev.
Aturan kerja:
1. Kerjakan satu fase per sesi, sesuai urutan.
2. Sebelum menulis kode, tulis rencana singkat (file yang dibuat atau diubah, endpoint, tabel) dan tunggu persetujuan saya.
3. Setiap endpoint: otorisasi peran + cek kepemilikan resource (sensei hanya kelasnya, murid hanya datanya).
4. Semua teks yang dibaca pengguna memakai TextBlock dan kunci i18n (bagian 5 blueprint).
5. Aturan bisnis (unlock bab, nilai 0-4, poin leaderboard) wajib punya unit test.
6. Migrasi lewat EF Core, sertakan seed data contoh.
7. Di akhir, jelaskan cara menjalankan dan mengujinya.
8. Jangan menambah fitur di luar blueprint (tanpa video call, listening, speaking, absensi).
```

### Prompt Fase 0 (fondasi)

```
Kerjakan Fase 0. Buat: (1) struktur solution dan docker-compose (PostgreSQL, Redis, Keycloak, gateway YARP, service Identity/Admin); (2) export realm Keycloak: client web-app (public, PKCE), client api, role admin/sensei/murid, satu admin seed; (3) service Identity/Admin: admin membuat akun murid dan sensei lewat Keycloak Admin API (password sementara + required action update password), import massal CSV, reset password, nonaktifkan, profil dengan preferensi bahasa dan tampilan Jepang; (4) frontend: login OIDC, layout berbeda per peran, pengganti bahasa ID/EN, toggle tampilan Jepang, komponen JapaneseText (ruby, kana, romaji) dan sistem i18n berbasis TextBlock. Berikan rencana dulu.
```

### Prompt Fase 1 (konten dan kelas)

```
Kerjakan Fase 1. Service Content: CRUD Level/Buku/Bab (admin), upload materi resumable (protokol tus) sampai 1 GB per file dengan tipe file bebas, unduhan lewat URL bertanda tangan. Service Class: kelas, anggota (sensei dan murid), ClassUnlock per bab, materi hanya bisa diakses murid bila bab terbuka, jadwal pertemuan (topik, platform Zoom/Discord/lainnya, link, waktu), feed ICS per pengguna dengan token, pengingat H-1 hari dan 1 jam via email dan web push (Hangfire atau Quartz). UI sensei: kelas, materi, buka bab, jadwal. UI murid: kelas saya, materi terbuka, kalender dengan tombol tambah ke Google Calendar.
```

### Prompt Fase 2 (latihan mandiri)

```
Kerjakan Fase 2. Service Practice: skema bank soal (Question, QuestionSource) dengan tipe pilihan ganda, tap teks yang benar atau susun kalimat, ketik jawaban (kana via konversi romaji), mencocokkan, dan flashcard. Generator soal otomatis dari data referensi (kana, kosakata, kanji, kalimat) per level dan bab. Engine drilling: murid memilih kategori dan pool dari bab yang terbuka, ulang tanpa batas, tanpa nilai. Catat DrillAnswer dan perbarui ItemMastery tiap jawaban. Endpoint kelemahan (per kategori, tag, baris kana, jenis partikel). UI murid: Latihan dan Kelemahanku. UI sensei: Progres Murid (terakhir aktif, akurasi, item lemah, riwayat sesi) dan komentar tanpa nilai.
```

### Prompt Fase 3 (kuis, ujian, tugas, nilai)

```
Kerjakan Fase 3. Kuis dan ujian dari bank soal: timer, batas percobaan, acak soal, jendela waktu buka dan tutup, penilaian otomatis. Tugas: jawaban teks dan/atau file sampai 1 GB, deadline, kirim ulang, nilai manual sensei dengan komentar. GradeScheme per kelas (bobot kuis, ujian, tugas) dan konversi persen ke skala 0-4 yang bisa diatur (default di blueprint). Hitung ClassGrade otomatis. UI nilai untuk murid dan sensei. Unit test untuk perhitungan nilai.
```

### Prompt Fase 4 (leaderboard live)

```
Kerjakan Fase 4. Service Insight: konsumsi event untuk membuat PointEvent dari drilling, kuis, dan ujian dengan aturan anti-farming di bagian 7. LeaderboardSeason per level (buka, tutup, arsip). Redis sorted set untuk peringkat, SignalR hub untuk update live (top N dan peringkat saya) dengan Redis backplane. Murid hanya melihat nama tampilan dan poin. UI leaderboard dan halaman admin untuk menutup season. Unit test untuk aturan poin.
```

### Prompt Fase 5 (lanjutan)

```
Kerjakan Fase 5. (a) Tulis tangan kana di canvas (mouse atau sentuh) dengan pencocokan stroke berbasis data KanjiVG, lalu kanji sebagai tahap berikutnya dengan opsi cek mandiri bila akurasi belum memadai. (b) Pipeline impor PDF ke draft soal dengan review admin dan kewajiban source/license. (c) Integrasi OAuth Google Calendar (opsional). Berikan rencana dan risiko teknis dulu.
```

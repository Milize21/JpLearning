# 05 · API & Event

## 1. Konvensi umum

| Hal | Aturan |
|---|---|
| Base path | `/api/{modul}` lewat Gateway. Dev: SPA (5173) → proxy Vite → Gateway (8080) |
| Format | JSON UTF-8. Properti **camelCase**; nilai enum **snake_case** (sama dengan DB); waktu ISO 8601 UTC berakhiran `Z`; id berupa string UUID |
| TextBlock/RichText | Dikirim apa adanya sesuai [03](03-skema-data.md) §3 |
| URL | Resource jamak, kebab-case: `/drill-sessions`, `/classes/{id}/grade-scheme` |
| Metode | GET baca · POST buat/aksi · PUT ganti penuh/upsert idempoten · PATCH ubah sebagian · DELETE hapus |
| Aksi non-CRUD | `POST /{resource}/{id}/{aksi}`, mis. `/publish`, `/archive`, `/deactivate`, `/close` |
| Status sukses | 200 baca/ubah · 201 + header `Location` untuk buat · 204 tanpa isi |
| Paginasi | `?page=1&pageSize=20` (maks 100) → `{ items, page, pageSize, totalCount }`. Log memakai cursor: `?cursor=…&limit=50` → `{ items, nextCursor }` |
| Filter & urut | Query eksplisit: `?status=published&sort=-createdAt` |
| Concurrency | Entitas dengan `xmin` mengembalikan field `version`; PATCH/PUT wajib mengirim `version`; beda → 409 `concurrency_conflict` |
| Idempotensi | Endpoint yang mungkin dikirim ulang dari HP (jawaban drilling) menerima id buatan klien |
| Versi API | Tanpa prefix versi. Perubahan breaking dikerjakan bersama frontend di repo yang sama. Event diberi versi (`.v1`) |
| Ukuran body | Maks 1 MB, kecuali upload tus |
| Rate limit (Gateway) 🟡 | Default 300 req/menit per pengguna; jawaban drilling 120/menit; `/ical/*` 60/menit per IP; upload 20/jam. Lewat batas → 429 `rate_limited` + `Retry-After` |

## 2. Format error (RFC 9457 Problem Details)

```json
{
  "type": "https://nomax.local/errors/chapter_locked",
  "title": "Chapter is locked",
  "status": 403,
  "code": "chapter_locked",
  "detail": "Chapter is not unlocked for any of your classes.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": { "displayName": ["must not be empty"] }
}
```

- `code` stabil dan dibaca mesin. Frontend memetakan `code` ke label `errors.<code>` ([06](06-teks-multibahasa.md)). `title`/`detail` berbahasa Inggris untuk developer, **tidak ditampilkan mentah** ke pengguna.
- `errors` hanya ada pada `validation_failed`.

| `code` | HTTP | Kapan |
|---|---|---|
| `validation_failed` | 400 | Input tidak valid |
| `unauthorized` | 401 | Tidak login / token tidak valid |
| `forbidden` | 403 | Peran tidak berhak |
| `account_inactive` | 403 | Akun dinonaktifkan |
| `chapter_locked` | 403 | Bab belum dibuka untuk murid |
| `not_found` | 404 | Tidak ada atau bukan milik pemanggil |
| `method_not_allowed` ➕ | 405 | Metode HTTP tidak tersedia untuk path itu (dibuat framework) |
| `conflict` | 409 | Konflik umum (mis. username sudah dipakai) |
| `concurrency_conflict` | 409 | Data sudah diubah orang lain |
| `has_dependents` | 409 | Masih dipakai data lain |
| `invalid_state` | 409 | Transisi status tidak valid |
| `already_submitted` | 409 | Tugas sudah dikirim dan tidak boleh kirim ulang |
| `attempts_exhausted` | 409 | Percobaan kuis habis |
| `attempt_expired` | 409 | Waktu attempt habis |
| `quiz_not_open` | 409 | Di luar jendela kuis |
| `session_ended` | 409 | Sesi drilling sudah berakhir |
| `empty_pool` | 409 | Tidak ada soal yang cocok untuk drilling |
| `file_not_ready` | 409 | File belum selesai diunggah |
| `payload_too_large` ➕ | 413 | Body request melebihi 1 MB (§1), di luar upload tus |
| `file_too_large` | 413 | Melebihi 1 GiB |
| `unsupported_media_type` ➕ | 415 | `Content-Type` request tidak didukung (dibuat framework) |
| `rate_limited` | 429 | Terlalu banyak request |
| `keycloak_unavailable` | 503 | Keycloak tidak bisa dihubungi |
| `internal_error` | 500 | Kesalahan tak terduga |

Kode baru wajib ditambahkan ke tabel ini dan ke label `errors.*` di frontend.

Respons yang dibuat framework tanpa kode dari handler (routing, autentikasi, batas Kestrel, Gateway) diberi kode dari status HTTP-nya: 400 `validation_failed`, 401 `unauthorized`, 403 `forbidden`, 404 `not_found`, 405 `method_not_allowed`, 409 `conflict`, 413 `payload_too_large`, 415 `unsupported_media_type`, 429 `rate_limited`, lainnya `internal_error`. Setiap problem response juga memuat `traceId`; detail exception tidak pernah dikirim ke klien.

## 3. Autentikasi

- Semua `/api/*` memakai `Authorization: Bearer <access_token>`.
- Tanpa JWT: `GET /ical/{token}.ics` (token di URL), `/health/*`, preflight tus/CORS.
- SignalR: `?access_token=…` hanya diterima pada `/hubs/*`.

## 4. Katalog endpoint — Identity (`/api/identity`)

Katalog ini adalah **daftar awal**. Endpoint boleh bertambah, tetapi wajib dicatat di sini lebih dulu. "Pengajar" = pengajar kelas terkait; "pemilik" = sesuai [01](01-peran-dan-akses.md) §2.

| Metode | Path | Peran | Aturan / catatan | Fase |
|---|---|---|---|---|
| GET | `/me` | semua | Profil + preferensi sendiri | 0 |
| PATCH | `/me/preferences` | semua | `uiLocale`, `scriptPref`, `timezone` (BR-AKUN-07) | 0 |
| GET | `/users` | admin | Filter `role`, `q`, `isActive`; paginasi | 0 |
| POST | `/users` | admin | BR-AKUN-02; respons memuat password sementara sekali | 0 |
| GET | `/users/{id}` | admin | | 0 |
| PATCH | `/users/{id}` | admin | `displayName`, `email`, `timezone` | 0 |
| POST | `/users/import?dryRun=true\|false` | admin | CSV multipart (BR-AKUN-03) | 0 |
| POST | `/users/{id}/reset-password` | admin | BR-AKUN-04 | 0 |
| POST | `/users/{id}/deactivate` | admin | BR-AKUN-05 | 0 |
| POST | `/users/{id}/activate` | admin | BR-AKUN-05 | 0 |

Rincian kontrak Identity (diimplementasikan Fase 0.2; skema lengkap di `/openapi/identity.json`):

- Profil (`/me`, `/users/*`): `{ id, username, email, displayName, role, uiLocale, scriptPref, timezone, isActive, createdAt }`.
- `GET /users`: `role` (`admin`/`sensei`/`murid`), `q` (cari di username, nama tampilan, email; `%` dan `_` dicari apa adanya), `isActive`, `page`, `pageSize`; urut nama tampilan lalu username.
- `POST /users` → 201 `{ user, temporaryPassword }`; username/email yang sudah dipakai → 409 `conflict`; Keycloak tidak bisa dihubungi → 503 `keycloak_unavailable` dan tidak ada yang tersimpan.
- `PATCH /me/preferences` dan `PATCH /users/{id}`: hanya field yang dikirim yang berubah; `email: ""` menghapus email.
- `POST /users/{id}/reset-password` → 200 `{ temporaryPassword }`.
- `POST /users/{id}/deactivate`: menonaktifkan diri sendiri atau admin aktif terakhir → 409 `invalid_state`.
- `POST /users/import`: multipart field `file`; `dryRun` default `true`. Respons `{ dryRun, totalRows, errors: [{ row, column, code }], toCreate, skipped, created, failure: { row, username, code } | null, passwordsCsv }`. `row` dihitung seperti spreadsheet (header = baris 1). Kode error per sel: `empty_file`, `missing_column`, `unknown_column`, `too_many_rows`, `required`, `invalid_format`, `too_long`, `invalid_role`, `invalid_timezone`, `invalid_locale`, `duplicate_in_file`, `email_taken` (label frontend: `identity.importErrors.*`).

## 5. Katalog endpoint — Content (`/api/content`)

| Metode | Path | Peran | Aturan / catatan | Fase |
|---|---|---|---|---|
| GET | `/levels` | semua | Level + buku | 1 |
| POST · PATCH · DELETE | `/levels`, `/levels/{id}` | admin | BR-KONTEN-03 | 1 |
| GET | `/books/{id}` | semua | Daftar bab + `isUnlocked` untuk murid | 1 |
| POST · PATCH · DELETE | `/books`, `/books/{id}` | admin | | 1 |
| GET | `/chapters/{id}` | semua | Murid: wajib akses bab | 1 |
| POST · PATCH · DELETE | `/chapters`, `/chapters/{id}` | admin | | 1 |
| GET | `/chapters/{id}/materials` | semua | Murid: BR-KONTEN-01 | 1 |
| POST | `/chapters/{id}/materials` | staf | | 1 |
| PATCH · DELETE | `/materials/{id}` | pembuat, admin | BR-KONTEN-02 | 1 |
| GET | `/materials/{id}/download` | semua | BR-FILE-04 → 302 ke URL bertanda tangan | 1 |
| POST · HEAD · PATCH · DELETE | `/uploads`, `/uploads/{id}` | sesuai purpose | Protokol tus (§13), BR-FILE-06 | 1 |
| GET | `/files/{id}/content?exp=…&sig=…` | tanda tangan | Header BR-FILE-02 | 1 |
| GET | `/reference/{type}?levelId=&chapterId=` | semua | `type`: `kana`, `kanji`, `vocab`, `grammar`, `sentences`; murid hanya bab terbuka | 1 |
| POST | `/reference/{type}/import` | admin | CSV/JSON + `source` + `license` wajib | 1 |
| PATCH · DELETE | `/reference/{type}/{id}` | admin | | 1 |

## 6. Katalog endpoint — Classroom (`/api/classroom`, `/ical`)

| Metode | Path | Peran | Aturan / catatan | Fase |
|---|---|---|---|---|
| GET | `/classes` | semua | Admin: semua; sensei: yang diajar; murid: keanggotaan aktif | 1 |
| POST | `/classes` | staf | Pembuat menjadi pengajar utama | 1 |
| GET · PATCH | `/classes/{id}` | anggota (GET), pengajar, admin | | 1 |
| POST | `/classes/{id}/archive` | pengajar utama, admin | | 1 |
| GET · POST | `/classes/{id}/students` | pengajar, admin | Tambah murid berdasarkan daftar id | 1 |
| DELETE | `/classes/{id}/students/{studentId}` | pengajar, admin | Status `removed` | 1 |
| GET · POST · DELETE | `/classes/{id}/teachers[/{teacherId}]` | pengajar utama, admin | BR-AKSES-03 | 1 |
| GET | `/classes/{id}/unlocks` | anggota, pengajar, admin | | 1 |
| PUT | `/classes/{id}/unlocks/{chapterId}` | pengajar, admin | Buka bab (idempoten) → `ChapterUnlocked` | 1 |
| DELETE | `/classes/{id}/unlocks/{chapterId}` | pengajar, admin | Tutup bab → `ChapterLocked` | 1 |
| GET | `/classes/{id}/sessions?from=&to=` | anggota, pengajar, admin | Link sesuai BR-JADWAL-02 | 1 |
| POST | `/classes/{id}/sessions` | pengajar, admin | BR-JADWAL-01, 05, 06 | 1 |
| PATCH | `/sessions/{id}` | pengajar, admin | | 1 |
| POST | `/sessions/{id}/cancel` | pengajar, admin | | 1 |
| GET | `/me/schedule?from=&to=` | semua | Gabungan semua kelas | 1 |
| POST · DELETE | `/me/calendar-token` | semua | Buat/rotasi (URL tampil sekali) atau cabut (BR-JADWAL-04) | 1 |
| GET · POST | `/me/push-subscriptions` | semua | Simpan langganan web push perangkat | 1 |
| DELETE | `/me/push-subscriptions/{id}` | semua | | 1 |
| GET | `/push/public-key` | semua | VAPID public key | 1 |
| GET | `/ical/{token}.ics` | publik (token) | BR-JADWAL-03 | 1 |

## 7. Katalog endpoint — Practice (`/api/practice`)

**Bank soal**

| Metode | Path | Peran | Aturan / catatan | Fase |
|---|---|---|---|---|
| GET | `/questions?levelId=&chapterId=&category=&status=&scope=&q=` | staf | Sensei: `builtin` + milik sendiri; admin: semua | 2 |
| POST | `/questions` | staf | Validasi payload per `kind` ([03](03-skema-data.md) §8) | 2 |
| GET · PATCH | `/questions/{id}` | pemilik, admin | BR-SOAL-04 | 2 |
| POST | `/questions/{id}/submit-review` · `/publish` · `/archive` · `/duplicate` | pemilik, admin | BR-SOAL-02 | 2 |
| POST | `/questions/{id}/preview-answer` | staf | Evaluasi tanpa menyimpan (uji coba) | 2 |
| GET · POST · PATCH | `/sources`, `/sources/{id}` | admin | | 2 |
| POST | `/generator/runs` | admin | `levelId`, `chapterIds`, `templates` → draf `review` (BR-SOAL-05) | 2 |

**Drilling & progres**

| Metode | Path | Peran | Aturan / catatan | Fase |
|---|---|---|---|---|
| POST | `/drill-sessions` | murid | `{category, chapterIds?, mode, inputMode, classId?}` → `{sessionId, poolSize, fallback?, batch}` | 2 |
| GET | `/drill-sessions/{id}/next?count=10` | pemilik | Batch soal versi aman | 2 |
| POST | `/drill-sessions/{id}/answers` | pemilik | `{answerId, questionId, inputMode, answer, responseMs}` → `{isCorrect, correctAnswer, explanation, mastery: {before, after, status}}`; idempoten | 2 |
| POST | `/drill-sessions/{id}/end` | pemilik | Ringkasan sesi | 2 |
| GET | `/me/drill-sessions` · `/me/weaknesses` · `/me/mastery?chapterId=&category=` | murid | BR-MASTERY-02, 03 | 2 |
| POST | `/preview/drill` | staf | Mode uji coba (BR-AKSES-05) | 2 |
| GET | `/classes/{id}/progress` | pengajar, admin | Per murid: terakhir latihan, jawaban 7 hari, akurasi, rata-rata mastery, kelompok lemah | 2 |
| GET | `/students/{id}/weaknesses?classId=` · `/students/{id}/drill-sessions` | SenseiDari, admin | | 2 |
| GET | `/comments?targetType=&targetId=` | murid (miliknya), staf | | 2 |
| POST | `/comments` | staf | Komentar tanpa nilai | 2 |

**Kuis, tugas, nilai**

| Metode | Path | Peran | Aturan / catatan | Fase |
|---|---|---|---|---|
| GET · POST | `/classes/{id}/quizzes` | pengajar, admin (murid: GET yang terbit) | | 3 |
| GET · PATCH | `/quizzes/{id}` | pengajar, admin (murid: info tanpa soal) | BR-KUIS-10 | 3 |
| PUT | `/quizzes/{id}/questions` | pengajar, admin | Daftar soal + poin + urutan | 3 |
| POST | `/quizzes/{id}/publish` · `/archive` | pengajar, admin | | 3 |
| POST | `/quizzes/{id}/attempts` | murid | Mulai/lanjutkan (BR-KUIS-01, 02) | 3 |
| GET | `/attempts/{id}` | pemilik, pengajar, admin | Murid: soal versi aman; pembahasan sesuai BR-KUIS-09 | 3 |
| PUT | `/attempts/{id}/answers/{questionId}` | pemilik | Autosave (BR-KUIS-06) | 3 |
| POST | `/attempts/{id}/submit` | pemilik | | 3 |
| POST | `/attempts/{id}/answers/{questionId}/grade` | pengajar, admin | Nilai `long_answer` | 3 |
| GET | `/quizzes/{id}/attempts` · `/me/quizzes` | pengajar, admin · murid | | 3 |
| GET · POST | `/classes/{id}/assignments` | pengajar, admin (murid: GET yang terbit) | | 3 |
| GET · PATCH | `/assignments/{id}` | pengajar, admin (murid: GET) | | 3 |
| POST | `/assignments/{id}/publish` · `/archive` | pengajar, admin | | 3 |
| PUT | `/assignments/{id}/submission` | murid | Kirim/kirim ulang (BR-TUGAS-01…03) | 3 |
| GET | `/assignments/{id}/submissions` · `/submissions/{id}` | pengajar, admin · pemilik | | 3 |
| GET | `/submissions/{id}/files/{fileId}/download` | pemilik, pengajar, admin | 302 ke URL bertanda tangan | 3 |
| POST | `/submissions/{id}/grade` | pengajar, admin | BR-TUGAS-04 | 3 |
| GET | `/me/assignments` · `/me/grades` | murid | | 3 |
| GET · PUT | `/classes/{id}/grade-scheme` | pengajar, admin | BR-NILAI-06 | 3 |
| GET | `/classes/{id}/grades` | pengajar, admin | | 3 |

## 7a. Katalog endpoint — Insight (`/api/insight`, `/hubs/leaderboard`)

| Metode | Path | Peran | Aturan / catatan | Fase |
|---|---|---|---|---|
| GET | `/leaderboards?levelId=` | semua | Season aktif, top N, peringkat saya (murid: level kelasnya) | 4 |
| GET | `/seasons?levelId=` | semua | Season aktif dan arsip | 4 |
| GET | `/seasons/{id}/standings?page=` | semua | Arsip peringkat | 4 |
| POST | `/seasons/{id}/close` | admin | BR-SEASON-02 | 4 |
| GET | `/me/activity?cursor=` | semua | | 4 |
| GET | `/students/{id}/activity?cursor=` | SenseiDari, admin | | 4 |
| GET | `/classes/{id}/activity-summary` | pengajar, admin | Terakhir aktif per murid | 4 |

## 8. Integration event

Amplop setiap event (disimpan di `outbox_messages`):

```json
{ "eventId": "0192f1e2-…", "type": "AnswerSubmitted.v1", "occurredAt": "2026-10-18T12:00:00Z", "payload": { } }
```

| Event | Produsen | Konsumen | Payload | Fase |
|---|---|---|---|---|
| `UserAccountCreated.v1` ➕ | Identity | Insight (log) | `userId, role, displayName, createdBy` | 1 |
| `UserDeactivated.v1` ➕ | Identity | Insight (sembunyikan dari leaderboard), Classroom (batalkan notifikasi) | `userId` | 1 |
| `UserReactivated.v1` ➕ | Identity | Insight | `userId` | 1 |
| `UserDisplayNameChanged.v1` ➕ | Identity | Insight (cache nama) | `userId, displayName` | 1 |
| `ChapterUnlocked.v1` ✅ | Classroom | Insight (log) | `classId, chapterId, unlockedBy, unlockedAt` | 1 |
| `ChapterLocked.v1` ➕ | Classroom | Insight (log) | `classId, chapterId, lockedBy, lockedAt` | 1 |
| `SessionCreated.v1` ✅ | Classroom | Insight (log) | `sessionId, classId, startsAt, endsAt, sequence` | 1 |
| `SessionUpdated.v1` ➕ · `SessionCancelled.v1` ➕ | Classroom | Insight (log) | sama seperti di atas | 1 |
| `AnswerSubmitted.v1` ✅ | Practice | Insight (poin, log) | `answerId, sessionId, studentId, levelId, chapterId, questionId, itemType, itemId, category, isCorrect, inputMode, answeredAt` | 2 |
| `DrillSessionEnded.v1` ➕ | Practice | Insight (log) | `sessionId, studentId, answerCount, correctCount, startedAt, endedAt` | 2 |
| `QuizGraded.v1` ✅ | Practice | Insight (poin, log) | `attemptId, quizId, quizKind, classId, levelId, studentId, scorePct, attemptNo, gradedAt` | 3 |
| `SubmissionGraded.v1` ✅ | Practice | Insight (log) | `submissionId, assignmentId, classId, studentId, gradePct, gradedBy, gradedAt` | 3 |
| `SeasonClosed.v1` ➕ | Insight | UI lewat hub | `seasonId, levelId, newSeasonId, closedAt` | 4 |
| `MaterialViewed.v1` ➕ (opsional) | Content | Insight (log) | `materialId, chapterId, userId, viewedAt`; maks 1 per materi per pengguna per jam | 4 |

Aturan:
- `AnswerSubmitted` hanya untuk jawaban **drilling**. Jawaban kuis tidak menerbitkan event per jawaban (poin kuis berasal dari `QuizGraded`).
- Perubahan payload yang hanya menambah field boleh di versi yang sama. Perubahan breaking → versi baru (`.v2`), keduanya diterbitkan sampai semua konsumen pindah.
- Konsumen wajib idempoten (inbox).
- Mode uji coba staf tidak menerbitkan event apa pun.

## 9. SignalR — `/hubs/leaderboard`

| Arah | Nama | Isi |
|---|---|---|
| klien → server | `JoinSeason(seasonId)` | Server memeriksa hak lihat, memasukkan ke grup `season:{seasonId}`, lalu mengembalikan snapshot awal |
| klien → server | `LeaveSeason(seasonId)` | |
| server → grup | `LeaderboardUpdated` | `{ seasonId, top: [{ rank, entryKey, displayName, points }], updatedAt }` |
| server → pengguna | `MyRankUpdated` | `{ seasonId, rank, points, entryKey }` |
| server → pengguna | `PointsAwarded` | `{ points, rule, sourceType }` (dipakai layar drilling untuk animasi "+1") |
| server → grup | `SeasonClosed` | `{ seasonId, newSeasonId }` |

- `entryKey` = kunci samaran per season (HMAC dari `seasonId` + `studentId`, dipotong). Murid mengenali barisnya sendiri dari `MyRankUpdated` tanpa melihat id pengguna lain.
- Broadcast grup maksimal 1 kali per detik per season (BR-SEASON-04).

## 10. Jenis notifikasi

| `type` | Kapan | Kanal | Isi utama |
|---|---|---|---|
| `session_created` | Sesi baru | email, web push | Kelas, topik, waktu (zona penerima), platform, tautan ke halaman jadwal |
| `session_updated` | Sesi diubah | email, web push | Yang berubah (sebelum → sesudah) |
| `session_cancelled` | Sesi dibatalkan | email, web push | Kelas, topik, waktu semula |
| `session_reminder_1d` | 24 jam sebelum | email, web push | Ringkasan sesi |
| `session_reminder_1h` | 1 jam sebelum | email, web push | Ringkasan + link meeting (bila berhak) |

Payload web push: `{ title, body, url }` dalam bahasa penerima. Email: subjek + versi teks + versi HTML bermerek NomaX.

## 11. Jenis log aktivitas (`insight.activity_logs.type`)

| `type` | Sumber event |
|---|---|
| `user_created`, `user_deactivated`, `user_reactivated` | Identity |
| `chapter_unlocked`, `chapter_locked` | Classroom |
| `session_created`, `session_updated`, `session_cancelled` | Classroom |
| `drill_session_ended` | Practice |
| `quiz_graded`, `submission_graded` | Practice |
| `season_closed` | Insight |
| `material_viewed` (opsional) | Content |

"Terakhir aktif" seorang murid = `max(ts)` dari log aktivitas miliknya. Sebelum Fase 4, halaman progres memakai waktu jawaban drilling terakhir dari modul Practice.

## 12. Feed ICS — `GET /ical/{token}.ics`

Isi mengikuti BR-JADWAL-03. Contoh satu event:

```
BEGIN:VEVENT
UID:0192f1e2-7c1a-7b3e-9a40-5d2c8e1f0a11@nomax
SEQUENCE:2
DTSTAMP:20261018T120000Z
DTSTART:20261021T120000Z
DTEND:20261021T133000Z
SUMMARY:[N5 Sabtu Pagi] Bab 3 – Partikel を
DESCRIPTION:Platform: Zoom\nLink: https://zoom.us/j/123\nCatatan: Siapkan buku bab 3
URL:https://zoom.us/j/123
STATUS:CONFIRMED
END:VEVENT
```

UX di frontend:
- Tombol per sesi **"Tambah ke Google Calendar"** memakai URL template `https://calendar.google.com/calendar/render?action=TEMPLATE&text=…&dates=…/…&details=…`.
- Kartu **"Langganan kalender"** menampilkan URL ICS (salin) dan panduan: Google Calendar → *Kalender lain* → *Dari URL*.
- Beri tahu pengguna bahwa Google Calendar memperbarui kalender langganan secara berkala (bisa beberapa jam), sehingga pengingat yang andal tetap lewat email dan web push.

## 13. Upload tus — `/api/content/uploads`

| Hal | Aturan |
|---|---|
| Protokol | tus 1.0.0, ekstensi `creation`, `termination`, `expiration` |
| Auth | Header Bearer; klien memperbarui token di `onBeforeRequest` (`tus-js-client`) |
| Metadata | `filename`, `filetype`, `purpose` (`material` · `book_cover` · `submission` · `question_import`) |
| Validasi awal | `Upload-Length` ≤ `Upload:MaxFileBytes`; purpose sesuai peran (BR-FILE-06); rate limit |
| Id | Server membuat UUIDv7 = `stored_files.id`; `Location: /api/content/uploads/{id}` |
| Selesai | File dipindah ke storage, SHA-256 dihitung, status `ready`. Lalu klien memanggil API resource pemilik dengan `fileId` (mis. membuat materi, mengirim tugas) |
| Klien | Potongan 8 MB; `retryDelays: [0, 1000, 3000, 5000, 10000]`; progres dan tombol lanjut/batal di UI |

## 14. OpenAPI & klien frontend

- Setiap modul mendaftarkan endpoint ke dokumen OpenAPI sendiri: `/openapi/identity.json`, `/openapi/content.json`, `/openapi/classroom.json`, `/openapi/practice.json`, `/openapi/insight.json`.
- `npm --prefix web run gen:api` membuat `web/src/lib/api/schema/{modul}.ts` dengan `openapi-typescript`. File hasil generate di-commit dan **tidak diedit manual**.
- Klien `openapi-fetch` dengan middleware: menambahkan Bearer token, mencoba refresh saat 401 lalu mengarahkan ke login bila gagal, dan mengubah Problem Details menjadi error bertipe (`ApiError` dengan `code`).
- Setiap perubahan endpoint: perbarui OpenAPI → generate ulang tipe → perbaiki error TypeScript yang muncul.

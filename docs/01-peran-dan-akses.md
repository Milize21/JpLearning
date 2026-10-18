# 01 · Peran & Hak Akses

## 1. Peran

| Peran (realm role Keycloak) | Konstanta kode | Deskripsi |
|---|---|---|
| `admin` | `Roles.Admin` | Pengelola komunitas. Memiliki semua hak sensei (A7) ditambah pengelolaan akun, struktur konten, bank soal bawaan, dan season |
| `sensei` | `Roles.Sensei` | Pengajar. Mengelola kelas yang ia ajar |
| `murid` | `Roles.Murid` | Pelajar. Mengakses kelas tempat ia menjadi anggota |

Aturan akun ✅:
- **Satu akun = satu peran** (akun terpisah). Orang yang sama yang menjadi sensei dan murid memakai dua akun.
- **Akun dibuat admin.** Registrasi mandiri dimatikan di Keycloak.
- **Password hanya disimpan di Keycloak.** Database aplikasi tidak pernah menyimpan password atau hash-nya.

## 2. Relasi kepemilikan (dipakai di seluruh dokumen)

| Notasi | Definisi | Sumber data |
|---|---|---|
| `Mengajar(S, K)` | Sensei/admin S tercatat sebagai pengajar kelas K | `classroom.class_teachers` |
| `Anggota(M, K)` | Murid M anggota aktif kelas K | `classroom.class_students` dengan `status = 'active'` |
| `SenseiDari(S, M)` | Ada kelas K dengan `Mengajar(S, K)` dan `Anggota(M, K)` | gabungan dua tabel di atas |
| `BabTerbuka(M, C)` | Ada kelas K dengan `Anggota(M, K)` dan baris `class_unlocks(K, C)`; berlaku gabungan lintas kelas | BR-AKSES-01 |
| `Pemilik(U, R)` | Kolom pemilik resource R bernilai U (`created_by`, `owner_id`, `uploaded_by`, `student_id`, sesuai entitas) | tabel resource |

Admin melewati semua cek kepemilikan, tetapi aksi administratifnya tetap tercatat di log aktivitas.

## 3. Matriks kemampuan (v1 diperinci)

| Kemampuan | Admin | Sensei | Murid | Rujukan |
|---|---|---|---|---|
| Membuat akun, impor CSV, reset password, nonaktifkan/aktifkan | ✓ | – | – | BR-AKUN-01…05 |
| Mengubah preferensi sendiri (bahasa UI, tampilan Jepang, zona waktu) | ✓ | ✓ | ✓ | BR-AKUN-07 |
| Mengubah nama tampilan | ✓ (semua akun) | – | – | D-21 🟡 |
| Kelola Level/Buku/Bab dan data referensi | ✓ | – | – | BR-KONTEN-03 |
| Kelola bank soal bawaan (`scope = builtin`), sumber soal, impor | ✓ | – | – | BR-SOAL |
| Membuat soal sendiri (`scope = private`) | ✓ | ✓ (milik sendiri) | – | BR-SOAL-03 |
| Membuat kelas | ✓ | ✓ (otomatis jadi pengajar utama) | – | |
| Mengatur anggota dan pengajar kelas | ✓ | kelas yang diajar | – | |
| Upload materi | ✓ | ✓ | – | |
| Mengubah/menghapus materi | ✓ | milik sendiri | – | BR-KONTEN-02 |
| Buka/tutup bab per kelas | ✓ | kelas yang diajar | – | BR-AKSES-01, 02 |
| Jadwal pertemuan + link Zoom/Discord | ✓ | kelas yang diajar | lihat (kelasnya) | BR-JADWAL-02 |
| Menyusun kuis, ujian, tugas dari bank soal | ✓ | kelas yang diajar | – | BR-KUIS, BR-TUGAS |
| Menilai tugas dan jawaban esai, memberi komentar | ✓ | kelas yang diajar | – | BR-TUGAS-04 |
| Mengatur skema nilai kelas | ✓ | kelas yang diajar | – | BR-NILAI-06 |
| Melihat progres dan kelemahan | semua murid | murid di kelasnya | diri sendiri | BR-MASTERY |
| Drilling, kuis, tugas | uji coba | uji coba | ✓ | BR-AKSES-05 |
| Melihat leaderboard | semua level | semua level | level kelasnya (Q-06) | BR-SEASON-05 |
| Menutup season | ✓ | – | – | D-18 🟡 |

## 4. Visibilitas data per entitas

| Data | Murid | Sensei | Admin |
|---|---|---|---|
| Profil pengguna lain | nama tampilan saja (leaderboard) | profil dasar murid dan rekan pengajar di kelasnya (tanpa email) | semua |
| Email pengguna | milik sendiri | – | semua |
| Materi | bab terbuka dan sudah terbit (`publish_at`) | semua | semua |
| Soal bank | hanya saat mengerjakan, **tanpa kunci jawaban** | builtin + milik sendiri | semua |
| Jawaban drilling, mastery, kelemahan | milik sendiri | murid di kelasnya | semua |
| Kuis, ujian, tugas | kelasnya, status `published` | kelas yang diajar | semua |
| Percobaan kuis dan submission tugas | milik sendiri | kelas yang diajar | semua |
| Nilai kelas | milik sendiri | kelas yang diajar | semua |
| Komentar | yang ditujukan kepadanya | yang ia tulis + yang ada di kelas yang diajar | semua |
| Link meeting | kelasnya | kelas yang diajar | semua |
| Log aktivitas | milik sendiri | murid di kelasnya | semua |
| Leaderboard | nama tampilan + poin + peringkat | sama | sama |

## 5. Implementasi otorisasi (dua lapis) ✅

**Lapis 1 — policy peran di endpoint**

| Policy | Peran yang lolos |
|---|---|
| `Authenticated` | semua pengguna login dengan profil aktif |
| `MuridOnly` | murid |
| `StaffOnly` | admin, sensei |
| `AdminOnly` | admin |

**Lapis 2 — cek kepemilikan resource di handler**

1. Handler memuat resource (atau memfilter query) berdasarkan relasi di §2.
2. Cek akses lewat layanan akses, terutama `IClassAccessReader` dari `Nomax.Modules.Classroom.Contracts` (lihat [02](02-arsitektur.md) §6).
3. Bila gagal, kembalikan kode sesuai §6.
4. **Query daftar selalu difilter di SQL** berdasarkan akses. Dilarang mengambil semua data lalu memfilter di memori.
5. Setiap endpoint baru wajib punya tes integrasi otorisasi: peran salah → 403, bukan pemilik → 404 (lihat [09](09-kualitas-dan-keamanan.md)).

## 6. Kode respons penolakan

| Situasi | HTTP | `code` |
|---|---|---|
| Tidak login atau token tidak valid | 401 | `unauthorized` |
| Peran tidak berhak atas endpoint | 403 | `forbidden` |
| Resource milik orang lain atau tidak ada (mencegah enumerasi) | 404 | `not_found` |
| Bab terkunci untuk murid | 403 | `chapter_locked` |
| Akun dinonaktifkan | 403 | `account_inactive` |

## 7. Mode uji coba (preview) untuk staf 🟡

Admin dan sensei bisa mencoba drilling dan kuis dari sisi murid. Jawaban dievaluasi dan umpan balik tampil, tetapi **tidak disimpan**: tidak ada `drill_sessions`, `drill_answers`, `item_masteries`, `quiz_attempts`, poin, atau log aktivitas (BR-AKSES-05, D-22).

## 8. Konfigurasi Keycloak ✅/🟡

| Item | Nilai |
|---|---|
| Realm | `nomax` |
| Registrasi mandiri | OFF |
| Login | username atau email |
| Brute-force detection | ON |
| Password policy 🟡 | minimal 8 karakter, tidak sama dengan username |
| Client `nomax-web` | public, Standard Flow, PKCE `S256`; redirect `http://localhost:5173/*` (dev); web origins sama; audience mapper menambahkan `nomax-api` ke `aud` |
| Client `nomax-api` | confidential, Service Account ON, role `realm-management`: `manage-users`, `view-users`, `query-users`; dipakai modul Identity untuk Keycloak Admin API |
| Realm roles | `admin`, `sensei`, `murid` |
| Claim | `realm_access.roles` dipetakan ke role claim di API; pastikan claim `sub` ada (client scope `basic`) |
| Akun baru | password sementara + required action `UPDATE_PASSWORD` |
| Profil pengguna (user profile) ➕ 🟡 | `email`, `firstName`, `lastName` opsional dan hanya bisa diubah admin, karena aplikasi menyimpan salinannya di `identity.user_profiles` dan email memang opsional (BR-AKUN-02). Tanpa ini Keycloak 24+ memaksa pengguna melengkapi profil saat login (D-30) |
| Masa token 🟡 | access token 5 menit; SSO session idle 7 hari; SSO session max 30 hari; refresh token rotation ON |
| Bahasa halaman login | internationalization ON (EN, ID bila tersedia) |
| Tema login | tema bermerek NomaX (logo + warna) — opsional di Fase 0 |

## 9. Akun nonaktif & sinkronisasi profil

- Menonaktifkan akun: Keycloak `enabled = false` + logout semua sesi + `identity.user_profiles.is_active = false`.
- API menolak request dari profil nonaktif dengan 403 `account_inactive`. Status `is_active` di-cache maksimal 60 detik.
- Role di token adalah sumber otorisasi. `user_profiles.role` adalah salinan yang disinkronkan saat akun dibuat atau diubah admin.
- `user_profiles.id` sama dengan `sub` Keycloak. Profil dibuat bersamaan dengan pembuatan akun oleh admin (BR-AKUN-02).

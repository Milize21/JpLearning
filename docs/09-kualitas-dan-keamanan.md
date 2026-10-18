# 09 · Kualitas & Keamanan

## 1. Strategi tes

| Lapisan | Alat | Yang dites | Kapan |
|---|---|---|---|
| Unit backend | xUnit, NSubstitute, Shouldly, `FakeTimeProvider` | Semua aturan `BR-*`, validator, kalkulator domain, mapping | Setiap perubahan |
| Integrasi API | `WebApplicationFactory` + Testcontainers (PostgreSQL, Redis) | Endpoint, otorisasi, migrasi, query | Setiap endpoint baru |
| Arsitektur | NetArchTest (fork `NetArchTest.eNhancedEdition`, MIT; paket aslinya tidak dirawat sejak 2021) + cek `ProjectReference` | Aturan dependency antar-modul ([02](02-arsitektur.md) §6.1) | Selalu |
| Unit frontend | Vitest + Testing Library | Parser furigana, romaji, label, komponen Jepang, hook | Setiap perubahan |
| E2E | Playwright + `@axe-core/playwright` | Alur utama per fase + aksesibilitas | Akhir sub-langkah |

Konvensi:
- **Nama tes memuat ID aturan**: `BR_POIN_03_halves_points_after_third_correct_answer_same_day`.
- Kasus uji di [04](04-aturan-bisnis.md) wajib menjadi tes (data minimum).
- Waktu selalu lewat `TimeProvider` sehingga tes bisa mengatur jam (hari WIB, tenggat, timer).
- Tes integrasi memakai PostgreSQL sungguhan (Testcontainers), JWT uji yang ditandatangani kunci lokal, dan Keycloak Admin API yang di-mock lewat interface. Tidak ada tes yang memanggil internet.
- **Matriks otorisasi** untuk setiap endpoint: tanpa login → 401; peran salah → 403; bukan pemilik → 404; pemilik/peran benar → 2xx.
- Target cakupan (panduan): `Domain/` ≥ 90% baris; backend keseluruhan ≥ 70%; `web/src/i18n` dan `components/japanese` ≥ 90%.

## 2. Gerbang kualitas (sebelum tugas dinyatakan selesai)

```
dotnet build Nomax.slnx                      # warning = error
dotnet format Nomax.slnx --verify-no-changes
dotnet test Nomax.slnx
npm --prefix web run typecheck
npm --prefix web run lint
npm --prefix web run lint:tokens
npm --prefix web run test
npm --prefix web run build
npm --prefix web run e2e                     # alur yang terdampak
dotnet list Nomax.slnx package --vulnerable --include-transitive   # tidak ada high/critical
npm --prefix web audit --omit=dev            # tidak ada high/critical
```

Nama perintah final ditetapkan di Fase 0 dan dicatat di `CLAUDE.md` (perintah backend sudah diuji di Fase 0.1; perintah `web` menyusul di 0.3). CI (mis. GitHub Actions) menjalankan gerbang yang sama setelah repo punya remote.

## 3. Model ancaman ringkas

Aset: akun dan sesi, data pribadi (email, nama), nilai dan progres, file tugas, link meeting, integritas leaderboard.

| Ancaman | Mitigasi |
|---|---|
| Mengakses data murid lain (IDOR) | Otorisasi dua lapis, 404 untuk bukan pemilik, query terfilter di SQL, matriks tes otorisasi |
| Eskalasi peran | Peran hanya dari token Keycloak; hanya admin yang membuat akun dan menetapkan peran |
| XSS lewat konten, komentar, atau nama file | Escaping React; Markdown tanpa HTML mentah; file selalu diunduh sebagai attachment; CSP ketat |
| File berbahaya (tipe bebas) | Tidak pernah dirender inline; `nosniff`; storage di luar web root; nama acak; pemindaian antivirus opsional |
| Pencurian token | Token di memori/sessionStorage; access token 5 menit; refresh rotation; CSP; token di URL hanya untuk hub dan ICS |
| Bocornya kunci jawaban | Versi aman soal ([03](03-skema-data.md) §8.3), evaluasi hanya di server |
| Farming atau curang poin | Evaluasi server, anti-farming BR-POIN, idempotensi, rate limit, penilaian diri = 0 poin |
| Bocornya link meeting | Hanya untuk anggota (BR-JADWAL-02); token ICS bisa dirotasi |
| Brute force login | Brute-force detection Keycloak |
| Penyalahgunaan upload | Rate limit upload, batas ukuran, pembersihan file yatim |
| Injeksi SQL | EF Core dengan parameter; dilarang menyusun SQL dari string |
| CSRF | API memakai Bearer token (bukan cookie), sehingga tidak rentan CSRF klasik |
| Rahasia bocor | Tidak ada rahasia di repo; user-secrets/env; pemindaian rahasia opsional (gitleaks) |

## 4. Checklist keamanan

- **Auth & otorisasi**: policy peran + cek kepemilikan di setiap endpoint; profil nonaktif ditolak; SignalR hanya menerima token di `/hubs/*`.
- **Input**: validasi di setiap request (FluentValidation); batas panjang teks; URL hanya `https://`; TextBlock/RichText divalidasi.
- **File**: BR-FILE-01…06; header unduhan; URL bertanda tangan berumur pendek.
- **Header HTTP** (Gateway): `Strict-Transport-Security` (produksi), `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: camera=(), microphone=(), geolocation=()`, `Content-Security-Policy` awal: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self' <origin-keycloak> wss:; frame-ancestors 'none'; base-uri 'self'` untuk halaman SPA (dipasang saat Gateway menyajikan SPA). Respons API, ICS, dan redirect memakai CSP `default-src 'none'; frame-ancestors 'none'; base-uri 'none'` sejak Fase 0.1; UI Scalar (khusus dev) dikecualikan.
- **Rahasia**: tidak pernah di-commit; `.env.example` berisi placeholder.
- **Log**: tanpa token, password, isi file, atau email; aksi admin tercatat di log aktivitas.
- **Dependency**: cek lisensi ([AGENTS.md](../AGENTS.md) §12) dan kerentanan sebelum menambah paket.

## 5. Privasi & data pribadi (UU No. 27 Tahun 2022 tentang Pelindungan Data Pribadi)

- **Data minimal**: username, email (opsional), nama tampilan, preferensi, dan data belajar. Tidak ada data sensitif lain.
- **Tujuan jelas**: penyelenggaraan kelas dan pembelajaran komunitas; data tidak dijual atau dibagikan ke pihak lain.
- **Transparansi**: halaman kebijakan privasi sederhana sebelum rilis beta.
- **Hak pemilik data**: koreksi lewat admin; penghapusan lewat anonimisasi ([03](03-skema-data.md) §14); ekspor data milik sendiri menyusul.
- **Anak di bawah umur**: bila ada murid di bawah 18 tahun, butuh persetujuan orang tua/wali (Q-14 ❓).
- **Leaderboard**: hanya nama tampilan dan poin. Sarankan nama tampilan bukan nama lengkap.

## 6. Target performa

| Metrik | Target |
|---|---|
| API baca p95 | < 200 ms |
| Kirim jawaban drilling p95 | < 300 ms |
| Poin tampil di leaderboard sejak jawaban benar | < 2 detik |
| LCP halaman murid (4G, HP kelas menengah) | < 2,5 detik |
| JavaScript awal (gzip) | < 250 KB; rute lain dimuat lazy |

Praktik: index sesuai [03](03-skema-data.md); hindari N+1 (projection/`Include` yang tepat); paginasi wajib untuk daftar; `AsNoTracking` untuk query baca; cache pendek untuk cek akses; font subset per unicode-range; code-splitting per rute.

## 7. Operasi

- **Log & health**: lihat [02](02-arsitektur.md) §15. Uptime check sederhana untuk `/health/ready` di produksi.
- **Backup**: `pg_dump` harian (simpan 7 harian + 4 mingguan), folder file, dan export realm Keycloak, disalin ke luar server. **Restore diuji** sebelum rilis dan setiap 3 bulan.
- **Pembaruan**: dependency dicek bulanan; patch keamanan segera.

## 8. Checklist rilis (beta/produksi)

- [ ] Domain + HTTPS aktif; HSTS menyala.
- [ ] Rahasia produksi terpasang (client secret Keycloak, signing key storage, VAPID, SMTP) dan berbeda dari dev.
- [ ] Keycloak produksi: user dev dihapus, redirect URI produksi, SMTP Keycloak terkonfigurasi.
- [ ] Migrasi database dijalankan; seed dev **tidak** berjalan di produksi.
- [ ] Backup otomatis berjalan dan restore sudah diuji.
- [ ] Halaman kebijakan privasi dan kontak tersedia.
- [ ] Monitoring uptime dan log minimal tersedia.
- [ ] E2E alur utama lulus di staging.
- [ ] Checklist keamanan §4 diperiksa; audit dependency bersih.
- [ ] Semua penanda `ja-review` sudah diperiksa sensei.
- [ ] Target performa §6 tercapai dengan data contoh yang realistis.

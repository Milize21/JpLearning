# Infrastruktur pengembangan

Layanan pendukung berjalan di Docker, sedangkan aplikasi .NET dan web berjalan langsung di mesin (D-25, [docs/02](../docs/02-arsitektur.md) §13).

## Prasyarat

.NET 10 SDK (lihat `global.json`), Docker Desktop, Node.js LTS (22 atau 24), Git.

## Menjalankan pertama kali

```bash
# 1. Variabel lokal. infra/.env TIDAK di-commit; ganti semua "change-me".
cp infra/.env.example infra/.env

# 2. Nyalakan layanan dan tunggu sampai semuanya "healthy" (Keycloak ±1 menit).
docker compose -f infra/docker-compose.yml up -d
docker compose -f infra/docker-compose.yml ps

# 3. Rahasia API lewat user-secrets (sekali per mesin). Samakan dengan isi infra/.env.
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=nomax;Username=nomax;Password=<POSTGRES_PASSWORD>" --project src/Nomax.Api
dotnet user-secrets set "Keycloak:AdminClientSecret" "<NOMAX_API_CLIENT_SECRET>" --project src/Nomax.Api

# 4. Jalankan API dan Gateway (dua terminal).
dotnet run --project src/Nomax.Api
dotnet run --project src/Nomax.Gateway
```

Cek: `http://localhost:8080/health/ready` dan `http://localhost:5080/health/ready` menjawab `Healthy`.

## Sistem live (semua sekaligus, pulih otomatis)

Supervisor `scripts/dev-live.mjs` (Node, tanpa paket tambahan) menjalankan API, Gateway, dan Vite, lalu menjaganya tetap hidup:

- Layanan yang mati atau tidak menjawab health check dinaikkan lagi; bila Docker/infra mati, Docker Desktop (Windows) dan `docker compose up -d` dijalankan ulang.
- Perubahan di `src/` (`.cs`, `.csproj`, `.json`, …) → build ulang → restart API/Gateway. Build yang gagal tidak menurunkan instance yang sedang jalan. Frontend memakai hot reload Vite.
- API dan Gateway dijalankan dari salinan di `.live/run`, jadi `dotnet build`/`dotnet test` di repo tidak bentrok dengan DLL yang terkunci.

```bash
node scripts/dev-live.mjs start           # di latar belakang; buka http://localhost:5173
node scripts/dev-live.mjs status          # apa yang hidup + build terakhir
node scripts/dev-live.mjs restart api     # api | gateway | web
node scripts/dev-live.mjs stop            # hentikan aplikasi (Docker tetap jalan)
node scripts/dev-live.mjs run             # di terminal ini (Ctrl+C berhenti)
```

Log: `.live/logs/` (`supervisor.log`, `api.log`, `gateway.log`, `web.log`, `build.log`). Folder `.live/` tidak di-commit.

## Port

| Layanan | Alamat host | Keterangan |
|---|---|---|
| PostgreSQL 18 | `localhost:5432` | database `nomax` (aplikasi) dan `keycloak` |
| Valkey 9.2 | `localhost:6379` | pengganti Redis berlisensi BSD-3 (D-29) |
| Keycloak 26.8 | `http://localhost:8081` | realm `nomax`; konsol admin di `/admin` |
| Mailpit | `http://localhost:8025` (UI), `localhost:1025` (SMTP) | penangkap email dev |
| Nomax.Api | `http://localhost:5080` | |
| Nomax.Gateway | `http://localhost:8080` | pintu masuk SPA dan klien lain |

Semua port Docker hanya dibuka di `127.0.0.1`.

**Port 5432/6379 sudah dipakai proyek lain?** Isi `POSTGRES_PORT` dan/atau `VALKEY_PORT` di `infra/.env` (mis. 5433 dan 6380), lalu arahkan API ke port yang sama:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5433;Database=nomax;Username=nomax;Password=<POSTGRES_PASSWORD>" --project src/Nomax.Api
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6380" --project src/Nomax.Api
```

## Kredensial pengembangan

Hanya untuk mesin developer. Realm produksi tidak memuat user ini ([docs/09](../docs/09-kualitas-dan-keamanan.md) §8).

| Username | Password | Peran | `sub` (= `identity.user_profiles.id`) |
|---|---|---|---|
| `admin.dev` | `Nomax-dev-admin` | admin | `a0000000-0000-4000-8000-000000000001` |
| `sensei.dev` | `Nomax-dev-sensei` | sensei | `a0000000-0000-4000-8000-000000000002` |
| `murid1.dev` | `Nomax-dev-murid1` | murid | `a0000000-0000-4000-8000-000000000003` |
| `murid2.dev` | `Nomax-dev-murid2` | murid | `a0000000-0000-4000-8000-000000000004` |

Email user dev memakai domain `nomax.test`. Admin Keycloak (realm `master`) dan secret client `nomax-api` ada di `infra/.env`.

## Realm Keycloak

- Sumber: `keycloak/realm-nomax.json` (sesuai [docs/01](../docs/01-peran-dan-akses.md) §8). Secret `nomax-api` ditulis sebagai placeholder `${NOMAX_API_CLIENT_SECRET}` dan diisi Keycloak dari environment saat import.
- Atribut profil (email, nama depan, nama belakang) opsional dan hanya bisa diubah admin, karena aplikasi menyimpan salinannya di `identity.user_profiles`. Tanpa pengaturan ini Keycloak 24+ memaksa pengguna melengkapi profil saat login.
- Keycloak **hanya meng-import realm saat database-nya masih kosong**. Setelah mengubah file realm, ulangi dari nol:

```bash
docker compose -f infra/docker-compose.yml down -v   # menghapus volume PostgreSQL (data dev hilang)
docker compose -f infra/docker-compose.yml up -d
```

## Catatan Windows

- Repo di drive yang tidak mencatat kepemilikan file (mis. exFAT) membuat git menolak dengan "dubious ownership". Tambahkan pengecualian sekali: `git config --global --add safe.directory <path-repo>`.
- File teks memakai LF (`.gitattributes`, `.editorconfig`).

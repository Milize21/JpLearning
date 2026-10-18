# CLAUDE.md

@AGENTS.md

## Tambahan khusus Claude Code

- Komunikasi dengan pemilik proyek dalam Bahasa Indonesia.
- Mulai sesi dengan `/fase <nomor>` (contoh: `/fase 0.1`). Perintah ini membaca brief di `docs/08`, menulis RENCANA, lalu berhenti menunggu persetujuan.
- Sebelum menyatakan tugas selesai, jalankan `/cek-aturan` untuk review kepatuhan terhadap AGENTS.md.
- Perubahan skema data selalu lewat `/ubah-skema <deskripsi>`.
- Plan Mode boleh dipakai untuk menyusun rencana, tetapi isinya tetap mengikuti template RENCANA (AGENTS.md §3.2).
- Jangan commit atau push kecuali diminta.
- Baca dokumen secara selektif sesuai peta dokumen (AGENTS.md §2). Jangan memuat seluruh folder `docs/` sekaligus.

## Perintah proyek

Diuji di akhir Fase 0.1 (Windows 11, .NET SDK 10.0.402, Docker Desktop 29.7). Detail setup, port, dan kredensial dev: `infra/README.md`.

- Infrastruktur dev: `docker compose -f infra/docker-compose.yml up -d` (butuh `infra/.env` dari `infra/.env.example`)
- Rahasia API (sekali per mesin): `dotnet user-secrets set "ConnectionStrings:Postgres" "<koneksi>" --project src/Nomax.Api` dan `dotnet user-secrets set "Keycloak:AdminClientSecret" "<NOMAX_API_CLIENT_SECRET>" --project src/Nomax.Api`
- Tool lokal: `dotnet tool restore` (dotnet-ef dari `dotnet-tools.json`)
- Migrasi baru: `dotnet ef migrations add <NamaDeskriptif> --project src/Modules/<Modul>/Nomax.Modules.<Modul> --startup-project src/Nomax.Api --context <Modul>DbContext --output-dir Infrastructure/Migrations` (di dev diterapkan otomatis saat API start)
- Build: `dotnet build Nomax.slnx` (warning = error)
- Format: `dotnet format Nomax.slnx --verify-no-changes`
- Tes: `dotnet test Nomax.slnx` (runner Microsoft.Testing.Platform lewat `global.json`; satu proyek: `dotnet test --project tests/<Proyek>`). Tes integrasi butuh Docker berjalan (Testcontainers PostgreSQL + Valkey).
- Sistem live (wajib menyala selama bekerja, permintaan pemilik proyek): `node scripts/dev-live.mjs start` · `status` · `restart <api|gateway|web>` · `stop`. Menjalankan API, Gateway, Vite dari `.live/`, restart otomatis bila mati, build ulang saat `src/` berubah. Detail: `infra/README.md`.
- Jalankan manual: `dotnet run --project src/Nomax.Api` (5080) · `dotnet run --project src/Nomax.Gateway` (8080)
- Audit paket: `dotnet list Nomax.slnx package --vulnerable --include-transitive`
- Frontend (`web/`, Node 22+): `npm --prefix web run dev` (5173) · `test` (Vitest) · `lint` · `lint:tokens` · `typecheck` · `build` · `format:check` · `gen:api` (butuh API hidup) · `e2e` (Playwright + Chrome lokal; butuh infra, memakai server yang sudah hidup)
- E2E membuat akun uji di Keycloak dev; bersihkan akun selain user seed dan akun pemilik `qyra` setelahnya.

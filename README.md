<p align="center">
  <img src="design/brand/NomaX_Group_Logo.jpg" width="96" alt="Logo NomaX Group">
</p>

# NomaX JP Learn

Platform web komunitas untuk belajar bahasa Jepang dari **kana sampai JLPT N3**: materi per bab, latihan mandiri (drilling) tanpa batas, kuis/ujian/tugas bernilai 0–4, jadwal kelas dengan pengingat, progres dan kelemahan, serta leaderboard live per level. Kelas tatap muka tetap di Zoom/Discord. Seluruh antarmuka tampil dalam Bahasa Indonesia atau Inggris dengan pendamping Jepang (kanji, furigana, kana, romaji) yang bisa diatur.

> "NomaX JP Learn" masih nama kerja (lihat Q-01 di `docs/11-keputusan.md`).

## Status

**Fase 0 selesai: login 3 peran lewat Keycloak, manajemen akun oleh admin (termasuk impor CSV), dan UI bilingual dengan pendamping Jepang.** Pembangunan dilakukan lewat vibe coding per fase, mengikuti `docs/08-roadmap-fase.md`. Cara menjalankan: [`infra/README.md`](infra/README.md).

## Isi repo

| Path | Isi |
|---|---|
| [`AGENTS.md`](AGENTS.md) | Aturan wajib untuk semua AI coding assistant |
| [`CLAUDE.md`](CLAUDE.md) | Memuat AGENTS.md untuk Claude Code + perintah proyek |
| [`.claude/commands/`](.claude/commands/) | Perintah Claude Code: `/fase`, `/cek-aturan`, `/ubah-skema` |
| [`docs/`](docs/README.md) | Blueprint v2: visi, akses, arsitektur, skema, aturan bisnis, API, teks Jepang, desain, roadmap, kualitas, prompt, keputusan |
| [`design/`](design/) | `tokens.css`, `tailwind-theme.css`, `preview.html`, logo |
| [`infra/`](infra/README.md) | Docker Compose dev, realm Keycloak, cara menjalankan |
| `src/`, `tests/` | Solution .NET 10 (`Nomax.slnx`): Gateway, host API, BuildingBlocks, tes |

## Mulai vibe coding

1. Pasang prasyarat: **.NET 10 SDK**, **Node.js LTS** (22 atau 24), **Docker Desktop**, **Git**.
2. Tinjau pertanyaan terbuka ❓ dan keputusan 🟡 di [`docs/11-keputusan.md`](docs/11-keputusan.md) yang dibutuhkan fase berikutnya.
3. Buka Claude Code di folder ini (aturan dimuat otomatis) dan ketik `/fase 0.1`.
4. Baca RENCANA dari AI, setujui atau koreksi, lalu biarkan AI mengerjakan.
5. Sebelum menutup sesi: `/cek-aturan`, baca LAPORAN, lalu commit.
6. Lanjut ke `/fase 0.2`, `/fase 0.3`, dan seterusnya.

Tool lain (Cursor, Codex, Copilot, Gemini CLI) umumnya membaca `AGENTS.md` otomatis. Bila tidak, tempel Master prompt dari [`docs/10-prompt-ai.md`](docs/10-prompt-ai.md).

## Sistem desain singkat

| Urutan | Warna | Peran | Token utama |
|---|---|---|---|
| 1 · Utama | Putih (Shiro 白) | Latar, kartu, ruang lega | `#FFFFFF` |
| 2 · Sekunder | Merah (Aka 赤) | Merek, aksi utama, status aktif, koreksi | aksi `#B9050E` · merah logo `#780000` |
| 3 · Ketiga | Hitam (Sumi 墨) | Teks, ikon, tombol sekunder, kontras | teks `#171717` · abu logo `#707070` |

Buka [`design/preview.html`](design/preview.html) di browser untuk melihat palet, tipografi, komponen, dan demo tampilan teks Jepang. Detail: [`docs/07-desain-ui-ux.md`](docs/07-desain-ui-ux.md).

## Arsitektur singkat

```
React SPA (PWA) ──OIDC PKCE──▶ Keycloak
      │ JWT
      ▼
Gateway (YARP) ──▶ Nomax.Api (modular monolith)
                     ├─ Identity   ├─ Content   ├─ Classroom
                     ├─ Practice   └─ Insight (SignalR + Redis)
                     ▼
               PostgreSQL (1 schema per modul)
```

Detail: [`docs/02-arsitektur.md`](docs/02-arsitektur.md).

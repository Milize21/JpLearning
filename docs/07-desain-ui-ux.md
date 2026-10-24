# 07 · Desain UI/UX

> Sumber nilai: [`design/tokens.css`](../design/tokens.css) · pemetaan Tailwind/shadcn: [`design/tailwind-theme.css`](../design/tailwind-theme.css) · pratinjau visual: buka [`design/preview.html`](../design/preview.html) di browser.

## 1. Arah desain: kertas, stempel, tinta (紙・判子・墨)

Tiga warna permintaan pemilik proyek diterjemahkan menjadi tiga benda yang akrab bagi pelajar bahasa Jepang:

| Urutan | Warna | Metafora | Peran di UI |
|---|---|---|---|
| **1 · Utama (dasar)** | **Putih** — Shiro 白 | Kertas buku catatan | Latar halaman, kartu, ruang lega yang tenang agar fokus pada isi |
| **2 · Sekunder** | **Merah** — Aka 赤 | Stempel hanko dan tinta koreksi sensei (赤ペン) | Identitas merek, aksi utama, status aktif, progres, koreksi. Dipakai hemat agar selalu bermakna |
| **3 · Ketiga** | **Hitam** — Sumi 墨 | Tinta kaligrafi | Teks, ikon, garis tegas, tombol sekunder, panel kontras |

Kepribadian visual: **rapi, fokus, hangat, disiplin.** Modern-minimal dengan sudut agak tegas (mengikuti geometri logo). Rasa Jepangnya halus: lewat warna tradisional (*wa-iro*), stempel hanko, dan tipografi yang benar, bukan hiasan sakura atau kanji raksasa di mana-mana.

## 2. Prinsip UX

1. **Satu layar, satu tujuan.** Satu aksi utama (tombol merah) per layar atau per bagian.
2. **Belajar di mana saja.** Halaman murid *mobile-first* (lebar 360 px), target sentuh ≥ 44 px, aksi di jangkauan jempol.
3. **Umpan balik instan dan jujur.** Jawaban dinilai < 300 ms; benar/salah jelas (○/×), langsung dengan jawaban yang benar dan pembahasan singkat.
4. **Progres selalu terlihat.** Murid tahu bab mana yang terbuka, apa yang lemah, dan apa langkah berikutnya.
5. **Bahasa Jepang menemani, tidak menghalangi.** Pendamping Jepang mengikuti preferensi dan aturan kepadatan ([06](06-teks-multibahasa.md) §8).
6. **Inklusif.** WCAG 2.2 AA, tidak bergantung warna saja, animasi bisa dikurangi.
7. **Gamifikasi yang sehat.** Poin, peringkat, dan stempel memotivasi tanpa *dark pattern*: tanpa notifikasi yang memaksa, tanpa hukuman.

## 3. Logo & identitas

- **File master**: [`design/brand/NomaX_Group_Logo.jpg`](../design/brand/NomaX_Group_Logo.jpg) (2000×2000). Isi: latar merah marun `#780000`, bentuk abu `#707070`, putih, dan hitam.
- **Turunan** (dibuat di Fase 0 dari file master, tanpa mengubah warna): favicon 16/32, `apple-touch-icon` 180, ikon PWA 192 dan 512 (+ maskable), gambar Open Graph. Versi vektor (SVG) dibuat desainer dari master; **AI tidak menggambar ulang logo**.
- **Pemakaian**: logo adalah *tile* persegi di atas latar **putih atau hitam**. Tile boleh diberi radius sudut maksimal 6 px; bentuk di dalamnya tidak boleh diubah.
- **Wordmark** di top bar: tile 32 px (HP 28 px) + teks "**NomaX** JP Learn" (Plus Jakarta Sans; "NomaX" berat 800, "JP Learn" berat 500; warna `ink`).
- **Ruang kosong** di sekeliling logo minimal 1/4 lebar logo. **Ukuran minimum** 24 px (16 px hanya untuk favicon).
- **Dilarang**: mengubah warna, memutar, meregangkan, memberi bayangan/efek, menaruh di atas latar merah (tile marun menyatu), memotong, atau menaruh di atas foto ramai.
- Nama produk final: Q-01.

## 4. Warna

### 4.1 Hierarki dan porsi

| Urutan | Warna | Porsi kira-kira di layar |
|---|---|---|
| 1 · Utama | Putih (Shiro) | ± 70% |
| 2 · Sekunder | Merah (Aka) | ± 10–20%, hemat dan bermakna |
| 3 · Ketiga | Hitam (Sumi) + abu turunannya | ± 10–20% |

> **Catatan istilah.** Bagi pemilik proyek, "warna utama" = putih. Di shadcn/ui, variabel `--primary` berarti warna **tombol aksi utama** (= merah). Agar tidak rancu, dokumen dan kode memakai nama palet `shiro`/`aka`/`sumi` dan token semantik (`bg`, `brand`, `action`, `ink`). Istilah "primary/secondary" hanya muncul di file pemetaan shadcn.

### 4.2 Palet dasar

Merah dibangun dari warna logo (`aka-900` = `#780000`) dalam ruang warna OKLCH. Abu dibangun dari abu logo (`sumi-500` = `#707070`). Kontras dihitung terhadap putih (rumus WCAG 2.x).

**Aka 赤 (merah)**

| Token | Hex | Kontras vs putih | Pemakaian |
|---|---|---|---|
| `aka-50` | `#FFF2F2` | 1.09 | Latar item aktif, latar error |
| `aka-100` | `#FFE2E2` | 1.22 | Hover item aktif |
| `aka-200` | `#FFCAC9` | 1.44 | Garis/aksen lembut |
| `aka-300` | `#FFA4A1` | 1.90 | Dekorasi, grafik |
| `aka-400` | `#FC6966` | 2.86 | Teks/aksen merah **di atas hitam** (6.93 vs sumi-950) |
| `aka-500` | `#F33336` | 3.93 | Aksen di atas hitam, ikon besar |
| `aka-600` | `#DA0719` | 5.22 | Merah cerah (dipakai terbatas) |
| **`aka-700`** | **`#B9050E`** | **6.79** | **Warna aksi utama**: tombol primer, link, teks error, indikator aktif |
| `aka-800` | `#980806` | 8.85 | Hover tombol primer, teks di atas `aka-50` |
| **`aka-900`** | **`#780000`** | **11.68** | **Merah logo NomaX**: momen merek, tombol primer saat ditekan |
| `aka-950` | `#470503` | 16.27 | Bayangan/teks di atas merah muda |

**Sumi 墨 (hitam & abu)**

| Token | Hex | Kontras vs putih | Pemakaian |
|---|---|---|---|
| `sumi-50` | `#FAFAFA` | 1.04 | Latar lembut (sidebar, header tabel) |
| `sumi-100` | `#F3F3F3` | 1.11 | Latar muted, skeleton |
| `sumi-200` | `#E6E6E6` | 1.25 | Garis pemisah, border kartu |
| `sumi-300` | `#D1D1D1` | 1.53 | Border tombol outline, dekorasi |
| `sumi-400` | `#8A8A8A` | 3.45 | Border input, ikon nonaktif (≥ 3:1 untuk komponen UI) |
| `sumi-500` | `#707070` | 4.95 | Abu logo; teks redup (hanya di atas putih/sumi-50) |
| `sumi-600` | `#545454` | 7.57 | Teks sekunder |
| `sumi-700` | `#3D3D3D` | 10.86 | Teks info, ikon |
| `sumi-800` | `#262626` | 15.13 | Teks isi panjang (opsional) |
| **`sumi-900`** | **`#171717`** | **17.93** | **Teks utama**, tombol sekunder, focus ring, tooltip |
| `sumi-950` | `#0A0A0A` | 19.80 | Panel gelap (footer, podium) |
| `kuro` | `#000000` | 21.00 | Hitam logo; dipakai sangat terbatas |

**Shiro 白 (putih)**: `shiro` = `#FFFFFF`, latar utama dan kartu.

### 4.3 Token semantik

Komponen memakai token semantik, bukan token palet, kecuali disebut khusus.

| Token CSS | Nilai | Pemakaian |
|---|---|---|
| `--nx-bg` | shiro | Latar halaman |
| `--nx-bg-subtle` | sumi-50 | Sidebar, header tabel, seksi lembut |
| `--nx-bg-muted` | sumi-100 | Latar netral, skeleton, chip |
| `--nx-surface` | shiro | Kartu, dialog, popover |
| `--nx-surface-inverse` | sumi-950 | Panel gelap |
| `--nx-ink` | sumi-900 | Teks utama |
| `--nx-ink-muted` | sumi-600 | Teks sekunder, furigana |
| `--nx-ink-subtle` | sumi-500 | Teks redup (hanya di atas putih/sumi-50) |
| `--nx-ink-inverse` | shiro | Teks di atas gelap/merah |
| `--nx-line` | sumi-200 | Garis pemisah, border kartu |
| `--nx-line-strong` | sumi-400 | Border input |
| `--nx-brand` | aka-900 | Momen merek (merah logo) |
| `--nx-action` / `-hover` / `-active` | aka-700 / aka-800 / aka-900 | Tombol aksi utama |
| `--nx-action-subtle` | aka-50 | Latar item aktif (nav, tab) |
| `--nx-on-action` | shiro | Teks di atas tombol merah |
| `--nx-ring` | sumi-900 | Focus ring (selalu dengan offset putih 2 px) |
| `--nx-success` / `-bg` | tokiwa-700 / tokiwa-50 | Benar, berhasil |
| `--nx-warning` / `-bg` | yamabuki-700 / yamabuki-50 | Peringatan, tenggat dekat |
| `--nx-danger` / `-bg` | aka-700 / aka-50 | Salah, error, destruktif |
| `--nx-info` / `-bg` | sumi-700 / sumi-100 | Info netral |

### 4.4 Aturan pemakaian warna

1. **Satu tombol merah per layar atau per bagian.** Aksi lain memakai tombol sekunder (hitam), outline, atau ghost.
2. **Merah tidak untuk blok dekoratif besar.** Area merah luas hanya untuk momen merek (mis. hero beranda atau kartu podium), maksimal satu per layar.
3. Teks merah di atas putih minimal `aka-700`; di atas hitam pakai `aka-400`/`aka-500`.
4. **"Dipilih" sebelum dinilai = border hitam**, bukan merah, agar tidak dikira "salah".
5. **Focus ring = hitam 2 px + offset putih 2 px**, bukan merah, agar berbeda dari error. Tanpa offset, ring hitam di atas tombol merah hanya 2.64:1.
6. **Error = merah + ikon + teks.** Aksi destruktif = tombol outline merah + dialog konfirmasi dengan label eksplisit ("Ya, hapus materi").
7. **Hierarki teks**: judul `sumi-900`, isi `sumi-900`/`sumi-800`, sekunder `sumi-600`, redup `sumi-500` (hanya di atas putih/`sumi-50`; di atas `sumi-100` kontrasnya 4.46 dan tidak lolos).
8. **Tidak ada warna di luar token**: tanpa biru, ungu, atau gradien pelangi. Nilai tidak dikenal = bug.

### 4.5 Pasangan kontras yang sudah diverifikasi

| Pasangan (teks/ikon di atas latar) | Rasio | Lolos untuk |
|---|---|---|
| `sumi-900` di atas putih | 17.93 | semua teks |
| `sumi-600` di atas putih | 7.57 | semua teks |
| `sumi-500` di atas putih | 4.95 | teks normal |
| `sumi-500` di atas `sumi-50` | 4.74 | teks normal |
| `sumi-600` di atas `sumi-100` | 6.82 | teks normal |
| putih di atas `aka-700` (tombol primer) | 6.79 | teks normal |
| putih di atas `aka-800` (hover) | 8.85 | teks normal |
| putih di atas `aka-900` | 11.68 | teks normal |
| `aka-700` di atas putih (link, error) | 6.79 | teks normal |
| `aka-700` di atas `aka-50` | 6.22 | teks normal |
| `aka-800` di atas `aka-50` (nav aktif) | 8.11 | teks normal |
| putih di atas `sumi-900` (tombol sekunder) | 17.93 | teks normal |
| `sumi-50` di atas `sumi-950` (panel gelap) | 18.97 | teks normal |
| `aka-400` di atas `sumi-950` | 6.93 | teks normal |
| `aka-500` di atas `sumi-950` | 5.03 | teks normal |
| `tokiwa-700` di atas `tokiwa-50` | 4.92 | teks normal |
| `yamabuki-700` di atas `yamabuki-50` | 5.72 | teks normal |
| `sumi-950` di atas `yamabuki-400` | 10.94 | teks normal |
| `sumi-400` (border input) di atas putih | 3.45 | komponen UI (≥ 3:1) |
| **Tidak lolos**: `sumi-500` di atas `sumi-100` (4.46), `aka-700` di atas `sumi-950` (2.91) | – | jangan dipakai untuk teks |

### 4.6 Warna status (wa-iro)

| Status | Teks/ikon | Latar | Ikon | Contoh |
|---|---|---|---|---|
| Sukses / benar | `tokiwa-700` `#007B43` (常磐, hijau abadi) | `tokiwa-50` `#ECF8F1` | ○ / `circle-check` | Jawaban benar, tersimpan |
| Peringatan | `yamabuki-700` `#8F5500` (山吹, kuning kerria) | `yamabuki-50` `#FFF8E6`, aksen `yamabuki-400` `#F8B500` | `triangle-alert` | Tenggat dekat, timer < 5 menit |
| Bahaya / salah | `aka-700` | `aka-50` | × / `circle-x` | Jawaban salah, error, timer < 1 menit |
| Info | `sumi-700` | `sumi-100` | `info` | Catatan netral |

Warna status **hanya** untuk status, tidak untuk dekorasi. `tokiwa-600` `#00894A` (4.49) hanya untuk ikon dan teks besar.

## 5. Tipografi (D-12 🟡)

| Peran | Font | Alasan |
|---|---|---|
| Antarmuka (Latin) | **Plus Jakarta Sans** (variable) | Geometris, sangat terbaca; dirancang foundry Indonesia (Tokotype), sentuhan lokal untuk komunitas Indonesia |
| Teks Jepang | **Noto Sans JP** (variable) | Glyph Jepang lengkap; dimuat per *unicode-range* sehingga ringan |
| Kartu belajar kana/kanji | **Klee One** | Bentuk seperti tulisan tangan buku pelajaran, sehingga murid melihat bentuk huruf yang benar untuk ditulis (mis. き, さ, り) |
| Angka (skor, timer, poin) | Plus Jakarta Sans `tabular-nums` | Angka tidak "melompat" saat berubah |

Font stack: `"Plus Jakarta Sans", "Noto Sans JP", system-ui, sans-serif`. Font di-host sendiri lewat paket `@fontsource`.

| Token | Ukuran (HP) | Line-height | Berat | Pemakaian |
|---|---|---|---|---|
| `display` | 36 px (30) | 1.2 | 800 | Hero beranda |
| `h1` | 30 px (26) | 1.25 | 700 | Judul halaman |
| `h2` | 24 px (22) | 1.3 | 700 | Judul seksi |
| `h3` | 20 px | 1.4 | 600 | Judul kartu |
| `body-lg` | 18 px | 1.7 | 400 | Bacaan materi |
| `body` | 16 px | 1.6 | 400 | Teks umum |
| `body-sm` | 14 px | 1.5 | 400/500 | Teks sekunder, tabel |
| `caption` | 12 px | 1.4 | 500 | Label kecil, pendamping Jepang |
| `learn-lg` | 64 px | 1.1 | 600 (Klee One) | Kartu kana/kosakata |
| `learn-xl` | 96 px | 1 | 600 (Klee One) | Satu huruf kana/kanji fokus |

Teks yang memuat furigana memakai line-height 2.0 (`--nx-leading-ruby`). Aturan teks Jepang lain: [06](06-teks-multibahasa.md) §7.

## 6. Layout & ruang

| Hal | Nilai |
|---|---|
| Grid dasar | 4 px (skala spacing bawaan Tailwind: `1` = 4 px) |
| Breakpoint | bawaan Tailwind: `sm` 640, `md` 768, `lg` 1024, `xl` 1280. Lebar minimum yang didukung 360 px |
| Lebar konten | maks 1200 px; teks bacaan maks 72 karakter per baris |
| Gutter | 16 px (HP), 24 px (tablet), 32 px (desktop) |
| Top bar | 56 px; garis merek merah 3 px di tepi paling atas aplikasi |
| Sidebar | 264 px (bisa diciutkan menjadi 72 px) |
| Bottom nav murid (HP) | 64 px + *safe-area inset* |
| Z-index | konten 0 · sticky 10 · dropdown 20 · overlay 40 · modal 50 · toast 60 |

Kerangka per peran:
- **Murid**: HP memakai top bar + bottom nav; mulai `lg` memakai sidebar + top bar.
- **Sensei & admin**: mulai `lg` memakai sidebar + top bar; di bawah `lg` memakai top bar + menu laci. Tabel berubah menjadi daftar kartu di HP.
- **Mode fokus** (drilling, kuis): tanpa navigasi; top bar ringkas berisi tombol keluar, progres, dan timer.

## 7. Bentuk & elevasi

| Hal | Nilai |
|---|---|
| Radius | `sm` 4 px (badge, chip) · `md` 6 px (tombol, input) · `lg` 10 px (kartu) · `xl` 14 px (dialog, sheet) · `full` (pil) |
| Border | kartu & pemisah 1 px `line`; input 1 px `line-strong`; terpilih 2 px `sumi-900` |
| Bayangan | kartu default **tanpa bayangan** (kesan kertas); `sm` saat hover kartu interaktif; `md` dropdown/popover; `lg` dialog |

## 8. Motif, ikon, ilustrasi

- **Stempel hanko** (判子): lingkaran bergaris `aka-700`, teks Jepang `aka-700`, sedikit miring (−8°). Dipakai di ringkasan latihan dan hasil kuis, mengikuti stempel klasik guru di Jepang:

| Akurasi / skor | Stempel | Arti |
|---|---|---|
| ≥ 80% | よくできました | Bagus sekali |
| 50–79% | がんばりました | Kamu sudah berusaha |
| < 50% | がんばりましょう | Ayo terus semangat |
| Bab dikuasai (rata-rata mastery ≥ 0,85) | 済 | Selesai (stempel kecil di kartu bab) |

- **Motif potongan diagonal** dari geometri logo: aksen garis atau potongan sudut, maksimal satu per layar (hero beranda, kartu podium, header kartu bab).
- **Ikon**: `lucide-react`, stroke 1.75, ukuran 16/20/24 px. Ikon tanpa teks wajib punya `aria-label`.
- **Ilustrasi**: minimal. Empty state memakai ikon garis + kalimat + satu aksi. Tanpa foto stok, tanpa emoji sebagai ikon.

## 9. Motion

| Token | Durasi | Pemakaian |
|---|---|---|
| `fast` | 120 ms | Hover, tekan tombol |
| `base` | 200 ms | Popover, panel umpan balik jawaban |
| `slow` | 320 ms | Sheet, dialog, stempel hanko |

Easing: `cubic-bezier(0.2, 0.8, 0.2, 1)`. Stempel: skala 1.4 → 1, rotasi −14° → −8°, opasitas 0 → 1. Jawaban salah: getar horizontal maksimal 4 px selama 200 ms. Dengan `prefers-reduced-motion`, semua gerak diganti fade singkat.

## 10. Katalog komponen

Basis: shadcn/ui (Radix) yang diberi token NomaX. Komponen domain dibuat di `web/src/features/*/components` atau `web/src/components/japanese`.

| Komponen | Varian / keadaan | Aturan |
|---|---|---|
| **Button** | `primary` (merah `action`, teks putih) · `secondary` (hitam `sumi-900`, teks putih) · `outline` (putih, border `sumi-300`) · `ghost` · `destructive-outline` (border & teks `aka-700`) · `link` | Tinggi 40 px (`sm` 32, `lg` 48). Satu `primary` per bagian. Loading: spinner + tetap selebar semula. Disabled: latar `sumi-100`, teks `sumi-400` |
| **IconButton** | ghost, outline | Area sentuh ≥ 44×44 px; `aria-label` wajib |
| **Input, Select, Textarea** | default, focus, error, disabled | Border `line-strong`; focus ring hitam + offset; error: border `aka-700` + ikon + pesan di bawah |
| **Checkbox, Radio** | checked | Tercentang = `sumi-900` |
| **Switch** | on/off | On = `action` (merah), mis. "Bab terbuka" |
| **Card** | default · interaktif (hover `shadow-sm`) · terpilih (border 2 px `sumi-900`) · terkunci (latar `sumi-50`, ikon gembok, teks `sumi-500`) | Tanpa bayangan default |
| **Badge** | `neutral` (`sumi-100`/`sumi-700`) · `brand` (`aka-50`/`aka-800`) · `solid` (`sumi-900`/putih) · `success` · `warning` · `danger` | Radius `sm` |
| **Tabs** | aktif | Garis bawah merah 2 px + teks `sumi-900` semibold |
| **Table** | header `bg-subtle`; baris dipisah `line`; hover `bg-subtle` | Di HP menjadi daftar kartu |
| **Dialog / Sheet** | – | Sheet dari bawah di HP; judul dengan pendamping `inline` |
| **Toast** | success, warning, danger, info | Ikon + teks; `aria-live="polite"` |
| **Tooltip** | – | Latar `sumi-900`, teks putih |
| **Progress** | bar, ring | Track `sumi-200`, isi `action`; ring untuk mastery bab |
| **Skeleton** | – | `sumi-100`, pulse halus |
| **EmptyState** | – | Ikon garis, judul + pendamping Jepang, satu aksi |
| **TopBar** | – | Putih, border bawah `line`; logo + wordmark, `ScriptToggle`, `LanguageSwitcher`, menu profil |
| **Sidebar** | item aktif | Latar `aka-50`, teks `aka-800`, indikator kiri 3 px `aka-700`; pendamping Jepang `below` |
| **BottomNav** (murid, HP) | item aktif | Ikon + teks `aka-700` + indikator; 5 item |
| **ChapterCard** | terbuka · terkunci · dikuasai | Dikuasai: stempel kecil 済 + ring mastery |
| **QuestionCard** | drilling, kuis | Stimulus besar (`font-learn` untuk kana/kanji tunggal); pilihan berupa tombol ≥ 48 px dengan nomor shortcut 1–6 |
| **AnswerFeedback** | benar · salah | Benar: panel `success-bg`, ○ + 正解 (せいかい), "Benar!"; salah: panel `danger-bg`, × + 不正解 (ふせいかい), jawaban benar + pembahasan. Tombol "Lanjut" (Enter). Auto-lanjut 800 ms setelah benar (bisa dimatikan) |
| **FlashCard** | depan · belakang | Balik dengan Spasi/ketuk; tombol "Belum ingat" (outline) dan "Ingat" (primary) |
| **TimerBadge** | normal · < 5 menit · < 1 menit | Netral → `warning` → `danger` + ikon; diumumkan pembaca layar tiap menit |
| **HankoStamp** | 4 jenis (§8) | Animasi `slow`; `aria-label` berisi arti |
| **LeaderboardRow / Podium** | baris saya | Baris saya: latar `aka-50` + border kiri `aka-700`, menempel (sticky) saat digulir. Podium 3 besar: kartu `sumi-950` dengan angka `aka-400` |
| **LiveIndicator** ➕ | tersambung · terputus | Pil kecil di papan peringkat: tersambung = border & teks `aka-700` + ikon siaran (berdenyut hanya bila gerak tidak dikurangi), teks "Live"; terputus = border `line`, teks `ink-muted`, ikon wifi-off, "Tidak tersambung". Selalu ikon + teks |
| **PointsAwardedBurst** ➕ | – | "+n poin" di header drilling saat `PointsAwarded` tiba: teks `aka-700` tebal, naik dan memudar 1,6 detik (`motion-safe`), diumumkan `aria-live="polite"`. Poin 0 tidak ditampilkan |
| **MyRankCard** ➕ | ada peringkat · belum ada poin | Kartu tautan di beranda murid ke halaman Peringkat: ikon piala `action`, nama season, "Peringkat n · x poin" atau ajakan mulai latihan |
| **ScheduleItem** | akan datang · berlangsung · selesai · batal | Tanggal, jam (zona pengguna), kelas, topik, ikon platform; tombol "Gabung" aktif 15 menit sebelum mulai |
| **UploadDropzone** | idle · uploading · paused · done · error | Seret atau pilih file; progres per file; lanjut/batal; ukuran maksimal tertulis |
| **GradeBadge** | 0–4 | Angka besar + persen, warna **netral hitam** (nilai bukan "salah") |
| **MasteryStatus** | belum · sedang · lemah · dikuasai | Ikon + label; lemah = `danger`, dikuasai = `success`, lainnya netral |
| **WeaknessList** | – | Kelompok terlemah + tombol "Latih ini" (memulai mode fokus lemah) |
| **JapaneseText, Label, ScriptToggle, LanguageSwitcher, FuriganaInput** | – | Lihat [06](06-teks-multibahasa.md) §7 |

## 11. Pola UX per alur

1. **Masuk & onboarding**: halaman sambutan (logo, tagline + pendamping, tombol merah "Masuk") → login Keycloak bertema NomaX → ganti password pertama kali → dialog onboarding 2 langkah: bahasa UI dan tampilan Jepang (pratinjau langsung kata 練習) → beranda sesuai peran.
2. **Beranda murid**: "Berikutnya" (jadwal terdekat + tombol Gabung), "Yang harus dikerjakan" (kuis/tugas terdekat), "Lanjutkan latihan" (kelompok terlemah, satu ketukan memulai mode fokus lemah), progres bab, posisi di leaderboard.
3. **Drilling**: pilih kategori → bab (default semua yang terbuka) → mode → **Mulai**. Layar fokus menampilkan jumlah jawaban dan streak. Setelah menjawab: umpan balik instan (○/×), jawaban benar, pembahasan singkat, dan animasi "+1" dari hub. Shortcut: `1`–`6` pilih, `Enter` kirim/lanjut, `Spasi` balik kartu, `Esc` keluar (dengan konfirmasi). Akhir sesi: ringkasan (jumlah, akurasi, item yang baru dikuasai, item lemah) + stempel hanko + tombol "Latih kelemahan".
4. **Kuis**: halaman info (durasi, sisa percobaan, jendela waktu) → konfirmasi mulai → satu soal per layar di HP (dengan navigator nomor soal), daftar soal di desktop → indikator "Tersimpan" → kirim dengan ringkasan soal yang belum dijawab → hasil (skor + stempel) → pembahasan sesuai BR-KUIS-09.
5. **Tugas**: instruksi (RichText) → unggah file (progres, bisa dilanjutkan) dan/atau teks → kirim → status (terkirim, terlambat, dinilai) → feedback sensei.
6. **Sensei membuka bab**: halaman kelas → tab Bab → daftar bab dengan switch dan tanggal dibuka; aksi "Buka sampai Bab N"; konfirmasi saat menutup bab; toast hasil.
7. **Sensei melihat progres**: tabel murid (terakhir aktif, jawaban 7 hari, akurasi, rata-rata mastery, kelemahan teratas, nilai) → detail murid (kelemahan per kategori/tag, sesi terbaru, komentar).
8. **Sensei menyusun kuis**: pilih soal dari bank (filter bab/kategori, pratinjau versi murid) → atur poin & urutan → atur waktu, jendela, percobaan → simpan draf → terbitkan.
9. **Admin mengimpor akun**: unggah CSV → pratinjau validasi per baris (baris error disorot) → buat akun → unduh file password sementara (peringatan "hanya bisa diunduh sekali").
10. **Jadwal & pengingat**: daftar per hari (Hari ini, Besok, Minggu ini), kartu langganan ICS, tombol "Aktifkan pengingat". Izin notifikasi browser **hanya diminta setelah pengguna menekan tombol itu**, tidak saat halaman dibuka.
11. **Leaderboard**: pilih level → podium 3 besar + daftar; baris saya menempel; indikator "Live"; tautan arsip season.

## 12. Arsitektur informasi & navigasi

| Peran | Navigasi utama |
|---|---|
| Murid (HP, bottom nav) | Beranda · Materi · Latihan · Jadwal · Peringkat. Kuis & Ujian, Tugas, Nilai, Kelemahan, Profil diakses dari Beranda dan menu profil |
| Murid (desktop, sidebar) | Beranda, Kelas saya, Materi, Latihan, Kelemahan, Kuis & Ujian, Tugas, Nilai, Jadwal, Peringkat |
| Sensei | Beranda, Kelas (tab: Ringkasan, Anggota, Bab, Materi, Jadwal, Kuis, Tugas, Nilai, Progres), Bank soal, Jadwal, Peringkat |
| Admin | Beranda, Pengguna, Konten (Level/Buku/Bab), Data referensi, Bank soal, Sumber soal, Kelas, Season, Peringkat |

Rute (React Router):

| Peran | Rute |
|---|---|
| Umum | `/`, `/masuk/callback`, `/keluar` |
| Murid | `/murid`, `/murid/kelas`, `/murid/kelas/:classId`, `/murid/materi`, `/murid/bab/:chapterId`, `/murid/latihan`, `/murid/latihan/:sessionId`, `/murid/kelemahan`, `/murid/kuis`, `/murid/kuis/:attemptId`, `/murid/tugas`, `/murid/tugas/:assignmentId`, `/murid/nilai`, `/murid/jadwal`, `/murid/peringkat`, `/murid/profil` |
| Sensei | `/sensei`, `/sensei/kelas`, `/sensei/kelas/:classId/:tab`, `/sensei/murid/:studentId`, `/sensei/bank-soal`, `/sensei/jadwal`, `/sensei/peringkat`, `/sensei/profil` |
| Admin | `/admin`, `/admin/pengguna`, `/admin/konten`, `/admin/referensi`, `/admin/bank-soal`, `/admin/sumber-soal`, `/admin/kelas`, `/admin/season`, `/admin/peringkat`, `/admin/profil` |

Setelah login, pengguna diarahkan ke beranda perannya. Membuka rute peran lain → halaman 403 yang ramah dengan tombol kembali ke beranda.

### 12.1 Wireframe acuan

Beranda murid (HP, 360 px):

```
┌──────────────────────────────────┐
│▀▀▀▀▀▀▀▀ garis merek merah 3px ▀▀▀▀│
│ [logo] NomaX JP Learn   あ▾ ID (B)│  top bar: ScriptToggle, bahasa, profil
├──────────────────────────────────┤
│ Halo, Budi!                       │
│ こんにちは                         │  pendamping (sesuai preferensi)
│ ┌──────────────────────────────┐ │
│ │ BERIKUTNYA · Rab 19.00 WIB    │ │  kartu jadwal terdekat
│ │ N5 Sabtu · Bab 3 Partikel を   │ │
│ │                  [ Gabung ]   │ │  tombol merah (aktif 15 menit sebelum)
│ └──────────────────────────────┘ │
│ Yang harus dikerjakan             │
│  · Kuis Bab 3      tutup Jum 21.00│
│  · Tugas Menulis   2 hari lagi    │
│ ┌──────────────────────────────┐ │
│ │ Lanjutkan latihan             │ │
│ │ Terlemah: baris さ (4 item)    │ │
│ │          [ Latih sekarang ]   │ │  tombol sekunder (hitam)
│ └──────────────────────────────┘ │
│ Peringkat N5: #4 · 312 poin       │
├──────────────────────────────────┤
│ Beranda  Materi  Latihan  Jadwal  │  bottom nav, item aktif merah
│                         Peringkat │
└──────────────────────────────────┘
```

Layar drilling (HP, mode fokus):

```
┌──────────────────────────────────┐
│ ✕      Jawaban 12      streak 5   │  tanpa navigasi
├──────────────────────────────────┤
│ Pilih bacaan yang benar           │  prompt (TextBlock)
│ 正しい読み方を選んでください          │  pendamping
│                                    │
│              学校                  │  stimulus besar, font-learn, jaDisplay=plain
│                                    │
│  ┌────────────┐  ┌────────────┐   │
│  │ 1  がっこう │  │ 2  がくこう │   │  tombol ≥ 48 px, shortcut 1–4
│  └────────────┘  └────────────┘   │
│  ┌────────────┐  ┌────────────┐   │
│  │ 3  かっこう │  │ 4  がっこ   │   │
│  └────────────┘  └────────────┘   │
├──────────────────────────────────┤
│ ○ 正解  Benar!               +1   │  panel umpan balik (tokiwa)
│ 学校 (がっこう, gakkou) — sekolah   │
│                    [ Lanjut ⏎ ]   │
└──────────────────────────────────┘
```

Halaman kelas sensei (desktop):

```
┌───────────────┬───────────────────────────────────────────────────────┐
│ [logo] NomaX  │ Kelas › N5 Sabtu Pagi                  あ▾  ID▾  (S)  │
│               ├───────────────────────────────────────────────────────┤
│ ▌Beranda      │ N5 Sabtu Pagi                          [ + Jadwal ]   │
│  ホーム        │ Ringkasan  Anggota  Bab  Materi  Jadwal  Kuis  Nilai…  │
│ ▌Kelas (aktif)│ ───────────────── tab aktif bergaris merah             │
│  クラス        │ Bab                       Status          Dibuka       │
│  Bank soal    │ 1 · Salam                 [● Terbuka]     12 Sep       │
│  問題集        │ 2 · Perkenalan            [● Terbuka]     19 Sep       │
│  Jadwal       │ 3 · Partikel を            [○ Terkunci]    –            │
│  予定          │ [ Buka sampai bab… ▾ ]                                 │
└───────────────┴───────────────────────────────────────────────────────┘
```

## 13. Microcopy & nada

- Sapaan **"kamu"** (D-14 🟡), kalimat pendek, kata kerja di tombol ("Mulai latihan", bukan "OK").
- Pesan error = apa yang terjadi + apa yang bisa dilakukan. Contoh: "Bab ini belum dibuka sensei. Pilih bab lain atau tunggu pertemuan berikutnya."
- Tidak menyalahkan: "Belum tepat. Jawaban yang benar: …", bukan "Kamu salah".
- Empty state menjelaskan dan memberi satu aksi: "Belum ada materi di bab ini. Sensei akan menambahkannya."
- Bahasa Inggris: ramah dan sederhana, makna setara dengan versi Indonesia.
- Angka, tanggal, dan jam selalu lewat `Intl` ([06](06-teks-multibahasa.md) §11).

## 14. Aksesibilitas (WCAG 2.2 AA)

- [ ] Kontras teks ≥ 4.5:1, teks besar dan komponen UI ≥ 3:1 (pakai tabel §4.5).
- [ ] Fokus selalu terlihat (ring hitam + offset putih); urutan tab logis; tautan "Lewati ke konten".
- [ ] Semua fungsi bisa dengan keyboard. Shortcut drilling hanya aktif saat fokus di area soal.
- [ ] Target sentuh ≥ 44×44 px.
- [ ] Benar/salah, status, dan error tidak hanya dengan warna (ikon + teks).
- [ ] Atribut `lang` benar: `id`/`en` di `<html>`, `ja` di teks Jepang.
- [ ] Form: label terlihat (bukan hanya placeholder), error terhubung lewat `aria-describedby`.
- [ ] Live region untuk hasil jawaban, timer (per menit), dan toast.
- [ ] Menghormati `prefers-reduced-motion`.
- [ ] Zoom 200% dan lebar 320 px tanpa kehilangan fungsi.
- [ ] Tes otomatis axe (Playwright) untuk halaman utama di setiap fase.

## 15. Implementasi di kode

**Sumber dan alur token**

```
design/tokens.css            ← SATU-SATUNYA tempat nilai warna, font, radius, bayangan, motion (variabel --nx-*)
design/tailwind-theme.css    ← mengimpor tokens.css; memetakan ke Tailwind v4 (@theme) dan variabel shadcn/ui
web/src/styles/globals.css   ← @import "tailwindcss"; @import "../../../design/tailwind-theme.css";
design/preview.html          ← pratinjau visual tanpa build (memakai tokens.css langsung)
```

**Kelas Tailwind yang tersedia** (palet bawaan Tailwind dimatikan dengan `--color-*: initial`, sehingga `bg-red-500`, `text-gray-600`, dan sejenisnya **tidak menghasilkan CSS**):

| Kelompok | Nama warna untuk `bg-*`, `text-*`, `border-*`, `ring-*` |
|---|---|
| Permukaan | `canvas`, `canvas-subtle`, `canvas-muted`, `surface`, `surface-inverse` |
| Teks | `ink`, `ink-muted`, `ink-subtle`, `ink-inverse` |
| Garis | `line`, `line-strong` |
| Merek & aksi | `brand`, `action`, `action-hover`, `action-active`, `action-subtle`, `on-action` |
| Status | `success`, `success-bg`, `warning`, `warning-bg`, `danger`, `danger-bg`, `info`, `info-bg` |
| Palet (bila perlu) | `aka-50`…`aka-950`, `sumi-50`…`sumi-950`, `shiro`, `kuro`, `tokiwa-*`, `yamabuki-*`, `white`, `black` |
| shadcn/ui | `background`, `foreground`, `card`, `popover`, `primary`, `secondary`, `muted`, `accent`, `destructive`, `border`, `input`, `ring`, `sidebar-*` |
| Font | `font-sans`, `font-ja`, `font-learn` |

Contoh tombol primer:

```tsx
<button className="h-10 rounded-md bg-action px-4 font-semibold text-on-action
                   hover:bg-action-hover active:bg-action-active
                   focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring
                   focus-visible:ring-offset-2 focus-visible:ring-offset-canvas">
  <Label value={practice.start} companion="none" />
</button>
```

**Pemetaan shadcn/ui** (di `tailwind-theme.css`): `--primary` = merah aksi (`aka-700`) untuk tombol utama; `--secondary` = hitam (`sumi-900`) untuk tombol sekunder; `--accent` = `aka-50` untuk hover/terpilih ringan; `--destructive` = `aka-700`; `--ring` = `sumi-900`; `--radius` = 6 px.

**Aturan**
1. Dilarang nilai warna literal (hex/rgb/hsl/oklch) dan kelas arbitrer (`bg-[#B9050E]`) di komponen maupun `style={{…}}`.
2. Komponen shadcn hasil generate disesuaikan memakai token, bukan warna literal.
3. Fase 0 menambahkan pemeriksaan otomatis `npm run lint:tokens` yang gagal bila menemukan warna literal atau kelas palet bawaan Tailwind di `web/src`.
4. Mengubah token: ubah `design/tokens.css` + tabel di dokumen ini + cek ulang kontras.
5. Mode gelap belum termasuk cakupan; struktur token semantik sudah siap bila nanti ditambahkan.

# 06 · Teks Multibahasa & Jepang Pendamping

## 1. Tujuan ✅

Murid dan sensei ikut belajar bahasa Jepang saat memakai web. Setiap teks tampil dalam **bahasa UI** (Indonesia atau Inggris) dan bisa didampingi **teks Jepang** (kanji, furigana, kana, romaji) sesuai preferensi pengguna.

## 2. Empat jenis teks

| Jenis | Contoh | Penyimpanan | Aturan |
|---|---|---|---|
| **Label UI** | Menu "Latihan", tombol "Simpan" | Kode frontend `web/src/i18n/labels/*` | Wajib id + en + ja (§6) |
| **TextBlock** (konten pendek) | Judul bab, prompt soal, pilihan jawaban, kalimat contoh | `jsonb` ([03](03-skema-data.md) §3.1) | Validasi per pemakaian |
| **RichText** (konten panjang) | Isi materi bacaan, penjelasan tata bahasa | `jsonb` ([03](03-skema-data.md) §3.2) | Markdown terbatas + markup furigana |
| **Teks operasional** | Nama kelas, topik pertemuan, komentar, feedback | `varchar`/`text` | Ditulis bebas oleh staf, tidak diterjemahkan |

## 3. Markup furigana

Format satu baris yang mudah diketik sensei, dipakai di label UI, form konten, dan RichText.

```
{漢字|かんじ}        → {"t":"漢字","r":"かんじ"}            kanji + bacaan (furigana)
{は||wa}             → {"t":"は","ro":"wa"}                tanpa furigana, romaji di-override (partikel)
{日本|にほん|nihon}  → {"t":"日本","r":"にほん","ro":"nihon"} bacaan + override romaji
します               → {"t":"します"}                       teks di luar kurung = segmen biasa (digabung)
(spasi ASCII)        → {"t":" "}                           batas kata untuk romaji; TIDAK tampil di teks Jepang
{{n}}                → placeholder                          hanya untuk label UI (§6.3), bukan furigana
\{  \}  \|  \\      → karakter literal                     escape
```

Contoh:

```
{私|わたし}{は||wa}{日本語|にほんご}{を||o}{勉強|べんきょう} します。
```

Teks Jepang tampil: `私は日本語を勉強します。` · Romaji: `watashi wa nihongo o benkyou shimasu.`

Aturan parser (frontend `furigana.ts` dan validator backend memakai aturan yang sama):
1. Kurung kurawal tidak tertutup, teks dasar kosong, atau lebih dari dua pemisah `|` → error.
2. Bacaan (`r`) hanya boleh hiragana, katakana, dan `ー`.
3. Segmen yang mengandung kanji **wajib** punya bacaan, supaya semua mode tampilan berfungsi.
4. Partikel は, へ, を yang berfungsi sebagai partikel wajib diberi override romaji (`wa`, `e`, `o`). Validator memberi peringatan untuk segmen は/へ/を yang berdiri sendiri tanpa override.
5. **Pisahkan kata dengan spasi biasa** agar romaji terbaca. Spasi tidak tampil di teks Jepang. Form editor menampilkan pratinjau romaji secara langsung.

## 4. Preferensi pengguna

| Preferensi | Nilai | Default |
|---|---|---|
| `ui_locale` | `id`, `en` | `id` |
| `script_pref` | `off`, `kanji_furigana`, `kana`, `romaji`, `kanji_furigana_romaji` | `kanji_furigana` |

Dua varian tampilan teks Jepang:

**Varian pendamping** (`companion`): teks Jepang di samping teks ID/EN, misalnya label menu "Latihan".

| `script_pref` | Tampil |
|---|---|
| `off` | Latihan |
| `kanji_furigana` | Latihan · 練習 (furigana れんしゅう di atasnya) |
| `kana` | Latihan · れんしゅう |
| `romaji` | Latihan · renshuu |
| `kanji_furigana_romaji` | Latihan · 練習 (furigana) · renshuu |

**Varian konten** (`content`): teks Jepang adalah isi utama, misalnya kalimat contoh atau soal.

| `script_pref` | Tampil |
|---|---|
| `off` | Ditulis apa adanya (kanji tanpa furigana) |
| `kanji_furigana` | Apa adanya + furigana |
| `kana` | Kanji diganti bacaannya (kana saja) |
| `romaji` | Apa adanya + baris romaji di bawahnya |
| `kanji_furigana_romaji` | Apa adanya + furigana + baris romaji |

Preferensi bisa diganti kapan saja lewat toggle di top bar dan langsung berlaku. Opsi "kanji tanpa furigana" untuk level lanjut: Q-10.

## 5. Aturan romaji (D-13 🟡)

Gaya **wāpuro** dengan vokal panjang ditulis ganda (`renshuu`, `benkyou`), sesuai contoh v1. Romaji dihasilkan di frontend, tidak disimpan.

1. Segmen dengan `ro` → pakai `ro` apa adanya.
2. Selain itu, sumber = `r` bila ada, kalau tidak `t`. Konversi dengan WanaKana `toRomaji`.
3. Pasca-proses: `ー` → ulangi vokal sebelumnya (`ラーメン` → `raamen`); `ん` sebelum vokal atau `y` → `n'` (`ほんや` → `hon'ya`); semua huruf kecil.
4. Penggabungan: antar-segmen **tanpa spasi**, kecuali segmen spasi (`" "`) dan segmen ber-`ro` (diberi spasi di kiri-kanan). Tanda baca Jepang dipetakan: `。`→`.` `、`→`,` `！`→`!` `？`→`?` `「」`→`"`. Spasi ganda dirapikan.
5. Hasil WanaKana yang berbeda dari tabel di bawah wajib dikoreksi di pasca-proses dan dikunci dengan unit test.

Kasus uji (`romaji.test.ts`):

```
Markup                                                            → Romaji
{練習|れんしゅう}                                                  → renshuu
{私|わたし}{は||wa}{日本語|にほんご}{を||o}{勉強|べんきょう}します。   → watashi wa nihongo o benkyoushimasu.
{勉強|べんきょう} します                                           → benkyou shimasu
{食|た}べる                                                        → taberu
ラーメン                                                           → raamen
コンピューター                                                      → konpyuutaa
{本屋|ほんや}                                                       → hon'ya
{禁煙|きんえん}                                                     → kin'en
きって                                                             → kitte
{東京|とうきょう}                                                   → toukyou
{学校|がっこう} {へ||e} {行|い}きます                                → gakkou e ikimasu
```

## 6. Label UI

### 6.1 Struktur file

```
web/src/i18n/
├── tb.ts          # tb(id, en, jaMarkup) → Label (parse markup sekali saat modul dimuat)
├── furigana.ts    # parseFurigana(markup) → JaSegment[]
├── romaji.ts      # toRomaji(segments) sesuai §5
├── prefs.tsx      # PrefsProvider + usePrefs() (uiLocale, scriptPref), sinkron ke /api/identity/me/preferences
└── labels/
    ├── common.ts  # save, cancel, back, next, loading, empty, retry, …
    ├── nav.ts
    ├── errors.ts  # satu label per `code` error API (05 §2)
    └── identity.ts · content.ts · classroom.ts · practice.ts · insight.ts
```

### 6.2 Contoh

```ts
// web/src/i18n/labels/nav.ts
export const nav = {
  home:     tb("Beranda",   "Home",        "ホーム"),
  learn:    tb("Materi",    "Materials",   "{教材|きょうざい}"),
  practice: tb("Latihan",   "Practice",    "{練習|れんしゅう}"),
  schedule: tb("Jadwal",    "Schedule",    "{予定|よてい}"),
  ranking:  tb("Peringkat", "Leaderboard", "ランキング"),
} as const;
```

### 6.3 Placeholder & bentuk jamak

```ts
questionCount: tb("{{n}} soal", { one: "{{n}} question", other: "{{n}} questions" }, "{{n}}{問|もん}"),
// pemakaian: t(practice.questionCount, { n: 5 })
```

Bahasa Indonesia dan Jepang tidak punya bentuk jamak; bahasa Inggris memakai `Intl.PluralRules`.

### 6.4 Aturan label

1. **Tiga bahasa wajib** (dijamin tipe `tb()`); tidak boleh ada teks UI yang ditulis langsung di JSX.
2. Kunci: `<namespace>.<camelCase>`; namespace mengikuti modul.
3. Istilah Jepang mengikuti [glosarium](glosarium.md) agar konsisten di seluruh aplikasi.
4. **Teks Jepang yang dibuat atau diubah AI** diberi komentar `// ja-review` di barisnya dan dicantumkan di laporan akhir sesi. Sensei memeriksa, lalu penanda dihapus.
5. Jangan menyusun kalimat dengan menggabung string; pakai placeholder.
6. Setiap `code` error API punya label di `errors.ts`; fallback `errors.internal_error`.
7. Atribut HTML (`aria-label`, `placeholder`, `title`, `alt`) hanya memakai teks bahasa UI (lewat `useT()`), karena atribut tidak bisa memuat ruby.

## 7. Komponen frontend

| Komponen / hook | Fungsi |
|---|---|
| `<JapaneseText value variant display? size?>` | Merender `JaSegment[]` atau TextBlock sesuai `script_pref` dan `jaDisplay`. `variant`: `companion` atau `content` (§4) |
| `<Label value companion?>` | Teks bahasa UI + pendamping Jepang. `companion`: `below`, `inline`, `tooltip`, `none` (default mengikuti §8) |
| `useT()` | Mengembalikan teks bahasa UI saja, untuk atribut dan string non-JSX |
| `<RichTextView value>` | Markdown aman (`react-markdown` tanpa HTML mentah) + plugin markup furigana → `JapaneseText` varian konten |
| `<ScriptToggle>` | Pilihan 5 mode tampilan Jepang + pratinjau langsung; disimpan ke profil (optimistic update) |
| `<LanguageSwitcher>` | ID / EN |
| `<FuriganaInput>` | Input untuk staf: mengetik markup, pratinjau ruby + romaji, menampilkan error/peringatan parser |

Aturan render:
- Semua teks Jepang dibungkus elemen dengan `lang="ja"` (agar browser memilih glyph Jepang, bukan Tionghoa) dan font `font-ja`. Romaji memakai `lang="ja-Latn"`.
- Furigana memakai `<ruby>漢字<rp>(</rp><rt>かんじ</rt><rp>)</rp></ruby>`. Ukuran `rt` 0,5em, warna `text-ink-muted`. Baris yang memuat furigana memakai line-height ruby (token `--nx-leading-ruby`).
- Teks Jepang tidak pernah dimiringkan (*italic*) dan tidak diberi letter-spacing negatif.
- Kartu belajar kana/kanji (flashcard, soal tulis) memakai font `font-learn` (Klee One, gaya tulisan tangan buku pelajaran) agar bentuk huruf sama dengan yang ditulis tangan.
- Ketuk/klik teks Jepang pendamping → popover kecil: kanji, kana, romaji, dan arti (teks bahasa UI). Segmen konten yang punya `v` (id kosakata) → popover arti dari `content.vocab`.

## 8. Kepadatan pendamping (agar UI tetap bersih)

| Lokasi | Pendamping default |
|---|---|
| Sidebar navigasi, judul halaman, judul seksi | `below` |
| Bottom navigation (HP) | `none` (muncul lewat tekan lama) |
| Empty state, onboarding, ringkasan akhir latihan | `below` |
| Tombol, label form, sel tabel, badge, toast | `none` (tombol: `tooltip`) |
| Judul dialog | `inline` |

Bila `script_pref = off`, semua pendamping disembunyikan.

## 9. Anti-bocor jawaban (`jaDisplay` di payload soal)

| `jaDisplay` | Efek | Contoh pemakaian |
|---|---|---|
| `user_pref` | Mengikuti `script_pref` pengguna (varian konten) | Soal arti kosakata |
| `plain` | Teks Jepang apa adanya: tanpa furigana, tanpa romaji, mode `kana` diabaikan | Soal membaca kanji |
| `no_romaji` | Mengikuti preferensi tetapi romaji tidak pernah tampil | Soal mengenal kana |

Setelah murid menjawab, layar umpan balik menampilkan versi lengkap (kanji + furigana + romaji + arti) sebagai bahan belajar.

## 10. Backend

- Tipe di `Nomax.SharedKernel` dengan nama properti C# yang jelas dan nama JSON pendek:

```csharp
public sealed record JaSegment(
    [property: JsonPropertyName("t")] string Text,
    [property: JsonPropertyName("r")] string? Reading = null,
    [property: JsonPropertyName("ro")] string? Romaji = null,
    [property: JsonPropertyName("v")] Guid? VocabId = null);

public sealed record TextBlock(
    [property: JsonPropertyName("id")] string? Indonesian,
    [property: JsonPropertyName("en")] string? English,
    [property: JsonPropertyName("ja")] IReadOnlyList<JaSegment>? Japanese);

public sealed record RichText(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("id")] string Indonesian,
    [property: JsonPropertyName("en")] string? English);
```

- Validator FluentValidation per pemakaian (`ForTitle`, `ForPrompt`, `ForStimulus`, `ForSentence`) sesuai [03](03-skema-data.md) §3.1 dan aturan parser §3.
- Pemetaan EF ke `jsonb`: satu cara untuk semua modul (value converter System.Text.Json + `ValueComparer`, atau pemetaan JSON bawaan EF), ditetapkan di Fase 1 dan dipakai konsisten.
- Server tidak menghasilkan romaji. Email dan web push memakai template per `ui_locale` (id/en) tanpa pendamping Jepang 🟡.
- Pencarian teks konten: `ILIKE` pada `kolom->>'id'`; `pg_trgm` bila data membesar.

## 11. Format tanggal & angka

| Hal | `id` | `en` |
|---|---|---|
| Locale `Intl` | `id-ID` | `en-GB` |
| Tanggal | Sab, 18 Okt 2026 | Sat, 18 Oct 2026 |
| Jam | 19.30 WIB (24 jam + singkatan zona) | 19:30 GMT+7 (singkatan zona dari `Intl`) |
| Angka desimal | 81,3 | 81.3 |
| Waktu relatif | 3 menit lalu | 3 minutes ago |

Semua format memakai `Intl.DateTimeFormat`, `Intl.NumberFormat`, dan `Intl.RelativeTimeFormat` dengan zona waktu dari profil pengguna. Tidak ada format tanggal manual.

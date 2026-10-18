import { tb } from "../tb";

// Japanese text below is an AI draft using docs/glosarium.md; every line is marked for review by a sensei.

export const structure = {
  title: tb("Konten", "Content", "コンテンツ"), // ja-review
  description: tb("Level, buku, dan bab yang dipakai semua kelas.", "Levels, books and chapters used by every class.", "レベル、 {教科書|きょうかしょ}、 {課|か}"), // ja-review
  levels: tb("Level", "Levels", "レベル"), // ja-review
  level: tb("Level", "Level", "レベル"), // ja-review
  books: tb("Buku", "Books", "{教科書|きょうかしょ}"), // ja-review
  book: tb("Buku", "Book", "{教科書|きょうかしょ}"), // ja-review
  chapters: tb("Bab", "Chapters", "{課|か}"), // ja-review
  chapter: tb("Bab", "Chapter", "{課|か}"), // ja-review
  chapterNumber: tb("Bab {{n}}", "Chapter {{n}}", "{第|だい}{{n}}{課|か}"), // ja-review
  addLevel: tb("Tambah level", "Add level", "レベル {追加|ついか}"), // ja-review
  addBook: tb("Tambah buku", "Add book", "{教科書|きょうかしょ} {追加|ついか}"), // ja-review
  addChapter: tb("Tambah bab", "Add chapter", "{課|か} {追加|ついか}"), // ja-review
  editLevel: tb("Ubah level", "Edit level", "レベル {編集|へんしゅう}"), // ja-review
  editBook: tb("Ubah buku", "Edit book", "{教科書|きょうかしょ} {編集|へんしゅう}"), // ja-review
  editChapter: tb("Ubah bab", "Edit chapter", "{課|か} {編集|へんしゅう}"), // ja-review
  code: tb("Kode", "Code", "コード"), // ja-review
  codeHint: tb("1–16 huruf kecil, angka, atau tanda hubung, mis. n5.", "1–16 lowercase letters, digits or hyphens, e.g. n5.", "{例|れい}: n5"), // ja-review
  number: tb("Nomor bab", "Chapter number", "{課|か} の {番号|ばんごう}"), // ja-review
  sortOrder: tb("Urutan", "Order", "{順番|じゅんばん}"), // ja-review
  chapterCount: tb("{{n}} bab", { one: "{{n}} chapter", other: "{{n}} chapters" }, "{{n}} {課|か}"), // ja-review
  emptyLevels: tb("Belum ada level", "No levels yet", "レベル {は||wa} まだ ありません"), // ja-review
  emptyBooks: tb("Belum ada buku di level ini", "No books in this level yet", "{教科書|きょうかしょ} {は||wa} まだ ありません"), // ja-review
  emptyChapters: tb("Belum ada bab di buku ini", "No chapters in this book yet", "{課|か} {は||wa} まだ ありません"), // ja-review
  deleteTitle: tb("Hapus {{name}}?", "Delete {{name}}?", "{削除|さくじょ} しますか"), // ja-review
  deleteBody: tb("Hanya bisa dihapus bila belum dipakai kelas, materi, atau data lain.", "Only possible while no class, material or other data uses it.", "{未使用|みしよう} の とき だけ {削除|さくじょ} できます"), // ja-review
  deleteConfirm: tb("Ya, hapus", "Yes, delete", "{削除|さくじょ} する"), // ja-review
  deleted: tb("Terhapus", "Deleted", "{削除|さくじょ} しました"), // ja-review
  delete: tb("Hapus", "Delete", "{削除|さくじょ}"), // ja-review
  open: tb("Buka", "Open", "{開|ひら}く"), // ja-review
} as const;

/** Fields of a TextBlock (docs/03 §3.1) in content forms. */
export const textFields = {
  indonesian: tb("Bahasa Indonesia", "Indonesian", "インドネシア{語|ご}"), // ja-review
  english: tb("Bahasa Inggris", "English", "{英語|えいご}"), // ja-review
  japanese: tb("Bahasa Jepang", "Japanese", "{日本語|にほんご}"), // ja-review
  japaneseHint: tb(
    "Tulis furigana sebagai {漢字|かんじ}, partikel sebagai {は||wa}, dan pisahkan kata dengan spasi.", // ja-review
    "Write furigana as {漢字|かんじ}, particles as {は||wa}, and separate words with spaces.", // ja-review
    "{振|ふ}り{仮名|がな} {は||wa} \\{ \\} で {囲|かこ}みます", // ja-review
  ),
  preview: tb("Pratinjau", "Preview", "プレビュー"), // ja-review
  markupError: tb("Markup Jepang belum benar: {{issue}}", "The Japanese markup needs fixing: {{issue}}", "{日本語|にほんご} の {入力|にゅうりょく} {を||o} {確認|かくにん} してください"), // ja-review
  particleWarning: tb("Partikel {{text}} sebaiknya diberi romaji, mis. {は||wa}.", "Particle {{text}} should get its romaji, e.g. {は||wa}.", "{助詞|じょし} の ローマ{字|じ} {を||o} {確認|かくにん}"), // ja-review
} as const;

export const materials = {
  title: tb("Materi", "Materials", "{教材|きょうざい}"), // ja-review
  add: tb("Tambah materi", "Add material", "{教材|きょうざい} {追加|ついか}"), // ja-review
  edit: tb("Ubah materi", "Edit material", "{教材|きょうざい} {編集|へんしゅう}"), // ja-review
  empty: tb("Belum ada materi di bab ini", "No materials in this chapter yet", "{教材|きょうざい} {は||wa} まだ ありません"), // ja-review
  emptyStudent: tb("Sensei belum menambahkan materi.", "Your sensei hasn't added materials yet.", "{先生|せんせい} が {準備中|じゅんびちゅう} です"), // ja-review
  kind: tb("Jenis", "Type", "{種類|しゅるい}"), // ja-review
  kindRichtext: tb("Bacaan", "Reading", "{読|よ}み{物|もの}"), // ja-review
  kindFile: tb("File", "File", "ファイル"), // ja-review
  kindLink: tb("Tautan", "Link", "リンク"), // ja-review
  titleField: tb("Judul", "Title", "タイトル"), // ja-review
  body: tb("Isi bacaan (Markdown)", "Reading text (Markdown)", "{本文|ほんぶん}"), // ja-review
  bodyHint: tb("Boleh: ## judul, **tebal**, *miring*, daftar, tabel, > kutipan, [tautan](https://…), dan furigana {漢字|かんじ}.", "Allowed: ## headings, **bold**, *italic*, lists, tables, > quotes, [links](https://…) and furigana {漢字|かんじ}.", "Markdown と {振|ふ}り{仮名|がな}"), // ja-review
  bodyEnglish: tb("Isi bacaan dalam bahasa Inggris", "Reading text in English", "{英語|えいご} の {本文|ほんぶん}"), // ja-review
  url: tb("Alamat tautan (https://)", "Link address (https://)", "URL"), // ja-review
  file: tb("File", "File", "ファイル"), // ja-review
  publishAt: tb("Terbit pada", "Publish at", "{公開|こうかい} {日時|にちじ}"), // ja-review
  publishAtHint: tb("Kosongkan agar langsung terlihat murid.", "Leave empty to show it to students now.", "{空欄|くうらん} なら すぐ {公開|こうかい}"), // ja-review
  scheduled: tb("Terbit {{date}}", "Publishes {{date}}", "{{date}} {公開|こうかい}"), // ja-review
  openLink: tb("Buka tautan", "Open link", "リンク {を||o} {開|ひら}く"), // ja-review
  externalLink: tb("Tautan luar ({{domain}})", "External link ({{domain}})", "{外部|がいぶ} リンク"), // ja-review
  download: tb("Unduh {{name}}", "Download {{name}}", "ダウンロード"), // ja-review
  deleteTitle: tb("Hapus materi {{name}}?", "Delete material {{name}}?", "{教材|きょうざい} {を||o} {削除|さくじょ} しますか"), // ja-review
  deleteBody: tb("Murid tidak bisa membukanya lagi. File-nya ikut dihapus.", "Students can no longer open it. Its file is deleted too.", "{生徒|せいと} {は||wa} {見|み}られなく なります"), // ja-review
  created: tb("Materi ditambahkan", "Material added", "{追加|ついか} しました"), // ja-review
  readMore: tb("Baca", "Read", "{読|よ}む"), // ja-review
} as const;

export const upload = {
  choose: tb("Pilih file", "Choose a file", "ファイル {を||o} {選|えら}ぶ"), // ja-review
  maxSize: tb("Maksimal 1 GB. Unggahan bisa dilanjutkan bila koneksi putus.", "Up to 1 GB. Uploads resume after a lost connection.", "{最大|さいだい} 1 GB"), // ja-review
  progress: tb("Mengunggah {{name}} · {{percent}}%", "Uploading {{name}} · {{percent}}%", "アップロード {中|ちゅう} {{percent}}%"), // ja-review
  paused: tb("Dijeda · {{percent}}%", "Paused · {{percent}}%", "{一時停止|いちじていし}"), // ja-review
  pause: tb("Jeda", "Pause", "{一時停止|いちじていし}"), // ja-review
  resume: tb("Lanjutkan", "Resume", "{再開|さいかい}"), // ja-review
  done: tb("{{name}} siap", "{{name}} is ready", "{完了|かんりょう}"), // ja-review
  failed: tb("Unggahan gagal. Coba lagi.", "The upload failed. Try again.", "アップロード {失敗|しっぱい}"), // ja-review
  remove: tb("Ganti file", "Change file", "ファイル {変更|へんこう}"), // ja-review
} as const;

export const chapterPage = {
  back: tb("Kembali ke daftar bab", "Back to the chapters", "{課|か} の {一覧|いちらん} {へ||e}"), // ja-review
  lockedTitle: tb("Bab ini belum dibuka", "This chapter isn't open yet", "この {課|か} {は||wa} まだ {解放|かいほう} されていません"), // ja-review
  lockedBody: tb("Sensei akan membukanya saat kelas sampai di bab ini.", "Your sensei opens it when the class reaches this chapter.", "{授業|じゅぎょう} で {解放|かいほう} されます"), // ja-review
  myMaterials: tb("Materi", "Materials", "{教材|きょうざい}"), // ja-review
  materialsDescription: tb("Bab yang sudah dibuka di kelas-kelasmu.", "Chapters opened in your classes.", "{解放|かいほう} された {課|か}"), // ja-review
  unlocked: tb("Terbuka", "Open", "{解放|かいほう}"), // ja-review
  locked: tb("Terkunci", "Locked", "ロック"), // ja-review
} as const;

export const reference = {
  title: tb("Data referensi", "Reference data", "{参照|さんしょう} データ"), // ja-review
  description: tb("Kana, kanji, kosakata, tata bahasa, dan kalimat. Setiap baris menyimpan sumber dan lisensinya.", "Kana, kanji, vocabulary, grammar and sentences. Every row keeps its source and license.", "{出典|しゅってん} と ライセンス {付|つ}き"), // ja-review
  kana: tb("Kana", "Kana", "{仮名|かな}"), // ja-review
  kanji: tb("Kanji", "Kanji", "{漢字|かんじ}"), // ja-review
  vocab: tb("Kosakata", "Vocabulary", "{語彙|ごい}"), // ja-review
  grammar: tb("Tata bahasa", "Grammar", "{文法|ぶんぽう}"), // ja-review
  sentences: tb("Kalimat", "Sentences", "{文|ぶん}"), // ja-review
  import: tb("Impor", "Import", "インポート"), // ja-review
  file: tb("File CSV atau JSON", "CSV or JSON file", "CSV または JSON ファイル"), // ja-review
  importTitle: tb("Impor {{type}}", "Import {{type}}", "インポート"), // ja-review
  importHelp: tb("CSV (pemisah koma atau titik koma) atau JSON. Daftar dalam satu sel dipisah dengan |. Kolom chapter berisi nomor bab dari buku yang dipilih.", "CSV (comma or semicolon) or JSON. Lists inside a cell are separated by |. The chapter column holds chapter numbers of the chosen book.", "CSV または JSON"), // ja-review
  columns: tb("Kolom: {{columns}}", "Columns: {{columns}}", "{列|れつ}: {{columns}}"), // ja-review
  source: tb("Sumber", "Source", "{出典|しゅってん}"), // ja-review
  license: tb("Lisensi", "License", "ライセンス"), // ja-review
  licenseHint: tb("Wajib diisi, mis. CC BY-SA 4.0 (JMdict) atau internal.", "Required, e.g. CC BY-SA 4.0 (JMdict) or internal.", "{必須|ひっす}"), // ja-review
  check: tb("Periksa file", "Check file", "ファイル {を||o} {確認|かくにん}"), // ja-review
  summary: tb("{{n}} baris · {{create}} baru · {{skip}} sudah ada", "{{n}} rows · {{create}} new · {{skip}} already exist", "{{n}} {行|ぎょう}"), // ja-review
  createN: tb("Buat {{n}} baris", { one: "Create {{n}} row", other: "Create {{n}} rows" }, "{{n}} {行|ぎょう} {作成|さくせい}"), // ja-review
  imported: tb("{{n}} baris diimpor", { one: "{{n}} row imported", other: "{{n}} rows imported" }, "{{n}} {行|ぎょう} インポート しました"), // ja-review
  errorsTitle: tb("Perbaiki baris berikut lalu periksa lagi. Belum ada data yang dibuat.", "Fix these rows and check again. Nothing was created yet.", "{修正|しゅうせい} してください"), // ja-review
  row: tb("Baris", "Row", "{行|ぎょう}"), // ja-review
  column: tb("Kolom", "Column", "{列|れつ}"), // ja-review
  problem: tb("Masalah", "Problem", "{問題|もんだい}"), // ja-review
  wholeFile: tb("seluruh file", "whole file", "ファイル {全体|ぜんたい}"), // ja-review
  defaultChapter: tb("Bab untuk semua baris", "Chapter for all rows", "{全行|ぜんぎょう} の {課|か}"), // ja-review
  noChapter: tb("Tanpa bab", "No chapter", "{課|か} なし"), // ja-review
  unmapped: tb("Belum dipetakan ke bab (belum dipakai generator soal)", "Not mapped to a chapter (not used by the question generator yet)", "{課|か} {未設定|みせってい}"), // ja-review
  mapChapter: tb("Bab", "Chapter", "{課|か}"), // ja-review
  empty: tb("Belum ada data", "No data yet", "データ {は||wa} まだ ありません"), // ja-review
  character: tb("Karakter", "Character", "{文字|もじ}"), // ja-review
  romaji: tb("Romaji", "Romaji", "ローマ{字|じ}"), // ja-review
  reading: tb("Bacaan", "Reading", "{読|よ}み"), // ja-review
  meanings: tb("Arti", "Meanings", "{意味|いみ}"), // ja-review
  pattern: tb("Pola", "Pattern", "パターン"), // ja-review
  text: tb("Kalimat", "Sentence", "{文|ぶん}"), // ja-review
  filterLevel: tb("Level", "Level", "レベル"), // ja-review
  deleteTitle: tb("Hapus baris ini?", "Delete this row?", "{削除|さくじょ} しますか"), // ja-review
  deleteBody: tb("Tidak bisa dihapus bila sudah dipakai soal.", "Not possible once a question uses it.", "{問題|もんだい} で {使用中|しようちゅう} なら {削除|さくじょ} できません"), // ja-review
} as const;

/** Cell error codes of the reference import (docs/05 §5, ReferenceImport.Codes). */
export const referenceImportErrors = {
  empty_file: tb("File kosong", "The file is empty", "ファイル が {空|から} です"), // ja-review
  missing_column: tb("Kolom wajib tidak ada", "A required column is missing", "{必須|ひっす} {列|れつ} が ありません"), // ja-review
  unknown_column: tb("Kolom tidak dikenal", "Unknown column", "{不明|ふめい} な {列|れつ}"), // ja-review
  too_many_rows: tb("Lebih dari 2.000 baris", "More than 2,000 rows", "{行数|ぎょうすう} が {多|おお}すぎます"), // ja-review
  required: tb("Wajib diisi", "Required", "{必須|ひっす}"), // ja-review
  invalid_format: tb("Format salah", "Wrong format", "{形式|けいしき} が {不正|ふせい}"), // ja-review
  too_long: tb("Terlalu panjang", "Too long", "{長|なが}すぎます"), // ja-review
  invalid_value: tb("Nilai tidak dikenal", "Unknown value", "{不正|ふせい} な {値|あたい}"), // ja-review
  duplicate_in_file: tb("Ganda di dalam file", "Duplicate in the file", "ファイル {内|ない} で {重複|ちょうふく}"), // ja-review
  unknown_chapter: tb("Nomor bab tidak ada di buku yang dipilih", "No such chapter in the chosen book", "{課|か} が {見|み}つかりません"), // ja-review
  unknown_grammar: tb("Pola tata bahasa belum ada di level ini", "Grammar pattern not found in this level", "{文法|ぶんぽう} が {見|み}つかりません"), // ja-review
} as const;

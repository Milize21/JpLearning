import { tb } from "../tb";

// Japanese text below is an AI draft using docs/glosarium.md; every line is marked for review by a sensei.
// The hanko texts (よくできました, がんばりました, がんばりましょう, 済) come from docs/07 §8.

export const categoryLabels = {
  hiragana: tb("Hiragana", "Hiragana", "{平仮名|ひらがな}"), // ja-review
  katakana: tb("Katakana", "Katakana", "{片仮名|かたかな}"), // ja-review
  kanji: tb("Kanji", "Kanji", "{漢字|かんじ}"), // ja-review
  vocab: tb("Kosakata", "Vocabulary", "{語彙|ごい}"), // ja-review
  grammar: tb("Tata bahasa", "Grammar", "{文法|ぶんぽう}"), // ja-review
  sentence: tb("Kalimat", "Sentences", "{文|ぶん}"), // ja-review
} as const;

export const masteryStatusLabels = {
  not_started: tb("Belum dicoba", "Not started", "{未学習|みがくしゅう}"), // ja-review
  learning: tb("Sedang dipelajari", "Learning", "{学習中|がくしゅうちゅう}"), // ja-review
  weak: tb("Lemah", "Weak", "{苦手|にがて}"), // ja-review
  mastered: tb("Dikuasai", "Mastered", "{習得|しゅうとく}"), // ja-review
} as const;

export const setup = {
  title: tb("Latihan", "Practice", "{練習|れんしゅう}"), // ja-review
  description: tb("Latihan tanpa batas dari bab yang sudah dibuka. Tidak memengaruhi nilai.", "Unlimited practice from open chapters. It never affects your grade.", "{何度|なんど} でも {練習|れんしゅう} できます"), // ja-review
  category: tb("Kategori", "Category", "カテゴリー"), // ja-review
  chapters: tb("Bab", "Chapters", "{課|か}"), // ja-review
  allChapters: tb("Semua bab yang terbuka", "All open chapters", "{全|すべ}て の {課|か}"), // ja-review
  mode: tb("Mode", "Mode", "モード"), // ja-review
  modeNormal: tb("Normal", "Normal", "{通常|つうじょう}"), // ja-review
  modeNormalHint: tb("Semua soal diacak, tanpa pengulangan sampai habis.", "All questions shuffled, no repeats until the end.", "ランダム {出題|しゅつだい}"), // ja-review
  modeWeak: tb("Fokus kelemahan", "Weak spots", "{苦手|にがて} {克服|こくふく}"), // ja-review
  modeWeakHint: tb("Lebih sering menampilkan soal yang belum kamu kuasai.", "Shows what you haven't mastered more often.", "{苦手|にがて} な {問題|もんだい} {中心|ちゅうしん}"), // ja-review
  input: tb("Cara menjawab", "How to answer", "{回答|かいとう} {方法|ほうほう}"), // ja-review
  inputChoice: tb("Pilih & susun", "Choose & build", "{選択|せんたく}"), // ja-review
  inputType: tb("Ketik jawaban", "Type the answer", "{入力|にゅうりょく}"), // ja-review
  start: tb("Mulai", "Start", "{開始|かいし}"), // ja-review
  noChapters: tb("Belum ada bab yang terbuka. Sensei akan membukanya di kelasmu.", "No chapter is open yet. Your sensei opens them in your class.", "{解放|かいほう} された {課|か} が ありません"), // ja-review
  recent: tb("Latihan terakhir", "Recent practice", "{最近|さいきん} の {練習|れんしゅう}"), // ja-review
  sessionLine: tb("{{correct}}/{{total}} benar", "{{correct}}/{{total}} correct", "{{correct}}/{{total}} {正解|せいかい}"), // ja-review
  weakLink: tb("Lihat kelemahanku", "See my weak spots", "{苦手|にがて} {一覧|いちらん}"), // ja-review
} as const;

export const drill = {
  exit: tb("Keluar", "Exit", "{終了|しゅうりょう}"), // ja-review
  exitTitle: tb("Akhiri latihan?", "End practice?", "{練習|れんしゅう} {を||o} {終|お}わりますか"), // ja-review
  exitBody: tb("Jawabanmu sudah tersimpan. Kamu akan melihat ringkasannya.", "Your answers are saved. You'll see a summary.", "{回答|かいとう} {は||wa} {保存|ほぞん} されています"), // ja-review
  exitConfirm: tb("Ya, akhiri", "Yes, end", "{終|お}わる"), // ja-review
  answered: tb("Jawaban {{n}}", "Answers {{n}}", "{回答|かいとう} {{n}}"), // ja-review
  streak: tb("Beruntun {{n}}", "Streak {{n}}", "{連続|れんぞく} {{n}}"), // ja-review
  submit: tb("Kirim", "Submit", "{送信|そうしん}"), // ja-review
  next: tb("Lanjut", "Next", "{次|つぎ}{へ||e}"), // ja-review
  correct: tb("Benar!", "Correct!", "{正解|せいかい}"), // ja-review
  wrong: tb("Belum tepat", "Not quite", "{不正解|ふせいかい}"), // ja-review
  theAnswer: tb("Jawaban benar", "Correct answer", "{正|ただ}しい {答|こた}え"), // ja-review
  explanation: tb("Pembahasan", "Explanation", "{解説|かいせつ}"), // ja-review
  typeKanaHint: tb("Ketik romaji, otomatis menjadi kana.", "Type romaji; it turns into kana.", "ローマ{字|じ} で {入力|にゅうりょく}"), // ja-review
  typeRomajiHint: tb("Ketik dalam huruf latin (romaji).", "Type in Latin letters (romaji).", "ローマ{字|じ} で {入力|にゅうりょく}"), // ja-review
  typeLabel: tb("Jawabanmu", "Your answer", "あなた の {答|こた}え"), // ja-review
  multiHint: tb("Pilih semua jawaban yang benar.", "Choose every correct answer.", "{正|ただ}しい もの {を||o} {全|すべ}て {選|えら}ぶ"), // ja-review
  tapHint: tb("Ketuk kata untuk menyusun kalimat. Ketuk lagi untuk melepas.", "Tap words to build the sentence. Tap again to remove.", "{単語|たんご} {を||o} タップ"), // ja-review
  yourSentence: tb("Kalimatmu", "Your sentence", "あなた の {文|ぶん}"), // ja-review
  words: tb("Pilihan kata", "Words", "{単語|たんご}"), // ja-review
  reset: tb("Ulangi", "Reset", "やり{直|なお}す"), // ja-review
  matchHint: tb("Pilih kiri, lalu pasangannya di kanan.", "Pick a left item, then its partner on the right.", "{左|ひだり} と {右|みぎ} {を||o} つなぐ"), // ja-review
  pairWith: tb("Pasangkan {{left}} dengan {{right}}", "Pair {{left}} with {{right}}", "つなぐ"), // ja-review
  flip: tb("Balik kartu", "Flip card", "カード {を||o} めくる"), // ja-review
  notYet: tb("Belum ingat", "Not yet", "まだ"), // ja-review
  remembered: tb("Ingat", "I knew it", "{覚|おぼ}えた"), // ja-review
  autoNext: tb("Lanjut otomatis setelah benar", "Continue automatically when correct", "{自動|じどう} で {次|つぎ}{へ||e}"), // ja-review
  fallback: tb("Data kelemahanmu belum cukup, jadi ini latihan normal dulu.", "Not enough weak-spot data yet, so this is a normal round first.", "{通常|つうじょう} モード で {始|はじ}めます"), // ja-review
  shortcuts: tb("Pintasan: 1–6 pilih · Enter kirim/lanjut · Spasi balik kartu · Esc keluar", "Shortcuts: 1–6 choose · Enter submit/next · Space flip · Esc exit", "ショートカット"), // ja-review
  loading: tb("Menyiapkan soal…", "Getting questions ready…", "{準備中|じゅんびちゅう}…"), // ja-review
  progress: tb("Soal {{n}} dari {{total}}", "Question {{n}} of {{total}}", "{{n}} / {{total}}"), // ja-review
  question: tb("Soal", "Question", "{問題|もんだい}"), // ja-review
  choices: tb("Pilihan jawaban", "Answer choices", "{選択肢|せんたくし}"), // ja-review
  masteryUp: tb("Penguasaan {{before}}% → {{after}}%", "Mastery {{before}}% → {{after}}%", "{習得度|しゅうとくど} {{before}}% → {{after}}%"), // ja-review
} as const;

export const summary = {
  title: tb("Ringkasan latihan", "Practice summary", "{練習|れんしゅう} の まとめ"), // ja-review
  answered: tb("{{n}} jawaban", { one: "{{n}} answer", other: "{{n}} answers" }, "{回答|かいとう} {{n}}"), // ja-review
  accuracy: tb("Akurasi {{pct}}%", "Accuracy {{pct}}%", "{正答率|せいとうりつ} {{pct}}%"), // ja-review
  newlyMastered: tb("Baru dikuasai", "Newly mastered", "{新|あら}たに {習得|しゅうとく}"), // ja-review
  stillWeak: tb("Masih lemah", "Still weak", "まだ {苦手|にがて}"), // ja-review
  none: tb("Tidak ada", "None", "なし"), // ja-review
  practiceWeak: tb("Latih kelemahan", "Practise weak spots", "{苦手|にがて} {を||o} {練習|れんしゅう}"), // ja-review
  again: tb("Latihan lagi", "Practise again", "もう {一度|いちど}"), // ja-review
  home: tb("Kembali ke beranda", "Back to home", "ホーム {へ||e}"), // ja-review
  stampGreat: tb("Bagus sekali", "Very good", "よくできました"), // ja-review
  stampTried: tb("Kamu sudah berusaha", "You tried hard", "がんばりました"), // ja-review
  stampKeepGoing: tb("Ayo terus semangat", "Keep it up", "がんばりましょう"), // ja-review
  stampDone: tb("Selesai", "Done", "{済|ずみ}"), // ja-review
} as const;

export const weaknesses = {
  title: tb("Kelemahanku", "My weak spots", "{苦手|にがて}"), // ja-review
  description: tb("Dihitung dari jawaban latihanmu. Kuis tidak ikut dihitung.", "Based on your practice answers; quizzes are not counted.", "{練習|れんしゅう} の {結果|けっか} から"), // ja-review
  groups: tb("Kelompok terlemah", "Weakest groups", "{苦手|にがて} な グループ"), // ja-review
  items: tb("Item terlemah", "Weakest items", "{苦手|にがて} な {項目|こうもく}"), // ja-review
  empty: tb("Belum ada data. Mulai latihan dulu.", "No data yet. Start practising first.", "まだ データ が ありません"), // ja-review
  practiceThis: tb("Latih ini", "Practise this", "これ {を||o} {練習|れんしゅう}"), // ja-review
  itemCount: tb("{{n}} item", { one: "{{n}} item", other: "{{n}} items" }, "{{n}} {項目|こうもく}"), // ja-review
  weakCount: tb("{{n}} lemah", "{{n}} weak", "{苦手|にがて} {{n}}"), // ja-review
  averageMastery: tb("Rata-rata penguasaan {{pct}}%", "Average mastery {{pct}}%", "{平均|へいきん} {{pct}}%"), // ja-review
  accuracy: tb("Akurasi {{pct}}%", "Accuracy {{pct}}%", "{正答率|せいとうりつ} {{pct}}%"), // ja-review
  attempts: tb("{{n}}× dijawab", "Answered {{n}}×", "{{n}} {回|かい}"), // ja-review
  kanaRow: tb("Baris {{row}}", "Row {{row}}", "{{row}} {行|ぎょう}"), // ja-review
  particle: tb("Partikel {{name}}", "Particle {{name}}", "{助詞|じょし} {{name}}"), // ja-review
  tag: tb("Tag {{name}}", "Tag {{name}}", "タグ {{name}}"), // ja-review
  chapter: tb("Bab", "Chapter", "{課|か}"), // ja-review
} as const;

export const kindLabels = {
  mcq: tb("Pilihan ganda", "Multiple choice", "{選択|せんたく}"), // ja-review
  tap_sentence: tb("Susun kalimat", "Build a sentence", "{並|なら}べ{替|か}え"), // ja-review
  type_answer: tb("Ketik jawaban", "Typed answer", "{入力|にゅうりょく}"), // ja-review
  match: tb("Pasangkan", "Matching", "マッチング"), // ja-review
  flashcard: tb("Kartu hafalan", "Flashcard", "フラッシュカード"), // ja-review
  handwrite_kana: tb("Tulis kana", "Write kana", "{仮名|かな} {書|か}き"), // ja-review
  handwrite_kanji: tb("Tulis kanji", "Write kanji", "{漢字|かんじ} {書|か}き"), // ja-review
  long_answer: tb("Jawaban panjang", "Long answer", "{記述|きじゅつ}"), // ja-review
} as const;

export const questionStatusLabels = {
  draft: tb("Draf", "Draft", "{下書|したが}き"), // ja-review
  review: tb("Review", "In review", "レビュー {中|ちゅう}"), // ja-review
  published: tb("Terbit", "Published", "{公開|こうかい}"), // ja-review
  archived: tb("Diarsipkan", "Archived", "アーカイブ"), // ja-review
} as const;

export const scopeLabels = {
  builtin: tb("Bawaan", "Built-in", "{共通|きょうつう}"), // ja-review
  private: tb("Pribadi", "Private", "{個人|こじん}"), // ja-review
} as const;

export const jaDisplayLabels = {
  user_pref: tb("Ikuti tampilan murid", "Follow the student's display", "{生徒|せいと} の {設定|せってい}"), // ja-review
  plain: tb("Teks polos: tanpa furigana & romaji", "Plain: no furigana or romaji", "ふりがな なし"), // ja-review
  no_romaji: tb("Tanpa romaji", "No romaji", "ローマ{字|じ} なし"), // ja-review
} as const;

export const bank = {
  title: tb("Bank soal", "Question bank", "{問題集|もんだいしゅう}"), // ja-review
  description: tb("Soal bawaan dan soal milikmu. Soal yang sudah dijawab murid isinya terkunci.", "Built-in questions and your own. Answered questions keep their content.", "{問題|もんだい} の {一覧|いちらん}"), // ja-review
  create: tb("Buat soal", "New question", "{問題|もんだい} {作成|さくせい}"), // ja-review
  edit: tb("Ubah soal", "Edit question", "{問題|もんだい} {編集|へんしゅう}"), // ja-review
  search: tb("Cari di instruksi soal", "Search the instructions", "{検索|けんさく}"), // ja-review
  kind: tb("Jenis", "Kind", "{種類|しゅるい}"), // ja-review
  status: tb("Status", "Status", "{状態|じょうたい}"), // ja-review
  scope: tb("Cakupan", "Scope", "{範囲|はんい}"), // ja-review
  level: tb("Level", "Level", "レベル"), // ja-review
  chapter: tb("Bab", "Chapter", "{課|か}"), // ja-review
  category: tb("Kategori", "Category", "カテゴリー"), // ja-review
  all: tb("Semua", "All", "すべて"), // ja-review
  empty: tb("Belum ada soal untuk filter ini", "No questions for these filters", "{問題|もんだい} が ありません"), // ja-review
  count: tb("{{n}} soal", { one: "{{n}} question", other: "{{n}} questions" }, "{{n}} {問|もん}"), // ja-review
  answered: tb("Sudah dijawab", "Answered", "{回答|かいとう} あり"), // ja-review
  locked: tb("Murid sudah menjawab soal ini, jadi isinya terkunci. Instruksi, pembahasan, dan tag tetap bisa diubah. Duplikat soal untuk mengubah isi.", "Students already answered this question, so its content is locked. Instructions, explanation and tags can still change. Duplicate it to change the content.", "{内容|ないよう} {は||wa} {変更|へんこう} できません"), // ja-review
  submitReview: tb("Ajukan review", "Submit for review", "レビュー {依頼|いらい}"), // ja-review
  publish: tb("Terbitkan", "Publish", "{公開|こうかい}"), // ja-review
  archive: tb("Arsipkan", "Archive", "アーカイブ"), // ja-review
  duplicate: tb("Duplikat", "Duplicate", "{複製|ふくせい}"), // ja-review
  preview: tb("Pratinjau murid", "Student preview", "{生徒|せいと} {画面|がめん}"), // ja-review
  tryIt: tb("Coba jawab", "Try it", "{試|ため}す"), // ja-review
  previewNote: tb("Uji coba tidak menyimpan jawaban dan tidak mengubah penguasaan murid.", "Trying does not save answers or change anyone's mastery.", "{保存|ほぞん} されません"), // ja-review
  done: tb("Tersimpan", "Saved", "{保存|ほぞん} しました"), // ja-review
  actionDone: tb("Status soal diperbarui", "Question status updated", "{更新|こうしん} しました"), // ja-review
  duplicated: tb("Salinan draf dibuat", "Draft copy created", "{複製|ふくせい} しました"), // ja-review
  selectForPublish: tb("Pilih soal {{name}}", "Select question {{name}}", "{選択|せんたく}"), // ja-review
  publishSelected: tb("Terbitkan yang dipilih ({{n}})", "Publish selected ({{n}})", "{選択|せんたく} した {問題|もんだい} {を||o} {公開|こうかい}"), // ja-review
  publishedMany: tb("{{n}} soal diterbitkan", { one: "{{n}} question published", other: "{{n}} questions published" }, "{{n}} {問|もん} {公開|こうかい}"), // ja-review
  sources: tb("Sumber soal", "Question sources", "{出典|しゅってん}"), // ja-review
  generator: tb("Generator", "Generator", "{自動|じどう} {生成|せいせい}"), // ja-review
} as const;

export const editor = {
  prompt: tb("Instruksi soal", "Instructions", "{指示|しじ}"), // ja-review
  stimulus: tb("Stimulus (yang ditanyakan)", "Stimulus (what is asked)", "{問題文|もんだいぶん}"), // ja-review
  stimulusOptional: tb("Stimulus (opsional)", "Stimulus (optional)", "{問題文|もんだいぶん}"), // ja-review
  choices: tb("Pilihan", "Choices", "{選択肢|せんたくし}"), // ja-review
  choice: tb("Pilihan {{n}}", "Choice {{n}}", "{選択肢|せんたくし} {{n}}"), // ja-review
  isCorrect: tb("Benar", "Correct", "{正解|せいかい}"), // ja-review
  multi: tb("Boleh lebih dari satu jawaban benar", "More than one correct answer", "{複数|ふくすう} {正解|せいかい}"), // ja-review
  addChoice: tb("Tambah pilihan", "Add choice", "{追加|ついか}"), // ja-review
  remove: tb("Hapus", "Remove", "{削除|さくじょ}"), // ja-review
  tokens: tb("Potongan kata (urutan benar dari atas)", "Words (in the correct order, top to bottom)", "{単語|たんご}"), // ja-review
  token: tb("Kata {{n}}", "Word {{n}}", "{単語|たんご} {{n}}"), // ja-review
  addToken: tb("Tambah kata", "Add word", "{追加|ついか}"), // ja-review
  accepted: tb("Jawaban yang diterima (satu per baris)", "Accepted answers (one per line)", "{正解|せいかい}"), // ja-review
  answerScript: tb("Huruf jawaban", "Answer script", "{文字|もじ}"), // ja-review
  scriptKana: tb("Kana (murid mengetik romaji, otomatis jadi kana)", "Kana (students type romaji, converted to kana)", "{仮名|かな}"), // ja-review
  scriptRomaji: tb("Romaji", "Romaji", "ローマ{字|じ}"), // ja-review
  scriptFree: tb("Bebas", "Free text", "{自由|じゆう}"), // ja-review
  kanaInsensitive: tb("Hiragana dan katakana dianggap sama", "Treat hiragana and katakana alike", "ひらがな と カタカナ {を||o} {区別|くべつ} しない"), // ja-review
  pairs: tb("Pasangan", "Pairs", "ペア"), // ja-review
  left: tb("Kiri {{n}}", "Left {{n}}", "{左|ひだり} {{n}}"), // ja-review
  right: tb("Kanan {{n}}", "Right {{n}}", "{右|みぎ} {{n}}"), // ja-review
  addPair: tb("Tambah pasangan", "Add pair", "{追加|ついか}"), // ja-review
  front: tb("Depan kartu", "Front", "{表|おもて}"), // ja-review
  back: tb("Belakang kartu", "Back", "{裏|うら}"), // ja-review
  explanation: tb("Pembahasan (opsional, Markdown)", "Explanation (optional, Markdown)", "{解説|かいせつ}"), // ja-review
  tags: tb("Tag (pisahkan dengan koma), mis. kana_row:ka, particle:wa", "Tags (comma separated), e.g. kana_row:ka, particle:wa", "タグ"), // ja-review
  jaDisplay: tb("Tampilan teks Jepang", "Japanese display", "{日本語|にほんご} の {表示|ひょうじ}"), // ja-review
  jaDisplayHint: tb("Cegah jawaban bocor, mis. sembunyikan furigana pada soal membaca kanji.", "Prevent leaks, e.g. hide furigana when the question asks for the reading.", "{答|こた}え が {見|み}えない よう に"), // ja-review
  notSupported: tb("Jenis ini belum bisa dibuat di editor.", "This kind can't be created in the editor yet.", "{未対応|みたいおう}"), // ja-review
  needCorrect: tb("Tandai minimal satu pilihan yang benar.", "Mark at least one correct choice.", "{正解|せいかい} {を||o} {選|えら}んでください"), // ja-review
} as const;

export const generator = {
  title: tb("Generator soal", "Question generator", "{問題|もんだい} の {自動|じどう} {生成|せいせい}"), // ja-review
  description: tb("Membuat soal bawaan dari data referensi yang sudah dipetakan ke bab. Hasilnya masuk review dulu.", "Creates built-in questions from reference data mapped to chapters. Results go to review first.", "レビュー {後|ご} に {公開|こうかい}"), // ja-review
  level: tb("Level", "Level", "レベル"), // ja-review
  templates: tb("Template", "Templates", "テンプレート"), // ja-review
  run: tb("Buat soal", "Generate", "{生成|せいせい}"), // ja-review
  result: tb("{{created}} soal dibuat, {{skipped}} dilewati karena sudah ada.", "{{created}} questions created, {{skipped}} skipped because they exist.", "{{created}} {問|もん} {作成|さくせい}"), // ja-review
  review: tb("Tinjau hasil", "Review results", "{確認|かくにん}"), // ja-review
  kana_to_romaji: tb("Kana → pilih romaji", "Kana → choose romaji", "{仮名|かな} → ローマ{字|じ}"), // ja-review
  romaji_to_kana: tb("Romaji → pilih kana", "Romaji → choose kana", "ローマ{字|じ} → {仮名|かな}"), // ja-review
  kana_type_romaji: tb("Kana → ketik romaji", "Kana → type romaji", "{仮名|かな} → {入力|にゅうりょく}"), // ja-review
  vocab_to_meaning: tb("Kata → pilih arti", "Word → choose meaning", "{単語|たんご} → {意味|いみ}"), // ja-review
  meaning_to_vocab: tb("Arti → pilih kata", "Meaning → choose word", "{意味|いみ} → {単語|たんご}"), // ja-review
  vocab_type_reading: tb("Kata berkanji → ketik bacaan", "Kanji word → type reading", "{読|よ}み {入力|にゅうりょく}"), // ja-review
  kanji_to_reading: tb("Kanji → pilih bacaan", "Kanji → choose reading", "{漢字|かんじ} → {読|よ}み"), // ja-review
  kanji_to_meaning: tb("Kanji → pilih arti", "Kanji → choose meaning", "{漢字|かんじ} → {意味|いみ}"), // ja-review
  sentence_tap: tb("Terjemahan → susun kalimat", "Translation → build the sentence", "{並|なら}べ{替|か}え"), // ja-review
} as const;

export const sources = {
  title: tb("Sumber soal", "Question sources", "{出典|しゅってん}"), // ja-review
  description: tb("Setiap soal wajib punya sumber dan lisensi. Soal JLPT resmi atau PDF tanpa izin tidak boleh dimasukkan.", "Every question needs a source and a license. Official JLPT questions or unlicensed PDFs are not allowed.", "{出典|しゅってん} と ライセンス {は||wa} {必須|ひっす}"), // ja-review
  add: tb("Tambah sumber", "Add source", "{出典|しゅってん} {追加|ついか}"), // ja-review
  name: tb("Nama", "Name", "{名前|なまえ}"), // ja-review
  license: tb("Lisensi", "License", "ライセンス"), // ja-review
  url: tb("Alamat (https://, opsional)", "Address (https://, optional)", "URL"), // ja-review
  reviewed: tb("Lisensi direview {{date}}", "License reviewed {{date}}", "{{date}} {確認済|かくにんず}み"), // ja-review
  notReviewed: tb("Lisensi belum direview", "License not reviewed yet", "{未確認|みかくにん}"), // ja-review
  markReviewed: tb("Tandai lisensi sudah direview", "Mark license as reviewed", "{確認済|かくにんず}み に する"), // ja-review
  empty: tb("Belum ada sumber", "No sources yet", "{出典|しゅってん} {は||wa} まだ ありません"), // ja-review
} as const;

export const progress = {
  tab: tb("Progres", "Progress", "{進捗|しんちょく}"), // ja-review
  title: tb("Progres latihan", "Practice progress", "{練習|れんしゅう} の {進捗|しんちょく}"), // ja-review
  description: tb("Dari jawaban drilling murid di level kelas ini. Drilling tidak memengaruhi nilai.", "From students' drill answers in this class's level. Drilling never affects grades.", "{成績|せいせき} に {影響|えいきょう} しません"), // ja-review
  student: tb("Murid", "Student", "{生徒|せいと}"), // ja-review
  lastPracticed: tb("Terakhir latihan", "Last practice", "{最終|さいしゅう} {練習|れんしゅう}"), // ja-review
  answers7d: tb("Jawaban 7 hari", "Answers (7 days)", "7{日間|かかん} の {回答|かいとう}"), // ja-review
  accuracy: tb("Akurasi", "Accuracy", "{正答率|せいとうりつ}"), // ja-review
  averageMastery: tb("Rata-rata penguasaan", "Average mastery", "{平均|へいきん} {習得度|しゅうとくど}"), // ja-review
  topWeakness: tb("Kelemahan teratas", "Top weakness", "{一番|いちばん} の {苦手|にがて}"), // ja-review
  never: tb("Belum pernah", "Never", "なし"), // ja-review
  empty: tb("Belum ada murid di kelas ini", "No students in this class yet", "{生徒|せいと} が いません"), // ja-review
  detail: tb("Detail murid", "Student details", "{生徒|せいと} {詳細|しょうさい}"), // ja-review
  sessions: tb("Sesi latihan terbaru", "Recent drill sessions", "{最近|さいきん} の {練習|れんしゅう}"), // ja-review
  noSessions: tb("Belum ada sesi latihan", "No drill sessions yet", "まだ ありません"), // ja-review
  comments: tb("Komentar", "Comments", "コメント"), // ja-review
  commentLabel: tb("Komentar untuk murid (tanpa nilai)", "Comment for the student (no grade)", "コメント"), // ja-review
  send: tb("Kirim komentar", "Send comment", "{送信|そうしん}"), // ja-review
  sent: tb("Komentar terkirim", "Comment sent", "{送信|そうしん} しました"), // ja-review
  noComments: tb("Belum ada komentar", "No comments yet", "コメント {は||wa} ありません"), // ja-review
  back: tb("Kembali ke progres kelas", "Back to class progress", "{進捗|しんちょく} {へ||e} {戻|もど}る"), // ja-review
  forSession: tb("Komentar untuk sesi ini", "Comment on this session", "この {練習|れんしゅう} {へ||e} の コメント"), // ja-review
  fromTeacher: tb("Dari {{name}}", "From {{name}}", "{{name}} より"), // ja-review
  myComments: tb("Komentar sensei", "Comments from your sensei", "{先生|せんせい} から の コメント"), // ja-review
} as const;

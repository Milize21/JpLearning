import { tb } from "../tb";

// Japanese text below is an AI draft using docs/glosarium.md; every line is marked for review by a sensei.

export const quizKindLabels = {
  quiz: tb("Kuis", "Quiz", "{小|しょう}テスト"), // ja-review
  exam: tb("Ujian", "Exam", "{試験|しけん}"), // ja-review
} as const;

export const publishLabels = {
  draft: tb("Draf", "Draft", "{下書|したが}き"), // ja-review
  published: tb("Terbit", "Published", "{公開|こうかい}"), // ja-review
  archived: tb("Diarsipkan", "Archived", "アーカイブ"), // ja-review
  publish: tb("Terbitkan", "Publish", "{公開|こうかい}"), // ja-review
  archive: tb("Arsipkan", "Archive", "アーカイブ"), // ja-review
  done: tb("Status diperbarui", "Status updated", "{更新|こうしん} しました"), // ja-review
} as const;

export const quizzes = {
  title: tb("Kuis & ujian", "Quizzes & exams", "{小|しょう}テスト と {試験|しけん}"), // ja-review
  description: tb("Dikerjakan dengan batas waktu. Nilainya masuk ke nilai kelas.", "Taken with a time limit. They count toward your class grade.", "{成績|せいせき} に {入|はい}ります"), // ja-review
  create: tb("Buat kuis", "New quiz", "{作成|さくせい}"), // ja-review
  edit: tb("Ubah kuis", "Edit quiz", "{編集|へんしゅう}"), // ja-review
  empty: tb("Belum ada kuis", "No quizzes yet", "まだ ありません"), // ja-review
  emptyStudent: tb("Belum ada kuis atau ujian untukmu.", "No quizzes or exams for you yet.", "まだ ありません"), // ja-review
  name: tb("Judul", "Title", "タイトル"), // ja-review
  kind: tb("Jenis", "Kind", "{種類|しゅるい}"), // ja-review
  timeLimit: tb("Batas waktu (menit, kosongkan bila tanpa batas)", "Time limit (minutes, empty for none)", "{制限時間|せいげんじかん}"), // ja-review
  maxAttempts: tb("Kesempatan mengerjakan", "Attempts allowed", "{受験|じゅけん} {回数|かいすう}"), // ja-review
  openAt: tb("Dibuka (opsional)", "Opens (optional)", "{開始|かいし}"), // ja-review
  closeAt: tb("Ditutup (opsional)", "Closes (optional)", "{締切|しめきり}"), // ja-review
  shuffle: tb("Acak urutan soal dan pilihan", "Shuffle questions and choices", "ランダム {出題|しゅつだい}"), // ja-review
  showReview: tb("Tampilkan pembahasan setelah ditutup atau kesempatan habis", "Show the review after closing or when attempts run out", "{解説|かいせつ} {を||o} {表示|ひょうじ}"), // ja-review
  locked: tb("Sudah ada murid yang mengerjakan: soal, poin, jenis, dan batas waktu terkunci.", "Students already started: questions, points, kind and time limit are locked.", "{変更|へんこう} できない {項目|こうもく} が あります"), // ja-review
  questions: tb("Soal", "Questions", "{問題|もんだい}"), // ja-review
  questionCount: tb("{{n}} soal · {{points}} poin", "{{n}} questions · {{points}} points", "{{n}} {問|もん}"), // ja-review
  pickQuestions: tb("Pilih soal dari bank", "Pick questions from the bank", "{問題|もんだい} {を||o} {選|えら}ぶ"), // ja-review
  pickHint: tb("Hanya soal terbit. Urutan mengikuti urutan pilihan.", "Published questions only. The order follows your picks.", "{公開|こうかい} {済|ず}み の {問題|もんだい}"), // ja-review
  points: tb("Poin", "Points", "{配点|はいてん}"), // ja-review
  selected: tb("Terpilih ({{n}})", "Selected ({{n}})", "{選択|せんたく} {{n}}"), // ja-review
  moveUp: tb("Naikkan", "Move up", "{上|うえ}{へ||e}"), // ja-review
  attempts: tb("Pengerjaan", "Attempts", "{受験|じゅけん}"), // ja-review
  noAttempts: tb("Belum ada yang mengerjakan", "No attempts yet", "まだ ありません"), // ja-review
  pending: tb("{{n}} menunggu dinilai", "{{n}} to grade", "{採点待|さいてんま}ち {{n}}"), // ja-review
  gradeAnswer: tb("Nilai jawaban ini", "Grade this answer", "{採点|さいてん}"), // ja-review
  givePoints: tb("Poin (maks. {{max}})", "Points (max {{max}})", "{点数|てんすう}"), // ja-review
  graded: tb("Jawaban dinilai", "Answer graded", "{採点|さいてん} しました"), // ja-review
  studentAnswer: tb("Jawaban murid", "Student's answer", "{生徒|せいと} の {答|こた}え"), // ja-review
  noAnswer: tb("Tidak dijawab", "Not answered", "{未回答|みかいとう}"), // ja-review
  window: tb("{{open}} – {{close}}", "{{open}} – {{close}}", "{{open}} – {{close}}"),
  minutes: tb("{{n}} menit", { one: "{{n}} minute", other: "{{n}} minutes" }, "{{n}} {分|ふん}"), // ja-review
  noLimit: tb("Tanpa batas waktu", "No time limit", "{時間|じかん} {制限|せいげん} なし"), // ja-review
  attemptsUsed: tb("Kesempatan {{used}}/{{max}}", "Attempts {{used}}/{{max}}", "{{used}}/{{max}} {回|かい}"), // ja-review
  best: tb("Skor terbaik {{pct}}%", "Best score {{pct}}%", "{最高|さいこう} {{pct}}%"), // ja-review
  start: tb("Mulai", "Start", "{開始|かいし}"), // ja-review
  resume: tb("Lanjutkan", "Continue", "{続|つづ}ける"), // ja-review
  result: tb("Lihat hasil", "See result", "{結果|けっか}"), // ja-review
  closed: tb("Ditutup", "Closed", "{終了|しゅうりょう}"), // ja-review
  notYetOpen: tb("Belum dibuka", "Not open yet", "{開始前|かいしまえ}"), // ja-review
  startConfirm: tb("Mulai sekarang?", "Start now?", "{始|はじ}めますか"), // ja-review
  startBody: tb("Waktu berjalan di server sejak kamu mulai, meskipun halaman ditutup.", "The timer runs on the server from the start, even if you close the page.", "{時間|じかん} {は||wa} {止|と}まりません"), // ja-review
} as const;

export const attempt = {
  timeLeft: tb("Sisa waktu {{time}}", "Time left {{time}}", "{残|のこ}り {{time}}"), // ja-review
  saved: tb("Tersimpan", "Saved", "{保存|ほぞん} しました"), // ja-review
  saving: tb("Menyimpan…", "Saving…", "{保存中|ほぞんちゅう}…"), // ja-review
  saveFailed: tb("Gagal menyimpan. Coba lagi.", "Couldn't save. Try again.", "{保存|ほぞん} できませんでした"), // ja-review
  answered: tb("Sudah dijawab", "Answered", "{回答済|かいとうず}み"), // ja-review
  unanswered: tb("Belum dijawab", "Not answered", "{未回答|みかいとう}"), // ja-review
  question: tb("Soal {{n}}", "Question {{n}}", "{第|だい}{{n}}{問|もん}"), // ja-review
  navigator: tb("Daftar soal", "Question list", "{問題|もんだい} {一覧|いちらん}"), // ja-review
  previous: tb("Sebelumnya", "Previous", "{前|まえ}{へ||e}"), // ja-review
  next: tb("Berikutnya", "Next", "{次|つぎ}{へ||e}"), // ja-review
  submit: tb("Kirim jawaban", "Submit answers", "{提出|ていしゅつ}"), // ja-review
  submitTitle: tb("Kirim semua jawaban?", "Submit all answers?", "{提出|ていしゅつ} しますか"), // ja-review
  submitBody: tb("{{n}} soal belum dijawab. Setelah dikirim, jawaban tidak bisa diubah.", "{{n}} questions are unanswered. Answers can't change after submitting.", "{未回答|みかいとう} {{n}} {問|もん}"), // ja-review
  submitConfirm: tb("Ya, kirim", "Yes, submit", "{提出|ていしゅつ} する"), // ja-review
  longPlaceholder: tb("Tulis jawabanmu di sini", "Write your answer here", "{答|こた}え {を||o} {書|か}く"), // ja-review
  saveLong: tb("Simpan jawaban", "Save answer", "{保存|ほぞん}"), // ja-review
  yourAnswer: tb("Jawabanmu", "Your answer", "あなた の {答|こた}え"), // ja-review
  savedAnswer: tb("Jawaban tersimpan: {{value}}", "Saved answer: {{value}}", "{保存|ほぞん}: {{value}}"), // ja-review
  resultTitle: tb("Hasil", "Result", "{結果|けっか}"), // ja-review
  score: tb("Skor {{pct}}%", "Score {{pct}}%", "{得点|とくてん} {{pct}}%"), // ja-review
  waitingManual: tb("Jawaban esai sedang dinilai sensei. Skor muncul setelah itu.", "Your sensei is grading the written answers. The score appears afterwards.", "{採点中|さいてんちゅう}"), // ja-review
  reviewHidden: tb("Pembahasan muncul setelah kuis ditutup atau kesempatanmu habis.", "The review appears after the quiz closes or your attempts run out.", "{解説|かいせつ} {は||wa} {後|あと} で {表示|ひょうじ}"), // ja-review
  noReview: tb("Sensei hanya menampilkan skor untuk kuis ini.", "Your sensei only shows the score for this quiz.", "{得点|とくてん} のみ"), // ja-review
  earned: tb("{{earned}}/{{points}} poin", "{{earned}}/{{points}} points", "{{earned}}/{{points}}"), // ja-review
  backToList: tb("Kembali ke daftar kuis", "Back to quizzes", "{一覧|いちらん}{へ||e}"), // ja-review
  expired: tb("Waktu habis. Jawaban yang tersimpan sudah dikirim.", "Time is up. Your saved answers were submitted.", "{時間切|じかんぎ}れ"), // ja-review
} as const;

export const assignments = {
  title: tb("Tugas", "Assignments", "{課題|かだい}"), // ja-review
  description: tb("Kirim tulisan atau file sebelum tenggat. Kiriman terlambat tetap diterima dan ditandai.", "Send text or files before the due time. Late work is accepted and marked.", "{締切|しめきり} {前|まえ} に {提出|ていしゅつ}"), // ja-review
  create: tb("Buat tugas", "New assignment", "{課題|かだい} {作成|さくせい}"), // ja-review
  edit: tb("Ubah tugas", "Edit assignment", "{課題|かだい} {編集|へんしゅう}"), // ja-review
  empty: tb("Belum ada tugas", "No assignments yet", "まだ ありません"), // ja-review
  name: tb("Judul", "Title", "タイトル"), // ja-review
  instructions: tb("Instruksi (Markdown)", "Instructions (Markdown)", "{指示|しじ}"), // ja-review
  dueDate: tb("Tanggal tenggat", "Due date", "{締切日|しめきりび}"), // ja-review
  dueTime: tb("Jam tenggat", "Due time", "{締切|しめきり} {時刻|じこく}"), // ja-review
  due: tb("Tenggat {{date}}", "Due {{date}}", "{締切|しめきり} {{date}}"), // ja-review
  allowResubmit: tb("Boleh kirim ulang sebelum dinilai", "Allow resubmitting before grading", "{再提出|さいていしゅつ} {可|か}"), // ja-review
  submissions: tb("Kiriman", "Submissions", "{提出物|ていしゅつぶつ}"), // ja-review
  submissionCount: tb("{{n}} kiriman · {{graded}} dinilai", "{{n}} submitted · {{graded}} graded", "{{n}} {件|けん}"), // ja-review
  noSubmissions: tb("Belum ada kiriman", "No submissions yet", "まだ ありません"), // ja-review
  late: tb("Terlambat", "Late", "{遅延|ちえん}"), // ja-review
  notSubmitted: tb("Belum dikirim", "Not submitted", "{未提出|みていしゅつ}"), // ja-review
  submitted: tb("Terkirim {{date}}", "Submitted {{date}}", "{{date}} {提出|ていしゅつ}"), // ja-review
  gradedLabel: tb("Dinilai: {{pct}}", "Graded: {{pct}}", "{採点済|さいてんず}み: {{pct}}"), // ja-review
  yourText: tb("Jawaban tertulis (opsional bila ada file)", "Written answer (optional with files)", "{本文|ほんぶん}"), // ja-review
  files: tb("File (maks. 10)", "Files (up to 10)", "ファイル"), // ja-review
  addFile: tb("Tambah file", "Add file", "ファイル {追加|ついか}"), // ja-review
  removeFile: tb("Hapus {{name}}", "Remove {{name}}", "{削除|さくじょ}"), // ja-review
  send: tb("Kirim tugas", "Submit assignment", "{提出|ていしゅつ}"), // ja-review
  resend: tb("Kirim ulang", "Resubmit", "{再提出|さいていしゅつ}"), // ja-review
  sent: tb("Tugas terkirim", "Assignment submitted", "{提出|ていしゅつ} しました"), // ja-review
  needContent: tb("Tulis jawaban atau lampirkan file.", "Write an answer or attach a file.", "{本文|ほんぶん} か ファイル が {必要|ひつよう}"), // ja-review
  feedback: tb("Feedback sensei", "Feedback from your sensei", "{先生|せんせい} から の コメント"), // ja-review
  grade: tb("Nilai (0–100)", "Grade (0–100)", "{点数|てんすう}"), // ja-review
  feedbackField: tb("Feedback (opsional)", "Feedback (optional)", "コメント"), // ja-review
  saveGrade: tb("Simpan nilai", "Save grade", "{保存|ほぞん}"), // ja-review
  gradeSaved: tb("Nilai tersimpan", "Grade saved", "{保存|ほぞん} しました"), // ja-review
  open: tb("Buka", "Open", "{開|ひら}く"), // ja-review
  backToList: tb("Kembali ke daftar tugas", "Back to assignments", "{一覧|いちらん}{へ||e}"), // ja-review
} as const;

export const grades = {
  title: tb("Nilai", "Grades", "{成績|せいせき}"), // ja-review
  myTitle: tb("Nilai saya", "My grades", "{私|わたし} の {成績|せいせき}"), // ja-review
  description: tb("Skala 0–4 dari kuis, ujian, dan tugas. Latihan tidak memengaruhi nilai.", "0–4 scale from quizzes, exams and assignments. Practice never affects grades.", "{練習|れんしゅう} {は||wa} {含|ふく}みません"), // ja-review
  empty: tb("Belum ada nilai", "No grades yet", "まだ ありません"), // ja-review
  noComponent: tb("Belum ada komponen yang dinilai", "Nothing graded yet", "{評価|ひょうか} なし"), // ja-review
  final: tb("Nilai akhir", "Final", "{最終|さいしゅう}"), // ja-review
  quizAvg: tb("Rata-rata kuis", "Quiz average", "{小|しょう}テスト {平均|へいきん}"), // ja-review
  examAvg: tb("Rata-rata ujian", "Exam average", "{試験|しけん} {平均|へいきん}"), // ja-review
  assignmentAvg: tb("Rata-rata tugas", "Assignment average", "{課題|かだい} {平均|へいきん}"), // ja-review
  weight: tb("bobot {{w}}%", "weight {{w}}%", "{比重|ひじゅう} {{w}}%"), // ja-review
  student: tb("Murid", "Student", "{生徒|せいと}"), // ja-review
  scheme: tb("Skema nilai", "Grade scheme", "{評価|ひょうか} {基準|きじゅん}"), // ja-review
  schemeHint: tb("Bobot harus berjumlah 100. Tabel skala dibaca dari batas tertinggi.", "Weights must add up to 100. The scale is read from the highest minimum.", "{合計|ごうけい} 100"), // ja-review
  wQuiz: tb("Bobot kuis (%)", "Quiz weight (%)", "{小|しょう}テスト"), // ja-review
  wExam: tb("Bobot ujian (%)", "Exam weight (%)", "{試験|しけん}"), // ja-review
  wAssignment: tb("Bobot tugas (%)", "Assignment weight (%)", "{課題|かだい}"), // ja-review
  scaleMin: tb("Mulai dari (%)", "From (%)", "{以上|いじょう}"), // ja-review
  scaleGpa: tb("Nilai 0–4", "Grade 0–4", "{評点|ひょうてん}"), // ja-review
  addStep: tb("Tambah baris", "Add row", "{追加|ついか}"), // ja-review
  saveScheme: tb("Simpan skema", "Save scheme", "{保存|ほぞん}"), // ja-review
  schemeSaved: tb("Skema disimpan. Nilai dihitung ulang.", "Scheme saved. Grades recomputed.", "{保存|ほぞん} しました"), // ja-review
  weightsInvalid: tb("Bobot harus 0 atau lebih dan berjumlah 100.", "Weights must be 0 or more and add up to 100.", "{合計|ごうけい} {は||wa} 100"), // ja-review
  defaultScheme: tb("Memakai skema bawaan", "Using the default scheme", "{標準|ひょうじゅん}"), // ja-review
} as const;

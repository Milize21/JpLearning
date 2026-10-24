import { tb } from "../tb";

export const nav = {
  home: tb("Beranda", "Home", "ホーム"), // ja-review
  profile: tb("Profil", "Profile", "プロフィール"), // ja-review
  users: tb("Pengguna", "Users", "ユーザー"), // ja-review
  content: tb("Konten", "Content", "コンテンツ"), // ja-review
  reference: tb("Data referensi", "Reference data", "{参照|さんしょう} データ"), // ja-review
  classes: tb("Kelas", "Classes", "クラス"), // ja-review
  myClasses: tb("Kelas saya", "My classes", "クラス"), // ja-review
  materials: tb("Materi", "Materials", "{教材|きょうざい}"), // ja-review
  schedule: tb("Jadwal", "Schedule", "{予定|よてい}"), // ja-review
  practice: tb("Latihan", "Practice", "{練習|れんしゅう}"), // ja-review
  weaknesses: tb("Kelemahan", "Weak spots", "{苦手|にがて}"), // ja-review
  questionBank: tb("Bank soal", "Question bank", "{問題集|もんだいしゅう}"), // ja-review
  questionSources: tb("Sumber soal", "Question sources", "{出典|しゅってん}"), // ja-review
  quizzes: tb("Kuis & ujian", "Quizzes & exams", "{小|しょう}テスト"), // ja-review
  assignments: tb("Tugas", "Assignments", "{課題|かだい}"), // ja-review
  grades: tb("Nilai", "Grades", "{成績|せいせき}"), // ja-review
  login: tb("Masuk", "Log in", "ログイン"), // ja-review
  logout: tb("Keluar", "Log out", "ログアウト"), // ja-review
  mainNavigation: tb("Navigasi utama", "Main navigation", "メイン メニュー"), // ja-review
  profileMenu: tb("Menu profil", "Profile menu", "プロフィール メニュー"), // ja-review
  language: tb("Bahasa", "Language", "{言語|げんご}"), // ja-review
  japaneseDisplay: tb("Tampilan Jepang", "Japanese display", "{日本語|にほんご} の {表示|ひょうじ}"), // ja-review
} as const;

export const roles = {
  admin: tb("Admin", "Admin", "{管理者|かんりしゃ}"), // ja-review
  sensei: tb("Sensei", "Teacher", "{先生|せんせい}"), // ja-review
  murid: tb("Murid", "Student", "{生徒|せいと}"), // ja-review
} as const;

export const languages = {
  id: tb("Bahasa Indonesia", "Indonesian", "インドネシア{語|ご}"), // ja-review
  en: tb("Bahasa Inggris", "English", "{英語|えいご}"), // ja-review
} as const;

export const scriptModes = {
  off: tb("Tanpa pendamping Jepang", "No Japanese companion", "{日本語|にほんご} なし"), // ja-review
  kanji_furigana: tb("Kanji + furigana", "Kanji + furigana", "{漢字|かんじ} + {振|ふ}り{仮名|がな}"), // ja-review
  kana: tb("Kana saja", "Kana only", "{仮名|かな} のみ"), // ja-review
  romaji: tb("Romaji", "Romaji", "ローマ{字|じ}"), // ja-review
  kanji_furigana_romaji: tb(
    "Kanji + furigana + romaji",
    "Kanji + furigana + romaji",
    "{漢字|かんじ} + {振|ふ}り{仮名|がな} + ローマ{字|じ}", // ja-review
  ),
  previewWord: tb("Latihan", "Practice", "{練習|れんしゅう}"), // ja-review
} as const;

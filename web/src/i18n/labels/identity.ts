import { tb, type UiLabel } from "../tb";

export const welcome = {
  title: tb("Belajar bahasa Jepang bersama komunitas", "Learn Japanese with your community", "{一緒|いっしょ}に {日本語|にほんご} {を||o} {学|まな}ぼう"), // ja-review
  body: tb("Materi, latihan, jadwal kelas, dan peringkat dalam satu tempat.", "Materials, practice, class schedules and rankings in one place.", "{教材|きょうざい}、 {練習|れんしゅう}、 {予定|よてい}、 ランキング"), // ja-review
  redirecting: tb("Mengarahkan ke halaman masuk…", "Taking you to the login page…", "ログイン {画面|がめん} {へ||e} {移動中|いどうちゅう}…"), // ja-review
  signingIn: tb("Sedang masuk…", "Signing you in…", "ログイン {中|ちゅう}…"), // ja-review
  signInFailed: tb("Gagal masuk. Coba lagi.", "Couldn't sign you in. Please try again.", "ログイン できませんでした"), // ja-review
} as const;

export const pages = {
  forbiddenTitle: tb("Halaman ini bukan untuk peranmu", "This page isn't for your role", "この ページ に アクセス できません"), // ja-review
  forbiddenBody: tb("Kamu masuk sebagai {{role}}. Kembali ke berandamu untuk melanjutkan.", "You are signed in as {{role}}. Go back to your home page to continue.", "{権限|けんげん} が ありません"), // ja-review
  notFoundTitle: tb("Halaman tidak ditemukan", "Page not found", "ページ が {見|み}つかりません"), // ja-review
  notFoundBody: tb("Alamat ini tidak ada. Periksa lagi tautannya.", "This address doesn't exist. Check the link.", "アドレス {を||o} {確認|かくにん} してください"), // ja-review
  inactiveTitle: tb("Akunmu dinonaktifkan", "Your account is deactivated", "アカウント が {無効|むこう} です"), // ja-review
  inactiveBody: tb("Hubungi admin komunitas untuk mengaktifkannya lagi.", "Contact a community admin to reactivate it.", "{管理者|かんりしゃ} に {連絡|れんらく} してください"), // ja-review
  loadFailed: tb("Data gagal dimuat.", "Couldn't load the data.", "{読|よ}み{込|こ}み に {失敗|しっぱい} しました"), // ja-review
} as const;

export const home = {
  greeting: tb("Halo, {{name}}!", "Hi, {{name}}!", "こんにちは"), // ja-review
  muridEmptyTitle: tb("Belum ada kelas", "No classes yet", "クラス {は||wa} まだ ありません"), // ja-review
  muridEmptyBody: tb("Sensei akan menambahkanmu ke kelas. Materi, latihan, dan jadwal muncul di sini setelahnya.", "Your sensei will add you to a class. Materials, practice and schedules will appear here.", "{先生|せんせい} が クラス に {追加|ついか} します"), // ja-review
  senseiEmptyTitle: tb("Belum ada kelas yang kamu ajar", "You don't teach a class yet", "{担当|たんとう} クラス {は||wa} まだ ありません"), // ja-review
  senseiEmptyBody: tb("Kelas yang kamu ajar akan muncul di sini.", "Classes you teach will appear here.", "ここ に {表示|ひょうじ} されます"), // ja-review
  adminTitle: tb("Ringkasan admin", "Admin overview", "{管理|かんり}"), // ja-review
  adminUsersCard: tb("Buat dan kelola akun murid, sensei, dan admin.", "Create and manage student, teacher and admin accounts.", "アカウント {管理|かんり}"), // ja-review
  openUsers: tb("Buka daftar pengguna", "Open users", "ユーザー {一覧|いちらん}"), // ja-review
  activeAccounts: tb("Akun aktif", "Active accounts", "{有効|ゆうこう} アカウント"), // ja-review
} as const;

export const profile = {
  account: tb("Akun", "Account", "アカウント"), // ja-review
  username: tb("Username", "Username", "ユーザー{名|めい}"), // ja-review
  displayName: tb("Nama tampilan", "Display name", "{表示名|ひょうじめい}"), // ja-review
  email: tb("Email", "Email", "メール"), // ja-review
  role: tb("Peran", "Role", "{役割|やくわり}"), // ja-review
  timezone: tb("Zona waktu", "Time zone", "タイムゾーン"), // ja-review
  noEmail: tb("Belum ada email", "No email", "メール なし"), // ja-review
  displayNameHint: tb("Nama tampilan muncul di peringkat dan hanya bisa diubah admin.", "Your display name appears on the leaderboard and only admins can change it.", "{表示名|ひょうじめい} {は||wa} {管理者|かんりしゃ} が {変更|へんこう} します"), // ja-review
  preferences: tb("Preferensi", "Preferences", "{設定|せってい}"), // ja-review
  uiLanguage: tb("Bahasa tampilan", "Display language", "{表示|ひょうじ} {言語|げんご}"), // ja-review
  previewLabel: tb("Pratinjau", "Preview", "プレビュー"), // ja-review
} as const;

export const onboarding = {
  title: tb("Selamat datang!", "Welcome!", "ようこそ"), // ja-review
  step: tb("Langkah {{step}} dari {{total}}", "Step {{step}} of {{total}}", "{{step}} / {{total}}"),
  languageQuestion: tb("Pilih bahasa tampilan", "Choose your display language", "{表示|ひょうじ} {言語|げんご} {を||o} {選|えら}んで ください"), // ja-review
  scriptQuestion: tb("Bagaimana teks Jepang ditampilkan?", "How should Japanese text be shown?", "{日本語|にほんご} の {表示|ひょうじ} {を||o} {選|えら}んで ください"), // ja-review
  hint: tb("Bisa diganti kapan saja lewat tombol あ di bagian atas.", "You can change this any time with the あ button at the top.", "いつでも {変更|へんこう} できます"), // ja-review
  start: tb("Mulai", "Start", "{開始|かいし}"), // ja-review
} as const;

export const users = {
  title: tb("Pengguna", "Users", "ユーザー"), // ja-review
  description: tb("Akun dibuat oleh admin. Password hanya disimpan di layanan login.", "Accounts are created by admins. Passwords live only in the login service.", "アカウント {管理|かんり}"), // ja-review
  add: tb("Tambah akun", "Add account", "アカウント {追加|ついか}"), // ja-review
  importCsv: tb("Impor CSV", "Import CSV", "CSV インポート"), // ja-review
  searchPlaceholder: tb("Cari nama, username, atau email", "Search name, username or email", "{検索|けんさく}"), // ja-review
  filterRole: tb("Filter peran", "Filter by role", "{役割|やくわり} で {絞|しぼ}り{込|こ}み"), // ja-review
  filterStatus: tb("Filter status", "Filter by status", "{状態|じょうたい} で {絞|しぼ}り{込|こ}み"), // ja-review
  empty: tb("Tidak ada akun yang cocok", "No matching accounts", "{該当|がいとう} する アカウント {は||wa} ありません"), // ja-review
  createTitle: tb("Tambah akun", "Add account", "アカウント {追加|ついか}"), // ja-review
  createSubmit: tb("Buat akun", "Create account", "アカウント {作成|さくせい}"), // ja-review
  usernameHint: tb("3–64 karakter: huruf kecil, angka, titik, garis bawah, atau tanda hubung.", "3–64 characters: lowercase letters, digits, dots, underscores or hyphens.", "{英小文字|えいこもじ} と {数字|すうじ}"), // ja-review
  emailHint: tb("Opsional. Dipakai untuk pengingat jadwal.", "Optional. Used for schedule reminders.", "リマインダー に {使|つか}います"), // ja-review
  emailClearHint: tb("Kosongkan untuk menghapus email.", "Leave empty to remove the email.", "{空欄|くうらん} で {削除|さくじょ}"), // ja-review
  created: tb("Akun dibuat", "Account created", "{作成|さくせい} しました"), // ja-review
  updated: tb("Perubahan tersimpan", "Changes saved", "{保存|ほぞん} しました"), // ja-review
  tempPasswordTitle: tb("Password sementara", "Temporary password", "{仮|かり} パスワード"), // ja-review
  tempPasswordWarning: tb("Salin dan berikan ke pemilik akun sekarang. Password ini hanya ditampilkan sekali dan wajib diganti saat masuk pertama kali.", "Copy it and give it to the account owner now. It is shown only once and must be changed at the first login.", "この パスワード {は||wa} {一度|いちど} だけ {表示|ひょうじ} されます"), // ja-review
  editTitle: tb("Ubah akun", "Edit account", "アカウント {編集|へんしゅう}"), // ja-review
  resetPassword: tb("Reset password", "Reset password", "パスワード {再発行|さいはっこう}"), // ja-review
  resetConfirmTitle: tb("Reset password {{name}}?", "Reset {{name}}'s password?", "パスワード {を||o} {再発行|さいはっこう} しますか"), // ja-review
  resetConfirmBody: tb("Semua sesi akun ini berakhir dan pemiliknya harus membuat password baru saat masuk.", "All sessions of this account end and the owner must set a new password at the next login.", "{再|さい}ログイン が {必要|ひつよう} です"), // ja-review
  resetConfirm: tb("Ya, reset password", "Yes, reset password", "{再発行|さいはっこう} する"), // ja-review
  deactivate: tb("Nonaktifkan", "Deactivate", "{無効化|むこうか}"), // ja-review
  activate: tb("Aktifkan", "Activate", "{有効化|ゆうこうか}"), // ja-review
  deactivateConfirmTitle: tb("Nonaktifkan {{name}}?", "Deactivate {{name}}?", "{無効化|むこうか} しますか"), // ja-review
  deactivateConfirmBody: tb("Akun ini tidak bisa masuk dan semua sesinya berakhir. Data belajarnya tetap tersimpan.", "This account can no longer log in and all its sessions end. Its learning data is kept.", "ログイン できなく なります"), // ja-review
  deactivateConfirm: tb("Ya, nonaktifkan akun", "Yes, deactivate", "{無効化|むこうか} する"), // ja-review
  activated: tb("Akun diaktifkan", "Account activated", "{有効|ゆうこう} に しました"), // ja-review
  deactivated: tb("Akun dinonaktifkan", "Account deactivated", "{無効|むこう} に しました"), // ja-review
  rowActions: tb("Aksi untuk {{name}}", "Actions for {{name}}", "{操作|そうさ}"), // ja-review
} as const;

export const importUsers = {
  title: tb("Impor akun dari CSV", "Import accounts from CSV", "CSV から インポート"), // ja-review
  help: tb("Kolom wajib: username, display_name, role (sensei atau murid). Opsional: email, timezone, ui_locale. Maksimal 500 baris; pemisah koma atau titik koma.", "Required columns: username, display_name, role (sensei or murid). Optional: email, timezone, ui_locale. Up to 500 rows; comma or semicolon separated.", "{最大|さいだい} 500 {行|ぎょう}"), // ja-review
  chooseFile: tb("File CSV", "CSV file", "CSV ファイル"), // ja-review
  check: tb("Periksa file", "Check file", "ファイル {を||o} {確認|かくにん}"), // ja-review
  summary: tb("{{n}} baris · {{create}} akun baru · {{skip}} sudah ada", "{{n}} rows · {{create}} new · {{skip}} already exist", "{{n}} {行|ぎょう}"), // ja-review
  errorsTitle: tb("Perbaiki baris berikut lalu periksa lagi. Belum ada akun yang dibuat.", "Fix these rows and check again. No account was created.", "{修正|しゅうせい} して ください"), // ja-review
  row: tb("Baris", "Row", "{行|ぎょう}"), // ja-review
  column: tb("Kolom", "Column", "{列|れつ}"), // ja-review
  problem: tb("Masalah", "Problem", "{問題|もんだい}"), // ja-review
  create: tb("Buat {{n}} akun", { one: "Create {{n}} account", other: "Create {{n}} accounts" }, "{{n}} {件|けん} {作成|さくせい}"), // ja-review
  nothingNew: tb("Tidak ada akun baru di file ini.", "There are no new accounts in this file.", "{新|あたら}しい アカウント {は||wa} ありません"), // ja-review
  done: tb("{{n}} akun dibuat", { one: "{{n}} account created", other: "{{n}} accounts created" }, "{{n}} {件|けん} {作成|さくせい} しました"), // ja-review
  stopped: tb("Impor berhenti di baris {{row}} ({{username}}). Akun sebelumnya sudah dibuat; impor ulang file yang sama akan melewatinya.", "The import stopped at row {{row}} ({{username}}). Earlier accounts were created; importing the same file again skips them.", "{途中|とちゅう} で {停止|ていし} しました"), // ja-review
  downloadPasswords: tb("Unduh file password", "Download password file", "パスワード ファイル {を||o} ダウンロード"), // ja-review
  downloadOnce: tb("File ini hanya tersedia sekarang dan tidak disimpan di server. Unduh sebelum menutup jendela ini.", "This file is available only now and is not stored on the server. Download it before closing this window.", "{一度|いちど} だけ ダウンロード できます"), // ja-review
} as const;

/** Client-side and server-side field messages in forms. */
export const fieldErrors = {
  required: tb("Wajib diisi", "Required", "{必須|ひっす}"), // ja-review
  invalid: tb("Isian tidak valid", "Invalid value", "{入力|にゅうりょく} が {正|ただ}しくありません"), // ja-review
  invalidUsername: tb("3–64 karakter: a–z, 0–9, titik, garis bawah, tanda hubung", "3–64 characters: a–z, 0–9, dot, underscore, hyphen", "{英小文字|えいこもじ} と {数字|すうじ}"), // ja-review
  invalidEmail: tb("Alamat email tidak valid", "Invalid email address", "メール アドレス が {正|ただ}しくありません"), // ja-review
  tooLong: tb("Maksimal {{max}} karakter", "At most {{max}} characters", "{{max}} {文字|もじ} まで"), // ja-review
} as const;

/** Labels for the per-cell CSV error codes of docs/05 §4. */
export const importErrors = {
  empty_file: tb("File kosong", "The file is empty", "ファイル が {空|から} です"), // ja-review
  missing_column: tb("Kolom wajib tidak ada", "Required column missing", "{必須|ひっす} {列|れつ} が ありません"), // ja-review
  unknown_column: tb("Kolom tidak dikenal", "Unknown column", "{不明|ふめい}な {列|れつ}"), // ja-review
  too_many_rows: tb("Lebih dari 500 baris", "More than 500 rows", "500 {行|ぎょう} {を||o} {超|こ}えています"), // ja-review
  required: tb("Wajib diisi", "Required", "{必須|ひっす}"), // ja-review
  invalid_format: tb("Format tidak valid", "Invalid format", "{形式|けいしき} が {正|ただ}しくありません"), // ja-review
  too_long: tb("Terlalu panjang", "Too long", "{長|なが}すぎます"), // ja-review
  invalid_role: tb("Peran harus sensei atau murid", "Role must be sensei or murid", "{役割|やくわり} が {正|ただ}しくありません"), // ja-review
  invalid_timezone: tb("Zona waktu tidak dikenal", "Unknown time zone", "タイムゾーン が {正|ただ}しくありません"), // ja-review
  invalid_locale: tb("Bahasa harus id atau en", "Language must be id or en", "{言語|げんご} が {正|ただ}しくありません"), // ja-review
  duplicate_in_file: tb("Ganda di file ini", "Duplicate in this file", "ファイル {内|ない} で {重複|ちょうふく}"), // ja-review
  email_taken: tb("Email sudah dipakai akun lain", "Email used by another account", "メール {は||wa} {使用済|しようず}み です"), // ja-review
} as const satisfies Record<string, UiLabel>;

export function importErrorLabel(code: string): UiLabel {
  return code in importErrors ? importErrors[code as keyof typeof importErrors] : importErrors.invalid_format;
}

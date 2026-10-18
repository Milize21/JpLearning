import { tb, type UiLabel } from "../tb";

/** One label per error code of docs/05 §2 (docs/06 §6.4 rule 6). */
export const errors = {
  validation_failed: tb("Ada isian yang belum benar. Periksa lagi.", "Some fields need attention.", "{入力|にゅうりょく} {内容|ないよう} {を||o} {確認|かくにん} してください"), // ja-review
  unauthorized: tb("Sesi kamu berakhir. Silakan masuk lagi.", "Your session has ended. Please log in again.", "もう {一度|いちど} ログイン してください"), // ja-review
  forbidden: tb("Kamu tidak punya akses untuk ini.", "You don't have access to this.", "アクセス {権|けん} が ありません"), // ja-review
  account_inactive: tb("Akunmu dinonaktifkan. Hubungi admin.", "Your account is deactivated. Contact an admin.", "アカウント が {無効|むこう} です"), // ja-review
  chapter_locked: tb("Bab ini belum dibuka sensei.", "This chapter isn't open yet.", "この {課|か} {は||wa} まだ {解放|かいほう} されていません"), // ja-review
  not_found: tb("Data tidak ditemukan.", "Not found.", "{見|み}つかりません"), // ja-review
  method_not_allowed: tb("Permintaan ini tidak didukung.", "This request isn't supported.", "この {操作|そうさ} {は||wa} できません"), // ja-review
  conflict: tb("Data ini sudah dipakai.", "This is already in use.", "すでに {使|つか}われています"), // ja-review
  concurrency_conflict: tb("Data sudah diubah orang lain. Muat ulang lalu coba lagi.", "Someone else changed this. Reload and try again.", "{他|ほか} の {人|ひと} が {変更|へんこう} しました"), // ja-review
  has_dependents: tb("Masih dipakai data lain.", "Still in use elsewhere.", "{使用中|しようちゅう} です"), // ja-review
  invalid_state: tb("Tindakan ini tidak bisa dilakukan sekarang.", "This action isn't possible right now.", "{今|いま} {は||wa} この {操作|そうさ} が できません"), // ja-review
  already_submitted: tb("Sudah dikirim.", "Already submitted.", "{提出済|ていしゅつず}み です"), // ja-review
  attempts_exhausted: tb("Kesempatan mengerjakan sudah habis.", "No attempts left.", "{受験|じゅけん} {回数|かいすう} の {上限|じょうげん} です"), // ja-review
  attempt_expired: tb("Waktu mengerjakan sudah habis.", "Time is up.", "{時間切|じかんぎ}れ です"), // ja-review
  quiz_not_open: tb("Kuis belum dibuka atau sudah ditutup.", "This quiz isn't open.", "{受付|うけつけ} {期間外|きかんがい} です"), // ja-review
  session_ended: tb("Sesi latihan sudah berakhir.", "This practice session has ended.", "{練習|れんしゅう} {は||wa} {終了|しゅうりょう} しました"), // ja-review
  empty_pool: tb("Belum ada soal untuk pilihan ini.", "No questions for this selection yet.", "{問題|もんだい} が ありません"), // ja-review
  file_not_ready: tb("File belum selesai diunggah.", "The file isn't ready yet.", "ファイル の {準備|じゅんび} が できていません"), // ja-review
  payload_too_large: tb("Data terlalu besar.", "The request is too large.", "データ が {大|おお}きすぎます"), // ja-review
  file_too_large: tb("File lebih dari 1 GB.", "The file is larger than 1 GB.", "ファイル が {大|おお}きすぎます"), // ja-review
  unsupported_media_type: tb("Format tidak didukung.", "Unsupported format.", "{対応|たいおう} していない {形式|けいしき} です"), // ja-review
  rate_limited: tb("Terlalu banyak permintaan. Tunggu sebentar.", "Too many requests. Please wait a moment.", "しばらく {待|ま}って ください"), // ja-review
  keycloak_unavailable: tb("Layanan login sedang tidak tersedia. Coba lagi nanti.", "The login service is unavailable. Try again later.", "ログイン サービス に {接続|せつぞく} できません"), // ja-review
  internal_error: tb("Terjadi kesalahan. Coba lagi.", "Something went wrong. Please try again.", "エラー が {発生|はっせい} しました"), // ja-review
  network: tb("Tidak bisa terhubung ke server.", "Can't reach the server.", "サーバー に {接続|せつぞく} できません"), // ja-review
} as const satisfies Record<string, UiLabel>;

export type ErrorCode = keyof typeof errors;

export function errorLabel(code: string | undefined): UiLabel {
  return code !== undefined && code in errors ? errors[code as ErrorCode] : errors.internal_error;
}

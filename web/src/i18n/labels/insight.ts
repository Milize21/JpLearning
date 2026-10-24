import { tb } from "../tb";

/** Leaderboard, seasons and activity (Phase 4, docs/07 §11 flow 11). */
export const leaderboard = {
  title: tb("Peringkat", "Leaderboard", "ランキング"), // ja-review
  description: tb(
    "Poin dari drilling dan kuis di season aktif. Hanya nama tampilan, poin, dan peringkat yang terlihat.",
    "Points from drilling and quizzes in the active season. Only display names, points and ranks are shown.",
    "{練習|れんしゅう} と {小|しょう}テスト の ポイント", // ja-review
  ),
  level: tb("Level", "Level", "レベル"), // ja-review
  live: tb("Live", "Live", "ライブ"), // ja-review
  liveHint: tb("Papan diperbarui otomatis", "The board updates automatically", "{自動|じどう} で {更新|こうしん}"), // ja-review
  offline: tb("Tidak tersambung", "Not connected", "{未接続|みせつぞく}"), // ja-review
  rank: tb("Peringkat", "Rank", "{順位|じゅんい}"), // ja-review
  name: tb("Nama", "Name", "{名前|なまえ}"), // ja-review
  points: tb("Poin", "Points", "ポイント"), // ja-review
  pointsValue: tb("{{n}} poin", "{{n}} points", "{{n}} ポイント"), // ja-review
  rankValue: tb("#{{n}}", "#{{n}}", "{{n}} {位|い}"), // ja-review
  you: tb("Kamu", "You", "あなた"), // ja-review
  myPosition: tb("Posisimu", "Your position", "あなた の {順位|じゅんい}"), // ja-review
  noPointsYet: tb(
    "Kamu belum punya poin di season ini. Jawab soal drilling dengan benar untuk masuk papan.",
    "You have no points in this season yet. Answer drill questions correctly to get on the board.",
    "まだ ポイント が ありません", // ja-review
  ),
  emptyMine: tb("Belum ada poin — ayo latihan", "No points yet — time to practice", "まだ ポイント が ありません"), // ja-review
  emptyBoard: tb("Belum ada poin di season ini", "No points in this season yet", "まだ ポイント が ありません"), // ja-review
  noLevels: tb(
    "Belum ada papan peringkat. Papan muncul setelah kamu tergabung di kelas.",
    "No leaderboard yet. It appears once you are in a class.",
    "ランキング {は||wa} まだ ありません", // ja-review
  ),
  podium: tb("Tiga besar", "Top three", "トップ 3"), // ja-review
  list: tb("Daftar peringkat", "Ranking list", "{順位表|じゅんいひょう}"), // ja-review
  archive: tb("Arsip season", "Season archive", "シーズン の アーカイブ"), // ja-review
  archiveEmpty: tb("Belum ada season yang ditutup", "No closed seasons yet", "{終了|しゅうりょう} した シーズン {は||wa} ありません"), // ja-review
  viewStandings: tb("Lihat peringkat", "View ranking", "{順位|じゅんい} {を||o} {見|み}る"), // ja-review
  standingsOf: tb("Peringkat {{name}}", "Ranking of {{name}}", "{{name}} の {順位|じゅんい}"), // ja-review
  previous: tb("Sebelumnya", "Previous", "{前|まえ} {へ||e}"), // ja-review
  next: tb("Berikutnya", "Next", "{次|つぎ} {へ||e}"), // ja-review
  seasonClosedToast: tb(
    "Season ditutup. Papan baru dimulai dari nol.",
    "The season was closed. A new board starts from zero.",
    "シーズン が {終了|しゅうりょう} しました", // ja-review
  ),
  pointsAwarded: tb("+{{n}} poin", "+{{n}} points", "+{{n}} ポイント"), // ja-review
} as const;

export const seasons = {
  title: tb("Season", "Seasons", "シーズン"), // ja-review
  description: tb(
    "Satu season aktif per level. Menutup season mengarsipkan peringkat dan memulai season baru dari nol.",
    "One active season per level. Closing a season archives its ranking and starts a new one from zero.",
    "レベル ごと に シーズン が 1つ", // ja-review
  ),
  level: tb("Level", "Level", "レベル"), // ja-review
  season: tb("Season", "Season", "シーズン"), // ja-review
  status: tb("Status", "Status", "{状態|じょうたい}"), // ja-review
  started: tb("Mulai", "Started", "{開始|かいし}"), // ja-review
  ended: tb("Selesai", "Ended", "{終了|しゅうりょう}"), // ja-review
  active: tb("Aktif", "Active", "{進行中|しんこうちゅう}"), // ja-review
  closed: tb("Ditutup", "Closed", "{終了|しゅうりょう}"), // ja-review
  closeAction: tb("Tutup season", "Close season", "シーズン {を||o} {終了|しゅうりょう}"), // ja-review
  closeTitle: tb("Tutup {{name}}?", "Close {{name}}?", "{{name}} {を||o} {終了|しゅうりょう} しますか"), // ja-review
  closeDescription: tb(
    "Peringkat saat ini diarsipkan dan season baru dimulai dari nol untuk level ini. Tindakan ini tidak bisa dibatalkan.",
    "The current ranking is archived and a new season starts from zero for this level. This cannot be undone.",
    "{元|もと} に {戻|もど}せません", // ja-review
  ),
  closeConfirm: tb("Ya, tutup season", "Yes, close the season", "はい、{終了|しゅうりょう} します"), // ja-review
  closedToast: tb("Season ditutup, {{name}} dimulai", "Season closed, {{name}} started", "{{name}} が {始|はじ}まりました"), // ja-review
  empty: tb("Belum ada level", "No levels yet", "レベル が ありません"), // ja-review
} as const;

export const activity = {
  lastActive: tb("Terakhir aktif", "Last active", "{最終|さいしゅう} アクティブ"), // ja-review
} as const;

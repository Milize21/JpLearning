import { tb } from "../tb";

// Japanese text below is an AI draft using docs/glosarium.md; every line is marked for review by a sensei.

export const classes = {
  title: tb("Kelas", "Classes", "クラス"), // ja-review
  myClasses: tb("Kelas saya", "My classes", "{私|わたし} の クラス"), // ja-review
  description: tb("Kelas yang kamu ajar atau ikuti.", "Classes you teach or follow.", "クラス {一覧|いちらん}"), // ja-review
  create: tb("Buat kelas", "Create class", "クラス {作成|さくせい}"), // ja-review
  created: tb("Kelas dibuat", "Class created", "{作成|さくせい} しました"), // ja-review
  name: tb("Nama kelas", "Class name", "クラス{名|めい}"), // ja-review
  nameHint: tb("Mis. N5 Sabtu Pagi", "E.g. N5 Saturday morning", "{例|れい}: N5 {土曜|どよう} {朝|あさ}"), // ja-review
  level: tb("Level", "Level", "レベル"), // ja-review
  book: tb("Buku", "Book", "{教科書|きょうかしょ}"), // ja-review
  status: tb("Status", "Status", "{状態|じょうたい}"), // ja-review
  draft: tb("Draf", "Draft", "{下書|したが}き"), // ja-review
  active: tb("Aktif", "Active", "{有効|ゆうこう}"), // ja-review
  archived: tb("Diarsipkan", "Archived", "アーカイブ"), // ja-review
  draftHint: tb("Kelas draf belum memberi murid akses ke bab.", "A draft class gives students no chapter access yet.", "{下書|したが}き {中|ちゅう} {は||wa} {公開|こうかい} されません"), // ja-review
  students: tb("{{n}} murid", { one: "{{n}} student", other: "{{n}} students" }, "{生徒|せいと} {{n}} {人|にん}"), // ja-review
  teachersOf: tb("Pengajar: {{names}}", "Teachers: {{names}}", "{先生|せんせい}: {{names}}"), // ja-review
  empty: tb("Belum ada kelas", "No classes yet", "クラス {は||wa} まだ ありません"), // ja-review
  emptyStaff: tb("Buat kelas pertamamu, lalu tambahkan murid dan buka bab.", "Create your first class, then add students and open chapters.", "クラス {を||o} {作成|さくせい} しましょう"), // ja-review
  open: tb("Buka kelas", "Open class", "クラス {を||o} {開|ひら}く"), // ja-review
  archive: tb("Arsipkan kelas", "Archive class", "アーカイブ する"), // ja-review
  archiveTitle: tb("Arsipkan {{name}}?", "Archive {{name}}?", "アーカイブ しますか"), // ja-review
  archiveBody: tb("Murid tetap bisa membaca materi dan latihan, tetapi kelas tidak bisa diubah lagi.", "Students can still read materials and practise, but the class can no longer be changed.", "{変更|へんこう} できなく なります"), // ja-review
  archiveConfirm: tb("Ya, arsipkan", "Yes, archive", "アーカイブ する"), // ja-review
  archivedNote: tb("Kelas ini diarsipkan dan hanya bisa dibaca.", "This class is archived and read only.", "{閲覧|えつらん} のみ"), // ja-review
  activate: tb("Aktifkan kelas", "Activate class", "{有効|ゆうこう} に する"), // ja-review
  backToClasses: tb("Semua kelas", "All classes", "クラス {一覧|いちらん}"), // ja-review
} as const;

export const classTabs = {
  chapters: tb("Bab", "Chapters", "{課|か}"), // ja-review
  materials: tb("Materi", "Materials", "{教材|きょうざい}"), // ja-review
  members: tb("Anggota", "Members", "メンバー"), // ja-review
  schedule: tb("Jadwal", "Schedule", "{予定|よてい}"), // ja-review
} as const;

export const unlocks = {
  open: tb("Terbuka", "Open", "{解放|かいほう}"), // ja-review
  locked: tb("Terkunci", "Locked", "ロック"), // ja-review
  openedOn: tb("Dibuka {{date}}", "Opened {{date}}", "{{date}} {解放|かいほう}"), // ja-review
  toggle: tb("Buka bab {{name}}", "Open chapter {{name}}", "{課|か} {を||o} {解放|かいほう}"), // ja-review
  opened: tb("Bab dibuka untuk murid", "Chapter opened for students", "{解放|かいほう} しました"), // ja-review
  closed: tb("Bab ditutup", "Chapter closed", "ロック しました"), // ja-review
  closeTitle: tb("Tutup {{name}}?", "Close {{name}}?", "ロック しますか"), // ja-review
  closeBody: tb("Materinya tersembunyi lagi bagi murid tanpa akses lain. Riwayat latihan mereka tetap tersimpan.", "Materials are hidden again for students without other access. Their practice history is kept.", "{履歴|りれき} {は||wa} {残|のこ}ります"), // ja-review
  closeConfirm: tb("Ya, tutup bab", "Yes, close chapter", "ロック する"), // ja-review
  openUntil: tb("Buka sampai bab…", "Open up to chapter…", "{第|だい}…{課|か} まで {解放|かいほう}"), // ja-review
  openUntilDone: tb("{{n}} bab dibuka", { one: "{{n}} chapter opened", other: "{{n}} chapters opened" }, "{{n}} {課|か} {解放|かいほう}"), // ja-review
} as const;

export const members = {
  students: tb("Murid", "Students", "{生徒|せいと}"), // ja-review
  teachers: tb("Pengajar", "Teachers", "{先生|せんせい}"), // ja-review
  addStudents: tb("Tambah murid", "Add students", "{生徒|せいと} {追加|ついか}"), // ja-review
  usernames: tb("Username murid", "Student usernames", "{生徒|せいと} の ユーザー{名|めい}"), // ja-review
  usernamesHint: tb("Satu username per baris (atau dipisah koma). Username diberikan admin saat membuat akun.", "One username per line (or comma separated). Admins give out usernames when they create accounts.", "1 {行|ぎょう} に 1 {人|にん}"), // ja-review
  added: tb("{{n}} murid ditambahkan", { one: "{{n}} student added", other: "{{n}} students added" }, "{{n}} {人|にん} {追加|ついか} しました"), // ja-review
  notFound: tb("Tidak ditemukan sebagai akun murid aktif: {{names}}", "Not found as active student accounts: {{names}}", "{見|み}つかりません: {{names}}"), // ja-review
  remove: tb("Keluarkan", "Remove", "{外|はず}す"), // ja-review
  removeTitle: tb("Keluarkan {{name}} dari kelas?", "Remove {{name}} from the class?", "クラス から {外|はず}しますか"), // ja-review
  removeBody: tb("Akses ke bab kelas ini berakhir. Riwayat belajarnya tetap tersimpan.", "Access to this class's chapters ends. Learning history is kept.", "{履歴|りれき} {は||wa} {残|のこ}ります"), // ja-review
  removeConfirm: tb("Ya, keluarkan", "Yes, remove", "{外|はず}す"), // ja-review
  removed: tb("Dikeluarkan", "Removed", "{外|はず}しました"), // ja-review
  statusRemoved: tb("Keluar", "Removed", "{退会|たいかい}"), // ja-review
  accountInactive: tb("Akun nonaktif", "Account inactive", "{無効|むこう}"), // ja-review
  joined: tb("Bergabung {{date}}", "Joined {{date}}", "{{date}} {参加|さんか}"), // ja-review
  primary: tb("Pengajar utama", "Primary teacher", "{担任|たんにん}"), // ja-review
  addTeacher: tb("Tambah pengajar", "Add teacher", "{先生|せんせい} {追加|ついか}"), // ja-review
  teacherUsername: tb("Username sensei", "Teacher username", "{先生|せんせい} の ユーザー{名|めい}"), // ja-review
  makePrimary: tb("Jadikan pengajar utama", "Make primary teacher", "{担任|たんにん} に する"), // ja-review
  teacherAdded: tb("Pengajar ditambahkan", "Teacher added", "{追加|ついか} しました"), // ja-review
  removeTeacher: tb("Hapus pengajar", "Remove teacher", "{先生|せんせい} {を||o} {外|はず}す"), // ja-review
  lastTeacher: tb("Pengajar terakhir tidak bisa dihapus.", "The last teacher can't be removed.", "{最後|さいご} の {先生|せんせい} {は||wa} {外|はず}せません"), // ja-review
  onlyPrimary: tb("Hanya pengajar utama atau admin yang mengatur pengajar.", "Only the primary teacher or an admin manages teachers.", "{担任|たんにん} または {管理者|かんりしゃ} のみ"), // ja-review
  empty: tb("Belum ada murid di kelas ini", "No students in this class yet", "{生徒|せいと} {は||wa} まだ いません"), // ja-review
} as const;

export const schedule = {
  title: tb("Jadwal", "Schedule", "{予定|よてい}"), // ja-review
  description: tb("Pertemuan kelasmu di Zoom atau Discord. Waktu ditampilkan dalam zona waktumu.", "Your class meetings on Zoom or Discord, in your time zone.", "{授業|じゅぎょう} の {予定|よてい}"), // ja-review
  add: tb("Tambah jadwal", "Add session", "{予定|よてい} {追加|ついか}"), // ja-review
  edit: tb("Ubah jadwal", "Edit session", "{予定|よてい} {編集|へんしゅう}"), // ja-review
  created: tb("Jadwal dibuat. Murid diberi tahu.", "Session created. Students are notified.", "{作成|さくせい} しました"), // ja-review
  updated: tb("Jadwal diubah. Murid diberi tahu.", "Session changed. Students are notified.", "{変更|へんこう} しました"), // ja-review
  topic: tb("Topik", "Topic", "トピック"), // ja-review
  date: tb("Tanggal", "Date", "{日付|ひづけ}"), // ja-review
  startTime: tb("Mulai", "Starts", "{開始|かいし}"), // ja-review
  endTime: tb("Selesai", "Ends", "{終了|しゅうりょう}"), // ja-review
  platform: tb("Platform", "Platform", "プラットフォーム"), // ja-review
  zoom: tb("Zoom", "Zoom", "Zoom"),
  discord: tb("Discord", "Discord", "Discord"),
  other: tb("Lainnya", "Other", "その{他|ほか}"), // ja-review
  link: tb("Link", "Link", "リンク"), // ja-review
  meetingUrl: tb("Link pertemuan (https://)", "Meeting link (https://)", "{会議|かいぎ} リンク"), // ja-review
  meetingUrlHint: tb("Hanya terlihat oleh anggota dan pengajar kelas.", "Only members and teachers of the class see it.", "メンバー のみ {表示|ひょうじ}"), // ja-review
  note: tb("Catatan", "Note", "メモ"), // ja-review
  chapterOptional: tb("Bab (opsional)", "Chapter (optional)", "{課|か} ({任意|にんい})"), // ja-review
  noChapter: tb("Tanpa bab", "No chapter", "{課|か} なし"), // ja-review
  endAfterStart: tb("Waktu selesai harus setelah mulai, maksimal 8 jam.", "The end must be after the start, at most 8 hours.", "{終了|しゅうりょう} {は||wa} {開始|かいし} より {後|あと}"), // ja-review
  cancel: tb("Batalkan jadwal", "Cancel session", "{予定|よてい} {を||o} キャンセル"), // ja-review
  cancelTitle: tb("Batalkan {{topic}}?", "Cancel {{topic}}?", "キャンセル しますか"), // ja-review
  cancelBody: tb("Murid diberi tahu dan pengingat dihentikan.", "Students are told and reminders stop.", "{生徒|せいと} に {通知|つうち} されます"), // ja-review
  cancelConfirm: tb("Ya, batalkan", "Yes, cancel", "キャンセル する"), // ja-review
  cancelled: tb("Dibatalkan", "Cancelled", "キャンセル"), // ja-review
  ongoing: tb("Sedang berlangsung", "In progress", "{進行中|しんこうちゅう}"), // ja-review
  finished: tb("Selesai", "Finished", "{終了|しゅうりょう}"), // ja-review
  join: tb("Gabung", "Join", "{参加|さんか}"), // ja-review
  joinHint: tb("Tombol aktif 15 menit sebelum mulai.", "Available 15 minutes before the start.", "15 {分前|ふんまえ} から"), // ja-review
  addToGoogle: tb("Tambah ke Google Calendar", "Add to Google Calendar", "Google カレンダー に {追加|ついか}"), // ja-review
  today: tb("Hari ini", "Today", "{今日|きょう}"), // ja-review
  tomorrow: tb("Besok", "Tomorrow", "{明日|あした}"), // ja-review
  thisWeek: tb("Minggu ini", "This week", "{今週|こんしゅう}"), // ja-review
  later: tb("Nanti", "Later", "それ {以降|いこう}"), // ja-review
  past: tb("Sudah lewat", "Past", "{過去|かこ}"), // ja-review
  empty: tb("Belum ada jadwal", "No sessions yet", "{予定|よてい} {は||wa} まだ ありません"), // ja-review
  emptyBody: tb("Jadwal pertemuan dari sensei akan muncul di sini.", "Meetings scheduled by your sensei will appear here.", "ここ に {表示|ひょうじ} されます"), // ja-review
  next: tb("Berikutnya", "Next up", "{次|つぎ} の {授業|じゅぎょう}"), // ja-review
  showPast: tb("Tampilkan yang sudah lewat", "Show past sessions", "{過去|かこ} の {予定|よてい} {を||o} {表示|ひょうじ}"), // ja-review
} as const;

export const calendarFeed = {
  title: tb("Langganan kalender", "Calendar subscription", "カレンダー {購読|こうどく}"), // ja-review
  body: tb("Tambahkan semua jadwalmu ke Google Calendar atau aplikasi kalender lain.", "Add all your sessions to Google Calendar or another calendar app.", "カレンダー アプリ に {追加|ついか}"), // ja-review
  create: tb("Buat link kalender", "Create calendar link", "リンク {作成|さくせい}"), // ja-review
  rotate: tb("Buat link baru", "Create a new link", "{新|あたら}しい リンク"), // ja-review
  revoke: tb("Hapus link", "Remove link", "リンク {削除|さくじょ}"), // ja-review
  active: tb("Link kalendermu aktif sejak {{date}}.", "Your calendar link is active since {{date}}.", "{有効|ゆうこう} な リンク が あります"), // ja-review
  shownOnce: tb("Salin sekarang: link ini hanya ditampilkan sekali. Link baru membuat link lama berhenti bekerja.", "Copy it now: the link is shown only once. A new link stops the old one.", "この リンク {は||wa} {一度|いちど} だけ {表示|ひょうじ} されます"), // ja-review
  guide: tb("Google Calendar → Kalender lain → Dari URL → tempel link.", "Google Calendar → Other calendars → From URL → paste the link.", "Google カレンダー → URL で {追加|ついか}"), // ja-review
  refreshNote: tb("Google memperbarui kalender langganan setiap beberapa jam. Pengingat yang pasti tetap lewat email dan notifikasi.", "Google refreshes subscribed calendars every few hours. Reliable reminders still come by email and notifications.", "{更新|こうしん} に {時間|じかん} が かかります"), // ja-review
  revoked: tb("Link kalender dihapus", "Calendar link removed", "{削除|さくじょ} しました"), // ja-review
} as const;

export const reminders = {
  title: tb("Pengingat di perangkat ini", "Reminders on this device", "この {端末|たんまつ} の リマインダー"), // ja-review
  body: tb("Notifikasi 1 hari dan 1 jam sebelum pertemuan, juga saat jadwal berubah.", "Notifications 1 day and 1 hour before a meeting, and when the schedule changes.", "{授業|じゅぎょう} の {前|まえ} に {通知|つうち}"), // ja-review
  enable: tb("Aktifkan pengingat", "Turn on reminders", "リマインダー {を||o} オン"), // ja-review
  enabled: tb("Pengingat aktif di perangkat ini", "Reminders are on for this device", "オン に なっています"), // ja-review
  disable: tb("Matikan di perangkat ini", "Turn off on this device", "オフ に する"), // ja-review
  unsupported: tb("Browser ini belum mendukung notifikasi. Di iPhone, tambahkan aplikasi ke layar utama dulu.", "This browser can't show notifications. On iPhone, add the app to the home screen first.", "この ブラウザ {は||wa} {未対応|みたいおう} です"), // ja-review
  denied: tb("Izin notifikasi ditolak. Ubah di pengaturan browser.", "Notifications are blocked. Change it in the browser settings.", "{通知|つうち} が ブロック されています"), // ja-review
  unavailable: tb("Notifikasi belum disiapkan di server ini. Pengingat tetap dikirim lewat email.", "Notifications aren't set up on this server. Reminders still come by email.", "メール で {届|とど}きます"), // ja-review
  turnedOn: tb("Pengingat diaktifkan", "Reminders turned on", "オン に しました"), // ja-review
  turnedOff: tb("Pengingat dimatikan", "Reminders turned off", "オフ に しました"), // ja-review
  emailNote: tb("Pengingat juga dikirim ke email akunmu bila ada.", "Reminders also go to your account email, if you have one.", "メール でも {届|とど}きます"), // ja-review
} as const;

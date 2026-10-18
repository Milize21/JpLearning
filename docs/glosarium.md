# Glosarium

Istilah resmi proyek. Dipakai untuk nama di kode, label UI, dan dokumen agar konsisten.

> ⚠️ **Kolom Jepang adalah draf buatan AI** dan berstatus `ja-review`: wajib diperiksa sensei sebelum dipakai sebagai label final. Setelah diperiksa, hapus catatan ini.

## 1. Istilah domain

| Indonesia | English | Nama di kode | 日本語 | Kana | Romaji |
|---|---|---|---|---|---|
| Murid | Student | `Student`, role `murid` | 生徒 | せいと | seito |
| Sensei / pengajar | Teacher | `Teacher`, role `sensei` | 先生 | せんせい | sensei |
| Admin | Administrator | `Admin`, role `admin` | 管理者 | かんりしゃ | kanrisha |
| Kelas | Class | `StudyClass` (modul `Classroom`; `Class` ditolak analyzer CA1716 karena kata kunci) | クラス | – | kurasu |
| Level | Level | `Level` | レベル | – | reberu |
| Buku (pelajaran) | Book | `Book` | 教科書 | きょうかしょ | kyoukasho |
| Bab | Chapter | `Chapter` | 課 | か | ka |
| Materi | Material | `Material` | 教材 | きょうざい | kyouzai |
| Latihan | Practice | `Practice` | 練習 | れんしゅう | renshuu |
| Drilling | Drill | `Drill` | ドリル | – | doriru |
| Kuis | Quiz | `Quiz` | 小テスト | しょうテスト | shou tesuto |
| Ujian | Exam | `Exam` | 試験 | しけん | shiken |
| Tugas | Assignment | `Assignment` | 課題 | かだい | kadai |
| Pengumpulan tugas | Submission | `Submission` | 提出 | ていしゅつ | teishutsu |
| Nilai (rapor) | Grade | `Grade` | 成績 | せいせき | seiseki |
| Skor | Score | `Score` | 点数 | てんすう | tensuu |
| Soal | Question | `Question` | 問題 | もんだい | mondai |
| Bank soal | Question bank | `QuestionBank` | 問題集 | もんだいしゅう | mondaishuu |
| Jawaban | Answer | `Answer` | 答え | こたえ | kotae |
| Pembahasan | Explanation | `Explanation` | 解説 | かいせつ | kaisetsu |
| Benar | Correct | – | 正解 | せいかい | seikai |
| Salah | Incorrect | – | 不正解 | ふせいかい | fuseikai |
| Kelemahan | Weak points | `Weakness` | 苦手 | にがて | nigate |
| Penguasaan | Mastery | `Mastery` | 習熟度 | しゅうじゅくど | shuujukudo |
| Progres | Progress | `Progress` | 進捗 | しんちょく | shinchoku |
| Jadwal | Schedule | `Schedule` | 予定 | よてい | yotei |
| Pertemuan kelas | Class session | `ClassSession` | 授業 | じゅぎょう | jugyou |
| Kalender | Calendar | `Calendar` | カレンダー | – | karendaa |
| Pengingat | Reminder | `Reminder` | リマインダー | – | rimaindaa |
| Peringkat | Leaderboard | `Leaderboard` | ランキング | – | rankingu |
| Musim | Season | `Season` | シーズン | – | shiizun |
| Poin | Points | `Points` | ポイント | – | pointo |
| Kosakata | Vocabulary | `Vocab` | 語彙 | ごい | goi |
| Tata bahasa | Grammar | `Grammar` | 文法 | ぶんぽう | bunpou |
| Kalimat | Sentence | `Sentence` | 文 | ぶん | bun |
| Kana | Kana | `Kana` | 仮名 | かな | kana |
| Hiragana | Hiragana | – | 平仮名 | ひらがな | hiragana |
| Katakana | Katakana | – | 片仮名 | かたかな | katakana |
| Kanji | Kanji | `Kanji` | 漢字 | かんじ | kanji |
| Furigana | Furigana | – | 振り仮名 | ふりがな | furigana |
| Romaji | Romaji | – | ローマ字 | ローマじ | roomaji |
| Tulis tangan | Handwriting | `Handwrite` | 手書き | てがき | tegaki |
| Hafalan | Memorization | – | 暗記 | あんき | anki |
| Buka (bab) | Unlock | `Unlock` | 解放 | かいほう | kaihou |
| Kunci (bab) | Lock | `Lock` | ロック | – | rokku |
| Anggota | Member | `Member` | メンバー | – | menbaa |
| Beranda | Home | `Dashboard` | ホーム | – | hoomu |
| Profil | Profile | `Profile` | プロフィール | – | purofiiru |
| Pengaturan | Settings | `Settings` | 設定 | せってい | settei |
| Masuk | Log in | – | ログイン | – | roguin |
| Keluar | Log out | – | ログアウト | – | roguauto |
| Simpan | Save | – | 保存 | ほぞん | hozon |
| Batal | Cancel | – | キャンセル | – | kyanseru |
| Kirim (tugas) | Submit | – | 提出 | ていしゅつ | teishutsu |
| Mulai | Start | – | 開始 | かいし | kaishi |
| Selesai | Finish | – | 終了 | しゅうりょう | shuuryou |
| Unggah | Upload | `Upload` | アップロード | – | appuroodo |
| Unduh | Download | `Download` | ダウンロード | – | daunroodo |
| Komentar | Comment | `Comment` | コメント | – | komento |

## 2. Istilah teknis

| Istilah | Arti di proyek ini |
|---|---|
| TextBlock | Teks konten dengan versi `id`, `en`, dan segmen Jepang `ja` ([03](03-skema-data.md) §3.1) |
| RichText | Teks panjang Markdown terbatas + markup furigana ([03](03-skema-data.md) §3.2) |
| Markup furigana | Sintaks `{漢字\|かんじ}` untuk menulis teks Jepang beserta bacaannya ([06](06-teks-multibahasa.md) §3) |
| Pendamping (companion) | Teks Jepang yang menyertai teks ID/EN di UI |
| `script_pref` | Preferensi tampilan Jepang pengguna (5 mode) |
| `jaDisplay` | Pengaturan tampilan Jepang per soal agar tidak membocorkan jawaban |
| Unlock bab | Bab yang dibuka sensei untuk satu kelas (`class_unlocks`) |
| Mastery | Angka 0–1 tingkat penguasaan satu item oleh satu murid |
| Pool drilling | Kumpulan soal yang boleh muncul di sesi drilling seorang murid |
| Season | Periode leaderboard per level; ditutup lalu diganti yang baru |
| Modul | Bagian backend yang setara satu "service" v1 (Identity, Content, Classroom, Practice, Insight) |
| Contracts | Proyek berisi interface dan event publik sebuah modul |
| Outbox / inbox | Tabel untuk mengirim dan menerima event antar-modul secara andal |
| Mode uji coba | Staf mencoba drilling/kuis tanpa data tersimpan |
| Vibe coding | Membangun aplikasi lewat percakapan dengan AI, dikendalikan oleh dokumen dan aturan di repo ini |

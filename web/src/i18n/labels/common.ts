import { tb } from "../tb";

// Japanese text below is an AI draft using docs/glosarium.md; every line is marked for review by a sensei.
export const common = {
  brand: tb("NomaX JP Learn", "NomaX JP Learn", "NomaX JP Learn"),
  save: tb("Simpan", "Save", "{保存|ほぞん}"), // ja-review
  saved: tb("Tersimpan", "Saved", "{保存|ほぞん} しました"), // ja-review
  cancel: tb("Batal", "Cancel", "キャンセル"), // ja-review
  close: tb("Tutup", "Close", "{閉|と}じる"), // ja-review
  back: tb("Kembali", "Back", "{戻|もど}る"), // ja-review
  next: tb("Lanjut", "Next", "{次|つぎ}{へ||e}"), // ja-review
  previous: tb("Sebelumnya", "Previous", "{前|まえ}{へ||e}"), // ja-review
  loading: tb("Memuat…", "Loading…", "{読|よ}み{込|こ}み{中|ちゅう}…"), // ja-review
  retry: tb("Coba lagi", "Try again", "もう {一度|いちど}"), // ja-review
  search: tb("Cari", "Search", "{検索|けんさく}"), // ja-review
  edit: tb("Ubah", "Edit", "{編集|へんしゅう}"), // ja-review
  copy: tb("Salin", "Copy", "コピー"), // ja-review
  copied: tb("Tersalin", "Copied", "コピー しました"), // ja-review
  download: tb("Unduh", "Download", "ダウンロード"), // ja-review
  actions: tb("Aksi", "Actions", "{操作|そうさ}"), // ja-review
  all: tb("Semua", "All", "すべて"), // ja-review
  active: tb("Aktif", "Active", "{有効|ゆうこう}"), // ja-review
  inactive: tb("Nonaktif", "Inactive", "{無効|むこう}"), // ja-review
  status: tb("Status", "Status", "{状態|じょうたい}"), // ja-review
  optional: tb("opsional", "optional", "{任意|にんい}"), // ja-review
  menu: tb("Menu", "Menu", "メニュー"), // ja-review
  skipToContent: tb("Lewati ke konten", "Skip to content", "{本文|ほんぶん} {へ||e} スキップ"), // ja-review
  goHome: tb("Kembali ke beranda", "Back to home", "ホーム {へ||e} {戻|もど}る"), // ja-review
  pageOf: tb("Halaman {{page}} dari {{pages}}", "Page {{page}} of {{pages}}", "{{page}} / {{pages}} ページ"), // ja-review
  resultCount: tb("{{n}} hasil", { one: "{{n}} result", other: "{{n}} results" }, "{{n}} {件|けん}"), // ja-review
} as const;

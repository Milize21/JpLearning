---
description: Usulkan perubahan skema data sesuai protokol AGENTS.md §5.4, tanpa mengubah apa pun sebelum disetujui
argument-hint: <deskripsi perubahan skema>
---

Pemilik proyek ingin mengubah skema data: **$ARGUMENTS**

Ikuti protokol `AGENTS.md` §5.4:

1. Baca bagian terkait di `docs/03-skema-data.md`, aturan di `docs/04-aturan-bisnis.md`, endpoint di `docs/05-api-dan-event.md`, serta kode dan migrasi yang sudah ada.
2. Jelaskan **alasan** dan **dampak**: tabel/kolom yang berubah, endpoint dan DTO, aturan bisnis, event, data lama beserta migrasi datanya, dan risikonya.
3. Usulkan **diff dokumen** untuk `docs/03` (dan `docs/04`, `docs/05`, `docs/11` bila terdampak) dengan penanda ➕/✏️.
4. Usulkan nama migrasi EF dan urutan implementasinya.
5. **BERHENTI** dan tunggu persetujuan. Jangan mengubah dokumen, migrasi, atau kode sebelum disetujui.

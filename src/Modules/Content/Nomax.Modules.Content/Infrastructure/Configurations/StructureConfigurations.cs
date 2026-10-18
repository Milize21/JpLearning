using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Content.Domain;

namespace Nomax.Modules.Content.Infrastructure.Configurations;

// Tables of docs/03 §5.1–5.5 exactly. Foreign keys inside the content schema restrict deletion; the use cases check
// dependents first and answer 409 has_dependents (BR-KONTEN-03). Only the indexes listed in the document exist.

internal sealed class LevelConfiguration : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> builder)
    {
        builder.ToTable("levels");
        builder.HasKey(level => level.Id);
        builder.Property(level => level.Id).ValueGeneratedNever();
        builder.Property(level => level.Code).HasMaxLength(Level.CodeMaxLength);
        builder.Property(level => level.Name).HasJsonbConversion();
        builder.HasXminVersion();
        builder.HasIndex(level => level.Code).IsUnique();
    }
}

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.HasKey(book => book.Id);
        builder.Property(book => book.Id).ValueGeneratedNever();
        builder.Property(book => book.Title).HasJsonbConversion();
        builder.HasXminVersion();
        builder.HasOne<Level>().WithMany().HasForeignKey(book => book.LevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(book => book.CoverFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(book => new { book.LevelId, book.SortOrder });
    }
}

internal sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.ToTable("chapters");
        builder.HasKey(chapter => chapter.Id);
        builder.Property(chapter => chapter.Id).ValueGeneratedNever();
        builder.Property(chapter => chapter.Title).HasJsonbConversion();
        builder.HasXminVersion();
        builder.HasOne<Book>().WithMany().HasForeignKey(chapter => chapter.BookId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(chapter => new { chapter.BookId, chapter.Number }).IsUnique();
        builder.HasIndex(chapter => new { chapter.BookId, chapter.SortOrder });
    }
}

internal sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("materials", table => table.HasCheckConstraint(
            "ck_materials_content",
            "(kind = 'file' AND file_id IS NOT NULL) OR (kind = 'link' AND url IS NOT NULL) OR (kind = 'richtext' AND body IS NOT NULL)"));
        builder.HasKey(material => material.Id);
        builder.Property(material => material.Id).ValueGeneratedNever();
        builder.Property(material => material.Title).HasJsonbConversion();
        builder.Property(material => material.Body).HasJsonbConversion();
        builder.Property(material => material.Url).HasMaxLength(Material.UrlMaxLength);
        builder.Property(material => material.SortOrder).HasDefaultValueSql("0").ValueGeneratedNever();
        builder.HasXminVersion();
        builder.HasOne<Chapter>().WithMany().HasForeignKey(material => material.ChapterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(material => material.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(material => new { material.ChapterId, material.SortOrder });
    }
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files", table => table.HasCheckConstraint(
            "ck_stored_files_size_bytes",
            "size_bytes BETWEEN 0 AND 1073741824"));
        builder.HasKey(file => file.Id);
        builder.Property(file => file.Id).ValueGeneratedNever();
        builder.Property(file => file.StorageKey).HasMaxLength(StoredFile.StorageKeyMaxLength);
        builder.Property(file => file.OriginalName).HasMaxLength(StoredFile.OriginalNameMaxLength);
        builder.Property(file => file.Mime).HasMaxLength(StoredFile.MimeMaxLength);
        builder.Property(file => file.Sha256).HasColumnType("char(64)");
        builder.Property(file => file.Status).HasDefaultValueSql("'uploading'").ValueGeneratedNever();
        builder.HasIndex(file => file.StorageKey).IsUnique();
        builder.HasIndex(file => file.UploadedBy);
        builder.HasIndex(file => new { file.Status, file.CreatedAt });
    }
}

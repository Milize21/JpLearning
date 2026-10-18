using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Content.Domain;

namespace Nomax.Modules.Content.Infrastructure.Configurations;

// Reference data of docs/03 §5.6: shared columns + index (level_id, chapter_id), then the content columns per table.

internal static class ReferenceItemMapping
{
    public static void MapShared<T>(EntityTypeBuilder<T> builder, string table)
        where T : ReferenceItem
    {
        builder.ToTable(table);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Source).HasMaxLength(ReferenceItem.SourceMaxLength);
        builder.Property(item => item.License).HasMaxLength(ReferenceItem.LicenseMaxLength);
        builder.HasOne<Level>().WithMany().HasForeignKey(item => item.LevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Chapter>().WithMany().HasForeignKey(item => item.ChapterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.LevelId, item.ChapterId });
    }

    public static void TextArray(PropertyBuilder<List<string>> property) =>
        property.HasColumnType("text[]").HasDefaultValueSql("'{}'").ValueGeneratedNever();
}

internal sealed class KanaCharConfiguration : IEntityTypeConfiguration<KanaChar>
{
    public void Configure(EntityTypeBuilder<KanaChar> builder)
    {
        ReferenceItemMapping.MapShared(builder, "kana_chars");
        builder.Property(kana => kana.Character).HasColumnName("char").HasMaxLength(4);
        builder.Property(kana => kana.Romaji).HasMaxLength(8);
        builder.Property(kana => kana.RowKey).HasMaxLength(8);
        builder.HasIndex(kana => new { kana.Script, kana.Character }).IsUnique();
    }
}

internal sealed class KanjiConfiguration : IEntityTypeConfiguration<Kanji>
{
    public void Configure(EntityTypeBuilder<Kanji> builder)
    {
        ReferenceItemMapping.MapShared(builder, "kanji");
        builder.Property(kanji => kanji.Character).HasMaxLength(4);
        ReferenceItemMapping.TextArray(builder.Property(kanji => kanji.Onyomi));
        ReferenceItemMapping.TextArray(builder.Property(kanji => kanji.Kunyomi));
        builder.Property(kanji => kanji.Meanings).HasJsonbConversion();
        builder.Property(kanji => kanji.KanjivgCode).HasMaxLength(8);
        builder.HasIndex(kanji => kanji.Character).IsUnique();
    }
}

internal sealed class VocabConfiguration : IEntityTypeConfiguration<Vocab>
{
    public void Configure(EntityTypeBuilder<Vocab> builder)
    {
        ReferenceItemMapping.MapShared(builder, "vocab");
        builder.Property(vocab => vocab.Word).HasMaxLength(100);
        builder.Property(vocab => vocab.Reading).HasMaxLength(100);
        builder.Property(vocab => vocab.Meanings).HasJsonbConversion();
        builder.Property(vocab => vocab.Pos).HasMaxLength(32);
        ReferenceItemMapping.TextArray(builder.Property(vocab => vocab.Tags));
        builder.HasIndex(vocab => new { vocab.LevelId, vocab.Word, vocab.Reading }).IsUnique();
    }
}

internal sealed class GrammarPointConfiguration : IEntityTypeConfiguration<GrammarPoint>
{
    public void Configure(EntityTypeBuilder<GrammarPoint> builder)
    {
        ReferenceItemMapping.MapShared(builder, "grammar_points");
        builder.Property(grammar => grammar.Pattern).HasMaxLength(100);
        builder.Property(grammar => grammar.Title).HasJsonbConversion();
        builder.Property(grammar => grammar.Explanation).HasJsonbConversion();
        ReferenceItemMapping.TextArray(builder.Property(grammar => grammar.Tags));
    }
}

internal sealed class SentenceConfiguration : IEntityTypeConfiguration<Sentence>
{
    public void Configure(EntityTypeBuilder<Sentence> builder)
    {
        ReferenceItemMapping.MapShared(builder, "sentences");
        builder.Property(sentence => sentence.Text).HasJsonbConversion();
        ReferenceItemMapping.TextArray(builder.Property(sentence => sentence.Tags));
        builder.HasOne<GrammarPoint>().WithMany().HasForeignKey(sentence => sentence.GrammarPointId).OnDelete(DeleteBehavior.Restrict);
    }
}

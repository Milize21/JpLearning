using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Content.Domain;

namespace Nomax.Modules.Content.Infrastructure;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "content";

    public DbSet<Level> Levels => Set<Level>();

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Chapter> Chapters => Set<Chapter>();

    public DbSet<Material> Materials => Set<Material>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    public DbSet<KanaChar> KanaChars => Set<KanaChar>();

    public DbSet<Kanji> Kanji => Set<Kanji>();

    public DbSet<Vocab> Vocab => Set<Vocab>();

    public DbSet<GrammarPoint> GrammarPoints => Set<GrammarPoint>();

    public DbSet<Sentence> Sentences => Set<Sentence>();

    protected override string Schema => SchemaName;
}

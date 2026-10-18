using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Time.Testing;
using Nomax.BuildingBlocks.Persistence;
using Npgsql;

namespace Nomax.BuildingBlocks.Tests.Persistence;

public sealed class ModelConventionsTests : IDisposable
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 18, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 10, 18, 2, 0, 0, TimeSpan.Zero);

    // Building the model and the SQL script never opens a connection; port 1 makes an accidental connect fail fast.
    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Timeout=1");
    private readonly SampleDbContext _context;

    public ModelConventionsTests()
    {
        _context = CreateContext();
    }

    private IEntityType SampleItem => _context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(SampleItem))!;

    [Fact]
    public void Tables_and_columns_are_snake_case_in_the_module_schema()
    {
        SampleItem.GetTableName().ShouldBe("sample_items");
        SampleItem.GetSchema().ShouldBe("sample");
        SampleItem.GetProperties().Select(property => property.GetColumnName()).ShouldBe(
            ["id", "created_at", "display_name", "previous_status", "status", "updated_at"],
            ignoreOrder: true);
    }

    [Fact]
    public void Migration_history_lives_in_the_module_schema()
    {
        var relational = RelationalOptionsExtension.Extract(_context.GetService<IDbContextOptions>());

        relational.MigrationsHistoryTableName.ShouldBe("__ef_migrations_history");
        relational.MigrationsHistoryTableSchema.ShouldBe("sample");
    }

    [Fact]
    public void Enums_are_text_with_snake_case_values_and_a_check_constraint()
    {
        var status = SampleItem.FindProperty(nameof(Persistence.SampleItem.Status))!;
        status.GetValueConverter()!.ConvertToProvider(SampleStatus.InReview).ShouldBe("in_review");
        status.GetColumnType().ShouldBe("text");

        var checks = SampleItem.GetCheckConstraints().ToDictionary(check => check.ModelName, check => check.Sql);
        checks["ck_sample_items_status"].ShouldBe("\"status\" IN ('draft', 'in_review', 'published')");
        checks["ck_sample_items_previous_status"].ShouldBe("\"previous_status\" IN ('draft', 'in_review', 'published')");
    }

    [Fact]
    public void Timestamps_default_to_now_in_the_database()
    {
        SampleItem.FindProperty(nameof(Persistence.SampleItem.CreatedAt))!.GetDefaultValueSql().ShouldBe("now()");
        SampleItem.FindProperty(nameof(Persistence.SampleItem.UpdatedAt))!.GetDefaultValueSql().ShouldBe("now()");
    }

    [Fact]
    public void Generated_sql_contains_the_conventions()
    {
        var script = _context.Database.GenerateCreateScript();

        script.ShouldContain("CREATE TABLE sample.sample_items");
        script.ShouldContain("created_at timestamp with time zone NOT NULL DEFAULT (now())");
        script.ShouldContain("CONSTRAINT ck_sample_items_status CHECK (\"status\" IN ('draft', 'in_review', 'published'))");
    }

    [Fact]
    public void Interceptor_sets_both_timestamps_on_insert()
    {
        var item = new SampleItem { Id = Guid.CreateVersion7(), DisplayName = "Bab 1" };
        _context.Add(item);

        TimestampsInterceptor.ApplyTimestamps(_context.ChangeTracker, T1);

        item.CreatedAt.ShouldBe(T1);
        item.UpdatedAt.ShouldBe(T1);
    }

    [Fact]
    public void Interceptor_only_moves_updated_at_on_update()
    {
        var item = new SampleItem { Id = Guid.CreateVersion7(), DisplayName = "Bab 1" };
        _context.Add(item);
        TimestampsInterceptor.ApplyTimestamps(_context.ChangeTracker, T1);
        _context.Entry(item).State = EntityState.Unchanged;

        item.DisplayName = "Bab 1 (revisi)";
        _context.ChangeTracker.DetectChanges();
        TimestampsInterceptor.ApplyTimestamps(_context.ChangeTracker, T2);

        item.CreatedAt.ShouldBe(T1);
        item.UpdatedAt.ShouldBe(T2);
        _context.Entry(item).Property(nameof(Persistence.SampleItem.CreatedAt)).IsModified.ShouldBeFalse();
    }

    [Fact]
    public void Interceptor_uses_the_injected_clock()
    {
        using var context = CreateContext(new TimestampsInterceptor(new FakeTimeProvider(T2)));
        var item = new SampleItem { Id = Guid.CreateVersion7(), DisplayName = "Bab 2" };
        context.Add(item);

        // SavingChanges runs before the connection is opened; the failed save is expected without a database.
        Should.Throw<Exception>(() => context.SaveChanges());

        item.CreatedAt.ShouldBe(T2);
    }

    public void Dispose()
    {
        _context.Dispose();
        _dataSource.Dispose();
    }

    private SampleDbContext CreateContext(TimestampsInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SampleDbContext>();
        builder.UseNomaxPostgres(_dataSource, SampleDbContext.SchemaName);
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new SampleDbContext(builder.Options);
    }
}

public enum SampleStatus
{
    Draft,
    InReview,
    Published,
}

public sealed class SampleItem : IHasTimestamps
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public SampleStatus Status { get; set; }

    public SampleStatus? PreviousStatus { get; set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "sample";

    public DbSet<SampleItem> SampleItems => Set<SampleItem>();

    protected override string Schema => SchemaName;
}

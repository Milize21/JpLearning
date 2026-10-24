using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Hosting;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Content.Features.Books;
using Nomax.Modules.Content.Features.Chapters;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Features.Files;
using Nomax.Modules.Content.Features.Levels;
using Nomax.Modules.Content.Features.Materials;
using Nomax.Modules.Content.Features.Reference;
using Nomax.Modules.Content.Infrastructure;
using Nomax.Modules.Content.Infrastructure.Files;
using Nomax.Modules.Content.Public;
using tusdotnet;

namespace Nomax.Modules.Content;

/// <summary>
/// Content module (docs/02 §3): levels, books, chapters, materials, files and reference data.
/// </summary>
public static class ContentModule
{
    public const string Name = "content";

    public static IServiceCollection AddContentModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddNomaxModule(Name);
        services.AddModuleDbContext<ContentDbContext>(ContentDbContext.SchemaName);
        services.AddValidatorsFromAssemblyContaining<CreateLevelValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<UploadOptions>().Bind(configuration.GetSection(UploadOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IFileStorage, DiskFileStorage>();
        services.AddSingleton<SignedUrls>();
        services.AddSingleton<TusUploads>();
        services.AddScoped<UploadEvents>();
        services.AddHostedService<UploadCleanup>();

        services.AddScoped<IContentCatalog, ContentCatalog>();
        services.AddScoped<IReferenceDataReader, ReferenceDataReader>();
        services.AddScoped<IStoredFileService, StoredFileService>();
        services.AddScoped<IDevelopmentSeeder, ContentDevelopmentSeeder>();

        services.AddScoped<ChapterGate>();
        services.AddScoped<ContentUsage>();
        services.AddScoped<CoverFiles>();

        services.AddScoped<ListLevelsHandler>();
        services.AddScoped<CreateLevelHandler>();
        services.AddScoped<UpdateLevelHandler>();
        services.AddScoped<DeleteLevelHandler>();
        services.AddScoped<GetBookHandler>();
        services.AddScoped<CreateBookHandler>();
        services.AddScoped<UpdateBookHandler>();
        services.AddScoped<DeleteBookHandler>();
        services.AddScoped<GetChapterHandler>();
        services.AddScoped<CreateChapterHandler>();
        services.AddScoped<UpdateChapterHandler>();
        services.AddScoped<DeleteChapterHandler>();
        services.AddScoped<ListReferenceHandler>();
        services.AddScoped<ImportReferenceHandler>();
        services.AddScoped<UpdateReferenceHandler>();
        services.AddScoped<DeleteReferenceHandler>();
        services.AddScoped<GetBookCoverHandler>();
        services.AddScoped<ListMaterialsHandler>();
        services.AddScoped<CreateMaterialHandler>();
        services.AddScoped<UpdateMaterialHandler>();
        services.AddScoped<DeleteMaterialHandler>();
        services.AddScoped<DownloadMaterialHandler>();
        services.AddScoped<DownloadFileHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapModuleGroup(Name);
        ListLevelsHandler.Map(group);
        CreateLevelHandler.Map(group);
        UpdateLevelHandler.Map(group);
        DeleteLevelHandler.Map(group);
        GetBookHandler.Map(group);
        CreateBookHandler.Map(group);
        UpdateBookHandler.Map(group);
        DeleteBookHandler.Map(group);
        GetChapterHandler.Map(group);
        CreateChapterHandler.Map(group);
        UpdateChapterHandler.Map(group);
        DeleteChapterHandler.Map(group);
        ListReferenceHandler.Map(group);
        ImportReferenceHandler.Map(group);
        UpdateReferenceHandler.Map(group);
        DeleteReferenceHandler.Map(group);
        GetBookCoverHandler.Map(group);
        ListMaterialsHandler.Map(group);
        CreateMaterialHandler.Map(group);
        UpdateMaterialHandler.Map(group);
        DeleteMaterialHandler.Map(group);
        DownloadMaterialHandler.Map(group);
        DownloadFileHandler.Map(group);

        // tus 1.0.0 (docs/05 §13): not part of the OpenAPI document; the client is tus-js-client.
        endpoints.MapTus(
                TusUploads.RoutePattern,
                httpContext => Task.FromResult(httpContext.RequestServices.GetRequiredService<TusUploads>().CreateConfiguration()))
            .RequireAuthorization(Policies.Authenticated)
            .WithUploadBodyLimit();
        return endpoints;
    }
}

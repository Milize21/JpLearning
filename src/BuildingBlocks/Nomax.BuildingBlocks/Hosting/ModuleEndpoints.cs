using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Nomax.BuildingBlocks.Auth;
using Scalar.AspNetCore;

namespace Nomax.BuildingBlocks.Hosting;

/// <summary>A registered module; its OpenAPI document is served at <c>/openapi/{Name}.json</c>.</summary>
public sealed record NomaxModule(string Name);

/// <summary>
/// Registration helpers each module uses from its <c>Add{Module}Module</c> and <c>Map{Module}Endpoints</c> (docs/02 §5).
/// </summary>
public static class ModuleEndpoints
{
    public static IServiceCollection AddNomaxModule(this IServiceCollection services, string name)
    {
        EnsureValidName(name);
        services.AddSingleton(new NomaxModule(name));
        services.AddOpenApi(name, options =>
        {
            options.ShouldInclude = description => description.GroupName == name;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = $"NomaX JP Learn — {name}";
                document.Info.Version = "v1";
                return Task.CompletedTask;
            });
            options.AddSchemaTransformer((schema, context, _) =>
            {
                // UtcDateTimeOffsetJsonConverter hides the type from the schema generator; restore it.
                var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
                if (type == typeof(DateTimeOffset))
                {
                    schema.Type = context.JsonTypeInfo.Type == type
                        ? JsonSchemaType.String
                        : JsonSchemaType.String | JsonSchemaType.Null;
                    schema.Format = "date-time";
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }

    /// <summary>
    /// <c>/api/{name}</c> route group. Its endpoints belong to the module's OpenAPI document and require a signed-in
    /// account by default; endpoints add stricter policies (<see cref="Policies"/>) on top.
    /// </summary>
    public static RouteGroupBuilder MapModuleGroup(this IEndpointRouteBuilder endpoints, string name)
    {
        EnsureValidName(name);
        return endpoints.MapGroup($"/api/{name}")
            .WithGroupName(name)
            .RequireAuthorization(Policies.Authenticated);
    }

    /// <summary>OpenAPI documents and the Scalar UI, development only (docs/02 §15).</summary>
    public static IEndpointRouteBuilder MapNomaxOpenApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!endpoints.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        {
            return endpoints;
        }

        endpoints.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();

        var modules = endpoints.ServiceProvider.GetServices<NomaxModule>().Select(module => module.Name).ToArray();
        endpoints.MapScalarApiReference("/scalar", options =>
        {
            options.WithTitle("NomaX JP Learn API");
            foreach (var module in modules)
            {
                options.AddDocument(module, module);
            }
        }).AllowAnonymous();

        return endpoints;
    }

    private static void EnsureValidName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!name.All(char.IsAsciiLetterLower))
        {
            throw new ArgumentException($"Module name '{name}' must be lowercase ASCII letters, e.g. 'identity'.", nameof(name));
        }
    }
}

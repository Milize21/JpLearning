using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Hosting;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Practice.Features.Assignments;
using Nomax.Modules.Practice.Features.Comments;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Features.Drill;
using Nomax.Modules.Practice.Features.Generator;
using Nomax.Modules.Practice.Features.Grades;
using Nomax.Modules.Practice.Features.Preview;
using Nomax.Modules.Practice.Features.Progress;
using Nomax.Modules.Practice.Features.Questions;
using Nomax.Modules.Practice.Features.Quizzes;
using Nomax.Modules.Practice.Features.Sources;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.Modules.Practice.Public;

namespace Nomax.Modules.Practice;

/// <summary>
/// Practice module (docs/02 §3): question bank, generator, drilling, mastery, weaknesses and comments. Quizzes,
/// assignments and grades follow in phase 3.
/// </summary>
public static class PracticeModule
{
    public const string Name = "practice";

    public static IServiceCollection AddPracticeModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddNomaxModule(Name);
        services.AddModuleDbContext<PracticeDbContext>(PracticeDbContext.SchemaName);
        services.AddValidatorsFromAssemblyContaining<CreateQuestionValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

        services.AddOptions<DrillOptions>().Bind(configuration.GetSection(DrillOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<MasteryOptions>().Bind(configuration.GetSection(MasteryOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<QuizOptions>().Bind(configuration.GetSection(QuizOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddHostedService<DrillSessionCloser>();
        services.AddHostedService<QuizAttemptExpirer>();
        services.AddHostedService<GradeRecomputer>();

        services.AddScoped<IContentUsageProbe, PracticeContentUsage>();
        services.AddScoped<IDevelopmentSeeder, PracticeDevelopmentSeeder>();

        services.AddScoped<PracticeAccess>();
        services.AddScoped<MasteryViews>();
        services.AddScoped<QuestionStore>();
        services.AddScoped<QuestionGeneratorService>();
        services.AddScoped<DrillPool>();
        services.AddScoped<DrillBatches>();
        services.AddScoped<DrillLifecycle>();
        services.AddScoped<ClassScope>();
        services.AddScoped<GradeService>();
        services.AddScoped<AttemptGrading>();

        services.AddScoped<ListQuestionsHandler>();
        services.AddScoped<CreateQuestionHandler>();
        services.AddScoped<GetQuestionHandler>();
        services.AddScoped<UpdateQuestionHandler>();
        services.AddScoped<QuestionActionsHandler>();
        services.AddScoped<PreviewAnswerHandler>();
        services.AddScoped<SourcesHandler>();
        services.AddScoped<RunGeneratorHandler>();
        services.AddScoped<DrillSessionsHandler>();
        services.AddScoped<SubmitAnswerHandler>();
        services.AddScoped<ProgressHandler>();
        services.AddScoped<CommentsHandler>();
        services.AddScoped<PreviewDrillHandler>();
        services.AddScoped<QuizzesHandler>();
        services.AddScoped<AttemptsHandler>();
        services.AddScoped<AssignmentsHandler>();
        services.AddScoped<GradesHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapPracticeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapModuleGroup(Name);
        ListQuestionsHandler.Map(group);
        CreateQuestionHandler.Map(group);
        GetQuestionHandler.Map(group);
        UpdateQuestionHandler.Map(group);
        QuestionActionsHandler.Map(group);
        PreviewAnswerHandler.Map(group);
        SourcesHandler.Map(group);
        RunGeneratorHandler.Map(group);
        DrillSessionsHandler.Map(group);
        SubmitAnswerHandler.Map(group);
        ProgressHandler.Map(group);
        CommentsHandler.Map(group);
        PreviewDrillHandler.Map(group);
        QuizzesHandler.Map(group);
        AttemptsHandler.Map(group);
        AssignmentsHandler.Map(group);
        GradesHandler.Map(group);
        return endpoints;
    }
}

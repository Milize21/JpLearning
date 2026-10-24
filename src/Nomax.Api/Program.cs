using Nomax.BuildingBlocks.Hosting;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Classroom;
using Nomax.Modules.Content;
using Nomax.Modules.Identity;
using Nomax.Modules.Practice;

var builder = WebApplication.CreateBuilder(args);
builder.AddNomaxApiDefaults();
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddContentModule(builder.Configuration);
builder.Services.AddClassroomModule(builder.Configuration);
builder.Services.AddPracticeModule(builder.Configuration);

var app = builder.Build();
app.UseNomaxApiDefaults();
app.MapIdentityEndpoints();
app.MapContentEndpoints();
app.MapClassroomEndpoints();
app.MapPracticeEndpoints();
app.MapNomaxApiDefaults();

await app.InitializeNomaxDatabasesAsync();
await app.RunAsync();

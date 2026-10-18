using Nomax.Gateway;

var builder = WebApplication.CreateBuilder(args);
builder.AddNomaxGateway();

var app = builder.Build();
app.UseNomaxGateway();
app.Run();

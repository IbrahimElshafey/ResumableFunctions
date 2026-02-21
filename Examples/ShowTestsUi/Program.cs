using ResumableFunctions.Handler.Core;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.MvcUi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var settings =
    new SqlServerResumableFunctionsSettings(null, "CallerSameNameGroup_Test")
    .SetCurrentServiceUrl("https://localhost:7220/");
//settings.CleanDbSettings.CompletedInstanceRetention = TimeSpan.FromSeconds(3);
//settings.CleanDbSettings.DeactivatedWaitTemplateRetention = TimeSpan.FromSeconds(3);
//settings.CleanDbSettings.PushedCallRetention = TimeSpan.FromSeconds(3);
builder.Services.AddResumableFunctionsCore(settings);
builder.Services
    .AddControllers()
    .AddResumableFunctionsMvcUi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.Services.UseResumableFunctions();
app.UseResumableFunctionsUi();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

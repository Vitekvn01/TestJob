using FluentValidation;
using TestJob.Models;
using TestJob.Options;
using TestJob.Services;
using TestJob.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => //отступы
    {
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IHtmlAnalysisService, HtmlAnalysisService>();
builder.Services.AddScoped<IValidator<HtmlRequest>, HtmlAnalysisRequestValidator>();
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "api/swagger/{documentName}/swagger.json";
    });

    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "api/swagger";
        options.SwaggerEndpoint("/api/swagger/v1/swagger.json", "TestJob v1");
    });
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
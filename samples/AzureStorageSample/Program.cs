using AzureStorageProvider;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Azure Storage API", Version = "v1" });

    options.MapType<FileContentResult>(() => new OpenApiSchema
    {
        Type = "file"
    });
});

builder.Services.AddAzureStorage(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("AzureStorageConnection");
    options.ContainerName = builder.Configuration.GetValue<string>("AppSettings:ContainerName");
});

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseProblemDetails();
app.UseHttpsRedirection();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure Storage API v1");
    options.RoutePrefix = string.Empty;
});

app.MapControllers();

app.Run();
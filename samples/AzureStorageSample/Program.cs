using AzureStorageProvider;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
Configure(app, app.Environment);

app.Run();

void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddControllers();

    services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Azure Storage API", Version = "v1" });

        options.MapType<FileContentResult>(() => new OpenApiSchema
        {
            Type = "file"
        });
    });

    services.AddAzureStorage(options =>
    {
        options.ConnectionString = configuration.GetConnectionString("AzureStorageConnection");
        options.ContainerName = configuration.GetValue<string>("AppSettings:ContainerName");
    });

    services.AddProblemDetails();
}

void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseProblemDetails();
    app.UseHttpsRedirection();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure Storage API v1");
        options.RoutePrefix = string.Empty;
    });

    app.UseRouting();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
    });
}
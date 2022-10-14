using Microsoft.OpenApi.Models;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Multitenant;
using Orleans.Storage;
using Orleans4Multitenant.Apis;

var builder = WebApplication.CreateBuilder(args);
string? tableStorageConnectionString = builder.Configuration["Azure:TableStorageConnectionString"];

builder.Host.UseOrleans((_, silo) => silo
    .UseLocalhostClustering()

    .AddMultitenantGrainStorageAsDefault<AzureTableGrainStorage, AzureTableStorageOptions, AzureTableGrainStorageOptionsValidator>(
            (silo, name) => silo.AddAzureTableGrainStorage(name, options =>
                options.ConfigureTableServiceClient(tableStorageConnectionString)),
                // Called during silo startup, to ensure that any common dependencies
                // needed for tenant-specific provider instances are initialized

            configureTenantOptions: (options, tenantId) => {
                options.ConfigureTableServiceClient(tableStorageConnectionString);
                options.TableName = $"OrleansGrainState{tenantId}";
            }   // Called on the first grain state access for a tenant in a silo,
                // to initialize the options for the tenant-specific provider instance
                // just before it is instantiated
        )
);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml"));
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Example Orleans 4 Multitenant API", Version = "v1" });
    options.OperationFilter<TenantHeader.AddAsOpenApiParameter>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    _ = app.UseSwagger()
           .UseSwaggerUI(options => options.EnableTryItOutByDefault());
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

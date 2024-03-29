using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Orleans4Multitenant.Apis;

public static class TenantHeader
{
    public const string Name = "tenant";

    internal class AddAsOpenApiParameter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
         => (operation.Parameters ??= []).Add(new OpenApiParameter
         {
             Name = Name,
             In = ParameterLocation.Header,
             Schema = new OpenApiSchema { Type = "string" }
         });
    }
}

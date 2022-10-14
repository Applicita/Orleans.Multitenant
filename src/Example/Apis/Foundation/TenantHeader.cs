using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Orleans4Multitenant.Apis;

public class TenantHeader
{
    public const string Name = "tenant";

    public class AddAsOpenApiParameter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
         => (operation.Parameters ??= new List<OpenApiParameter>()).Add(new OpenApiParameter
         {
             Name = Name,
             In = ParameterLocation.Header,
             Schema = new OpenApiSchema { Type = "string" }
         });
    }
}

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StudentComplaintPortal.Web.Filters;

public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var formFileParameters = context.ApiDescription.ParameterDescriptions
            .Where(p => p.ModelMetadata?.ModelType == typeof(IFormFile))
            .ToList();

        if (!formFileParameters.Any())
            return;

        // Remove the automatically generated parameters for IFormFile
        var parametersToRemove = operation.Parameters
            .Where(p => formFileParameters.Any(f => f.Name == p.Name))
            .ToList();

        foreach (var parameter in parametersToRemove)
        {
            operation.Parameters.Remove(parameter);
        }

        // Add multipart/form-data request body
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["file"] = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary"
                            },
                            ["fileType"] = new OpenApiSchema
                            {
                                Type = "string",
                                Description = "Allowed values: Photo, Video, VoiceNote"
                            },
                            ["content"] = new OpenApiSchema
                            {
                                Type = "string",
                                Nullable = true
                            }
                        },
                        Required = new HashSet<string> { "file", "fileType" }
                    }
                }
            }
        };
    }
}

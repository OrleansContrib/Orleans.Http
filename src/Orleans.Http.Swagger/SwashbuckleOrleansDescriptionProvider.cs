using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans;
using Orleans.ApplicationParts;
using Orleans.Metadata;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Orleans.Http.Swagger
{
    public static class HostingExtensions
    {
        public static IServiceCollection AddSwashbuckleOrleans(this IServiceCollection services)
        {
            return services.Replace(new ServiceDescriptor(
                  typeof(IApiDescriptionGroupCollectionProvider),
                 typeof(SwashbuckleOrleansDescriptionProvider), ServiceLifetime.Singleton));
        }
    }

    class SwashbuckleOrleansDescriptionProvider : IApiDescriptionGroupCollectionProvider
    {
        private static readonly List<Type> _methodAttributeTypes = new List<Type>{
            typeof(Orleans.Http.Abstractions.HttpGetAttribute),
            typeof(Orleans.Http.Abstractions.HttpPostAttribute),
            typeof(Orleans.Http.Abstractions.HttpPutAttribute),
            typeof(Orleans.Http.Abstractions.HttpDeleteAttribute)

        };

        private static readonly List<Type> _parameterAttributeTypes = new List<Type>{
            typeof(Orleans.Http.Abstractions.FromBodyAttribute),
            typeof(Orleans.Http.Abstractions.FromQueryAttribute)
        };

        private static readonly Type _routeAttributeType = typeof(Orleans.Http.Abstractions.RouteAttribute);
        GrainInterfaceFeature grainInterfaceFeature;
        IModelMetadataProvider metadataProvider;

        public SwashbuckleOrleansDescriptionProvider(IServiceProvider services)
        {
            var appPartsMgr = services.GetRequiredService<IApplicationPartManager>();
            this.grainInterfaceFeature = appPartsMgr.CreateAndPopulateFeature<Orleans.Metadata.GrainInterfaceFeature>();
            this.metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        }

        private static List<Type> DiscoverGrainTypesToMap(GrainInterfaceFeature grainInterfaceFeature)
        {
            var grainTypesToMap = new List<Type>();

            foreach (var grainInterfaceMetadata in grainInterfaceFeature.Interfaces)
            {
                var grainType = grainInterfaceMetadata.InterfaceType;

                // Only add to the list grains that either have the top-level route attribute or has one of the method attributes
                if (grainType.GetCustomAttributes(true).Contains(_routeAttributeType) ||
                    grainType.GetMethods()
                        .Any(m => m.GetCustomAttributes(true)
                            .Any(attr => attr.GetType() == _routeAttributeType || _methodAttributeTypes.Contains(attr.GetType()))))
                {
                    grainTypesToMap.Add(grainType);
                }
            }

            return grainTypesToMap;
        }

        public ApiDescriptionGroupCollection ApiDescriptionGroups
        {
            get
            {
                var apiDescriptions = CreateDescriptors();
                return new ApiDescriptionGroupCollection(apiDescriptions, 1);
            }
        }

        private List<ApiDescriptionGroup> CreateDescriptors()
        {
            var groups = new List<ApiDescriptionGroup>();
            var grainList = DiscoverGrainTypesToMap(grainInterfaceFeature);
            foreach (var grainType in grainList)
            {
                var typeDescription = grainType.GetCustomAttribute<DescriptionAttribute>();
                var groupName = typeDescription?.Description ?? grainType.Name;
                var apiItems = new List<ApiDescription>();
                var methods = grainType.GetMethods().Where(m => m.GetCustomAttributes(true)
                           .Any(attr => attr.GetType() == _routeAttributeType || _methodAttributeTypes.Contains(attr.GetType()))).ToArray();


                foreach (var methodInfo in methods)
                {
                    var methodAttributes = methodInfo.GetCustomAttributes(true)
                    .Where(attr => attr is Orleans.Http.Abstractions.MethodAttribute).SingleOrDefault() as Orleans.Http.Abstractions.MethodAttribute;
                    if (null == methodAttributes) {
                        continue;
                    }
                    var descriptor = new ControllerActionDescriptor()
                    {
                        ControllerName = groupName,
                        ActionName = methodInfo.Name,
                        DisplayName = methodInfo.Name,
                        MethodInfo = methodInfo,
                        ControllerTypeInfo = methodInfo.DeclaringType.GetTypeInfo(),
                        RouteValues = new Dictionary<string, string>() { { "controller", groupName } },
                        AttributeRouteInfo = new AttributeRouteInfo()
                        {
                            Name = methodInfo.Name,
                            Template = methodAttributes.Pattern
                        },
                        ActionConstraints = new List<IActionConstraintMetadata>() { new HttpMethodActionConstraint(new[] { methodAttributes.Method }) },
                        Parameters = new List<ParameterDescriptor>(),
                        BoundProperties = new List<ParameterDescriptor>(),
                        FilterDescriptors = new List<FilterDescriptor>(),
                        Properties = new Dictionary<object, object>()
                    };

                    var description = new ApiDescription()
                    {
                        ActionDescriptor = descriptor,
                        GroupName = groupName,
                        HttpMethod = methodAttributes.Method,
                        RelativePath = $"grains/{methodAttributes.Pattern}"
                    };

                    var methodParams = methodInfo.GetParameters();

                    foreach (var parameter in methodParams)
                    {
                        var bindsource = BindingSource.Path;
                        var attribute = parameter.GetCustomAttributes()
                  .Where(attr => _parameterAttributeTypes.Contains(attr.GetType())).FirstOrDefault();
                        if (attribute != null)
                        {
                            if (attribute is Orleans.Http.Abstractions.FromBodyAttribute)
                            {
                                bindsource = BindingSource.Body;
                            }
                            else if (attribute is Orleans.Http.Abstractions.FromQueryAttribute)
                            {
                                bindsource = BindingSource.Query;
                            }
                        }
                        var parameterDescriptor = new ControllerParameterDescriptor()
                        {
                            Name = parameter.Name,
                            ParameterType = parameter.ParameterType,
                            /* BindingInfo = new BindingInfo()
                             {
                                 BinderModelName = parameter.Name,
                                 BindingSource = bindsource,
                                 BinderType = parameter.ParameterType
                             },*/
                            ParameterInfo = parameter
                        };

                        descriptor.Parameters.Add(parameterDescriptor);

                        description.ParameterDescriptions.Add(new ApiParameterDescription()
                        {
                            ModelMetadata = metadataProvider.GetMetadataForType(parameter.ParameterType),
                            Name = parameter.Name,
                            RouteInfo = new ApiParameterRouteInfo(),
                            Type = parameter.ParameterType,
                            Source = bindsource,
                            IsRequired = true
                        });
                    }
                    //support path route ?

                    description.SupportedRequestFormats.Add(new ApiRequestFormat()
                    {
                        MediaType = "application/json"
                    });
                    description.SupportedResponseTypes.Add(new ApiResponseType()
                    {
                        ApiResponseFormats = new List<ApiResponseFormat>() {
                                new ApiResponseFormat() { MediaType = "application/json" }
                            },
                        ModelMetadata = metadataProvider
                                .GetMetadataForType(/*methodInfo.ReturnType*/typeof(System.Text.Json.JsonElement)),
                        Type = methodInfo.ReturnType,
                        StatusCode = (int)System.Net.HttpStatusCode.OK
                    }); ;
                    //InferRequestContentTypes(description);
                    apiItems.Add(description);
                }

                var group = new ApiDescriptionGroup(groupName, new ReadOnlyCollection<ApiDescription>(apiItems));

                groups.Add(group);
            }
            return groups;

        }

    }
}

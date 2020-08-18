# Orleans.Http

<p align="center">
  <img src="https://github.com/dotnet/orleans/blob/gh-pages/assets/logo.png" alt="Orleans.Http" width="300px"> 
  <h1>Orleans HTTP Endpoints</h1>
</p>

[![Build](https://github.com/OrleansContrib/Orleans.Http/workflows/CI/badge.svg)](https://github.com/OrleansContrib/Orleans.Http/actions)
[![Package Version](https://img.shields.io/nuget/v/Orleans.Http.svg)](https://www.nuget.org/packages/Orleans.Http)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Orleans.Http.svg)](https://www.nuget.org/packages/Orleans.Http)
[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/orleans?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

[Orleans](https://github.com/dotnet/orleans) is a framework that provides a straight-forward approach to building distributed high-scale computing applications, without the need to learn and apply complex concurrency or other scaling patterns. 

**Orleans.Http** is a package that use ASP.Net Core as a frontend endpoint for Orleans grains without the need of controller classes.

This package leverages ASP.NET Core Endpoint Routing to expose grains as HTTP endpoints without need of have to implement Controllers. The request is received and processed by grains methods itself. The idea is to perform in a similar way to Controllers without having to add any boilerplate code to your Orleans project.

At the silo startup, the package look for usage of `[Route]` and `[HttpXXX]` attributes onn grain interface methods and register a route on ASP.NET Core endpoints route table. When a request arrive to that route, the parameters are extracted from the request, mapped to grain method parameters, and the grain is invoked. If the grain return `Task<T>`, the returning object is serialized back to the caller using one of the registered `IMediaTypeHandler` and added to the Response body. 

# Installation

Installation is performed via [NuGet](https://www.nuget.org/packages?q=Orleans+Http)

## On your Silo Project

From Package Manager:

> PS> Install-Package Orleans.Http -prerelease

.Net CLI:

> \# dotnet add package Orleans.Http -prerelease

Paket: 

> \# paket add Orleans.Http -prerelease

## On your Grain Interface Project(s)

From Package Manager:

> PS> Install-Package Orleans.Http.Abstractions -prerelease

.Net CLI:

> \# dotnet add package Orleans.Http.Abstractions -prerelease

Paket: 

> \# paket add Orleans.Http.Abstractions -prerelease

# Serialization and parameter mapping

The serialization of both request and response body is handled by implementations of `IMediaTypeHandler`. The runtime checks the `Content-Type` header of the request and lookup for a `IMediaTypeHandler` registered implementation that can deserialize the body with that partticular media type. If one matches, the deserialization happens and the deserialized object is passed to the parameter of the grain method being invoked that is marked with the `[FromBody]` attribute. If there is no serializer registered for that `Content-Type`, it will just set the parameter as null. 

Whenever the method returns `Task<T>`, the runtime will check if there is an `Accept` header and use it to lookup for a `IMediaTypeHandler` that will then serialize the response back to the caller in the Response body. If there is no `Accept` header, it will then try to serialize the response using the `Content-Type` header instead.

Parameters can be also mapped from both the Route and the Query string by using `[FromRoute]` and `[FromQuery]` parameter attributes respectively. Those attributes works only for primitive types.

By default, Json (using `System.Text.Json`), XML and Forms are provided with `Orleans.Http` package. Protobuf (protobuf-net) is also available by adding the `Orleans.Http.MediaTypes.Protobuf` package.

# Custom Serializers

If you want to implement your own `IMediaTypeHandler` to handle a new (or existig) media type, just implement and add an instance of the following interface to the DI context and it will be invoked by the runtime:

```csharp
public interface IMediaTypeHandler
{
    string MediaType { get; } // This should be a unique media type i.e. application/mystuff
    ValueTask Serialize(object obj, PipeWriter writer);
    ValueTask<object> Deserialize(PipeReader reader, Type type, CancellationToken cancellationToken);
}
```

## Authentication / Authorizattion

In a similar way to ASP.NET Core Controllers, this package also allow the developer to leverage ASP.NET Core Authentication/Authorization middleware. Just add `[Authorize]` attribute to the grain method interface and it will just work the same way. Make sure to configure your ASP.NET Core Authentication/Authorizattion middleware.

# Routes and Attributes

The routes are generated in a similar way as ASP.NET Core Attribute Routing. By default, no route is generated and all grains are considered private.

The `Pattern` property of the attributes define the route to be used. If the `Pattern` property is ommited, the default route is used as the following pattern:

`{optionalPrefix}/{optionalTopLevelPattern}/{grainTypeName}/{grainId}/{methodName}`

By default, if an attribute `Pattern` property is set, it is required to somewhere into the pattern to add the `{grainId}` string otherwise the route will fail to be registered. Optionally, you can also add `{grainIdExtension}` in case of grains that have Compound keys. 

The following attributes are under the `Orleans.Http.Abstractions` package:

> Note: All routes described by the attribute takes into consideration the optional prefix set on yor Startup class when you call `MapGrains("prefix")`.

## `[Route]`

This attribute can be used on both interface and methods.

When applied to a *Grain Interface*, this attribute `Pattern` property value is added as the first node of the grain route.

When added to a *Grain Method*, this attribute tells the runtime to route ALL HTTP verbs to that particular Pattern for that method.

## `[HttpXXX]`

This attribute can be applied only to methods.

The value of `Patttern` property is used to generate the route of that method for a particular HTTP Verb. If the patttern starts with `/`, it will ignore all the prefixes and will be used as the de-facto route for that method. 

### `routeGrainProviderPolicy optional argument`

This argument allows you to set the route grain provider used for the speicified endpoint. This allows you to override the default behaviour and provide an `IGrain` that the request will go to. To register a route grain provider policy, use the extension method `UseRouteGrainProviders(Action<IRouteGrainProviderPolcyBuilder>)` on `IApplicationBuilder` In your `Startup` class. The default RouteGrainProvider policy may also be set there. Registering the policy takes in a generic type argument, this type must implement `IRouteGrainProvider`, optionally, if your implementing type is registered with dependency injection, it will use that type's lifetime, otherwise it will have a scoped lifetime by default. 

# Example usage

Using the .Net Core Generic Host configure Orleans the same way you would and add the ASP.NET Core to it:

```csharp
    var hostBuilder = new HostBuilder();
    hostBuilder.UseConsoleLifetime();
    hostBuilder.ConfigureLogging(logging => logging.AddConsole());
    hostBuilder.ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseUrls("http://*:9090");
        webBuilder.UseStartup<Startup>();
    });

    hostBuilder.UseOrleans(b =>
    {
        b.UseLocalhostClustering();

        b.ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Startup).Assembly));
    });

    var host = hostBuilder.Build();
    host.Start();
```

On your `Startup` class:

```csharp
public class Startup
{
    public const string SECRET = "THIS IS OUR AWESOME SUPER SECRET!!!";
    public void ConfigureServices(IServiceCollection services)
    {
        // Inject IHttpContextAccessor on your grain to get access to the HttpContext
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Add the authentication services if you want to use it. 
        // For this example, we're using a simple Jwt
        services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opt =>
        {
            opt.RequireHttpsMetadata = false;
            opt.SaveToken = true;
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SECRET)),
                ValidateIssuer = false,
                ValidateAudience = false
            };
        });

        services.AddAuthorization();

        // Add the GrainRouter service and any IMediaTypeHandler instances you want
        services
            .AddGrainRouter()
            .AddJsonMediaType()
            .AddProtobufMediaType();
        services.AddMvcCore().AddApiExplorer();
        services.AddSwashbuckleOrleans();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "My Api", Version="v1"});              
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
           c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        });
        // Enable ASP.NET Core Endpoint Routing
        app.UseRouting();

        // Enable Authentication
        app.UseAuthentication();

        // Enable Authorization
        app.UseAuthorization();

        // Configure ASP.NET Core endpoints
        app.UseEndpoints(endpoints =>
        {
            // Call MapGrains([prefix]) with an optional prefix for the routes.
            endpoints.MapGrains("grains");

            // You can add any other endpoints here like regular ASP.NET Controller, SignalR, you name it. 
            // Orleans endpoints are agnostic to other routes.
        });

        //Optionally register route grain provider policies
        app.UseRouteGrainProviders(routeGrainProviders =>
        {
            routeGrainProviders.RegisterRouteGrainProvider<YourCustomRouteGrainProvider>("SomePolicy");

            //You may also optionally set the defaulty policy, so it it used by every endpoint without a specified policy
            routeGrainProviders.SetDefaultRouteGrainProviderPolicy("SomePolicy");
        });
    }
}
```

# Contributions
PRs and feedback are **very** welcome!

using System.Reflection;
using AISearch.Api.Configuration;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// ===== AuthN/Z =====
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(jwtOptions =>
        {
            builder.Configuration.Bind("AzureAd", jwtOptions);

            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            // Useful logs when a token is rejected
            jwtOptions.Events = CreateJwtBearerEvents();
            // If you previously loosened issuer/audience, flip those flags here instead.
        },
        msIdentityOptions => builder.Configuration.Bind("AzureAd", msIdentityOptions));

builder.Services.AddAuthorization(o => { o.AddPolicy("RequireAuthenticatedUser", p => p.RequireAuthenticatedUser()); });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(ConfigureSwagger);

// ===== CORS (make it spec-compliant) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var corsSection = builder.Configuration.GetSection("CORS:AllowFrontend");
        var allowedOrigins = corsSection.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        var allowCredentials = corsSection.GetValue("AllowCredentials", true);

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();

            if (allowCredentials)
                policy.AllowCredentials();
        }
        else
        {
            // When no explicit origins are configured, fall back to wildcard WITHOUT credentials.
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            // DO NOT call AllowCredentials() with AllowAnyOrigin()
        }

        // If your JS needs to read response headers, list them here
        policy.WithExposedHeaders("Authorization"); // Avoid exposing "Authorization" back to JS
    });
});

// ===== App services/clients =====
builder.Services.Configure<SearchConfiguration>(builder.Configuration.GetSection("AzureSearch"));
var searchConfig = new SearchConfiguration();
builder.Configuration.GetSection("AzureSearch").Bind(searchConfig);
builder.Services.AddSingleton(searchConfig);
builder.Services.AddSingleton<ISearchConfiguration>(searchConfig);

RegisterAzureClients(builder.Services, builder.Configuration);
RegisterApplicationServices(builder.Services);

var app = builder.Build();

// ===== Swagger =====
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Search Multimodal API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "AI Search Multimodal API Documentation";
#if DEBUG
    c.DefaultModelsExpandDepth(-1);
    c.DocExpansion(DocExpansion.List);
    c.EnableDeepLinking();
    c.EnableFilter();
    c.ShowExtensions();
#endif
});

if (!app.Environment.IsDevelopment() || builder.Configuration["ASPNETCORE_HTTPS_PORT"] != null)
    app.UseHttpsRedirection();

// ===== Preflight short-circuit BEFORE auth =====
app.Use(async (ctx, next) =>
{
    if (HttpMethods.IsOptions(ctx.Request.Method))
    {
        // Let CORS handle it; no auth required
        ctx.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next();
});

// ===== Order matters =====
app.UseCors("AllowFrontend"); // CORS before auth so preflight succeeds
app.UseAuthentication(); // then authenticate
app.UseAuthorization(); // then authorize

app.MapControllers();

app.Run();

static JwtBearerEvents CreateJwtBearerEvents()
{
    return new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Authentication failed: {ExceptionType}: {Message}",
                context.Exception.GetType().Name, context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Token validated for: {Name}", context.Principal?.Identity?.Name ?? "(no name)");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Authentication challenge: {Error} - {Description}", context.Error,
                context.ErrorDescription);
            return Task.CompletedTask;
        }
    };
}

// --- keep your existing ConfigureSwagger, RegisterAzureClients, RegisterApplicationServices ---

static void ConfigureSwagger(SwaggerGenOptions c)
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Search Multimodal API",
        Version = "v1",
        Description =
            "REST API for Azure AI Search with multimodal capabilities including document upload, indexing, semantic search, vector search, and chat with document grounding.",
        Contact = new OpenApiContact { Name = "AI Search API", Email = "support@example.com" }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
                { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
}

static void RegisterAzureClients(IServiceCollection services, IConfiguration configuration)
{
    services.AddSingleton<SearchIndexClient>(provider =>
    {
        var config = provider.GetRequiredService<IOptions<SearchConfiguration>>().Value;
        var credential = new AzureKeyCredential(config.SearchAdminKey);
        return new SearchIndexClient(new Uri(config.ServiceEndpoint), credential);
    });

    services.AddSingleton<SearchClient>(provider =>
    {
        var config = provider.GetRequiredService<IOptions<SearchConfiguration>>().Value;
        var credential = new AzureKeyCredential(config.SearchAdminKey);
        return new SearchClient(new Uri(config.ServiceEndpoint), config.IndexName, credential);
    });

    services.AddSingleton<OpenAIClient>(provider =>
    {
        var config = provider.GetRequiredService<IOptions<SearchConfiguration>>().Value;
        var credential = new AzureKeyCredential(config.OpenAIApiKey);
        return new OpenAIClient(new Uri(config.OpenAIEndpoint), credential);
    });

    services.AddSingleton<BlobServiceClient>(provider =>
    {
        var config = provider.GetRequiredService<IOptions<SearchConfiguration>>().Value;
        var credential = new StorageSharedKeyCredential(
            new Uri(config.StorageAccountUrl).Host.Split('.')[0],
            config.StorageAccountKey);
        return new BlobServiceClient(new Uri(config.StorageAccountUrl), credential);
    });
}

static void RegisterApplicationServices(IServiceCollection services)
{
    services.AddHttpContextAccessor();
    services.AddScoped<IApiUserService, ApiUserService>();
    services.AddScoped<ISearchService, SearchService>();
    services.AddScoped<IIndexService, IndexService>();
    services.AddScoped<IDocumentService, DocumentService>();
    services.AddScoped<IMultimodalRagService, MultimodalRagService>();
    services.AddScoped<IContentExtraction, AzureDocumentIntelligenceExtractor>();
    services.AddScoped<EmbeddingGenerator>();
}
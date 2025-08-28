using AISearch.Web.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Configure authentication with MSAL and enhanced token handling
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.SaveTokens = true;

        var apiScope = builder.Configuration["DownstreamApi:Scopes"];
        if (!string.IsNullOrEmpty(apiScope)) options.Scope.Add(apiScope);
        options.Scope.Add("User.Read");
        options.Scope.Add("offline_access"); // Required for refresh tokens

        // Configure token lifetime and refresh settings
        options.UseTokenLifetime = false; // Don't tie session to token lifetime
        options.Events = CreateOpenIdConnectEvents();
    })
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches(); // Consider using distributed cache in production

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Register enhanced token services
builder.Services.AddScoped<ITokenRefreshService, TokenRefreshService>();

// Register auth service based on configuration
var useSimpleAuth = builder.Configuration.GetValue("UseSimpleAuth", false); // Default to enhanced auth
var useEnhancedAuth = builder.Configuration.GetValue("UseEnhancedAuth", true);

if (useEnhancedAuth)
{
    builder.Services.AddScoped<AISearch.Core.Interfaces.IUserAuthService, EnhancedUserAuthService>();
    builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
}
else if (useSimpleAuth)
{
    builder.Services.AddScoped<AISearch.Core.Interfaces.IUserAuthService, SimpleUserAuthService>();
    builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));
}
else
{
    builder.Services.AddScoped<AISearch.Core.Interfaces.IUserAuthService, UserAuthService>();
    builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
}

builder.Services.AddTransient<AuthorizationHeaderHandler>();

// Configure HttpClient for API calls
builder.Services.AddHttpClient("APIClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:7001"); // Correct API port from launchSettings.json
    client.Timeout = TimeSpan.FromMinutes(5);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

// Also configure a default HttpClient
builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Add token refresh middleware after authentication
app.UseMiddleware<TokenRefreshMiddleware>();

app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

static OpenIdConnectEvents CreateOpenIdConnectEvents()
{
    return new OpenIdConnectEvents
    {
        OnAccessDenied = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var error = context.Request.Query["error"].FirstOrDefault();
            logger.LogWarning("?? Access denied - Error: {Error}", error);

            var redirectUrl = error == "access_denied"
                ? "/Account/AccessDenied?error=access_denied"
                : "/Account/SignIn?error=access_denied";
            context.Response.Redirect(redirectUrl);
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("? Remote authentication failure: {Exception}", context.Failure);

            var redirectUrl = context.Failure?.Message?.Contains("access_denied") == true
                ? "/Account/AccessDenied?error=remote_access_denied"
                : "/Account/SignIn?error=remote_failure";
            context.Response.Redirect(redirectUrl);
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("? Token validated for user: {User}", context.Principal?.Identity?.Name);
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("? Authentication failed: {Exception}", context.Exception);
            context.Response.Redirect("/Account/SignIn?error=auth_failed");
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnTokenResponseReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("??? Token response received - Access token: {HasAccessToken}, Refresh token: {HasRefreshToken}",
                !string.IsNullOrEmpty(context.TokenEndpointResponse.AccessToken),
                !string.IsNullOrEmpty(context.TokenEndpointResponse.RefreshToken));
            return Task.CompletedTask;
        }
    };
}
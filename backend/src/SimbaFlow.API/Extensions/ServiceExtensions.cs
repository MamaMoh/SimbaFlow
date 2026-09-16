using Microsoft.AspNetCore.Authorization;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.RealTime;

namespace SimbaFlow.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Carter (Minimal API modules)
        services.AddCarter();

        // MediatR + Pipeline Behaviors (order matters)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(ApplicationDbContext).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceLogBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);

        // Infrastructure (EF Core + Identity + JWT + services)
        services.AddInfrastructure(configuration);

        // SignalR
        services.AddSignalR();
        services.AddScoped<ISignalRBroadcaster, SignalRBroadcaster>();

        // HTTP Context
        services.AddHttpContextAccessor();

        // Authorization
        var authBuilder = services.AddAuthorizationBuilder()
            .AddPolicy("SuperAdmin", policy => policy.RequireAssertion(ctx =>
                ctx.User.HasClaim("role", "SuperAdmin") ||
                ctx.User.HasClaim(System.Security.Claims.ClaimTypes.Role, "SuperAdmin") ||
                ctx.User.IsInRole("SuperAdmin")))

            // The token issued to someone who has passed only the first factor and still has to
            // enrol in MFA. It is signed with the same key and carries the same issuer and audience
            // as a full token, so on its own the API would accept it anywhere — which is exactly
            // what enforcing MFA is meant to prevent. The token_use claim marked it as limited but
            // nothing ever read that claim.
            //
            // Reading it here, in the default policy, covers every endpoint that says
            // RequireAuthorization() without arguments — which is all of them but the two below.
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => !ctx.User.HasClaim(c => c.Type == "token_use"))
                .Build())

            // ...and these two opt back in, because enrolling is the one thing such a token exists
            // to do. A full token is accepted here too, so an enrolled user can re-enrol.
            .AddPolicy("MfaEnrollment", policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    !ctx.User.HasClaim(c => c.Type == "token_use")
                    || ctx.User.HasClaim("token_use", "mfa_setup")));

        // Rate Limiting
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned by caller, not shared. A single fixed window across all callers is the
            // wrong shape for a login limiter twice over: one careless client exhausts it for
            // everyone, and an attacker spraying one password across many accounts is throttled no
            // harder than a single user who mistypes.
            //
            // The limit is well above what a person does and well below what a spray needs. It is
            // not 5/minute because an agency office sits behind one NAT address, and a morning
            // where eight staff sign in together must not lock out the ninth.
            options.AddPolicy("login", ClientPartition(permitLimit: 15, window: TimeSpan.FromMinutes(1)));

            // Password reset accepts an arbitrary email from an anonymous caller. Keep it tight:
            // enough for a person who mistypes their address, far too slow to enumerate accounts.
            options.AddPolicy("auth", ClientPartition(permitLimit: 10, window: TimeSpan.FromMinutes(15)));

            // Refresh is chatty — several tabs rotating a token at once is normal — so this is
            // generous. It exists to make guessing at refresh tokens pointless, not to pace clients.
            options.AddPolicy("refresh", ClientPartition(permitLimit: 60, window: TimeSpan.FromMinutes(1)));

            options.AddFixedWindowLimiter("general", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 2;
            });

            options.AddFixedWindowLimiter("upload", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });
        });

        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(
                    configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"])
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        // Health Checks
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("DefaultConnection")!, name: "postgresql");

        // OpenAPI
        services.AddOpenApi();

        return services;
    }

    /// <summary>
    /// A fixed window per calling client, keyed on the client's address.
    ///
    /// The address is the one left by UseForwardedHeaders, which is the browser's — requests arrive
    /// via nginx and the Next.js server, so without that every caller would look identical and this
    /// would silently become one shared bucket for the whole platform. If no address can be
    /// determined at all, such a request is given its own small allowance rather than being pooled
    /// with everyone else's, so a missing address can never lock real users out.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> ClientPartition(
        int permitLimit, TimeSpan window) =>
        httpContext =>
        {
            var address = httpContext.Connection.RemoteIpAddress;
            var key = address is null ? "unknown" : address.ToString();

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            });
        };
}

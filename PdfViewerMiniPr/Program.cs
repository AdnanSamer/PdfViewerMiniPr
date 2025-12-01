using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Syncfusion.Licensing;
using PdfViewerMiniPr;
using PdfViewrMiniPr.Infrastructure.Email;
using PdfViewrMiniPr.Aplication.Interfaces;
using PdfViewrMiniPr.Aplication.Services;
using PdfViewrMiniPr.Domain.Interfaces;
using PdfViewrMiniPr.Infrastructure.Database;
using PdfViewrMiniPr.Infrastructure.Pdf;
using PdfViewrMiniPr.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Register Syncfusion license
SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JFaF1cX2hIfExzWmFZfVtgfF9HZlZRRmYuP1ZhSXxWd0RjXH9WcXJVQGBbVUJ9XEM=");

// Add services to the container.

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PdfViewerMiniPr", Version = "v1" });

    // JWT Bearer auth in Swagger (Authorize button)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddMemoryCache();

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection.GetValue<string>("Secret") ?? "THIS_IS_A_DEV_SECRET_KEY_CHANGE_ME";
var jwtIssuer = jwtSection.GetValue<string>("Issuer") ?? "PdfViewerMiniPr";
var jwtAudience = jwtSection.GetValue<string>("Audience") ?? "PdfViewerMiniPrClient";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = signingKey
    };
});

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("PdfViewerMiniPr"));
});

// SMTP / Email settings
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

// Repositories
builder.Services.AddScoped(typeof(IRepositoryBase<>), typeof(RepositoryBase<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
builder.Services.AddScoped<IWorkflowExternalAccessRepository, WorkflowExternalAccessRepository>();
builder.Services.AddScoped<IWorkflowStampRepository, WorkflowStampRepository>();

// Domain/application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IInternalReviewService, InternalReviewService>();
builder.Services.AddScoped<IExternalReviewService, ExternalReviewService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IPdfStampService, SyncfusionPdfStampService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Seed initial users
SeedData.Initialize(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

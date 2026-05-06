
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BookApi;
using BookApi.Data;

var builder = WebApplication.CreateBuilder(args);

// ========== 数据库配置 - 针对 Neon PostgreSQL ==========
// 方式1：从环境变量读取连接字符串（匹配你的 ConnectionStrings_BookQuote）
// .NET 会自动将环境变量 ConnectionStrings_BookQuote 映射到 Configuration["ConnectionStrings:BookQuote"]
var connectionString = builder.Configuration.GetConnectionString("BookQuote");

// 备用方案：如果上面的方式读不到，直接从环境变量读取
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings_BookQuote");
}

// 如果还是读不到，尝试其他常见的环境变量名
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? Environment.GetEnvironmentVariable("NEON_DATABASE_URL");
}

// 检查是否成功读取到连接字符串
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("错误：无法读取数据库连接字符串！");
    Console.WriteLine("请确保环境变量 ConnectionStrings_BookQuote 已设置。");
    throw new InvalidOperationException("数据库连接字符串未配置");
}

Console.WriteLine($"数据库连接字符串已读取（长度：{connectionString.Length} 字符）");

// 使用 PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ========== JWT 认证配置（保持不变）==========
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "BookApi",
            ValidAudience = "BookApp",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("din-hemliga-nyckel-som-ar-minst-32-tecken-lang123!"))
        };
    });

builder.Services.AddControllers();
builder.Services.AddCors();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var app = builder.Build();

// 配置跨域
app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ========== 自动创建/更新数据库 ==========
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // 对于 Neon PostgreSQL，使用 Migrate
        db.Database.Migrate();
        Console.WriteLine("数据库迁移成功！");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"数据库迁移失败: {ex.Message}");
        throw;
    }
}

app.Run();



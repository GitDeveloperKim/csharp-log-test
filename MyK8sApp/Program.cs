using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;
using System.Diagnostics;
using Serilog.Templates; // 상단에 추가

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog 설정
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "csharp-log-api")
    .Enrich.WithProperty("version", "1.2.3")
    // 요구하신 포맷에 맞춰 필드명을 강제로 매핑합니다.
    .WriteTo.Console(new ExpressionTemplate(
        "{ {@t: 'timestamp', @l: 'level', service: 'csharp-log-api', version: '1.2.3', trace_id, span_id, user_id, method, path, status_code, duration_ms, message: @m, error: @x} }\n"))
    .CreateLogger();

builder.Host.UseSerilog(); // 기본 로그 대신 serilog 사용 

// --- 기존 서비스 설정 코드 ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

// 2. HTTP 요청 정보를 로그 필드에 심어주는 커스텀 미들웨어
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    // .NET 8의 기본 추적 ID 추출
    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    var spanId = Activity.Current?.SpanId.ToString() ?? "";

    try
    {
        await next();
        sw.Stop();
        
        // 필드명을 템플릿과 일치시킵니다.
        Log.Information("{method} {path} 완료. status_code: {status_code}, duration_ms: {duration_ms}, user_id: {user_id}, trace_id: {trace_id}, span_id: {span_id}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            "ys88sem.kim",
            traceId,
            spanId);
    }
    catch (Exception ex)
    {
        sw.Stop();
        Log.Error(ex, "오류 발생. method: {method}, path: {path}, trace_id: {trace_id}", 
            context.Request.Method, context.Request.Path, traceId);
        throw;
    }
});

app.MapControllers();

// 3. 앱 시작 시 테스트 로그 작성
Log.Information("ASP.NET Core Server가 시작되었습니다. EFK 수집 테스트 로그입니다.");

app.MapGet("/test-api", () => new { 
    message = "Minimal API 호출 성공!", 
    timestamp = DateTime.Now 
});
app.Run();
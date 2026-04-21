using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;
using Serilog.Context; // 추가 필요
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
    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    var spanId = Activity.Current?.SpanId.ToString() ?? "";

    // LogContext를 사용하여 이 블록 안에서 발생하는 모든 로그에 속성을 강제로 주입합니다.
    using (LogContext.PushProperty("trace_id", traceId))
    using (LogContext.PushProperty("span_id", spanId))
    using (LogContext.PushProperty("method", context.Request.Method))
    using (LogContext.PushProperty("path", context.Request.Path))
    using (LogContext.PushProperty("user_id", "ys88sem.kim"))
    {
        try
        {
            await next(); // 여기서 API 로직(MapGet 등)이 실행됩니다.
            sw.Stop();
            
            // 1. 인프라 로그 (요청 처리 완료 로그)
            Log.Information("{method} {path} 완료. status_code: {status_code}, duration_ms: {duration_ms}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(ex, "오류 발생. status_code: 500");
            throw;
        }
    }
});

app.MapControllers();

// 3. 앱 시작 시 테스트 로그 작성
Log.Information("ASP.NET Core Server가 시작되었습니다. EFK 수집 테스트 로그입니다.");

// 테스트용 엔드포인트: 사용자가 직접 로그 메시지를 남기는 시나리오
app.MapGet("/test-api", () => 
{
    // 1. 비즈니스 로직 수행 (예시)
    var responseMessage = "Minimal API 호출 성공!";
    
    // 2. 사용자 정의 로그 기록 (이 메시지가 JSON의 "message" 필드로 들어갑니다)
    // 요구하신 포맷의 "message":"user written log" 부분을 구현한 것입니다.
    Log.Information("user written log: 데이터를 성공적으로 조회했습니다.");

    return Results.Ok(new { 
        message = responseMessage, 
        timestamp = DateTime.UtcNow // K8s 환경에서는 UTC 사용을 권장합니다.
    });
});
app.Run();
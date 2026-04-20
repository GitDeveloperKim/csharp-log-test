using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog 설정: 콘솔에 JSON 형식으로 로그를 출력하도록 설정
// 이렇게 설정해야 EFK의 Fluent Bit이 로그를 필드별(Level, Message, Exception 등)로 파싱할 수 있습니다.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter()) // JSON 포맷터 적용
    .Enrich.FromLogContext()              // 로그에 컨텍스트 정보 추가
    .CreateLogger();

// 2. 기본 로거 대신 Serilog를 사용하도록 설정
builder.Host.UseSerilog();

// --- 기존 서비스 설정 코드 ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- 미들웨어 설정 코드 ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// 3. 앱 시작 시 테스트 로그 작성
Log.Information("ASP.NET Core Server가 시작되었습니다. EFK 수집 테스트 로그입니다.");

app.Run();
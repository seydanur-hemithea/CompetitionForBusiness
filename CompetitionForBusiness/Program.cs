using CompetitionForBusiness.Data;
using CompetitionForBusiness.Hubs;

using Microsoft.EntityFrameworkCore;

using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddScoped<CompetitionForBusiness.Services.AiAnalysisService>();

// Mobil Uygulama ve Web Eriþimleri Ýçin CORS Politikasý
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials(); // SignalR için þarttýr
    });
});

// Supabase PostgreSQL DbContext Kaydý
// 1. Sadece Render / Sistem Environment Variable hafýzasýndan okuma yapýlýr.
// (Render panelinde Key ismi: ConnectionStrings__SupabaseConnection olacaktýr)
string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SupabaseConnection");

// 2. Güvenlik ve Doðrulama Kontrolü
if (string.IsNullOrEmpty(connectionString))
{
    // Eðer Render panelinde tanýmlanmamýþsa sunucu güvenli þekilde açýlmayý reddeder.
    throw new InvalidOperationException("KRÝTÝK HATA: Render üzerinde 'ConnectionStrings__SupabaseConnection' çevre deðiþkeni bulunamadý!");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// SignalR Hub Endpoint Tanýmlamasý
app.MapHub<QuizHub>("/quizHub");

app.Run();



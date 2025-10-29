using Earthquakeapi.Models;      // MongoDB entity (EarthquakeAlert)
using EarthquakeApi.Models;      // API veri modeli (Earthquake)
using EarthquakeApi.Services;    // Extension method GetValidDate
using Earthquakeapi.Services;
using Earthquakeapi.Settings;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton<EarthquakeAlertService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddHttpClient<EarthquakeService>();

// CORS policy ekle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDevClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// CORS middleware
app.UseCors("AllowAngularDevClient");

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// POST endpoint: yeni deprem ekleme
app.MapPost("/api/earthquakes", async (EarthquakeAlert alert, EarthquakeAlertService service) =>
{
    await service.CreateAsync(alert);
    return Results.Created($"/api/earthquakes/{alert.Id}", alert);
}).RequireCors("AllowAngularDevClient");

// GET endpoint: Kandilli API’den deprem verisi çek
app.MapGet("/api/earthquakes", async (EarthquakeService service, EarthquakeAlertService alertService) =>
{
    var data = await service.GetEarthquakesAsync();

    foreach (Earthquake quake in data)
    {
        if (!quake.IsDangerous) continue;

        // Tarih güvenli şekilde al
        var dateString = quake.GetValidDate();
        if (string.IsNullOrWhiteSpace(dateString))
        {
            Console.WriteLine($"⚠️ Tarih yok: {quake.Title}");
            continue;
        }

        // API’nin yeni formatı yyyy-MM-dd HH:mm:ss
        if (!DateTime.TryParseExact(
                dateString,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var quakeDate))
        {
            Console.WriteLine($"⚠️ Geçersiz tarih formatı: {dateString} ({quake.Title})");
            continue;
        }

        // MongoDB’de aynı kayıt var mı kontrol et
        bool exists = await alertService.ExistsAsync(quake.Title, quakeDate);
        if (!exists)
        {
            var alert = new EarthquakeAlert
            {
                Title = quake.Title,
                Magnitude = quake.Mag,
                Date = quakeDate,
                IsDangerous = quake.IsDangerous,
                ClosestLocationName = quake.ClosestLocationName ?? ""
            };
            await alertService.CreateAsync(alert);
        }
    }

    return Results.Ok(new { result = data });
});

// GET endpoint: mock veri
app.MapGet("/api/earthquakes/mock", () =>
{
    var fakeData = new[]
    {
        new {
            earthquake_id = "mock1",
            date = "2025.07.24 12:00:00",
            title = "Sahte Deprem",
            mag = 4.5,
            depth = 10,
            geojson = new {
                type = "Point",
                coordinates = new double[] { 38.1355, 37.0132 }
            },
            isDangerous = true,
            closestLocationName = "Örnek Santral"
        },
    };
    return Results.Ok(new { result = fakeData });
}).RequireCors("AllowAngularDevClient");

app.Run();


// -----------------------------
// Extension method: Earthquake sınıfı için
// -----------------------------
namespace EarthquakeApi.Services
{
    using EarthquakeApi.Models;

    public static class EarthquakeExtensions
    {
        public static string GetValidDate(this Earthquake quake)
        {
            return quake.Date ?? string.Empty;
        }
    }

}

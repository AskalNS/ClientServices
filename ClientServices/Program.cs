using ClientService.EF;
using ClientService.Services;
using ClientService.Utils;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Utils;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ClinService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EncryptionUtils>();
builder.Services.AddScoped<HalykService>();
builder.Services.AddHttpClient<HalykService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://127.0.0.1:5500") // ����� ����� ���� ��������-URL
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // ���� ����������� cookies
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    Console.WriteLine(builder.Configuration.GetConnectionString("DefaultConnection"));
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated(); // <== ������ �� �� �������, ��� ��������
}

app.Run();

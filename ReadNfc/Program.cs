using PCSC;
using ReadNfc.Service;

var builder = WebApplication.CreateBuilder(args);

// DI
builder.Services.AddControllers();
builder.Services.AddSingleton<IContextFactory>(ContextFactory.Instance);

// 1) نسجّل الخدمة كـ Singleton علشان الـ Controllers يقدّروا يحقنوها
builder.Services.AddSingleton<NFCBackgroundService>();

// 2) ونسجلها كـ HostedService باستخدام نفس الـ Singleton
builder.Services.AddHostedService(sp => sp.GetRequiredService<NFCBackgroundService>());

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
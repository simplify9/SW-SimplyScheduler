using Microsoft.EntityFrameworkCore;
using SW.Scheduler; // added for AddScheduler extension

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core InMemory
builder.Services.AddDbContext<SampleApplication.Data.AppDbContext>(opt =>
    opt.UseInMemoryDatabase("AppDb"));

// Scheduler (scans current assembly for jobs)
builder.Services.AddScheduler();

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
using Infrastructure.Dispatching;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApplicationDispatch();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;

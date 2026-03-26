using DeviceDataManager.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDeviceDataManagerInfrastructure();

var host = builder.Build();
host.Run();

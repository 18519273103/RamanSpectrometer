using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RamanSpectrometer.Core.Configurations;
using RamanSpectrometer.Core.Control;
using RamanSpectrometer.Core.Factory;
using RamanSpectrometer.Core.Interfaces;
using RamanSpectrometer.Core.Models;
using RamanSpectrometer.Hardware.Virtual;
using RamanSpectrometer.Services.Analysis;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("application.json", optional: false, reloadOnChange: true)
    .Build();

var controlConfig = configuration
    .GetRequiredSection(nameof(SpectrometerConfig))
    .Get<SpectrometerConfig>()
    ?? throw new InvalidOperationException($"Missing {nameof(SpectrometerConfig)} configuration.");

var hyperparameterConfig = configuration
    .GetRequiredSection(nameof(HyperparameterConfig))
    .Get<HyperparameterConfig>()
    ?? throw new InvalidOperationException($"Missing {nameof(HyperparameterConfig)} configuration.");

//DI容器
var services = new ServiceCollection();

services.AddSingleton(controlConfig);
services.AddSingleton(hyperparameterConfig);
services.AddHardwareDrivers(configuration, typeof(VirtualLaser).Assembly);
services.AddSingleton<QuantitativeAnalysisService>(serviceProvider =>
    new QuantitativeAnalysisService(serviceProvider.GetRequiredService<HyperparameterConfig>()));

using var provider = services.BuildServiceProvider();

var bench = provider.GetRequiredService<LaserSpectrometerControl>();
var laser = provider.GetRequiredService<ILaserController>();
var spectrometer = provider.GetRequiredService<ISpectrometerController>();
var analyzer = provider.GetRequiredService<QuantitativeAnalysisService>();

Console.WriteLine("拉曼光谱定量分析系统 - 控制台模拟");
Console.WriteLine("====================================");
Console.WriteLine($"配置参数: K={analyzer.K:F6}, B={analyzer.B:F6}");
Console.WriteLine();

Console.WriteLine("------初始化虚拟硬件-------");
Console.WriteLine($"激光器初始化: {laser.Initialize("COM3")}");
Console.WriteLine($"光谱仪初始化: {spectrometer.Initialize("SPEC-001")}");
spectrometer.SetTime(100);
Console.WriteLine($"激光器状态: {laser.GetStatus()}");
Console.WriteLine($"光谱仪状态: {spectrometer.GetStatus()}");
Console.WriteLine();

Console.WriteLine("------关闭激光器，采集暗光谱-------");
laser.TurnOff();
var dark = spectrometer.AcquireSpectrum();
Console.WriteLine($"激光器状态: {laser.GetStatus()}");
Console.WriteLine($"暗光谱点数: {dark.Wavelengths.Length}");
Console.WriteLine();

Console.WriteLine("-------打开激光器，采集并计算样品浓度-------");
// RunSample("test_20mg.csv", 20.0, dark);
// RunSample("test_80mg.csv", 80.0, dark);
laser.TurnOn();
var raw = spectrometer.AcquireSpectrum();
var concentration = spectrometer.Getconcentration();
RunSample(raw, concentration, dark);

Console.WriteLine();
Console.WriteLine("[4] 关闭激光器，模拟流程结束");
laser.TurnOff();
Console.WriteLine($"激光器状态: {laser.GetStatus()}");

void RunSample(SpectrumData raw, double expectedConcentration, SpectrumData dark)
{
    var peakArea = analyzer.CalPeakArea(raw, dark);
    var concentration = analyzer.CalculateConcentration(raw, dark);
    var error = Math.Abs(concentration - expectedConcentration);
    var relativeError = expectedConcentration == 0 ? 0 : error / expectedConcentration * 100;

    Console.WriteLine($"峰面积: {peakArea:F2}");
    Console.WriteLine($"计算浓度: {concentration:F2} mg/L");
    Console.WriteLine($"真实浓度: {expectedConcentration:F2} mg/L");
    Console.WriteLine($"绝对误差: {error:F2} mg/L，相对误差: {relativeError:F2}%");

    Console.WriteLine();
}

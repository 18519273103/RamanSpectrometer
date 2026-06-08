using Microsoft.Extensions.Configuration;
using RamanSpectrometer.Core.Configurations;
using RamanSpectrometer.Core.Control;
using RamanSpectrometer.Core.Interfaces;
using RamanSpectrometer.Core.Models;
using RamanSpectrometer.Hardware.Virtual;
using RamanSpectrometer.Services.Analysis;
using Xunit;
using Xunit.Abstractions;

namespace RamanSpectrometer.Tests;

public class SystemIntegrationTest : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleDirectory;

    public SystemIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        _sampleDirectory = Path.Combine(Path.GetTempPath(), "RamanSpectrometer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sampleDirectory);
    }

    [Fact]
    public void RunFullPipelineTest()
    {
        WriteLine("=====================================================");
        WriteLine("拉曼光谱定量分析系统 - 全链路集成测试启动");
        WriteLine("=====================================================");
        WriteLine();

        WriteLine("[阶段 1: 初始化系统环境]");

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

        var bench = new LaserSpectrometerControl(controlConfig);
        ILaserController laser = new VirtualLaser(bench);
        ISpectrometerController spectrometer = new VirtualSpectrometer(bench);
        var analyzer = new QuantitativeAnalysisService(hyperparameterConfig);

        Assert.True(laser.Initialize("COM3"));
        Assert.True(spectrometer.Initialize("SPEC-001"));
        spectrometer.SetTime(100);

        WriteLine("虚拟测试舱、激光器、光谱仪、算法模块加载完毕。");
        WriteLine($"  -> 配置参数: K = {analyzer.K:F6}, B = {analyzer.B:F6}");
        WriteLine();

        WriteLine("[阶段 2: 采集系统暗场背景]");
        laser.TurnOff();

        var dark = spectrometer.AcquireSpectrum();
        WriteLine("暗光谱采集成功。");
        WriteLine($"  -> 获取到 {dark.Wavelengths.Length} 个数据点。");
        WriteLine();

        WriteLine("[阶段 3: 仪器标定 (50 mg/L 标准液)]");
        bench.FilePath = controlConfig.FilePath;
        laser.TurnOn();
        var standard = spectrometer.AcquireSpectrum();
        laser.TurnOff();

        const double concentration = 50.0;
        var calibArea = analyzer.StandardSample(standard, dark, concentration);

        WriteLine("标定样品处理完成。");
        WriteLine($"  -> 50 mg/L 样品测得峰面积为: {calibArea:F2}");
        WriteLine($"  -> 使用配置参数: K = {analyzer.K:F6}, B = {analyzer.B:F6}");
        WriteLine();

        WriteLine("[阶段 4: 未知样品测试与算法验证]");
        var resultA = TestUnknownSample(bench, laser, spectrometer, analyzer, dark, "E:\\project\\Solution\\RamanSpectrometer.Solution\\data\\test_20mg.csv");
        ValidateResult("测试样 A", resultA, 20.0);

        var resultB = TestUnknownSample(bench, laser, spectrometer, analyzer, dark, "E:\\project\\Solution\\RamanSpectrometer.Solution\\data\\test_80mg.csv");
        ValidateResult("测试样 B", resultB, 80.0);

        WriteLine();
        WriteLine("=====================================================");
        WriteLine("全链路集成测试结束。");
        WriteLine("=====================================================");
    }

    public void Dispose()
    {
        if (Directory.Exists(_sampleDirectory))
        {
            Directory.Delete(_sampleDirectory, recursive: true);
        }
    }

    private double TestUnknownSample(
        LaserSpectrometerControl bench,
        ILaserController laser,
        ISpectrometerController spectrometer,
        QuantitativeAnalysisService analyzer,
        SpectrumData darkSpectrum,
        string testFileName)
    {
        bench.FilePath = testFileName;

        laser.TurnOn();
        var rawSpectrum = spectrometer.AcquireSpectrum();
        laser.TurnOff();

        return analyzer.CalculateConcentration(rawSpectrum, darkSpectrum);
    }

    private void ValidateResult(string sampleName, double calculatedValue, double expectedValue)
    {
        var tolerance = expectedValue * 0.5;
        var error = Math.Abs(calculatedValue - expectedValue);

        WriteLine($"  > {sampleName} - 预测浓度: {calculatedValue:F2} mg/L (理论: {expectedValue})，误差: {error:F2}，允许: {tolerance:F2}");
        Assert.InRange(calculatedValue, expectedValue - tolerance, expectedValue + tolerance);
    }

    private void WriteLine(string message = "") => _output.WriteLine(message);
}

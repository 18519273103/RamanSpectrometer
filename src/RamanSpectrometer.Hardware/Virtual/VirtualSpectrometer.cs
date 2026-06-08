using RamanSpectrometer.Core.Control;
using RamanSpectrometer.Core.Enums;
using RamanSpectrometer.Core.Interfaces;
using RamanSpectrometer.Core.Models;

namespace RamanSpectrometer.Hardware.Virtual;

/// <summary>
/// 接入虚拟光谱仪
/// </summary>
public class VirtualSpectrometer : ISpectrometerController
{
    //等待时间
    private int _waitTime = 100;
    private readonly LaserSpectrometerControl _bench;

    public VirtualSpectrometer(LaserSpectrometerControl bench)
    {
        _bench = bench;
    }

    public bool Initialize(string deviceId) => true;

    public void SetTime(int waitTime)
    {
        if (waitTime <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waitTime), "Integration time must be greater than zero.");
        }

        _waitTime = waitTime;
    }

    public SpectrumData AcquireSpectrum()
    {
        //模拟等待时间
        Thread.Sleep(_waitTime);

        //控制逻辑：如果打开了激光器则读取相应原始光谱文件，否则生成暗光谱
        return _bench.IsLaserOn
            ? LoadFile(_bench.FilePath)
            : GenerateDarkNoiseSpectrum();
    }

    public double Getconcentration()
    {
        return _bench.Concentration;
    }

    public DeviceStatus GetStatus() => DeviceStatus.Idle;

    /// <summary>
    /// 生成暗光谱
    /// </summary>
    /// <returns>data</returns>
    private static SpectrumData GenerateDarkNoiseSpectrum()
    {
        const int dataLength = 1024;
        var data = new SpectrumData(dataLength);
        var random = new Random();

        for (var i = 0; i < dataLength; i++)
        {
            data.Wavelengths[i] = 500 + i * (1000.0 / (dataLength - 1));
            data.Intensities[i] = random.NextDouble() * 5.0;
        }

        return data;
    }

    private static SpectrumData LoadFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"找不到文件: {filePath}", filePath);
        }

        var lines = File.ReadAllLines(filePath);
        var data = new SpectrumData(lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length >= 2 && double.TryParse(parts[0], out var x) && double.TryParse(parts[1], out var y))
            {
                data.Wavelengths[i] = x;
                data.Intensities[i] = y;
            }
        }

        return data;
    }
}

namespace RamanSpectrometer.Core.Control;

using RamanSpectrometer.Core.Configurations;



/// <summary>
/// 激光器和光谱仪状态联动
/// 激光器
/// </summary>
public class LaserSpectrometerControl
{
    //控制激光器开闭
    public bool IsLaserOn { get; set; }
    public string FilePath { get; set; }

    public double Concentration {get;set;}

    public LaserSpectrometerControl(SpectrometerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        FilePath = config.FilePath;
        Concentration = config.Concentration;
    }
}

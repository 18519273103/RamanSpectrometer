namespace RamanSpectrometer.Core.Configurations;

/// <summary>
/// 光谱仪状态联动控制
///     -FilePath 对接采样文件
///     -concentration 预期浓度
/// </summary>
public class SpectrometerConfig
{
    public string FilePath { get; set; } = string.Empty;
    public double Concentration { get; set; }
}

namespace RamanSpectrometer.Core.Models;

/// <summary>
/// 光谱数据
/// </summary>
public class SpectrumData
{
    //曼德拉位移
    public double[] Wavelengths { get; set; }
    //信号强度
    public double[] Intensities { get; set; }

    public SpectrumData(int length)
    {
        Wavelengths = new double[length];
        Intensities = new double[length];
    }
}

namespace RamanSpectrometer.Core.Interfaces;
using RamanSpectrometer.Core.Enums;
using RamanSpectrometer.Core.Models;


/// <summary>
/// 光谱仪同步控制接口
/// </summary>
public interface ISpectrometerController
{
    bool Initialize(string deviceId);
    void SetTime(int seconds);
    SpectrumData AcquireSpectrum();
    DeviceStatus GetStatus();

    //预期浓度
    double Getconcentration();
}

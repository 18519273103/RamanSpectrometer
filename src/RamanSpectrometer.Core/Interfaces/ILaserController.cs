using RamanSpectrometer.Core.Enums;

namespace RamanSpectrometer.Core.Interfaces;

/// <summary>
/// 激光器接口
/// </summary>
public interface ILaserController
{
    bool Initialize(string deviceId);

    void TurnOn();
    void TurnOff();
    DeviceStatus GetStatus();
}

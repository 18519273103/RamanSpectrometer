using RamanSpectrometer.Core.Control;
using RamanSpectrometer.Core.Enums;
using RamanSpectrometer.Core.Interfaces;

namespace RamanSpectrometer.Hardware.Virtual;

/// <summary>
/// 接入虚拟激光器
/// </summary>
public class VirtualLaser : ILaserController
{
    private readonly LaserSpectrometerControl _bench;
    private DeviceStatus _status = DeviceStatus.Disconnected;
    private String _deviceId = "0";

    public VirtualLaser(LaserSpectrometerControl bench)
    {
        _bench = bench;
    }

    public bool Initialize(string deviceId)
    {
        _deviceId  = deviceId;
        _status = DeviceStatus.Idle;
        return true;
    }

    public void TurnOn()
    {
        _status = DeviceStatus.Active;
        _bench.IsLaserOn = true;
    }

    public void TurnOff()
    {
        _status = DeviceStatus.Idle;
        _bench.IsLaserOn = false;
    }

    public DeviceStatus GetStatus() => _status;
}

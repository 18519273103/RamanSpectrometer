namespace RamanSpectrometer.Core.Configurations;

public class HyperparameterConfig
{
    public double K { get; set; }
    public double B { get; set; }
    public double PeakPos { get; set; } = 1000.0;
    public double PeakHalfWid { get; set; } = 30.0;
    public double Baseline { get; set; } = 15.0;
    public int N { get; set; } = 2;
    public bool EnablePolynomial { get; set; } = true;
}

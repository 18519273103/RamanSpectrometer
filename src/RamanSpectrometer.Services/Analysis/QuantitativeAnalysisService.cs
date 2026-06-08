using MathNet.Numerics;
using RamanSpectrometer.Core.Configurations;
using RamanSpectrometer.Core.Models;

namespace RamanSpectrometer.Services.Analysis;

/// <summary>
/// 核心算法-计算浓度
/// </summary>
public class QuantitativeAnalysisService
{
    public double K { get; set; } = 0.01;
    public double B { get; set; } = 0.5;
    public double PeakPos { get; set; } = 1000.0;
    public double PeakHalfWid { get; set; } = 30.0;
    public double Baseline { get; set; } = 15.0;
    public int N { get; set; } = 2;
    public bool EnablePolynomial { get; set; } = true;

    public QuantitativeAnalysisService()
    {
    }

    public QuantitativeAnalysisService(HyperparameterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        K = config.K;
        B = config.B;
        PeakPos = config.PeakPos;
        PeakHalfWid = config.PeakHalfWid;
        Baseline = config.Baseline;
        N = config.N;
        EnablePolynomial = config.EnablePolynomial;
    }

    /// <summary>
    /// 主控制函数：计算浓度
    /// </summary>
    /// <param name="raw">原始光谱</param>
    /// <param name="dark">暗光谱</param>
    /// <returns>浓度</returns>
    public double CalculateConcentration(SpectrumData raw, SpectrumData dark)
    {
        var peakArea = CalPeakArea(raw, dark);
        var concentration = K * peakArea + B;

        return Math.Max(0, concentration);
    }

    /// <summary>
    /// 执行预处理并提取目标峰面积。
    /// </summary>
    /// <param name="raw">原始光谱</param>
    /// <param name="dark">暗光谱</param>
    /// <returns>预处理后的目标峰面积</returns>
    public double CalPeakArea(SpectrumData raw, SpectrumData dark)
    {
        var cleanSpectrum = SubtractDark(raw, dark);
        var baseSpectrum = EnablePolynomial
            ? ApplyPolynomial(cleanSpectrum)
            : cleanSpectrum;

        return ExtractPeak(baseSpectrum);
    }

    /// <summary>
    /// 计算标准样品预处理后的峰面积，K 和 B 从配置读取，不在这里覆盖。
    /// </summary>
    /// <param name="standard">标准样品光谱</param>
    /// <param name="dark">暗光谱</param>
    /// <param name="concentration">标准样品已知浓度</param>
    /// <returns>标准样品预处理后的峰面积</returns>
    public double StandardSample(SpectrumData standard, SpectrumData dark, double concentration)
    {
        if (concentration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(concentration), "标准样品浓度必须大于 0。");
        }

        var peakArea = CalPeakArea(standard, dark);
        if (peakArea <= 0)
        {
            throw new InvalidOperationException("标定峰面积必须大于 0。");
        }

        return peakArea;
    }

    public static SpectrumData SubtractDark(SpectrumData raw, SpectrumData dark)
    {
        ValidatePair(raw, dark);

        var length = raw.Wavelengths.Length;
        var result = new SpectrumData(length);

        for (var i = 0; i < length; i++)
        {
            result.Wavelengths[i] = raw.Wavelengths[i];
            result.Intensities[i] = Math.Max(0, raw.Intensities[i] - dark.Intensities[i]);
        }

        return result;
    }

    /// <summary>
    /// 使用多项式拟合全局基线，并从光谱中扣除该基线。
    /// </summary>
    /// <param name="spectrum">待校正光谱</param>
    /// <returns>基线校正后的光谱</returns>
    public SpectrumData ApplyPolynomial(SpectrumData spectrum)
    {
        ValidateSpectrum(spectrum);

        var fittingIndexes = GetBaselineFittingIndexes(spectrum.Wavelengths);
        var minX = spectrum.Wavelengths.Min();
        var maxX = spectrum.Wavelengths.Max();
        var coeff = FitPolynomial(spectrum.Wavelengths, spectrum.Intensities, fittingIndexes, N, minX, maxX);
        var result = new SpectrumData(spectrum.Wavelengths.Length);

        //多项式拟合
        for (var i = 0; i < spectrum.Wavelengths.Length; i++)
        {
            var normalizedX = NormalizeX(spectrum.Wavelengths[i], minX, maxX);
            var baseline = EvaluatePolynomial(coeff, normalizedX);
            result.Wavelengths[i] = spectrum.Wavelengths[i];
            result.Intensities[i] = Math.Max(0, spectrum.Intensities[i] - baseline);
        }

        return result;
    }

    /// <summary>
    /// 计算峰值处面积。
    /// </summary>
    /// <param name="spectrum">光谱数据</param>
    /// <returns>峰值处面积</returns>
    public double ExtractPeak(SpectrumData spectrum)
    {
        ValidateSpectrum(spectrum);

        //找半宽索引
        var startX = PeakPos - PeakHalfWid;
        var endX = PeakPos + PeakHalfWid;
        var startIndex = FindClosestIndex(spectrum.Wavelengths, startX);
        var endIndex = FindClosestIndex(spectrum.Wavelengths, endX);

        if (startIndex >= endIndex)
        {
            return 0;
        }
        //确定峰值范围
        var lStartIndex = Math.Max(0, startIndex - (int)Math.Ceiling(Baseline));
        var rEndIndex = Math.Min(spectrum.Intensities.Length - 1, endIndex + (int)Math.Ceiling(Baseline));
        var leftY = FindLocalMinimum(spectrum.Intensities, lStartIndex, startIndex);
        var rightY = FindLocalMinimum(spectrum.Intensities, endIndex, rEndIndex);
        var baseline = spectrum.Wavelengths[endIndex] - spectrum.Wavelengths[startIndex];

        if (baseline == 0)
        {
            return 0;
        }

        var baselineSlope = (rightY - leftY) / baseline;
        var totalArea = 0.0;

        //积分求面积
        for (var i = startIndex; i < endIndex; i++)
        {
            var x1 = spectrum.Wavelengths[i];
            var x2 = spectrum.Wavelengths[i + 1];
            var dx = x2 - x1;

            var y1Baseline = leftY + baselineSlope * (x1 - spectrum.Wavelengths[startIndex]);
            var y1Pure = Math.Max(0, spectrum.Intensities[i] - y1Baseline);

            var y2Baseline = leftY + baselineSlope * (x2 - spectrum.Wavelengths[startIndex]);
            var y2Pure = Math.Max(0, spectrum.Intensities[i + 1] - y2Baseline);

            totalArea += (y1Pure + y2Pure) * dx / 2.0;
        }

        return totalArea;
    }


    /// <summary>
    /// 获取需要拟合的坐标（去除峰值坐标）
    /// </summary>
    /// <param name="wavelengths">全部坐标</param>
    /// <returns>x轴坐标</returns>
    private List<int> GetBaselineFittingIndexes(double[] wavelengths)
    {
        var excludedStart = PeakPos - PeakHalfWid - Baseline;
        var excludedEnd = PeakPos + PeakHalfWid + Baseline;
        var indexes = new List<int>();

        for (var i = 0; i < wavelengths.Length; i++)
        {
            if (wavelengths[i] < excludedStart || wavelengths[i] > excludedEnd)
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    /// <summary>
    /// 多项式拟合系数
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="indexes"></param>
    /// <param name="n"></param>
    /// <param name="minX"></param>
    /// <param name="maxX"></param>
    /// <returns></returns>
    private static double[] FitPolynomial(double[] x, double[] y, IReadOnlyList<int> indexes, int n, double minX, double maxX)
    {
        var fittingX = new double[indexes.Count];
        var fittingY = new double[indexes.Count];

        for (var i = 0; i < indexes.Count; i++)
        {
            var sourceIndex = indexes[i];
            fittingX[i] = NormalizeX(x[sourceIndex], minX, maxX);
            fittingY[i] = y[sourceIndex];
        }

        return Fit.Polynomial(fittingX, fittingY, n);
    }

    private static double NormalizeX(double x, double minX, double maxX)
    {
        if (maxX == minX)
        {
            return 0;
        }

        return 2 * (x - minX) / (maxX - minX) - 1;
    }

    private static double EvaluatePolynomial(double[] coeff, double x)
    {
        var result = 0.0;
        for (var i = coeff.Length - 1; i >= 0; i--)
        {
            result = result * x + coeff[i];
        }

        return result;
    }

    private static void ValidatePair(SpectrumData raw, SpectrumData dark)
    {
        ValidateSpectrum(raw);
        ValidateSpectrum(dark);

        if (raw.Wavelengths.Length != dark.Wavelengths.Length || raw.Intensities.Length != dark.Intensities.Length)
        {
            throw new ArgumentException("原始光谱与暗光谱的数据长度不一致！");
        }
    }

    private static void ValidateSpectrum(SpectrumData spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        if (spectrum.Wavelengths.Length == 0 || spectrum.Intensities.Length == 0)
        {
            throw new ArgumentException("光谱数组不能为空。", nameof(spectrum));
        }

        if (spectrum.Wavelengths.Length != spectrum.Intensities.Length)
        {
            throw new ArgumentException("光谱的波长数组与强度数组长度不一致！", nameof(spectrum));
        }
    }

    private static int FindClosestIndex(double[] array, double target)
    {
        if (array.Length == 0)
        {
            throw new ArgumentException("数组不能为空。", nameof(array));
        }

        var index = Array.BinarySearch(array, target);
        if (index >= 0)
        {
            return index;
        }

        index = ~index;
        if (index == 0)
        {
            return 0;
        }

        if (index == array.Length)
        {
            return array.Length - 1;
        }

        return target - array[index - 1] < array[index] - target ? index - 1 : index;
    }

    private static double FindLocalMinimum(double[] intensities, int start, int end)
    {
        if (intensities.Length == 0)
        {
            throw new ArgumentException("数组不能为空。", nameof(intensities));
        }

        if (start > end)
        {
            throw new ArgumentException("局部搜索区间不合法。", nameof(start));
        }

        var min = double.MaxValue;
        for (var i = start; i <= end; i++)
        {
            if (intensities[i] < min)
            {
                min = intensities[i];
            }
        }

        return min;
    }
}


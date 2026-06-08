using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RamanSpectrometer.Core.Configurations;
using RamanSpectrometer.Core.Control;
using RamanSpectrometer.Core.Interfaces;

namespace RamanSpectrometer.Core.Factory;

/// <summary>
/// 激光器与光谱仪工厂类
/// </summary>
public static class HardwareServiceExtensions
{
    public static IServiceCollection AddHardwareDrivers(
        this IServiceCollection services,
        IConfiguration config,
        params Assembly[] driverAssemblies)
    {
        services.AddSingleton<LaserSpectrometerControl>(serviceProvider =>
        {
            var controlConfig = serviceProvider.GetRequiredService<SpectrometerConfig>();
            return new LaserSpectrometerControl(controlConfig);
        });

        //加载外部DLL插件
        string pluginFolder = config["HardwareConfig:PluginPath"] ?? "Drivers";
        var pluginAssemblies = LoadExternalPlugins(pluginFolder);

        var assemblies = driverAssemblies
            .Concat(AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic))
            .Concat(pluginAssemblies)
            .Distinct()
            .ToArray();

        //激光器
        var laserTypeName = config["HardwareConfig:LaserType"];
        if (string.IsNullOrWhiteSpace(laserTypeName))
        {
            throw new InvalidOperationException("Missing HardwareConfig:LaserType configuration.");
        }

        var laserType = GetDriverType<ILaserController>(laserTypeName, assemblies);
        services.AddSingleton<ILaserController>(serviceProvider =>
            (ILaserController)ActivatorUtilities.CreateInstance(serviceProvider, laserType));

        //光谱仪
        var spectrometerTypeName = config["HardwareConfig:SpectrometerType"];
        if (string.IsNullOrWhiteSpace(spectrometerTypeName))
        {
            throw new InvalidOperationException("Missing HardwareConfig:SpectrometerType configuration.");
        }

        var spectrometerType = GetDriverType<ISpectrometerController>(spectrometerTypeName, assemblies);
        services.AddSingleton<ISpectrometerController>(serviceProvider =>
            (ISpectrometerController)ActivatorUtilities.CreateInstance(serviceProvider, spectrometerType));

        return services;
    }

    private static Type GetDriverType<TDriver>(string typeName, IEnumerable<Assembly> assemblies)
    {
        var driverType = assemblies
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(assembly => {
                try {
                    return assembly.GetTypes();
                } catch (ReflectionTypeLoadException ex) {
                    return ex.Types.Where(type => type is not null)!;
                }
            })
            .FirstOrDefault(type => typeof(TDriver).IsAssignableFrom(type)
                                    && !type.IsInterface
                                    && !type.IsAbstract
                                    && (type.Name == typeName || type.FullName == typeName));

        if (driverType is null)
        {
            throw new InvalidOperationException($"装配失败: 未找到驱动类 '{typeName}'。");
        }

        return driverType;
    }

    /// <summary>
    /// 从指定目录物理加载 DLL 到内存中
    /// </summary>
    private static List<Assembly> LoadExternalPlugins(string folderName)
    {
        var loadedAssemblies = new List<Assembly>();
        
        var path = "E:\\project\\Solution\\RamanSpectrometer.Solution\\src\\RamanSpectrometer.App\\bin\\Debug\\drivers";
        if (!Directory.Exists(path))
        {
            return loadedAssemblies;
        }

        var dllFiles = Directory.GetFiles(path, "*.dll");
        foreach (var dllPath in dllFiles)
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                loadedAssemblies.Add(assembly);
                Console.WriteLine($"[插件系统] 成功加载外部硬件驱动: {Path.GetFileName(dllPath)}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[插件系统] 跳过文件 {Path.GetFileName(dllPath)}: {ex.Message}");
                Console.ResetColor();
            }
        }

        return loadedAssemblies;
    }
}

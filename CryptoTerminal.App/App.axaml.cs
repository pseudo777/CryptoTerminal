using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CryptoTerminal.App.Views;
using CryptoTerminal.Core.Interfaces;
using CryptoTerminal.Core.Services;
using CryptoTerminal.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CryptoTerminal.App;

public partial class App : Application
{
    // 全局服务容器
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. 配置依赖注入
        var serviceCollection = new ServiceCollection();

        // 注册服务：当有人要 IExchangeService 时，给它 MockExchangeService
        //serviceCollection.AddSingleton<IExchangeService, MockExchangeService>();
        serviceCollection.AddSingleton<IExchangeService, BinanceService>();

        // 注册 ViewModel
        serviceCollection.AddTransient<MainViewModel>();

        // 构建容器
        Services = serviceCollection.BuildServiceProvider();

        // 2. 启动主窗口
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 从容器中获取 MainViewModel 并赋值给 DataContext
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
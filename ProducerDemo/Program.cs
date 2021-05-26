using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ProducerDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddOptions<SerialPortSetting>()
                        .Bind(hostContext.Configuration.GetSection("SerialPortSetting"))
                        //.Configure(options =>
                        //{
                        //    options.PortName = "COM3";
                        //    options.BaudRate = 460800;
                        //    options.Parity = System.IO.Ports.Parity.None;
                        //    options.DataBits = 8;
                        //    options.StopBits = System.IO.Ports.StopBits.One;
                        //})
                        ;
                    //services.AddHostedService<Worker>();
                    //services.AddHostedService<Worker2>();
                    services.AddHostedService<Worker3>();
                });
    }
}

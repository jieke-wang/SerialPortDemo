using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Infrastructure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProducerDemo
{
    public class Worker4 : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker4> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private readonly Modbus _modbus;

        public Worker4(ILogger<Worker4> logger, IOptions<SerialPortSetting> options)
        {
            _logger = logger;
            _options = options;
            _modbus = new Modbus();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _modbus.Open(_options.Value.PortName, _options.Value.BaudRate, _options.Value.DataBits, _options.Value.Parity, _options.Value.StopBits);
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Factory.StartNew(async () =>
            {
                string msg = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "demo.json"));

                while (stoppingToken.IsCancellationRequested == false)
                {
                }
            }, stoppingToken).Unwrap();
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _modbus.Close();
            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _modbus.Dispose();
            base.Dispose();
        }
    }
}

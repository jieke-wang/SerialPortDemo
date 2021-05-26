using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProducerDemo
{
    public class Worker : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private SerialPort _serialPort;
        private long _sendCounter = 0;

        public Worker(ILogger<Worker> logger, IOptions<SerialPortSetting> options)
        {
            _logger = logger;
            _options = options;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _serialPort = new SerialPort(_options.Value.PortName, _options.Value.BaudRate, _options.Value.Parity, _options.Value.DataBits, _options.Value.StopBits);

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Factory.StartNew(async () =>
            {
                using MemoryStream ms = new MemoryStream();
                using (FileStream fs = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "demo.json"), FileMode.Open, FileAccess.Read, FileShare.Delete))
                {
                    await fs.CopyToAsync(ms);
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_serialPort.IsOpen == false)
                        {
                            _serialPort.Open();
                            _serialPort.DiscardOutBuffer();
                        }

                        _serialPort.DiscardOutBuffer();
                        //ms.Seek(0, SeekOrigin.Begin);
                        //await ms.CopyToAsync(_serialPort.BaseStream, stoppingToken);
                        //await _serialPort.BaseStream.FlushAsync(stoppingToken);

                        byte[] buffer = ms.ToArray();
                        _serialPort.Write(buffer, 0, buffer.Length);

                        _logger.LogInformation($"发送次数: {++_sendCounter}");
                        await Task.Delay(1000, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, ex.Message);
                        await Task.Delay(5000, stoppingToken);
                    }
                }
            }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).Unwrap();
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _serialPort?.Close();
            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _serialPort?.Dispose();
            base.Dispose();
        }
    }
}

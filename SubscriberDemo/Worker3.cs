using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SubscriberDemo
{
    public class Worker3 : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker3> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private SerialPort _serialPort;
        private long _reciveCounter = 0;
        private long _errorCounter = 0;

        public Worker3(ILogger<Worker3> logger, IOptions<SerialPortSetting> options)
        {
            _logger = logger;
            _options = options;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _serialPort = new SerialPort(_options.Value.PortName, _options.Value.BaudRate, _options.Value.Parity, _options.Value.DataBits, _options.Value.StopBits);
            _serialPort.Encoding = Encoding.UTF8;
            _serialPort.NewLine = "\n";
            _serialPort.DataReceived += SerialPort_DataReceivedAsync;

            return base.StartAsync(cancellationToken);
        }

        private void SerialPort_DataReceivedAsync(object sender, SerialDataReceivedEventArgs e)
        {
            _logger.LogInformation($"\n\n接收次数: {++_reciveCounter}; 错误次数: {_errorCounter}");
            DateTime startTime = DateTime.Now;

            try
            {
                string msg;
                msg = _serialPort.ReadLine().Trim('\0');
                //_logger.LogInformation($"\n\n{msg}\n\n");
                _logger.LogInformation($"接收字节大小: {msg.Length}");

                //_serialPort.DiscardOutBuffer();
                using JsonDocument jsonDocument = JsonDocument.Parse(msg);
                _serialPort.WriteLine("1");
            }
            catch (Exception ex)
            {
                _errorCounter++;
                _logger.LogError(ex, ex.Message);
                _serialPort.WriteLine("-1");
            }
            _logger.LogInformation($"耗时: {DateTime.Now - startTime}\n\n");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Factory.StartNew(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_serialPort.IsOpen == false)
                        {
                            _logger.LogInformation("打开串口");
                            _serialPort.Open();
                            _serialPort.DiscardInBuffer();
                        }
                        await Task.Delay(5000, stoppingToken);
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
            if (_serialPort != null)
                _serialPort.DataReceived -= SerialPort_DataReceivedAsync;
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

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

namespace SubscriberDemo
{
    public class Worker : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private SerialPort _serialPort;
        private long _reciveCounter = 0;

        public Worker(ILogger<Worker> logger, IOptions<SerialPortSetting> options)
        {
            _logger = logger;
            _options = options;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _serialPort = new SerialPort(_options.Value.PortName, _options.Value.BaudRate, _options.Value.Parity, _options.Value.DataBits, _options.Value.StopBits);
            _serialPort.DataReceived += SerialPort_DataReceivedAsync;
            //_serialPort.DiscardInBuffer();

            return base.StartAsync(cancellationToken);
        }

        private void SerialPort_DataReceivedAsync(object sender, SerialDataReceivedEventArgs e)
        {
            _logger.LogInformation($"接收次数: {++_reciveCounter}");
            try
            {
                //using MemoryStream ms = new MemoryStream();
                //lock(_serialPort.BaseStream)
                //{
                //    _serialPort.BaseStream.CopyTo(ms);
                //}
                //_logger.LogInformation($"\n\n{Encoding.UTF8.GetString(ms.ToArray())}\n\n");

                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead > 0)
                {
                    byte[] buffer = new byte[bytesToRead];
                    _serialPort.Read(buffer, 0, buffer.Length);
                    _logger.LogInformation($"\n\n{Encoding.UTF8.GetString(buffer)}\n\n");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
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
                            _serialPort.Open();
                            _serialPort.DiscardInBuffer();
                        }
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

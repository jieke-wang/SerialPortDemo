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
    public class Worker4 : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker4> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private SerialPort _serialPort;
        private long _reciveCounter = 0;
        private long _errorCounter = 0;
        private const string FrameHeader = "\0\0";
        private const string FrameTail = "\n\n";

        public Worker4(ILogger<Worker4> logger, IOptions<SerialPortSetting> options)
        {
            _logger = logger;
            _options = options;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _serialPort = new SerialPort(_options.Value.PortName, _options.Value.BaudRate, _options.Value.Parity, _options.Value.DataBits, _options.Value.StopBits);
            _serialPort.Encoding = Encoding.UTF8;
            //_serialPort.NewLine = "\n";
            _serialPort.DataReceived += SerialPort_DataReceivedAsync;

            return base.StartAsync(cancellationToken);
        }

        private void SerialPort_DataReceivedAsync(object sender, SerialDataReceivedEventArgs e)
        {
            _logger.LogInformation($"\n\n接收次数: {++_reciveCounter}; 错误次数: {_errorCounter}");
            DateTime startTime = DateTime.Now;

            string msg = string.Empty;
            try
            {
                msg = _serialPort.ReadTo(FrameTail).Trim('\0');
                //msg = _serialPort.ReadTo(FrameTail);
                msg = msg.Replace(FrameHeader, FrameTail);
                int frameHeaderPosition = msg.IndexOf(FrameTail);
                if (frameHeaderPosition > -1)
                    msg = msg.Substring(frameHeaderPosition + FrameTail.Length);
                _logger.LogInformation($"接收字节大小: {Encoding.UTF8.GetByteCount(msg)}");

                using JsonDocument jsonDocument = JsonDocument.Parse(msg);
                _serialPort.Write($"{FrameHeader}1{FrameTail}");
            }
            catch (Exception ex)
            {
                _errorCounter++;
                _logger.LogError(ex, ex.Message);
                _serialPort.Write($"{FrameHeader}-1{FrameTail}");
                if (string.IsNullOrWhiteSpace(msg) == false)
                    _logger.LogError(msg);
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

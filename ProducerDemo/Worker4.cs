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
    public class Worker4 : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker4> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private SerialPort _serialPort;
        private long _sendCounter = 0;
        private long _errorCounter = 0;
        private long _retryCounter = 0;
        private long _timeoutCounter = 0;
        private const string FrameHeader = "\0\0";
        private const string FrameTail = "\n\n";
        private TaskCompletionSource<string> _taskCompletionSource;

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
            string msg = _serialPort.ReadTo(FrameTail).Trim('\0');
            //string msg = _serialPort.ReadTo(FrameTail);
            msg = msg.Replace(FrameHeader, FrameTail);
            int frameHeaderPosition = msg.IndexOf(FrameTail);
            if (frameHeaderPosition > -1)
                msg = msg.Substring(frameHeaderPosition + FrameTail.Length);

            _taskCompletionSource?.TrySetResult(msg);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //await Task.Delay(2000);
            await Task.Factory.StartNew(async () =>
            {
                //string msg = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "baseinfo-min.json"));
                string msg = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "demo.json"));
                //string msg = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "demo2.json"));

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_serialPort.IsOpen == false)
                        {
                            _logger.LogInformation("打开串口");
                            _serialPort.Open();
                            _serialPort.DiscardOutBuffer();
                        }

                        _logger.LogInformation($"\n\n发送次数: {++_sendCounter}; 错误次数: {_errorCounter}; 重试次数: {_retryCounter}; 超时次数: {_timeoutCounter}");
                        DateTime startTime = DateTime.Now;

                        //byte[] data = Encoding.UTF8.GetBytes($"{FrameHeader}{msg}{FrameHeader}{msg}{FrameTail}");
                        byte[] data = Encoding.UTF8.GetBytes($"{FrameHeader}{msg}{FrameTail}");
                        _logger.LogInformation($"发送字节大小: {data.Length - FrameHeader.Length - FrameTail.Length}");

                        int retryCounter = 0;
                        while (true)
                        {
                            if (retryCounter > 0)
                                _logger.LogWarning($"重试次数: {retryCounter}");
                            _serialPort.Write(data, 0, data.Length);

                            using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3));
                            //cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(3));
                            _taskCompletionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                            string result = string.Empty;
                            using (cancellationTokenSource.Token.Register(() => _taskCompletionSource.SetCanceled()))
                            {
                                try
                                {
                                    result = await _taskCompletionSource.Task.ConfigureAwait(false);
                                }
                                catch (TaskCanceledException ex)
                                {
                                    _logger.LogError(ex, ex.Message);
                                    result = "-1";
                                    _timeoutCounter++;
                                }
                            }

                            if (result == "1") break;
                            retryCounter++;
                            await Task.Delay(1000, stoppingToken);
                            _logger.LogWarning($"重试: {retryCounter}轮");
                            _retryCounter++;
                        }

                        _logger.LogInformation($"耗时: {DateTime.Now - startTime}");
                        //await Task.Delay(100, stoppingToken);
                        //await Task.Delay(10, stoppingToken);
                        //await Task.Delay(0, stoppingToken);
                        //await Task.Delay(1000, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _errorCounter++;
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

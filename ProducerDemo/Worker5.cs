using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
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
    public class Worker5 : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker5> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private SerialPort _serialPort;
        private long _sendCounter = 0;
        private long _errorCounter = 0;
        private long _retryCounter = 0;
        private long _timeoutCounter = 0;

        private const string FrameHeader = "`";
        private const string FrameTail = "\r\n";
        private const string SlaveAddress = "01";
        private const string FunctionCode = "03";
        private const string StartAddress = "0000";

        private TaskCompletionSource<string> _taskCompletionSource;

        public Worker5(ILogger<Worker5> logger, IOptions<SerialPortSetting> options)
        {
            _logger = logger;
            _options = options;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _serialPort = new SerialPort(_options.Value.PortName, _options.Value.BaudRate, _options.Value.Parity, _options.Value.DataBits, _options.Value.StopBits);
            _serialPort.Encoding = Encoding.UTF8;
            _serialPort.DataReceived += SerialPort_DataReceivedAsync;

            return base.StartAsync(cancellationToken);
        }

        private void SerialPort_DataReceivedAsync(object sender, SerialDataReceivedEventArgs e)
        {
            #region 接收响应并解析
            string utf8Frame = _serialPort.ReadTo(FrameTail);
            int frameHeaderPosition = utf8Frame.IndexOf(FrameHeader);
            if (frameHeaderPosition == -1) return;
            utf8Frame = utf8Frame.Substring(frameHeaderPosition);

            string strCrc = utf8Frame.Substring(utf8Frame.Length - 2);
            byte crc = byte.Parse(strCrc, System.Globalization.NumberStyles.HexNumber);
            utf8Frame = utf8Frame.Substring(0, utf8Frame.Length - 2);
            byte[] data = Encoding.UTF8.GetBytes(utf8Frame);
            if (Checksum.GetChecksum(data) != crc)
                return;

            string slaveAddress = utf8Frame.Substring(1, 2);
            string functionCode = utf8Frame.Substring(3, 2);
            string startAddress = utf8Frame.Substring(5, 4);
            if (slaveAddress != SlaveAddress || functionCode != FunctionCode || startAddress != StartAddress)
                return;

            string frameLen = utf8Frame.Substring(9, 4);
            byte[] utf8Data = Encoding.UTF8.GetBytes(utf8Frame);
            string msg = Encoding.UTF8.GetString(utf8Data, 13, Convert.ToInt32(frameLen));
            #endregion

            // 设置结果
            _taskCompletionSource?.TrySetResult(msg);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //await Task.Delay(2000);
            await Task.Factory.StartNew(async () =>
            {
                //string msg = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "baseinfo-min.json"));
                //string msg = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "demo.json"));
                string msg = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "demo2.json"));

                while (!stoppingToken.IsCancellationRequested)
                {
                    DateTime startTime = DateTime.Now;
                    bool hasError = false;
                    try
                    {
                        if (_serialPort.IsOpen == false)
                        {
                            _logger.LogInformation("打开串口");
                            _serialPort.Open();
                            _serialPort.DiscardOutBuffer();
                        }

                        _logger.LogInformation($"发送次数: {++_sendCounter}; 错误次数: {_errorCounter}; 重试次数: {_retryCounter}; 超时次数: {_timeoutCounter}");

                        #region 组装数据帧
                        byte[] msgData = Encoding.UTF8.GetBytes(msg);

                        // 数据帧长度: 帧头 - 1; 从站地址 - 2; 功能码 - 2; 起始地址 - 4; 数据帧长度 - 4; 数据 - x; CRC校验 - 2; 帧尾 - 2
                        List<byte> frame = new List<byte>(17 + msgData.Length);
                        frame.AddRange(Encoding.UTF8.GetBytes(FrameHeader)); // 帧头
                        frame.AddRange(Encoding.UTF8.GetBytes(SlaveAddress)); // 从站地址
                        frame.AddRange(Encoding.UTF8.GetBytes(FunctionCode)); // 功能码
                        frame.AddRange(Encoding.UTF8.GetBytes(StartAddress)); // 起始地址
                        frame.AddRange(Encoding.UTF8.GetBytes(msgData.Length.ToString("0000"))); // 数据帧长度
                        frame.AddRange(msgData); // 数据
                        byte crc = Checksum.GetChecksum(frame.ToArray());
                        frame.AddRange(Encoding.UTF8.GetBytes(crc.ToString("X2"))); // CRC校验
                        frame.AddRange(Encoding.UTF8.GetBytes(FrameTail)); // 帧尾
                        #endregion

                        byte[] data = frame.ToArray();
                        _logger.LogInformation($"发送payload字节大小: {msgData.Length}");

                        double secondsTimeout = Math.Ceiling(data.Length * 8 / (double)_options.Value.BaudRate) + 2;

                        // 重试
                        int retryCounter = 0;
                        while (true)
                        {
                            if (retryCounter > 0)
                                _logger.LogWarning($"重试次数: {retryCounter}");
                            _serialPort.Write(data, 0, data.Length);

                            using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(secondsTimeout));
                            //cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3));
                            //cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(3));
                            _taskCompletionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                            string result = string.Empty;
                            // 超时取消
                            using (cancellationTokenSource.Token.Register(() => _taskCompletionSource.SetCanceled()))
                            {
                                try
                                {
                                    // 等待结果
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
                    }
                    catch (Exception ex)
                    {
                        _errorCounter++;
                        _logger.LogError(ex, ex.Message);
                        hasError = true;
                    }
                    finally
                    {
                        _logger.LogInformation($"耗时: {DateTime.Now - startTime}\n\n");
                    }

                    if (hasError)
                    {
                        await Task.Delay(5000, stoppingToken);
                    }
                    else
                    {
                        //await Task.Delay(100, stoppingToken);
                        //await Task.Delay(10, stoppingToken);
                        //await Task.Delay(0, stoppingToken);
                        //await Task.Delay(1000, stoppingToken);
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

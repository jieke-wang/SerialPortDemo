using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Infrastructure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SubscriberDemo
{
    public class Worker5 : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker5> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private SerialPort _serialPort;
        private long _reciveCounter = 0;
        private long _errorCounter = 0;

        private const string FrameHeader = "`";
        private const string FrameTail = "\r\n";

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
            _logger.LogInformation($"接收次数: {++_reciveCounter}; 错误次数: {_errorCounter}");
            DateTime startTime = DateTime.Now;

            string utf8Frame = string.Empty;
            try
            {
                #region 接收并解析数据帧
                utf8Frame = _serialPort.ReadTo(FrameTail);
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

                string frameLen = utf8Frame.Substring(9, 4);
                byte[] utf8Data = Encoding.UTF8.GetBytes(utf8Frame);
                _logger.LogInformation($"接收payload字节大小: {utf8Data.Length - 13}");
                string msg = Encoding.UTF8.GetString(utf8Data, 13, Convert.ToInt32(frameLen));
                utf8Frame = msg;
                #endregion

                #region 业务
                using JsonDocument jsonDocument = JsonDocument.Parse(msg); 
                #endregion

                #region 构建数据帧并响应
                byte[] msgData = Encoding.UTF8.GetBytes("1");

                // 数据帧长度: 帧头 - 1; 从站地址 - 2; 功能码 - 2; 起始地址 - 4; 数据帧长度 - 4; 数据 - x; CRC校验 - 2; 帧尾 - 2
                List<byte> frame = new List<byte>(17 + msgData.Length);
                frame.AddRange(Encoding.UTF8.GetBytes(FrameHeader)); // 帧头
                frame.AddRange(Encoding.UTF8.GetBytes(slaveAddress)); // 从站地址
                frame.AddRange(Encoding.UTF8.GetBytes(functionCode)); // 功能码
                frame.AddRange(Encoding.UTF8.GetBytes(startAddress)); // 起始地址
                frame.AddRange(Encoding.UTF8.GetBytes(msgData.Length.ToString("0000"))); // 数据帧长度
                frame.AddRange(msgData); // 数据
                crc = Checksum.GetChecksum(frame.ToArray());
                frame.AddRange(Encoding.UTF8.GetBytes(crc.ToString("X2"))); // CRC校验
                frame.AddRange(Encoding.UTF8.GetBytes(FrameTail)); // 帧尾

                data = frame.ToArray();
                _serialPort.Write(data, 0, data.Length);
                #endregion
            }
            catch (Exception ex)
            {
                _errorCounter++;
                _logger.LogError(ex, ex.Message);
                //_serialPort.Write($"{FrameHeader}-1{FrameTail}");
                if (string.IsNullOrWhiteSpace(utf8Frame) == false)
                    _logger.LogError(utf8Frame);
            }
            finally
            {
                _logger.LogInformation($"耗时: {DateTime.Now - startTime}\n\n");
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

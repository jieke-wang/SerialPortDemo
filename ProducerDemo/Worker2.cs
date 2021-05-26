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
    public class Worker2 : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker2> _logger;
        private readonly IOptions<SerialPortSetting> _options;
        private SerialPort _serialPort;
        private long _sendCounter = 0;
        private long _errorCounter = 0;
        private byte lineCharByte = Encoding.UTF8.GetBytes("\n")[0];
        private byte emptyCharByte = Encoding.UTF8.GetBytes("\0")[0];

        public Worker2(ILogger<Worker2> logger, IOptions<SerialPortSetting> options)
        {
            _logger = logger;
            _options = options;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _serialPort = new SerialPort(_options.Value.PortName, _options.Value.BaudRate, _options.Value.Parity, _options.Value.DataBits, _options.Value.StopBits);
            _serialPort.Encoding = Encoding.UTF8;
            _serialPort.NewLine = "\n";
            _serialPort.WriteBufferSize = 1024;
            //_serialPort.WriteTimeout = 1;
            //_serialPort.ReadTimeout = 1;

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
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
                            _serialPort.RtsEnable = true;
                        }

                        _logger.LogInformation($"\n\n发送次数: {++_sendCounter}; 错误次数: {_errorCounter}");
                        DateTime startTime = DateTime.Now;

                        _logger.LogInformation($"发送字节大小: {msg.Length}");
                        lock (_serialPort)
                        {
                            //_serialPort.DiscardOutBuffer();
                            //_serialPort.RtsEnable = true;
                            //_serialPort.WriteLine(msg);
                            //_serialPort.BaseStream.Flush();
                            //_serialPort.RtsEnable = false;

                            byte[] data = Encoding.UTF8.GetBytes(msg);
                            using (MemoryStream ms = new MemoryStream())
                            {
                                ms.Write(data, 0, data.Length);
                                ms.WriteByte(lineCharByte);
                                int paddingSize = _serialPort.WriteBufferSize - ((data.Length + 1) % _serialPort.WriteBufferSize);
                                if(paddingSize > 0)
                                {
                                    for (int i = 0; i < paddingSize; i++)
                                    {
                                        ms.WriteByte(emptyCharByte);
                                    }
                                }
                                data = ms.ToArray();
                                _serialPort.Write(data, 0, data.Length);
                            }
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

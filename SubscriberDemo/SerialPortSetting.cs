using System.IO.Ports;

using Microsoft.Extensions.Options;

namespace SubscriberDemo
{
    public class SerialPortSetting : IOptions<SerialPortSetting>
    {
        public SerialPortSetting Value => this;

        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public Parity Parity { get; set; }
        public int DataBits { get; set; }
        public StopBits StopBits { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

using Infrastructure;

namespace Demo
{
    class Program
    {
        //const string frameHeader = ":"; // 0x3A
        const string frameHeader = "`"; // 0x60
        const string frameTail = "\r\n"; // 0x0D, 0x0A
        const string slaveAddress = "01";
        const string functionCode = "03";
        const string startAddress = "0000";
        //const string msg = "Hello World";
        const string msg = "Hello World; 你好 世界";

        static void Main(string[] args)
        {
            //Console.WriteLine("Hello World!");
            Demo1();
        }

        static void Demo1()
        {
            List<byte> frame = BuildFrame();
            ResolveFrame(frame);
        }

        static List<byte> BuildFrame()
        {
            //byte[] msgData = Encoding.ASCII.GetBytes(msg);
            //byte[] utf8Data = Encoding.UTF8.GetBytes(msg);
            //byte[] msgData = Encoding.Convert(Encoding.UTF8, Encoding.ASCII, utf8Data);
            byte[] msgData = Encoding.UTF8.GetBytes(msg);

            // 数据帧长度: 帧头 - 1; 从站地址 - 2; 功能码 - 2; 起始地址 - 4; 数据帧长度 - 4; 数据 - x; CRC校验 - 2; 帧尾 - 2
            List<byte> frame = new List<byte>(17 + msgData.Length);
            frame.AddRange(Encoding.UTF8.GetBytes(frameHeader)); // 帧头
            frame.AddRange(Encoding.UTF8.GetBytes(slaveAddress)); // 从站地址
            frame.AddRange(Encoding.UTF8.GetBytes(functionCode)); // 功能码
            frame.AddRange(Encoding.UTF8.GetBytes(startAddress)); // 起始地址
            frame.AddRange(Encoding.UTF8.GetBytes(msgData.Length.ToString("0000"))); // 数据帧长度
            frame.AddRange(msgData); // 数据
            byte crc = Checksum.GetChecksum(frame.ToArray());
            frame.AddRange(Encoding.UTF8.GetBytes(crc.ToString("X2"))); // CRC校验
            frame.AddRange(Encoding.UTF8.GetBytes(frameTail)); // 帧尾

            return frame;
        }

        static void ResolveFrame(List<byte> frame)
        {
            string utf8Frame = Encoding.UTF8.GetString(frame.ToArray());
            int frameHeaderPosition = utf8Frame.IndexOf(frameHeader);
            if(frameHeaderPosition == -1) return;
            utf8Frame = utf8Frame.Substring(frameHeaderPosition);

            int frameTailPosition = utf8Frame.IndexOf(frameTail);
            if (frameTailPosition > -1)
            {
                utf8Frame = utf8Frame.Substring(0, frameTailPosition);
            }

            string strCrc = utf8Frame.Substring(utf8Frame.Length - 2);
            byte crc = byte.Parse(strCrc, System.Globalization.NumberStyles.HexNumber);
            utf8Frame = utf8Frame.Substring(0, utf8Frame.Length - 2);
            byte[] data = Encoding.UTF8.GetBytes(utf8Frame);
            if (Checksum.GetChecksum(data) != crc)
                return;

            string _slaveAddress = utf8Frame.Substring(1, 2);
            string _functionCode = utf8Frame.Substring(3, 2);
            string _startAddress = utf8Frame.Substring(5, 4);
            string _frameLen = utf8Frame.Substring(9, 4);
            byte[] utf8Data = Encoding.UTF8.GetBytes(utf8Frame);
            string _msg = Encoding.UTF8.GetString(utf8Data, 13, Convert.ToInt32(_frameLen));
            //byte[] asciiData = Encoding.ASCII.GetBytes(_msg);
            //byte[] utf8Data = Encoding.Convert(Encoding.ASCII, Encoding.UTF8, asciiData);
            //_msg = Encoding.UTF8.GetString(utf8Data);

            Console.WriteLine($"{_slaveAddress} {_functionCode} {_startAddress} {_frameLen} {_msg}");
        }

        static T[] Reverse<T>(T[] array)
        {
            if (BitConverter.IsLittleEndian == false)
                Array.Reverse(array);
            return array;
        }
    }
}

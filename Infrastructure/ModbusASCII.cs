using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
    public class ModbusASCII
    {
        public static string GenerateFrame(byte[] bytes)
        {
            string requestFrame = ":";
            int LRC = 0;

            foreach (byte b in bytes)
            {
                LRC -= b;
                requestFrame += string.Format("{0:00}", b);
            }

            string LRC8 = string.Format("{0:00}", Convert.ToString((byte)LRC, 16).ToUpper());
            return requestFrame + LRC8 + "\r\n";
        }

        public static string RequestF03ModbusASCII(int slaveID, int firstAddr, int countReg)
        {
            byte[] bytesFirstAddr = BitConverter.GetBytes(firstAddr);
            byte[] bytesCountReg = BitConverter.GetBytes(countReg);
            byte[] options = new byte[]
            {
                (byte)slaveID,
                3,
                bytesFirstAddr[1],
                bytesFirstAddr[0],
                bytesCountReg[1],
                bytesCountReg[0]
            };

            return GenerateFrame(options);
        }

        static void PrintModbusFrame(string noFormatText)
        {
            string result = "";
            char[] temp = noFormatText.ToCharArray();
            for (int i = 0; i < temp.Length; i++)
            {
                result += temp[i];
                if (i % 2 != 0) result += " ";
            }
            Console.WriteLine(result);
        }
    }
}

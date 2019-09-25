using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace hvTools
{
    namespace Modbus
    {
        internal static class Utilities
        {
            internal static int Convert2BytesToInt16(byte[] bytes)
            {
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);

                int i = BitConverter.ToInt16(bytes, 0);

                return i;
            }

            internal static byte[] ConvertInt32_To4Bytes(int intValue)
            {
                byte[] intBytes = BitConverter.GetBytes(intValue);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(intBytes);
                byte[] result = intBytes;
                return result;
            }

            internal static int GetInt16From4Bits(BitArray mw)
            {
                int s = 0;

                for (int i = 0; i < 16; i++)
                {
                    if (mw[i])
                        s += (int)Math.Pow(2, i);
                }

                return s;
            }

            internal static int Convert4BytesToInt32(byte[] bytes)
            {
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);

                int i = BitConverter.ToInt32(bytes, 0);

                return i;
            }
        }
        
        internal static class Build
        {

            internal static byte[] ReadCommand(string address)
            {
                switch (address[1])
                {
                    case 'B':
                        return Modbus.Coil.Read(address);
                    case 'W':
                        return Modbus.Word.Read(address);
                    case 'L':
                        return Modbus.Long.Read(address);
                    default:
                        return null;
                }
            }

            internal static byte[] WriteCommand(string address, int decValue)
            {
                switch (address[1])
                {
                    case 'B':
                        return Modbus.Coil.Write(address, decValue);
                    case 'W':
                        return Modbus.Word.Write(address, decValue);
                    case 'L':
                        return Modbus.Long.Write(address, decValue);
                    default:
                        return null;
                }
            }
        }

        internal static class Parse
        {

            internal static int Response(char regType, byte[] inStream)
            {
                switch (regType)
                {
                    case 'B':
                        return Modbus.Coil.ParseResponse(inStream);
                    case 'W':
                        return Modbus.Word.ParseResponse(inStream);
                    case 'L':
                        return Modbus.Long.ParseResponse(inStream);
                    default:
                        return -1;
                }
            }
        }

        internal static class Coil
        {
            static int True = 65280;
            static int False = 0;

            public static byte[] Read(string address)
            {
                //parse register adress into two bytes
                byte[] registerAddress = ParseAddress(address);

                //parse writing value into two bytes

                byte[] outStream = new byte[]
            {
                00, //Request ID: byte no.1
                01, //Request ID: byte no.2
                00, //Modbus/TCP protocol
                00, //Modbus/TCP protocol
                00, //
                06, //Remaining bytes in this message
                01, //Slave ID

                01, //Modbus function: Read Coils

                registerAddress[2], //Register address: byte no.1
                registerAddress[3], //Register address: byte no.2
                00, //
                01, //Read only 1 coil!
            };

                //send command
                return outStream;
            }

            public static byte[] Write(string address, int boolValue)
            {
                //parse register adress into two bytes
                byte[] registerAddress = ParseAddress(address);

                //parse writing value into two bytes
                int decValue;
                if (boolValue == 1)
                    decValue = Coil.True;
                else
                    decValue = Coil.False;

                byte[] valueToWrite = Utilities.ConvertInt32_To4Bytes(decValue);

                byte[] outStream = new byte[]
            {
                00, //Request ID: byte no.1
                01, //Request ID: byte no.2
                00, //Modbus/TCP protocol
                00, //Modbus/TCP protocol
                00, //
                06, //Remaining bytes in this message
                01, //Slave ID

                05, //Modbus function: Write a single Coil

                registerAddress[2], //Register address: byte no.1
                registerAddress[3], //Register address: byte no.2
                valueToWrite[2], //Value: byte no.1
                valueToWrite[3], //Value: byte no.2
            };

                //send command
                return outStream;
            }

            private static byte[] ParseAddress(string strAddress)
            {
                if (!strAddress.StartsWith("MB") || strAddress.Length != 8) throw new Exception("Not a valid BIT address");

                string strDecWord = strAddress.Substring(2, 5); //Word Decimal Address
                int decWord = Convert.ToInt32(strDecWord);

                string hexaBit = strAddress.Substring(7); //Bit Hexa-Address

                int decBit = Convert.ToInt32(hexaBit, 16);
                int decWholeAddress = decWord * 16 + decBit;
                return Utilities.ConvertInt32_To4Bytes(decWholeAddress);
            }

            internal static int ParseResponse(byte[] inStream)
            {
                byte readValue = inStream[9];

                if (readValue == 1)
                    return 1;
                else
                    return 0;
            }
        }

        internal static class Word
        {
            public static byte[] Write(string address, int decValue)
            {
                //parse register adress into two bytes
                byte[] registerAddress = ParseAddress(address);

                //parse writing value into two bytes
                byte[] valueToWrite = Utilities.ConvertInt32_To4Bytes(decValue);

                byte[] outStream = new byte[]
            {
                00, //Request ID: byte no.1
                01, //Request ID: byte no.2
                00, //Modbus/TCP protocol
                00, //Modbus/TCP protocol
                00, //
                06, //Remaining bytes in this message
                01, //Slave ID

                06, //Modbus function: Write 16bit(=2 bytes) to register

                registerAddress[2], //Register address: byte no.1
                registerAddress[3], //Register address: byte no.2
                valueToWrite[2], //Register address: byte no.1
                valueToWrite[3], //Register address: byte no.2
            };

                //send command
                return outStream;
            }

            public static byte[] Read(string address)
            {
                //parse register adress into two bytes
                byte[] registerAddress = ParseAddress(address);

                byte[] outStream = new byte[]
            {
                00, //Request ID: byte no.1
                01, //Request ID: byte no.2
                00, //Modbus/TCP protocol
                00, //Modbus/TCP protocol
                00, //
                06, //Remaining bytes in this message
                01, //Slave ID

                03, //Modbus function: Read from 16bit (= 1 word) register 

                registerAddress[2], //Register address: byte no.1
                registerAddress[3], //Register address: byte no.2
                00, //
                01, //Read only 1 register
            };

                //send command
                return outStream;
            }

            private static byte[] ParseAddress(string strAddress)
            {
                if (!strAddress.StartsWith("MW") || strAddress.Length != 7) throw new Exception("Not a valid WORD address");

                int decAddress = Convert.ToInt16(strAddress.Substring(2));
                byte[] bytesAddress = Utilities.ConvertInt32_To4Bytes(decAddress);
                return bytesAddress;
            }

            internal static int ParseResponse(byte[] inStream)
            {
                byte[] registerValue = new byte[] { inStream[9], inStream[10] };
                return Utilities.Convert2BytesToInt16(registerValue);
            }
        }

        internal static class Long
        {
            public static byte[] Write(string address, int decValue)
            {
                //parse register adress into two bytes
                byte[] registerAddress = ParseAddress(address);

                //parse writing value into two bytes
                byte[] valueToWrite = Utilities.ConvertInt32_To4Bytes(decValue);

                byte[] outStream = new byte[]
            {
                00, //Request ID: byte no.1
                01, //Request ID: byte no.2
                00, //Modbus/TCP protocol
                00, //Modbus/TCP protocol
                00, //
                11, //Remaining bytes in this message
                01, //Slave ID

                16, //Modbus function: Write multiple registers

                registerAddress[2], //Register address: byte no.1
                registerAddress[3], //Register address: byte no.2
                00, //Quantity of registers Hi
                02, //Quantity of registers Lo
                04, //Number of bytes
                valueToWrite[2], //Lo Reg, Hi Byte
                valueToWrite[3], //Lo Reg, Lo Byte
                valueToWrite[0], //Hi Reg, Hi Byte
                valueToWrite[1], //Hi Reg, Lo Byte
            };

                //send command
                return outStream;
            }

            public static byte[] Read(string address)
            {
                //parse register adress into two bytes
                byte[] registerAddress = ParseAddress(address);

                byte[] outStream = new byte[]
            {
                00, //Request ID: byte no.1
                01, //Request ID: byte no.2
                00, //Modbus/TCP protocol
                00, //Modbus/TCP protocol
                00, //
                06, //Remaining bytes in this message
                01, //Slave ID

                03, //Modbus function: Read from 16bit (= 1 word) register 

                registerAddress[2], //Register address: byte no.1
                registerAddress[3], //Register address: byte no.2
                00, //
                02, //Read 2 registers [16bit each] = 1 long
            };

                //send command
                return outStream;
            }

            private static byte[] ParseAddress(string strAddress)
            {
                if (!strAddress.StartsWith("ML") || strAddress.Length != 7) throw new Exception("Not a valid LONG address");

                int decAddress = Convert.ToInt16(strAddress.Substring(2));
                byte[] bytesAddress = Utilities.ConvertInt32_To4Bytes(decAddress);
                return bytesAddress;
            }

            internal static int ParseResponse(byte[] inStream)
            {
                //TODO:
                byte[] registerValue = new byte[] {inStream[11], inStream[12], inStream[9], inStream[10]  };
                return Utilities.Convert4BytesToInt32(registerValue);
            }
        }
    }
}

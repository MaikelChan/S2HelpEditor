using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace S2HelpEditor
{
    public static class Table
    {
        static List<int> HexValues = new List<int>();
        static List<char> CharValues = new List<char>();

        static bool TableLoaded = false;

        public static bool LoadTable(string TableFile)
        {
            string[] Lines = File.ReadAllLines(TableFile, Encoding.Default);

            for (int n = 0; n < Lines.Length; n++)
            {
                if (Lines[n].Length > 0 && !Lines[n].StartsWith("#")) //We discard empty lines and comments (they start with #)
                {
                    string[] Values = Lines[n].Split('=');

                    HexValues.Add(Convert.ToInt32(Values[0], 16));
                    if (Values.Length == 2)
                        CharValues.Add(Convert.ToChar(Values[1]));
                    else if (Values.Length == 3)
                        CharValues.Add('=');
                    else
                    {
                        return false;
                    }
                }
            }

            TableLoaded = true;
            return true;
        }

        public static string BinToString(byte[] Data, string TableFile)
        {
            if (!TableLoaded)
            {
                return Encoding.Default.GetString(Data);
            }
            
            string Text = "";

            for (int n = 0; n < Data.Length; n++)
            {
                if (Data[n] == 0xA5 && n < Data.Length - 1)
                {
                    int Value = (Data[n] << 8) | Data[n + 1];
                    int Index = HexValues.FindIndex(num => num == Value);

                    if (Index == -1)
                        Text += "<A5>";
                    else
                    {
                        Text += CharValues[Index];
                        n++;
                    }
                }
                else if (Data[n] == 0x26) //&, new line
                {
                    Text += Environment.NewLine;
                }
                else if (Data[n] > 0x7f || Data[n] < 0x20)
                {
                    Text += "<" + String.Format("{0:X2}", Data[n]) + ">";
                }
                else
                {
                    Text += Encoding.Default.GetString(Data, n, 1);
                }
            }

            return Text;
        }

        public static byte[] StringToBin(string Data, string TableFile)
        {
            if (!TableLoaded)
            {
                return Encoding.Default.GetBytes(Data);
            }
            
            string TempString = "";

            Data = Data.Replace(Environment.NewLine, "&");

            for (int n = 0; n < Data.Length; n++)
            {
                char CurrentChar = Convert.ToChar(Data.Substring(n, 1));
                int Index = CharValues.FindIndex(str => str == CurrentChar);
                byte[] Value = new byte[1];

                if (Index == -1)
                {
                    TempString += CurrentChar.ToString();
                }
                else
                {
                    TempString += "<" + Convert.ToString(HexValues[Index] >> 8, 16).ToUpper() + ">";
                    TempString += "<" + Convert.ToString(HexValues[Index] & 0xFF, 16).ToUpper() + ">";
                }
            }

            for (int n = 0; n < 256; n++)
            {
                if (n < 0x20 || n > 0x7f)
                {
                    byte[] Value = new byte[1];
                    Value[0] = Convert.ToByte(n);
                    TempString = TempString.Replace("<" + String.Format("{0:X2}", n) + ">", Encoding.Default.GetString(Value, 0, 1));
                }
            }

            byte[] Text = Encoding.Default.GetBytes(TempString);
            
            return Text;
        }
    }
}

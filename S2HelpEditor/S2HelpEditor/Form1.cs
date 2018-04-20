using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace S2HelpEditor
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        string TableFile;

        private void Form1_Load(object sender, EventArgs e)
        {
            TableFile = Path.Combine(Application.StartupPath, "table.txt");

            if (File.Exists(TableFile))
            {
                if (!Table.LoadTable(TableFile))
                {
                    MessageBox.Show("There was an error while loading the table file.");
                }
            }
            else
                MessageBox.Show("Unable to find \"table.txt\" file.");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ODlg = new OpenFileDialog();
            ODlg.Filter = "BIN Files (*.BIN)|*.bin|All files (*.*)|*.*";
            if (ODlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            string OutputFile = Path.Combine(Path.GetDirectoryName(ODlg.FileName), Path.GetFileNameWithoutExtension(ODlg.FileName)) + ".txt";
            Extract(ODlg.FileName, OutputFile);
        }
        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog ODlg = new OpenFileDialog();
            ODlg.Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*";
            if (ODlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            string OutputFile = Path.Combine(Path.GetDirectoryName(ODlg.FileName), Path.GetFileNameWithoutExtension(ODlg.FileName)) + ".BIN2";
            Insert(ODlg.FileName, OutputFile);
        }

        bool Extract(string InputFile, string OutputFile)
        {
            if (!File.Exists(InputFile)) return false;

            string Texts = "";

            using (FileStream fs = new FileStream(InputFile, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    fs.Seek(0xC, SeekOrigin.Begin);

                    UInt32 PointerTablePosition = br.ReadUInt32();
                    UInt32 NumberOfTexts = Convert.ToUInt32((fs.Length - PointerTablePosition) >> 2);
                    UInt32[] Pointers = new UInt32[NumberOfTexts];

                    Texts = "####### TOTALTEXTS:00000000 TOTALPOINTERS:" + String.Format("{0:X8}", NumberOfTexts) + " #######" + Environment.NewLine + Environment.NewLine;

                    //Read pointers                    
                    for (int n = 0; n < NumberOfTexts; n++)
                    {
                        fs.Seek(fs.Length - ((n * 4) + 4), SeekOrigin.Begin);
                        Pointers[n] = br.ReadUInt32();
                    }

                    //Read texts
                    UInt32 CurrentPosition = 0x10;
                    UInt32 CurrentText = 0;

                    while (CurrentPosition < PointerTablePosition)
                    {
                        //Figure out the length of the text
                        UInt32 TextLength = 0;
                        UInt32 TextPosition = CurrentPosition;

                        fs.Seek(CurrentPosition, SeekOrigin.Begin);
                        for (; ; )
                        {
                            if (br.ReadByte() == 0) break;
                            TextLength++;
                        }

                        //Read text
                        byte[] Data = new byte[TextLength];
                        fs.Seek(CurrentPosition, SeekOrigin.Begin);
                        fs.Read(Data, 0, Data.Length);

                        //Figure out how many pointers point to that text
                        List<int> NumberOfPointers = new List<int>();

                        for (int n = 0; n < Pointers.Length; n++)
                        {
                            if (Pointers[n] == TextPosition) NumberOfPointers.Add(n);
                        }

                        //Write the data to a string, so we can later write to a text file
                        Texts += "####### TEXT:" + String.Format("{0:X8}", CurrentText) + " POINTERS:" + String.Format("{0:X8}", NumberOfPointers.Count);
                        for (int i = 0; i < NumberOfPointers.Count; i++) Texts += " P" + String.Format("{0:X8}", i) + ":" + String.Format("{0:X8}", NumberOfPointers[i]);
                        Texts += " #######" + Environment.NewLine;

                        Texts += Table.BinToString(Data, TableFile) + Environment.NewLine + "####### END #######" + Environment.NewLine + Environment.NewLine;

                        //Skip zeroes till the next text
                        CurrentPosition += TextLength;
                        for (; ; )
                        {
                            if (br.ReadByte() != 0) break;
                            CurrentPosition++;
                        }

                        CurrentText++;
                    }

                    Texts = Texts.Replace("####### TOTALTEXTS:00000000", "####### TOTALTEXTS:" + String.Format("{0:X8}", CurrentText));
                }
            }

            File.WriteAllText(OutputFile, Texts, Encoding.Default);

            return true;
        }

        bool Insert(string InputFile, string OutputFile)
        {
            if (!File.Exists(InputFile)) return false;

            string[] TextLines = File.ReadAllLines(InputFile, Encoding.Default);
            //string[] Texts = new string[0];
            UInt32[] Pointers = new UInt32[0];
            int CurrentText = 0;
            bool ReadingText = false;
            string CurrentTextString = "";

            UInt32[] CurrentPointers = new UInt32[0];
            UInt32 CurrentBinPosition = 0x10;

            using (FileStream fs = new FileStream(OutputFile, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    //Parse the text file
                    foreach (string line in TextLines)
                    {
                        if (line.StartsWith("####### TOTALTEXTS:"))
                        {
                            //Read TOTALPOINTERS from the beginning of the text file
                            Pointers = new UInt32[Convert.ToUInt32(line.Substring(42, 8), 16)];
                        }
                        else if (line.StartsWith("####### TEXT:"))
                        {
                            ReadingText = true;

                            //Read pointers associated with this text
                            CurrentPointers = new UInt32[Convert.ToUInt32(line.Substring(31, 8), 16)];

                            for (int n = 0; n < CurrentPointers.Length; n++)
                            {
                                CurrentPointers[n] = Convert.ToUInt32(line.Substring(50 + (n * 19), 8), 16);
                            }
                        }
                        else if (line.Length == 0)
                        {
                            if (ReadingText == true) CurrentTextString += Environment.NewLine;
                        }
                        else if (line.StartsWith("####### END #######"))
                        {
                            //Convert the current text to a byte array
                            byte[] Data = Table.StringToBin(CurrentTextString.Substring(0, CurrentTextString.Length - Environment.NewLine.Length), TableFile);

                            //Write that array to the BIN file
                            fs.Seek(CurrentBinPosition, SeekOrigin.Begin);
                            fs.Write(Data, 0, Data.Length);

                            //Store the current text position in the pointer array.
                            for (int n = 0; n < CurrentPointers.Length; n++)
                            {
                                Pointers[CurrentPointers[n]] = CurrentBinPosition;
                            }

                            //Advance the writing position in the BIN file and add a 4 byte padding, so we can write the next text at the correct position
                            CurrentBinPosition += Convert.ToUInt32(Data.Length + 1);
                            CurrentBinPosition = Pad(CurrentBinPosition, 4);

                            ReadingText = false;
                            CurrentTextString = "";
                            CurrentText++;
                        }
                        else
                        {
                            CurrentTextString += line + Environment.NewLine;
                        }
                    }

                    UInt32 PointersPosition = Convert.ToUInt32(CurrentBinPosition + (Pointers.Length * 4) - 4);

                    //Write header
                    fs.Seek(0, SeekOrigin.Begin);
                    byte[] Header = { 0xe6, 0x2f, 0xf3, 0x6e, 0x01, 0xd0, 0xe3, 0x6f, 0x0b, 0x00, 0xf6, 0x6e };
                    fs.Write(Header, 0, Header.Length);
                    
                    //Write the pointer table start position
                    bw.Write(CurrentBinPosition);

                    //Write pointers
                    fs.Seek(PointersPosition, SeekOrigin.Begin);
                    for (int n = 0; n < Pointers.Length; n++)
                    {
                        bw.Write(Pointers[n]);
                        fs.Seek(-8, SeekOrigin.Current);
                    }
                }
            }

            return true;
        }

        uint Pad(uint value, uint padBytes)
        {
            if ((value % padBytes) != 0) return value + (padBytes - (value % padBytes));
            else return value;
        }

        bool Extract_Old(string InputFile, string OutputFile)
        {
            if (!File.Exists(InputFile)) return false;

            string[] Texts;

            using (FileStream fs = new FileStream(InputFile, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    fs.Seek(0xC, SeekOrigin.Begin);

                    UInt32 PointerTablePosition = br.ReadUInt32();
                    UInt32 NumberOfTexts = Convert.ToUInt32((fs.Length - PointerTablePosition) >> 2);
                    UInt32[] Pointers = new UInt32[NumberOfTexts];

                    Texts = new string[NumberOfTexts];

                    //Read pointers                    
                    for (int n = 0; n < NumberOfTexts; n++)
                    {
                        fs.Seek(fs.Length - ((n * 4) + 4), SeekOrigin.Begin);
                        Pointers[n] = br.ReadUInt32();
                    }

                    //Read texts
                    for (int n = 0; n < NumberOfTexts; n++)
                    {
                        //Figure out the length of the text
                        UInt32 TextLength = 0;

                        fs.Seek(Pointers[n], SeekOrigin.Begin);
                        for (; ; )
                        {
                            if (br.ReadByte() == 0) break;
                            TextLength++;
                        }

                        /*if (n < NumberOfTexts - 1)
                            TextLength = Pointers[n + 1] - Pointers[n];
                        else
                            TextLength = Convert.ToUInt32(PointerTablePosition - Pointers[n]);*/

                        //Read the text
                        fs.Seek(Pointers[n], SeekOrigin.Begin);
                        byte[] Data = new byte[TextLength];
                        fs.Seek(Pointers[n], SeekOrigin.Begin);
                        fs.Read(Data, 0, Data.Length);

                        Texts[n] = Table.BinToString(Data, TableFile);
                    }
                }
            }

            File.WriteAllLines(OutputFile, Texts, Encoding.Default);

            return true;
        }
    }
}

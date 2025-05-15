using System.IO;
using System.Linq;
using System.Text;
using System;

namespace UndetectedChromeDriver
{
    public class Patcher
    {
        public string DriverExecutablePath { get; private set; }

        public Patcher(string driverExecutablePath = null)
        {
            DriverExecutablePath = driverExecutablePath;
        }

        public void PatchAll()
        {
            if (!IsBinaryPatched())
            {
                PatchExecutable();
            }
        }

        private bool IsBinaryPatched()
        {
            if (DriverExecutablePath == null)
            {
                throw new Exception("driverExecutablePath is required.");
            }

            using (FileStream fs = new FileStream(DriverExecutablePath, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(fs, Encoding.GetEncoding("ISO-8859-1")))
                {
                    while (true)
                    {
                        string line = reader.ReadLine();

                        if (line == null)
                        {
                            break;
                        }

                        if (line.Contains("cdc_"))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
        }

        private int PatchExecutable()
        {
            int linect = 0;
            string replacement = GenerateRandomCDC();

            using (FileStream fs = new FileStream(DriverExecutablePath, FileMode.Open, FileAccess.ReadWrite))
            {
                byte[] buffer = new byte[1];
                StringBuilder check = new StringBuilder("....");
                int read = 0;

                while (true)
                {
                    read = fs.Read(buffer, 0, buffer.Length);

                    if (read == 0)
                    {
                        break;
                    }

                    check.Remove(0, 1);
                    check.Append((char)buffer[0]);

                    if (check.ToString() == "cdc_")
                    {
                        fs.Seek(-4, SeekOrigin.Current);
                        byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(replacement);
                        fs.Write(bytes, 0, bytes.Length);
                        linect++;
                    }
                }
            }

            return linect;
        }

        private string GenerateRandomCDC()
        {
            string chars = "abcdefghijklmnopqrstuvwxyz";
            Random random = new Random();
            char[] cdc = Enumerable.Repeat(chars, 26).Select(s => s[random.Next(s.Length)]).ToArray();

            for (var i = 4; i <= 6; i++)
            {
                cdc[cdc.Length - i] = char.ToUpper(cdc[cdc.Length - i]);
            }

            cdc[2] = cdc[0];
            cdc[3] = '_';

            return new string(cdc);
        }
    }
}
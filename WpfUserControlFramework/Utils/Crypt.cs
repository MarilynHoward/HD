using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualBasic;

namespace RestaurantPosWpf
{
    internal class Crypt
    {
        internal string DoEncrypt(string StringToEncrypt)
        {
            try
            {
                Byte j;
                string[] character = new string[StringToEncrypt.Length + 1];
                string sTmp;
                Int32 iTmp;

                StringToEncrypt = StringToEncrypt.Trim();

                if (StringToEncrypt.Length > 0)
                {
                    j = GetRndNumber();

                    for (Int32 i = 1; i <= StringToEncrypt.Length; i++)
                    {
                        iTmp = Strings.Asc(Strings.Mid(StringToEncrypt, i, 1)) + j;
                        sTmp = iTmp.ToString().Trim();
                        while (sTmp.Length != 3) sTmp = "0" + sTmp;

                        character[i] = sTmp;
                    }
                    sTmp = "";

                    for (Int32 i = 1; i <= StringToEncrypt.Length; i++)
                    {
                        sTmp = sTmp + Strings.Mid(character[i], 1, 1);
                    }
                    for (Int32 i = 1; i <= StringToEncrypt.Length; i++)
                    {
                        sTmp = sTmp + Strings.Mid(character[i], 2, 1);
                    }
                    for (Int32 i = 1; i <= StringToEncrypt.Length; i++)
                    {
                        sTmp = sTmp + Strings.Mid(character[i], 3, 1);
                    }
                    sTmp = Strings.Left(j.ToString(), 1) + sTmp + Strings.Right(j.ToString(), 1);

                    string sTmp2 = "";
                    for (Int32 i = 1; i <= Strings.Len(sTmp); i++)
                    {
                        sTmp2 += Strings.Chr(Strings.Asc(Strings.Mid(sTmp, i, 1)) + 17);
                    }
                    sTmp = sTmp2;
                    sTmp = Strings.Chr(GetRndConfusion()) + sTmp + Strings.Chr(GetRndConfusion());

                    StringToEncrypt = sTmp;
                }
                return StringToEncrypt;
            }
            catch
            {
                return StringToEncrypt;
            }
        }

        private bool IsEncrypted(string EncryptedString)
        {
            if (EncryptedString == DoDecrypt(EncryptedString)) return false;

            return true;
        }

        internal string DoDecrypt(string EncryptedString)
        {
            Byte j;
            string[] character = new string[EncryptedString.Length / 3 + 1];
            string sTmp = "";
            Int32 iTmp;
            string s;

            EncryptedString = EncryptedString.Trim();

            for (Int32 i = 1; i <= EncryptedString.Length; i++)
            {
                if (Strings.Asc(Strings.Mid(EncryptedString, i, 1)) > 90
                    || Strings.Asc(Strings.Mid(EncryptedString, i, 1)) < 65) return EncryptedString;
            }

            s = Strings.Mid(EncryptedString, 2, EncryptedString.Length - 2);

            if (EncryptedString.Length > 0)
            {
                for (Int32 i = 1; i <= s.Length; i++)
                {
                    sTmp = sTmp + Strings.Chr(Strings.Asc(Strings.Mid(s, i, 1)) - 17);
                }

                j = Convert.ToByte(Convert.ToInt64(Strings.Mid(sTmp, 1, 1)) * 10 + Convert.ToInt64(Strings.Mid(sTmp, sTmp.Length, 1)));

                sTmp = Strings.Mid(sTmp, 2, sTmp.Length - 2);

                for (Int32 i = 1; i <= s.Length / 3; i++)
                {
                    character[i] = Strings.Mid(sTmp, sTmp.Length, 1);
                    sTmp = Strings.Mid(sTmp, 1, sTmp.Length - 1);
                }

                for (Int32 i = 1; i <= s.Length / 3; i++)
                {
                    character[i] = Strings.Mid(sTmp, sTmp.Length, 1) + character[i];
                    sTmp = Strings.Mid(sTmp, 1, sTmp.Length - 1);
                }

                for (Int32 i = 1; i <= s.Length / 3; i++)
                {
                    character[i] = Strings.Mid(sTmp, sTmp.Length, 1) + character[i];
                    sTmp = Strings.Mid(sTmp, 1, sTmp.Length - 1);
                }

                sTmp = "";

                for (Int32 i = 1; i <= s.Length / 3; i++)
                {
                    sTmp = Strings.Chr(Convert.ToInt32(character[i]) - j) + sTmp;
                }

                EncryptedString = sTmp;
            }
            return EncryptedString;
        }

        private Byte GetRndNumber()
        {
            short i;
            do
            {
                VBMath.Randomize();
                i = (short)Conversion.Int((99) * VBMath.Rnd() + 10);
            }
            while (i < 10 || i > 99);

            return Convert.ToByte(i);
        }

        private byte GetRndConfusion()
        {
            short i;
            do
            {
                VBMath.Randomize();
                i = (short)Conversion.Int((99) * VBMath.Rnd() + 10);
            }
            while (i < 65 || i > 90);

            return Convert.ToByte(i);

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Data;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Web.Security;
using System.Data.SqlClient;
using System.Reflection;

namespace VLink
{
    /// <summary>
    /// Summary description for Sync
    /// </summary>
    [WebService(Namespace = "http://vmcnet.com/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]

    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]


    public class VLink : System.Web.Services.WebService
    {
        //These arbitrary strings will remain hardcoded in both the web service and the standalone application
        //
        static readonly string PasswordHash = "SaLmoNArms";
        static readonly string SaltKey = "t27nw@xp";
        static readonly string VIKey = "^dC2)wH4#UsKqZ0&";


        [WebMethod(Description = "Login Procedure", EnableSession = true)]
        public int Login(string user, string password)
        {
            bool? userisvalid = (bool?)Session["UserIsValid"];

            if (userisvalid == null) userisvalid = false;

            // check if we are already logged in, then log out
            //
            if (userisvalid == true)
            {
                Session["UserName"] = "";
                Session["UserIsValid"] = false;
            }

            // check the user information
            //            
            using (SqlConnection conn = AppUtils.GetConn())
            {
                // need to encode the strings
                //
                userisvalid = Membership.ValidateUser(Decrypt(user), Decrypt(password));

                Session["UserIsValid"] = userisvalid;

                if (userisvalid == true)
                {
                    Session["UserName"] = Decrypt(user);
                    Session["LastActivity"] = DateTime.Now;
                    return 0;
                }
            }
            return -1;
        }



        [WebMethod(Description = "Logout of Session", EnableSession = true)]
        public int Logout()
        {
            Session["UserName"] = "";
            Session["UserIsValid"] = false;

            return 0;
        }

        

        [WebMethod(Description = "Get List of Units", EnableSession = true)]
        public string GetUnits()
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            //define query
            DataTable tbl = new DataTable("UnitUser");
            DataTable tbl2 = new DataTable("VLinkUnit");

            string query = "SELECT UnitID FROM VLinkUser WHERE UserName = '" + Session["UserName"] + "'";

            //Execute query
            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand(query, conn))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter(comm))
                    {
                        try { adapter.Fill(tbl); }
                        catch (SqlException) { return "-4"; }
                    }
                }
            }

            if (tbl.Rows.Count < 1)
            {
                return "-5";
            }

            int j;

            for (j = 0; j < tbl.Rows.Count; j++)
            {
                query = "SELECT * FROM VLinkUnit WHERE UnitID = " + tbl.Rows[j]["UnitID"].ToString();

                using (SqlConnection conn = AppUtils.GetConn())
                {
                    using (SqlCommand comm = new SqlCommand(query, conn))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(comm))
                        {
                            try { adapter.Fill(tbl2); }
                            catch (SqlException) { return "-4"; }
                        }
                    }
                }
            }

            tbl2.Columns.Remove("Active");
            tbl2.Columns.Remove("CurrentAlarm");
            tbl2.Columns.Remove("SerialNumber");

            StringWriter writer = new StringWriter();
            tbl2.WriteXml(writer);
            return writer.ToString();
        }








        [WebMethod(Description = "Get Data Packets", EnableSession = true)]
        public string GetPackets(int unitid, DateTime? start, DateTime? end)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";
            
            if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";

            DataTable tbl = new DataTable("Packet");

            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_getpackets", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));

                    if (start == null || (start == DateTime.MinValue))
                        comm.Parameters.Add(new SqlParameter("@start", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@start", start));

                    if (end == null || (end == DateTime.MinValue) )
                        comm.Parameters.Add(new SqlParameter("@end", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@end", end));

                    using (SqlDataAdapter adapter = new SqlDataAdapter(comm))
                    {
                        try { adapter.Fill(tbl); }
                        catch (SqlException) { return "-4"; }
                    }
                }
            }

            tbl.Columns.Remove("id");
            tbl.Columns.Remove("UnitID");
            if ( tbl.Columns.Contains("RowNum"))  tbl.Columns.Remove("RowNum");

            StringWriter writer = new StringWriter();
            tbl.WriteXml(writer);
            return writer.ToString();
            
            //return "End of routine";
        }

        [WebMethod(Description = "Get Property", EnableSession = true)]
        public string GetProperty(int unitid, int propertyid)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";

            if (propertyid < 1) return "-6";

            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_getproperty", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
                    comm.Parameters.Add(new SqlParameter("@id", propertyid));

                    object st = comm.ExecuteScalar();

                    if (st == null) return "";

                    return st.ToString();
                }
            }

        }

        [WebMethod(Description = "Set Property", EnableSession = true)]
        public string SetProperty(int unitid, int propertyid, string value)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";

            if (propertyid < 1) return "-6";

            if (value == null) return "-7";

            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_setproperty", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
                    comm.Parameters.Add(new SqlParameter("@id", propertyid));
                    comm.Parameters.Add(new SqlParameter("@value", value));

                    object st = comm.ExecuteScalar();

                    if (st == null) return "";

                    return st.ToString();
                }
            }

        }


        [WebMethod(Description = "Get Alarms", EnableSession = true)]
        public string GetAlarms(int unitid, bool NewAlarms, DateTime? start, DateTime? end)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if (unitid != -1) if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";
            
            DataTable tbl = new DataTable("Alarm");
            int sendnew = 0;

            if (NewAlarms == true) sendnew = 1;
            
            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_getalarms", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@username", Session["UserName"]));
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
                    comm.Parameters.Add(new SqlParameter("@new", sendnew));
                    
                    if (start == null)
                        comm.Parameters.Add(new SqlParameter("@start", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@start", start));

                    if (end == null)
                        comm.Parameters.Add(new SqlParameter("@end", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@end", end));

                    using (SqlDataAdapter adapter = new SqlDataAdapter(comm))
                    {
                        try { adapter.Fill(tbl); }
                        catch (SqlException) { return "-4"; }
                    }
                }
            }

            //tbl.Columns.Remove("id");
            //tbl.Columns.Remove("UnitID");
            
            StringWriter writer = new StringWriter();
            tbl.WriteXml(writer);
            return writer.ToString();
        }

        [WebMethod(Description = "Acknowledge Alarm", EnableSession = true)]
        public string AcknowledgeAlarm(int unitid, int id, string name)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";

            if (id < 1) return "-6";

            if (name == null ) return "-9";

            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_acknowledgealarm", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
                    comm.Parameters.Add(new SqlParameter("@id", id));
                    comm.Parameters.Add(new SqlParameter("@name", name));

                    object st = comm.ExecuteScalar();

                    if (st == null) return "-4";

                    return st.ToString();
                }
            }
        }

        [WebMethod(Description = "Get Alarm Configuration", EnableSession = true)]
        public string GetAlarmConfiguration(int unitid)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if ( unitid != -1 ) if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";

            DataTable tbl = new DataTable("AlarmConfiguration");
            
            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_getalarmconfiguration", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;

                    comm.Parameters.Add(new SqlParameter("@username", Session["UserName"]));
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
            
                    using (SqlDataAdapter adapter = new SqlDataAdapter(comm))
                    {
                        try { adapter.Fill(tbl); }
                        catch (SqlException) { return "-4"; }
                    }
                }
            }

            //tbl.Columns.Remove("id");
            //tbl.Columns.Remove("UnitID");

            StringWriter writer = new StringWriter();
            tbl.WriteXml(writer);
            return writer.ToString();
        }

        [WebMethod(Description = "Delete Alarm Configuration", EnableSession = true)]
        public string DeleteAlarmConfiguration(int unitid, int id, int alarmid )
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if ( unitid != -1 ) if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";
                        
            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_deletealarmconfiguration", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@username", Session["UserName"]));
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
                    comm.Parameters.Add(new SqlParameter("@id", id));
                    comm.Parameters.Add(new SqlParameter("@alarmid", alarmid));

                    object st = comm.ExecuteScalar();

                    if (st == null) return "-4";

                    return st.ToString();
                }
            }            
        }

        [WebMethod(Description = "Configure New Alarm", EnableSession = true)]
        public string ConfigureAlarm(int id, int unitid, int alarmid, int sensorid, string LowLimit, string HighLimit, string OnAction, string OffAction, string Text, int linked)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if ( unitid != -1 ) if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";

            if (sensorid < 1) return "-10";
            
            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_configurealarm", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@username", Session["UserName"]));
                    comm.Parameters.Add(new SqlParameter("@id", id));
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
                    comm.Parameters.Add(new SqlParameter("@alarmid", alarmid));
                    comm.Parameters.Add(new SqlParameter("@sensorid", sensorid));

                    if (LowLimit == null ||  LowLimit.Length < 1)
                        comm.Parameters.Add(new SqlParameter("@low", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@low", LowLimit));

                    if (HighLimit == null || HighLimit.Length < 1)
                        comm.Parameters.Add(new SqlParameter("@high", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@high", HighLimit));

                    if (OnAction == null || OnAction.Length < 1)
                        comm.Parameters.Add(new SqlParameter("@onaction", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@onaction", OnAction));

                    if (OffAction == null || OffAction.Length < 1)
                        comm.Parameters.Add(new SqlParameter("@offaction", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@offaction", OffAction));

                    if (Text == null || Text.Length < 1)
                        comm.Parameters.Add(new SqlParameter("@text", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@text", Text));

                    comm.Parameters.Add(new SqlParameter("@linked", linked));

                    object st = comm.ExecuteScalar();

                    if (st == null) return "-4";

                    return st.ToString();
                }
            }
        }



        [WebMethod(Description = "Get Errors", EnableSession = true)]
        public string GetErrors(int unitid, bool NewErrors, DateTime? start, DateTime? end)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if (unitid != -1) if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";

            DataTable tbl = new DataTable("Error");
            int sendnew = 0;

            if (NewErrors == true) sendnew = 1;

            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_geterrors", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@username", Session["UserName"]));
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
                    comm.Parameters.Add(new SqlParameter("@new", sendnew));

                    if (start == null)
                        comm.Parameters.Add(new SqlParameter("@start", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@start", start));

                    if (end == null)
                        comm.Parameters.Add(new SqlParameter("@end", DBNull.Value));
                    else
                        comm.Parameters.Add(new SqlParameter("@end", end));

                    using (SqlDataAdapter adapter = new SqlDataAdapter(comm))
                    {
                        try { adapter.Fill(tbl); }
                        catch (SqlException) { return "-4"; }
                    }
                }
            }

            StringWriter writer = new StringWriter();
            tbl.WriteXml(writer);
            return writer.ToString();
        }

        [WebMethod(Description = "Acknowledge Error", EnableSession = true)]
        public string AcknowledgeError(int unitid, int id, string name)
        {
            if (AppUtils.CheckSession(Session) == false) return "-2";

            if (AppUtils.CheckUnitID(Session, unitid) == false) return "-3";

            if (id < 1) return "-6";

            if (name == null) return "-9";

            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand("proc_acknowledgeerror", conn))
                {
                    comm.CommandType = CommandType.StoredProcedure;
                    comm.Parameters.Add(new SqlParameter("@unitid", unitid));
                    comm.Parameters.Add(new SqlParameter("@id", id));
                    comm.Parameters.Add(new SqlParameter("@name", name));

                    object st = comm.ExecuteScalar();

                    if (st == null) return "-4";

                    return st.ToString();
                }
            }
        }









        private static string Encrypt(string plainText)
        {
            //if (1 == 1) return plainText;

            //http://social.msdn.microsoft.com/Forums/vstudio/en-US/d6a2836a-d587-4068-8630-94f4fb2a2aeb/encrypt-and-decrypt-a-string-in-c?forum=csharpgeneral

            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] keyBytes = new Rfc2898DeriveBytes(PasswordHash, Encoding.ASCII.GetBytes(SaltKey)).GetBytes(256 / 8);
            var symmetricKey = new RijndaelManaged() { Mode = CipherMode.CBC, Padding = PaddingMode.Zeros };
            var encryptor = symmetricKey.CreateEncryptor(keyBytes, Encoding.ASCII.GetBytes(VIKey));

            byte[] cipherTextBytes;

            using (var memoryStream = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                    cryptoStream.FlushFinalBlock();
                    cipherTextBytes = memoryStream.ToArray();
                    cryptoStream.Close();
                }
            }
            return Convert.ToBase64String(cipherTextBytes);
        }

        private static string Decrypt(string encryptedText)
        {
            //if (1 == 1) return encryptedText;

            //http://social.msdn.microsoft.com/Forums/vstudio/en-US/d6a2836a-d587-4068-8630-94f4fb2a2aeb/encrypt-and-decrypt-a-string-in-c?forum=csharpgeneral

            byte[] cipherTextBytes = Convert.FromBase64String(encryptedText);
            byte[] keyBytes = new Rfc2898DeriveBytes(PasswordHash, Encoding.ASCII.GetBytes(SaltKey)).GetBytes(256 / 8);
            var symmetricKey = new RijndaelManaged() { Mode = CipherMode.CBC, Padding = PaddingMode.None };

            var decryptor = symmetricKey.CreateDecryptor(keyBytes, Encoding.ASCII.GetBytes(VIKey));

            using (var memoryStream = new MemoryStream(cipherTextBytes))
            {
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    byte[] plainTextBytes = new byte[cipherTextBytes.Length];

                    int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);

                    return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount).TrimEnd("\0".ToCharArray());
                }
            }
        }

    }

}

/*
    //These arbitrary strings will remain hardcoded in both the web service and the standalone application
    static readonly string PasswordHash = "OrCHiDs";
    static readonly string SaltKey = "qw1mb@rt";
    static readonly string VIKey = "^fA4(pO8$JpByM6*";
*/


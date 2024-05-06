using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;


namespace VLink
{
    public static class AppUtils
    {
        public static string SQLDateFormat = "yyyy-MM-dd";
        public static bool ShortenNumbers = true;

        public static string StandardDateFormat = "dddd dd MMMM yyyy";
        //public static string StandardDateFormat = "D";
        public static string StandardDateTimeFormat = "dddd dd MMMM yyyy hh:mm tt";
        //public static string StandardDateTimeFormat = "f";

        public static SqlConnection GetConn()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["VLinkConnection"].ConnectionString;
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            return conn;
        }

        public static bool userisvalid = false;

        public static string Truncate(string text, int max)
        {
            if (String.IsNullOrEmpty(text)) return text;
            return text.Substring(0, Math.Min(text.Length, max));
        }


        public static string CleanText(string text)
        {
            string result = text;

            result = result.Replace("'", "");
            result = result.Replace("\"", "");
            result = result.Replace("--", "");
            result = result.Replace("<", "");
            result = result.Replace(">", "");

            return result;
        }

        public static string CarriageReturns(string text, bool breaks)
        {
            if (breaks)
            {
                if (text.Contains('\n')) text = text.Replace("\n", "<br />");
                else if (text.Contains('\r')) text = text.Replace("\r", "<br />");
                return text;
            }
            else
            {
                text = "<p>" + text + "</p>";
                if (text.Contains('\n')) text = text.Replace("\n", "</p><p>");
                else if (text.Contains('\r')) text = text.Replace("\r", "</p><p>");
                return text;
            }
        }

        public static string CleanText(string text, int max)
        {
            string result = CleanText(text);
            return Truncate(text, max);
        }



        public static void AddEvent(string text, string link)
        {
            string query = "INSERT INTO Events (Date, Event, Link) VALUES (@Date, @Event, @Link)";
            using (SqlConnection conn = GetConn())
            {
                using (SqlCommand comm = new SqlCommand(query, conn))
                {
                    comm.Parameters.AddWithValue("@Date", DateTime.Now);
                    comm.Parameters.AddWithValue("@Event", text);
                    comm.Parameters.AddWithValue("@Link", link);
                    comm.ExecuteNonQuery();
                }
            }
        }

        public static bool CheckSession(System.Web.SessionState.HttpSessionState MySession)
        {
            bool? userisvalid = (bool?)MySession["UserIsValid"];

            if (userisvalid == null) return false;
                        
            if (userisvalid == false) return false;

            DateTime currentdate = DateTime.Now;

            TimeSpan elapsed = DateTime.Now.Subtract((DateTime)MySession["LastActivity"]);

            MySession["LastActivity"] = DateTime.Now;

            if (elapsed.TotalSeconds > 300.0)
            {
                MySession["UserName"] = "";
                MySession["UserIsValid"] = false;
                return false;
            }

            return true;
        }

        public static bool CheckUnitID(System.Web.SessionState.HttpSessionState MySession, int unitid)
        {
            DataTable tbl = new DataTable("UnitUser");
            DataTable tbl2 = new DataTable("VLinkUnit");

            string query = "SELECT UnitID FROM VLinkUser WHERE UnitID = " + unitid + "AND UserName = '" + MySession["UserName"] + "'";

            //Execute query
            using (SqlConnection conn = AppUtils.GetConn())
            {
                using (SqlCommand comm = new SqlCommand(query, conn))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter(comm))
                    {
                        try { adapter.Fill(tbl); }
                        catch (SqlException) { return false; }
                    }
                }
            }

            if (tbl.Rows.Count < 1)
            {
                return false;
            }

            return true;
        }

    }
}
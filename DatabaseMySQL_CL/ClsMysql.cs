using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.ComponentModel;
using System.Linq;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.IO;

namespace DatabaseMySQL_CL
{
    public delegate void DatabaseChanged();

    public class ClsMysql
    {
        #region <-Fields->
        string mDatabase = "training", mServer = "localhost", mPrt = "3306", mUID = "root", mPWD = "1059816";
        bool mWriteOnClose = true;
        string ConnectionString;
        MySqlConnection Connection;
        readonly List<DataAdaptersList> myDataAdapter = new List<DataAdaptersList>();
        readonly DataSet myDataSet = new DataSet();
        MySqlCommandBuilder myCommandBuilder;
        DataTable DataTableColumns;
        public event DatabaseChanged DBChanged;
        List<string> TablesList = new List<string>();
        #endregion <-Fields->

        #region <-Properties->
        [Description("Database Name"), Category("Data")]
        public string DatabaseName
        {
            get { return mDatabase; }
            set { mDatabase = value; }
        }
        [Description("Server Address"), Category("Data")]
        public string ServerAddress
        {
            get { return mServer; }
            set { mServer = value; }
        }
        [Description("Port"), Category("Data")]
        public string Port
        {
            get { return mPrt; }
            set { mPrt = value; }
        }
        [Description("User Id"), Category("Data")]
        public string UserId
        {
            get { return mUID; }
            set { mUID = value; }
        }
        [Description("Password"), Category("Data")]
        public string Password
        {
            get { return mPWD; }
            set { mPWD = value; }
        }
        /// <summary>
        /// Write changes to database permanently when program closed
        /// </summary>
        public bool WriteOnClose
        {
            get { return mWriteOnClose; }
            set { mWriteOnClose = value; }
        }
        #endregion <-Properties->

        #region <-Methods->
        /// <summary>
        /// Connect to a database with given parameters
        /// </summary>
        /// <param name="DBServerAddress">Server address</param>
        /// <param name="DBPort">Port</param>
        /// <param name="DBName">Database name</param>
        /// <param name="DBUserID">Database user id</param>
        /// <param name="DBPassword">Database password</param>
        public void ConnectToDatabase(string DBServerAddress, string DBPort, string DBName, string DBUserID, string DBPassword)
        {
            try
            {
                ServerAddress = DBServerAddress;
                Port = DBPort;
                DatabaseName = DBName;
                UserId = DBUserID;
                Password = DBPassword;
                ConnectionString = "Server=" + ServerAddress + ";Port=" + Port + ";Database=" + DatabaseName + ";Uid=" + UserId + ";Pwd=" + Password + ";";
                Connection = new MySqlConnection(ConnectionString);
                if (Connection.State != ConnectionState.Open) Connection.Open();
                DataTableColumns = Connection.GetSchema("Tables");
                foreach (DataRow EachRow in DataTableColumns.Rows)
                {
                    if ((string)EachRow[3] != "SYSTEM_TABLE") TablesList.Add((string)EachRow[2]);
                    DataAdaptersList myDataAdaptersList = new DataAdaptersList
                    {
                        Da = new MySqlDataAdapter("SELECT * FROM " + (string)EachRow[2], Connection),
                        TableName = (string)EachRow[2]
                    };
                    myDataAdapter.Add(myDataAdaptersList);
                    myDataAdapter.Last().Da.Fill(myDataSet, (string)EachRow[2]);
                    //set Ds id field autoincrement true (somehow is not coming automatically)
                    myDataSet.Tables[(string)EachRow[2]].PrimaryKey = new DataColumn[] { myDataSet.Tables[(string)EachRow[2]].Columns[0] };
                    myDataSet.Tables[(string)EachRow[2]].Columns[0].AutoIncrement = true;
                    myDataSet.Tables[(string)EachRow[2]].Columns[0].AutoIncrementSeed = myDataSet.Tables[(string)EachRow[2]].Rows.Count + 1;
                    //set Ds id field autoincrement true (somehow is not coming automatically)
                    //build the command builder and insert, update and delete commands automatically
                    myCommandBuilder = new MySqlCommandBuilder(myDataAdapter.Last().Da);
                    MySqlCommand UpdateCommand = myCommandBuilder.GetUpdateCommand();
                    MySqlCommand InsertCommand = myCommandBuilder.GetInsertCommand();
                    MySqlCommand DeleteCommand = myCommandBuilder.GetDeleteCommand();
                    myCommandBuilder.Dispose(); //this is because a bug in MySQL connector
                    myDataAdapter.Last().Da.UpdateCommand = UpdateCommand;
                    myDataAdapter.Last().Da.InsertCommand = InsertCommand;
                    myDataAdapter.Last().Da.DeleteCommand = DeleteCommand;
                }
            }
            catch (MySqlException ex)
            {
                throw new Exception("Please specify a database to connect - " + ex.Message);
            }
            finally
            {
                if (Connection.State == ConnectionState.Open) Connection.Close();
            }
        }

        /// <summary>
        /// Writes changes to database permanently (for disconnected model)
        /// </summary>
        /// <param name="TableName">Table name to save changes to</param>
        public void WriteToDB(string TableName)
        {
            try
            {
                myDataAdapter.Find(a => a.TableName == TableName).Da.Update(myDataSet, myDataSet.Tables[TableName].TableName);
                myDataSet.AcceptChanges();
                DBChanged?.Invoke();
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot write changes to table " + TableName + ": " + ex.Message);
            }
        }

        public Dictionary<int, List<string>> ReadAllRecords(string TableName)
        {
            try
            {
                int counter = 0;
                Dictionary<int, List<string>> myList = new Dictionary<int, List<string>>();
                var Rows = myDataSet.Tables[TableName].Select();
                foreach(DataRow Row in Rows)
                {
                    myList.Add(counter, Row.ItemArray.ToList().OfType<string>().ToList());
                    counter++;
                }
                return myList;
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot get the record from table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Returns a specific filed value from the specified table based on given key field and its value
        /// </summary>
        /// <param name="TableName">Table name to fetch data from</param>
        /// <param name="FieldNameToSearch">The field name which you want to get value of</param>
        /// <param name="KeyField">Key feild name for SELECT statement</param>
        /// <param name="KeyFieldValue">Key field value for SELECT statement</param>
        /// <returns>Returns the requested field value as string</returns>
        public string Find(string TableName, string FieldNameToSearch, string KeyField, string KeyFieldValue)
        {
            try
            {
                return myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'")[0][FieldNameToSearch].ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot get the record from table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Finds all fields in a table with given search keyword
        /// </summary>
        /// <param name="TableName">Table name to search in</param>
        /// <param name="KeyField">Keyfield name</param>
        /// <param name="KeyFieldValue">Keyfield value</param>
        /// <returns>Returns a List<string> includes all the fields in the table match the keyword</string></returns>
        public List<string> Find(string TableName, string KeyField, string KeyFieldValue)
        {
            try
            {
                return (myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'")).Length > 0 ? myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'")[0].ItemArray.ToList().OfType<string>().ToList() : null;
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot get the record from table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Finds all fields in a table with given search keywords
        /// </summary>
        /// <param name="TableName">Table name to search in</param>
        /// <param name="KeyField">First keyfield name</param>
        /// <param name="KeyFieldValue">First keyfield value</param>
        /// <param name="SecondKeyField">Second keyfield name</param>
        /// <param name="SecondKeyFieldValue">Second keyfield value</param>
        /// <returns>Returns a List<string> includes all the fields in the table match the keywords</returns>
        public List<string> Find(string TableName, string KeyField, string KeyFieldValue, string SecondKeyField, string SecondKeyFieldValue)
        {
            try
            {
                return myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'" + " AND " + SecondKeyField + " = '" + SecondKeyFieldValue + "'")[0].ItemArray.ToList().OfType<string>().ToList();
            }
            catch (MySqlException ex)
            {
                throw new Exception("Cannot get the record from table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Returns a specific filed value from the specified table based on given key field and its value
        /// </summary>
        /// <param name="TableName">Table name to fetch data from</param>
        /// <param name="FieldNameToSearch">The field name which you want to get value of</param>
        /// <param name="KeyField">Key feild name for SELECT statement</param>
        /// <param name="KeyFieldValue">Key field value for SELECT statement</param>
        /// <param name="SecondKeyField">Second keyfield name</param>
        /// <param name="SecondKeyFieldValue">Second keyfield value</param>
        /// <returns>Returns the requested field value as string</returns>
        public string Find(string TableName, string FieldNameToSearch, string KeyField, string KeyFieldValue, string SecondKeyField, string SecondKeyFieldValue)
        {
            try
            {
                return myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'" + " AND " + SecondKeyField + " = '" + SecondKeyFieldValue + "'")[0][FieldNameToSearch].ToString();
            }
            catch (MySqlException ex)
            {
                throw new Exception("Cannot get the record from table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Updates specified databse/table/fields with given field names and values using the keyfield value
        /// </summary>
        /// <param name="TableName">Table name to update</param>
        /// <param name="Values">Field value list (must not include primary key field value)</param>
        /// <param name="KeyField">Keyfield name for WHERE statement</param>
        /// <param name="KeyFieldValue">Keyfield value for WHERE statement</param>
        public void Update(string TableName, List<string> Values, string KeyField, string KeyFieldValue)
        {
            try
            {
                var Row = myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'")[0];
                int ID = (int)Row[0];
                for (int i = 1; i < myDataSet.Tables[TableName].Columns.Count; i++)
                {
                    Row[i] = Values[i - 1];
                }
                myDataSet.Tables[TableName].Select("ID = " + ID)[0] = Row;
                DBChanged?.Invoke();
            }
            catch (MySqlException ex)
            {
                throw new Exception("Cannot update table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Updates specified databse/table with given field names and values using the keyfield values
        /// </summary>
        /// <param name="TableName">Table name to update</param>
        /// <param name="Values">Field value list (must not include primary key field value)</param>
        /// <param name="KeyField">Keyfield name for WHERE statement</param>
        /// <param name="KeyFieldValue">Keyfield value for WHERE statement</param>
        /// <param name="SecondKeyField">Second keyfield name for WHERE statement (AND)</param>
        /// <param name="SecondKeyFieldValue">Second keyfield value for WHERE statement (AND)</param>
        public void Update(string TableName, List<string> Values, string KeyField, string KeyFieldValue, string SecondKeyField, string SecondKeyFieldValue)
        {
            try
            {
                var Row = myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'" + " AND " + SecondKeyField + " = '" + SecondKeyFieldValue + "'")[0];
                int ID = (int)Row[0];
                for (int i = 1; i < myDataSet.Tables[TableName].Columns.Count; i++)
                {
                    Row[i] = Values[i - 1];
                }
                myDataSet.Tables[TableName].Select("ID = " + ID)[0] = Row;
                DBChanged?.Invoke();
            }
            catch (MySqlException ex)
            {
                throw new Exception("Cannot update table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Inserts given fields and the values into specified databse/table/fields
        /// </summary>
        /// <param name="TableName">Table name</param>
        /// <param name="Values">Value list for given field names (must not include primary key field value)</param>
        public void Insert(string TableName, List<string> Values)
        {
            try
            {
                DataRow Row = myDataSet.Tables[TableName].NewRow();
                for (int i = 1; i < myDataSet.Tables[TableName].Columns.Count; i++)
                {
                    Row[myDataSet.Tables[TableName].Columns[i]] = Values[i - 1];
                }
                myDataSet.Tables[TableName].Rows.Add(Row);
                DBChanged?.Invoke();
            }
            catch (MySqlException ex)
            {
                throw new Exception("Cannot insert into table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Removes specified record from given database/table
        /// </summary>
        /// <param name="TableName">Table name</param>
        /// <param name="KeyField">Keyfield name for WHERE statement</param>
        /// <param name="KeyFieldValue">Keyfield value for WHERE statement</param>
        public void Delete(string TableName, string KeyField, string KeyFieldValue)
        {
            try
            {
                myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'")[0].Delete();
                DBChanged?.Invoke();
            }
            catch (MySqlException ex)
            {
                throw new Exception("Cannot delete from table " + TableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Removes specified record from given database/table
        /// </summary>
        /// <param name="TableName">Table name</param>
        /// <param name="KeyField">Keyfield name for WHERE statement</param>
        /// <param name="KeyFieldValue">Keyfield value for WHERE statement</param>
        /// <param name="SecondKeyField"></param>
        /// <param name="SecondKeyFieldValue"></param>
        public void Delete(string TableName, string KeyField, string KeyFieldValue, string SecondKeyField, string SecondKeyFieldValue)
        {
            try
            {
                myDataSet.Tables[TableName].Select(KeyField + " = '" + KeyFieldValue + "'" + " AND " + SecondKeyField + " = '" + SecondKeyFieldValue + "'")[0].Delete();
                DBChanged?.Invoke();
            }
            catch (MySqlException ex)
            {
                throw new Exception("Cannot delete from table " + TableName + ": " + ex.Message);
            }
        }
        #endregion <-Methods->
    }

    internal class DataAdaptersList
    {
        internal MySqlDataAdapter Da;
        internal string TableName;
    }

    public static class EncryptionHelper
    {
        public static string Encrypt(string clearText)
        {
            string EncryptionKey = "abc123";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }
        public static string Decrypt(string cipherText)
        {
            string EncryptionKey = "abc123";
            cipherText = cipherText.Replace(" ", "+");
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            return cipherText;
        }
    }
}
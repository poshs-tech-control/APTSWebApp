using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Web.Mvc;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace APTS_WEB_APP.DataIntraction
{
    public class DataAccess
    {
        public static readonly string ConnectionString = GetConnectionString();
        private const int InternalTransferCommandTimeoutSeconds = 180;
        private const int LoginLocationCommandTimeoutSeconds = 120;
        private const int LoginLocationRetryDelayMilliseconds = 1500;
        private const string LoginLocationCacheFileName = "LoginLocationsCache.json";
        private static readonly TimeSpan LoginLocationCacheDuration = TimeSpan.FromHours(12);
        private static readonly object LoginLocationCacheLock = new object();
        private static List<LoginLocationItem> cachedLoginLocations = new List<LoginLocationItem>();
        private static DateTime cachedLoginLocationsAtUtc = DateTime.MinValue;

        private static bool ContainsHtmlInjection(params string[] values)
        {
            return values != null && values.Any(HtmlInjectionGuard.ContainsHtmlInjection);
        }

        private static string GetMemberProductionLocationCode(MemberMasterSaveRequest request)
        {
            string locationCode = request == null ? string.Empty : request.ProductionLocation;
            string locationTitle = request == null ? string.Empty : request.ProductionLocationTitle;

            if (string.Equals(locationCode, "CLOC00000012", StringComparison.OrdinalIgnoreCase)
                || string.Equals(locationTitle, "Chakan Godown", StringComparison.OrdinalIgnoreCase))
            {
                return "CLOC00000012";
            }

            return "CLOC00000010";
        }

        private static string GetMemberProductionLocationTitle(MemberMasterSaveRequest request)
        {
            string locationCode = GetMemberProductionLocationCode(request);
            return string.Equals(locationCode, "CLOC00000012", StringComparison.OrdinalIgnoreCase) ? "Chakan Godown" : "Sanaswadi";
        }

        public static bool HasValidConnectionString
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ConnectionString)
                    && !ConnectionString.Contains("YOUR_");
            }
        }

        public static object ExecuteTextScalar(string commandText, IDictionary<string, object> parameters)
        {
            if (!HasValidConnectionString)
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand(commandText, connection))
                {
                    command.CommandType = CommandType.Text;
                    AddParameters(command, parameters);
                    connection.Open();
                    return command.ExecuteScalar();
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public static void ExecuteTextReader(
            string commandText,
            IDictionary<string, object> parameters,
            Action<IDataRecord> readRow)
        {
            if (!HasValidConnectionString)
            {
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand(commandText, connection))
                {
                    command.CommandType = CommandType.Text;
                    AddParameters(command, parameters);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            readRow(reader);
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return;
            }
        }

        public string GetCurrentUserCode(Controller controller, string fallback = "USR0000001")
        {
            string userCode = Convert.ToString(controller.Session["UserCode"]);
            return string.IsNullOrWhiteSpace(userCode) ? fallback : userCode;
        }

        public string GetCurrentUserFullName(Controller controller, string fallback = "SYSTEM")
        {
            string userName = Convert.ToString(controller.Session["UserFullName"]);
            return string.IsNullOrWhiteSpace(userName) ? fallback : userName;
        }

        public string GetIpAddress(Controller controller, string fallback = "")
        {
            if (controller == null || controller.Request == null)
            {
                return fallback;
            }

            string ipAddress = FirstClientIp(controller.Request.ServerVariables["HTTP_X_FORWARDED_FOR"]);
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                ipAddress = FirstClientIp(controller.Request.ServerVariables["HTTP_X_REAL_IP"]);
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                ipAddress = FirstClientIp(controller.Request.ServerVariables["HTTP_CLIENT_IP"]);
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                ipAddress = FirstClientIp(controller.Request.ServerVariables["REMOTE_ADDR"]);
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                ipAddress = FirstClientIp(controller.Request.UserHostAddress);
            }

            if (IsLoopbackIpAddress(ipAddress))
            {
                string localIpAddress = GetLocalNetworkIpAddress();
                if (!string.IsNullOrWhiteSpace(localIpAddress))
                {
                    ipAddress = localIpAddress;
                }
            }

            return string.IsNullOrWhiteSpace(ipAddress) ? fallback : ipAddress;
        }

        private static string FirstClientIp(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return string.Empty;
            }

            string firstIp = ipAddress.Split(',')
                .Select(part => part.Trim())
                .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part) && !string.Equals(part, "unknown", StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(firstIp) ? string.Empty : firstIp;
        }

        private static bool IsLoopbackIpAddress(string ipAddress)
        {
            IPAddress parsedIpAddress;
            return IPAddress.TryParse(ipAddress, out parsedIpAddress) && IPAddress.IsLoopback(parsedIpAddress);
        }

        private static string GetLocalNetworkIpAddress()
        {
            try
            {
                IPAddress localIpAddress = Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address));

                return localIpAddress == null ? string.Empty : localIpAddress.ToString();
            }
            catch (SocketException)
            {
                return string.Empty;
            }
        }

        public LoginUser ValidateLogin(string loginId, string password)
        {
            if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            if (!HasValidConnectionString)
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_LoginDetails", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("P_UserName", MySqlDbType.VarChar).Value = loginId.Trim();
                    command.Parameters.Add("P_Password", MySqlDbType.VarChar).Value = password;
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        var loginUser = new LoginUser
                        {
                            UserCode = ReadString(reader, "USERCODE"),
                            UserFullName = ReadString(reader, "USERFULLNAME"),
                            UserType = ReadString(reader, "USERTYPE"),
                            UserName = ReadString(reader, "USERNAME"),
                            UserRole = ReadString(reader, "USERROLE"),
                            MenuAccess = new List<MenuAccessItem>()
                        };

                        if (reader.NextResult())
                        {
                            while (reader.Read())
                            {
                                loginUser.MenuAccess.Add(new MenuAccessItem
                                {
                                    MenuDTCode = ReadString(reader, "MenuDTCode"),
                                    MenuDTName = ReadString(reader, "MenuDTName"),
                                    Type = ReadString(reader, "TYPE"),
                                    TransactionID = ReadString(reader, "TransactionID")
                                });
                            }
                        }

                        return loginUser;
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public List<LoginLocationItem> LoadLoginLocationList()
        {
            if (!HasValidConnectionString)
            {
                return GetCachedLoginLocationList(true);
            }

            try
            {
                List<LoginLocationItem> locations = LoadLoginLocationListFromDatabase();
                if (locations.Count == 0)
                {
                    return GetCachedLoginLocationList(true);
                }

                SetCachedLoginLocationList(locations);
                return locations;
            }
            catch (MySqlException ex)
            {
                System.Diagnostics.Debug.WriteLine("WebApp_LocationMaster failed on first attempt: " + ex);
            }

            System.Threading.Thread.Sleep(LoginLocationRetryDelayMilliseconds);

            try
            {
                List<LoginLocationItem> locations = LoadLoginLocationListFromDatabase();
                if (locations.Count > 0)
                {
                    SetCachedLoginLocationList(locations);
                    return locations;
                }

                return GetCachedLoginLocationList(true);
            }
            catch (MySqlException ex)
            {
                System.Diagnostics.Debug.WriteLine("WebApp_LocationMaster failed after retry: " + ex);
                return GetCachedLoginLocationList(true);
            }
        }

        private static List<LoginLocationItem> LoadLoginLocationListFromDatabase()
        {
            var locations = new List<LoginLocationItem>();

            using (var connection = new MySqlConnection(ConnectionString))
            using (var command = new MySqlCommand("WebApp_LocationMaster", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = LoginLocationCommandTimeoutSeconds;
                connection.Open();

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        locations.Add(new LoginLocationItem
                        {
                            LocationCode = ReadString(reader, "LocationCode"),
                            CustomerCode = ReadString(reader, "CustomerCode"),
                            LocationTitle = ReadString(reader, "LocationTitle"),
                            LocationType = ReadString(reader, "LocationType"),
                            LocationTypeName = ReadString(reader, "LocationTypeName")
                        });
                    }
                }
            }

            return locations
                .Where(location => !string.IsNullOrWhiteSpace(location.LocationCode))
                .GroupBy(location => location.LocationCode)
                .Select(group => group.First())
                .OrderBy(location => location.LocationTitle)
                .ToList();
        }

        private static List<LoginLocationItem> GetCachedLoginLocationList(bool allowStale)
        {
            lock (LoginLocationCacheLock)
            {
                if (cachedLoginLocations.Count == 0)
                {
                    cachedLoginLocations = LoadPersistentLoginLocationList();
                    cachedLoginLocationsAtUtc = cachedLoginLocations.Count > 0 ? DateTime.UtcNow : DateTime.MinValue;
                }

                if (cachedLoginLocations.Count == 0)
                {
                    return new List<LoginLocationItem>();
                }

                bool cacheIsFresh = DateTime.UtcNow - cachedLoginLocationsAtUtc <= LoginLocationCacheDuration;
                return allowStale || cacheIsFresh
                    ? CloneLoginLocationList(cachedLoginLocations)
                    : new List<LoginLocationItem>();
            }
        }

        private static void SetCachedLoginLocationList(List<LoginLocationItem> locations)
        {
            lock (LoginLocationCacheLock)
            {
                cachedLoginLocations = CloneLoginLocationList(locations);
                cachedLoginLocationsAtUtc = DateTime.UtcNow;
            }

            SavePersistentLoginLocationList(locations);
        }

        private static List<LoginLocationItem> CloneLoginLocationList(IEnumerable<LoginLocationItem> locations)
        {
            return (locations ?? Enumerable.Empty<LoginLocationItem>())
                .Select(location => new LoginLocationItem
                {
                    LocationCode = location.LocationCode,
                    CustomerCode = location.CustomerCode,
                    LocationTitle = location.LocationTitle,
                    LocationType = location.LocationType,
                    LocationTypeName = location.LocationTypeName
                })
                .ToList();
        }

        private static List<LoginLocationItem> LoadPersistentLoginLocationList()
        {
            try
            {
                string cachePath = GetLoginLocationCacheFilePath();
                if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
                {
                    return new List<LoginLocationItem>();
                }

                string cacheJson = File.ReadAllText(cachePath);
                JArray rows = JArray.Parse(cacheJson);
                var locations = new List<LoginLocationItem>();

                foreach (JToken row in rows)
                {
                    locations.Add(new LoginLocationItem
                    {
                        LocationCode = Convert.ToString(row["LocationCode"]),
                        CustomerCode = Convert.ToString(row["CustomerCode"]),
                        LocationTitle = Convert.ToString(row["LocationTitle"]),
                        LocationType = Convert.ToString(row["LocationType"]),
                        LocationTypeName = Convert.ToString(row["LocationTypeName"])
                    });
                }

                return locations
                    .Where(location => !string.IsNullOrWhiteSpace(location.LocationCode))
                    .GroupBy(location => location.LocationCode)
                    .Select(group => group.First())
                    .OrderBy(location => location.LocationTitle)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Login location persistent cache read failed: " + ex);
                return new List<LoginLocationItem>();
            }
        }

        private static void SavePersistentLoginLocationList(IEnumerable<LoginLocationItem> locations)
        {
            try
            {
                List<LoginLocationItem> locationList = CloneLoginLocationList(locations);
                if (locationList.Count == 0)
                {
                    return;
                }

                string cachePath = GetLoginLocationCacheFilePath();
                if (string.IsNullOrWhiteSpace(cachePath))
                {
                    return;
                }

                string cacheDirectory = Path.GetDirectoryName(cachePath);
                if (!Directory.Exists(cacheDirectory))
                {
                    Directory.CreateDirectory(cacheDirectory);
                }

                var rows = new JArray(locationList.Select(location => new JObject
                {
                    ["LocationCode"] = location.LocationCode ?? string.Empty,
                    ["CustomerCode"] = location.CustomerCode ?? string.Empty,
                    ["LocationTitle"] = location.LocationTitle ?? string.Empty,
                    ["LocationType"] = location.LocationType ?? string.Empty,
                    ["LocationTypeName"] = location.LocationTypeName ?? string.Empty
                }));

                File.WriteAllText(cachePath, rows.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Login location persistent cache write failed: " + ex);
            }
        }

        private static string GetLoginLocationCacheFilePath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return string.IsNullOrWhiteSpace(baseDirectory)
                ? string.Empty
                : Path.Combine(baseDirectory, "App_Data", LoginLocationCacheFileName);
        }
        public bool ValidateLocationAccess(string userCode, string locationCode)
        {
            if (string.IsNullOrWhiteSpace(userCode) || string.IsNullOrWhiteSpace(locationCode))
            {
                return false;
            }

            if (!HasValidConnectionString)
            {
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Validation_LocationAccess", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("P_UserCode", MySqlDbType.VarChar).Value = userCode.Trim();
                    command.Parameters.Add("P_LocationCode", MySqlDbType.VarChar).Value = locationCode.Trim();
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() && ReadInt32(reader, "IsValid") > 0;
                    }
                }
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        public long InsertUserLoginHistory(LoginUser loginUser, LoginLocationItem selectedLocation, string ipAddress, string auditTrail)
        {
            if (loginUser == null || string.IsNullOrWhiteSpace(loginUser.UserCode))
            {
                return 0;
            }

            if (!HasValidConnectionString)
            {
                return 0;
            }

            string userName = !string.IsNullOrWhiteSpace(loginUser.UserFullName)
                ? loginUser.UserFullName
                : loginUser.UserName;
            string loginLocation = selectedLocation == null
                ? string.Empty
                : (!string.IsNullOrWhiteSpace(selectedLocation.LocationTitle) ? selectedLocation.LocationTitle : selectedLocation.LocationCode);

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_IU_UserLoginHistory", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("P_UserCode", MySqlDbType.VarChar).Value = loginUser.UserCode.Trim();
                    command.Parameters.Add("P_UserName", MySqlDbType.VarChar).Value = string.IsNullOrWhiteSpace(userName) ? loginUser.UserCode : userName.Trim();
                    command.Parameters.Add("P_LoginLocation", MySqlDbType.VarChar).Value = string.IsNullOrWhiteSpace(loginLocation) ? (object)DBNull.Value : loginLocation.Trim();
                    command.Parameters.Add("P_IPAddress", MySqlDbType.VarChar).Value = string.IsNullOrWhiteSpace(ipAddress) ? (object)DBNull.Value : ipAddress.Trim();
                    command.Parameters.Add("P_AuditTrail", MySqlDbType.Text).Value = string.IsNullOrWhiteSpace(auditTrail) ? (object)DBNull.Value : auditTrail;
                    connection.Open();
                    object historyId = command.ExecuteScalar();
                    long parsedHistoryId;
                    return long.TryParse(Convert.ToString(historyId), out parsedHistoryId) ? parsedHistoryId : 0;
                }
            }
            catch (MySqlException)
            {
                return 0;
            }
        }

        public bool UpdateUserLoginHistoryLogout(long loginHistoryId, string userCode)
        {
            if (loginHistoryId <= 0 && string.IsNullOrWhiteSpace(userCode))
            {
                return false;
            }

            if (!HasValidConnectionString)
            {
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_U_UserLoginHistoryLogout", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("P_UserLoginHistoryID", MySqlDbType.Int64).Value = loginHistoryId <= 0 ? (object)DBNull.Value : loginHistoryId;
                    command.Parameters.Add("P_UserCode", MySqlDbType.VarChar).Value = string.IsNullOrWhiteSpace(userCode) ? (object)DBNull.Value : userCode.Trim();
                    connection.Open();
                    command.ExecuteNonQuery();
                    return true;
                }
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        public List<SkuListItem> LoadSkuList()
        {
            var skuList = new List<SkuListItem>();

            if (!HasValidConnectionString)
            {
                return skuList;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("app_get_SKU", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            skuList.Add(new SkuListItem
                            {
                                SKEWCode = ReadString(reader, "SKEWCode"),
                                Weight = ReadNullableDecimal(reader, "Weight"),
                                MemberSize = ReadString(reader, "MemberSize"),
                                Active = ReadString(reader, "Active"),
                                TextColumn = ReadString(reader, "TextColumn")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return skuList;
            }

            return skuList;
        }

        public SkewDetailsItem LoadSkewDetails(string skewCode)
        {
            if (ContainsHtmlInjection(skewCode) || !HasValidConnectionString)
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_SkewDetailsByCode", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_SKEWCode", skewCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new SkewDetailsItem
                        {
                            Weight = ReadNullableDecimal(reader, "Weight"),
                            MemberType = ReadString(reader, "MemberType"),
                            MemberSize = ReadString(reader, "MemberSize")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public bool CheckMemberExists(string rfid)
        {
            if (ContainsHtmlInjection(rfid) || !HasValidConnectionString)
            {
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_usp_CheckMemberExists", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() && ReadInt32(reader, "ISValid") > 0;
                    }
                }
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        public MemberTagging.MemberDetails LoadMemberForTagging(string memberPlate)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(memberPlate);
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(scanValue))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("App_get_Membercode", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", scanValue);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        string memberCode = reader.FieldCount == 0 ? string.Empty : ReadString(reader, 0);
                        if (string.IsNullOrWhiteSpace(memberCode)
                            || string.Equals(memberCode, "Member Not Manufactured", StringComparison.OrdinalIgnoreCase))
                        {
                            return null;
                        }

                        return new MemberTagging.MemberDetails
                        {
                            MemberCode = memberCode,
                            Rfid = scanValue
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public MemberTagging.SaveResult SaveMemberTagging(MemberTagging.TaggingRequest request, string userCode, string userName)
        {
            if (!HasValidConnectionString || request == null)
            {
                return new MemberTagging.SaveResult { Success = false, Message = "Unable to save member tagging." };
            }

            string memberCode = string.IsNullOrWhiteSpace(request.MemberCode) ? string.Empty : request.MemberCode.Trim();
            var stickerCodes = (request.StickerCodes ?? new List<string>())
                .Select(QrOcrHelper.NormalizeQrScanValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.IsNullOrWhiteSpace(memberCode))
            {
                return new MemberTagging.SaveResult { Success = false, Message = "Please scan member plate and get MemberCode." };
            }

            if (stickerCodes.Count == 0)
            {
                return new MemberTagging.SaveResult { Success = false, Message = "Please add at least one QR Sticker." };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    foreach (string stickerCode in stickerCodes)
                    {
                        using (var command = new MySqlCommand("WebApp_IU_MemberTagging", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.AddWithValue("@P_RFID", stickerCode);
                            command.Parameters.AddWithValue("@P_MemberId", memberCode);
                            command.Parameters.AddWithValue("@P_Location", "Sanaswadi");
                            command.Parameters.AddWithValue("@P_EntryBy", string.IsNullOrWhiteSpace(userCode) ? string.Empty : userCode.Trim());
                            command.Parameters.AddWithValue("@P_Image", string.Empty);
                            command.Parameters.AddWithValue("@P_ImagePath", string.Empty);

                            string message = string.Empty;
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read() && reader.FieldCount > 0)
                                {
                                    message = ReadString(reader, 0);
                                }
                            }

                            if (!string.Equals(message, "Member Added", StringComparison.OrdinalIgnoreCase))
                            {
                                return new MemberTagging.SaveResult
                                {
                                    Success = false,
                                    Message = string.IsNullOrWhiteSpace(message) ? "Something went wrong while saving member tagging." : message
                                };
                            }
                        }
                    }
                }

                return new MemberTagging.SaveResult
                {
                    Success = true,
                    TotalRecords = stickerCodes.Count,
                    Message = "Member Added"
                };
            }
            catch (Exception)
            {
                return new MemberTagging.SaveResult { Success = false, Message = "Something went wrong while saving member tagging." };
            }
        }
        public NewBatchNumberStatus GetNewBatchNumberStatus(string batchNo)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(batchNo);
            if (ContainsHtmlInjection(batchNo, scanValue))
            {
                return new NewBatchNumberStatus { Exists = false, TableAvailable = HasValidConnectionString, Message = "HTML markup is not allowed in New Batch No." };
            }

            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(scanValue))
            {
                return new NewBatchNumberStatus { Exists = false, TableAvailable = HasValidConnectionString, Message = "New Batch No is required." };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    bool hasUploadTable;
                    BatchNumberUploadSource source = ResolveBatchNumberUploadSource(connection, scanValue, out hasUploadTable);
                    if (!hasUploadTable)
                    {
                        return new NewBatchNumberStatus { Exists = false, TableAvailable = false, Message = "New batch number master table not found." };
                    }

                    if (source == null)
                    {
                        return new NewBatchNumberStatus { Exists = false, TableAvailable = true, Message = "New Batch No not uploaded." };
                    }

                    using (var command = new MySqlCommand("SELECT * FROM `" + source.TableName + "` WHERE `" + source.BatchColumn + "` = @BatchNo LIMIT 1", connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@BatchNo", scanValue);

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return new NewBatchNumberStatus { Exists = false, TableAvailable = true, Message = "New Batch No not uploaded." };
                            }

                            string activeColumn = ResolveColumn(source.Columns, "Active", "IsActive");
                            string usedColumn = ResolveColumn(source.Columns, "Used", "IsUsed");
                            bool isActive = string.IsNullOrWhiteSpace(activeColumn) || IsTrueValue(ReadString(reader, activeColumn));
                            bool isUsed = !string.IsNullOrWhiteSpace(usedColumn) && IsBatchUsedValue(ReadString(reader, usedColumn));

                            return new NewBatchNumberStatus
                            {
                                Exists = true,
                                TableAvailable = true,
                                Active = isActive,
                                Used = isUsed,
                                Message = !isActive ? "New Batch No is not active." : (isUsed ? "New Batch No already used." : string.Empty)
                            };
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                return new NewBatchNumberStatus { Exists = false, TableAvailable = false, Message = ex.Message };
            }
        }

        public bool TryMarkNewBatchNumberUsed(string batchNo, string userCode, string ipAddress, out string message)
        {
            message = string.Empty;
            string scanValue = QrOcrHelper.NormalizeQrScanValue(batchNo);
            if (ContainsHtmlInjection(batchNo, scanValue))
            {
                message = "HTML markup is not allowed in New Batch No.";
                return false;
            }

            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(scanValue))
            {
                message = "New Batch No is required.";
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    bool hasUploadTable;
                    BatchNumberUploadSource source = ResolveBatchNumberUploadSource(connection, scanValue, out hasUploadTable);
                    if (!hasUploadTable)
                    {
                        message = "New batch number master table not found.";
                        return false;
                    }

                    if (source == null)
                    {
                        message = "New Batch No not uploaded.";
                        return false;
                    }

                    string usedColumn = ResolveColumn(source.Columns, "Used", "IsUsed");
                    if (string.IsNullOrWhiteSpace(usedColumn))
                    {
                        return true;
                    }

                    string reserveValue = string.Equals(source.TableName, "batchnumbermaster", StringComparison.OrdinalIgnoreCase) ? "INUSE" : "Yes";
                    var setParts = new List<string> { "`" + usedColumn + "` = @UsedValue" };
                    AddColumnSetIfExists(setParts, source.Columns, "UseDateTime", "NOW()");
                    AddColumnSetIfExists(setParts, source.Columns, "UsedDateTime", "NOW()");
                    AddColumnSetIfExists(setParts, source.Columns, "UsedBy", "@UserCode");
                    AddColumnSetIfExists(setParts, source.Columns, "ModifiedDate", "NOW()");
                    AddColumnSetIfExists(setParts, source.Columns, "ModifiedUser", "@UserCode");
                    AddColumnSetIfExists(setParts, source.Columns, "ModifiedBy", "@UserCode");
                    AddColumnSetIfExists(setParts, source.Columns, "ModifiedIP", "@IPAdd");
                    AddColumnSetIfExists(setParts, source.Columns, "IPAdd", "@IPAdd");

                    string activeColumn = ResolveColumn(source.Columns, "Active", "IsActive");
                    string where = " WHERE `" + source.BatchColumn + "` = @BatchNo AND (`" + usedColumn + "` IS NULL OR `" + usedColumn + "` = '' OR UPPER(`" + usedColumn + "`) IN ('NO','FALSE','0'))";
                    if (!string.IsNullOrWhiteSpace(activeColumn))
                    {
                        where += " AND UPPER(`" + activeColumn + "`) IN ('TRUE','YES','1')";
                    }

                    using (var command = new MySqlCommand("UPDATE `" + source.TableName + "` SET " + string.Join(", ", setParts) + where, connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@UsedValue", reserveValue);
                        command.Parameters.AddWithValue("@BatchNo", scanValue);
                        command.Parameters.AddWithValue("@UserCode", string.IsNullOrWhiteSpace(userCode) ? "SYSTEM" : userCode);
                        command.Parameters.AddWithValue("@IPAdd", string.IsNullOrWhiteSpace(ipAddress) ? string.Empty : ipAddress);
                        if (command.ExecuteNonQuery() == 1)
                        {
                            return true;
                        }

                        message = "New Batch No already used.";
                        return false;
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public void ReleaseNewBatchNumber(string batchNo, string userCode, string ipAddress)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(batchNo);
            if (ContainsHtmlInjection(batchNo, scanValue) || !HasValidConnectionString || string.IsNullOrWhiteSpace(scanValue))
            {
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    bool hasUploadTable;
                    BatchNumberUploadSource source = ResolveBatchNumberUploadSource(connection, scanValue, out hasUploadTable);
                    if (source == null)
                    {
                        return;
                    }

                    string usedColumn = ResolveColumn(source.Columns, "Used", "IsUsed");
                    if (string.IsNullOrWhiteSpace(usedColumn))
                    {
                        return;
                    }

                    var setParts = new List<string> { "`" + usedColumn + "` = NULL" };
                    if (!string.Equals(source.TableName, "batchnumbermaster", StringComparison.OrdinalIgnoreCase))
                    {
                        setParts[0] = "`" + usedColumn + "` = 'No'";
                    }

                    AddColumnSetIfExists(setParts, source.Columns, "UseDateTime", "NULL");
                    AddColumnSetIfExists(setParts, source.Columns, "UsedDateTime", "NULL");
                    AddColumnSetIfExists(setParts, source.Columns, "UsedBy", "NULL");
                    AddColumnSetIfExists(setParts, source.Columns, "ModifiedDate", "NOW()");
                    AddColumnSetIfExists(setParts, source.Columns, "ModifiedUser", "@UserCode");
                    AddColumnSetIfExists(setParts, source.Columns, "ModifiedBy", "@UserCode");
                    AddColumnSetIfExists(setParts, source.Columns, "ModifiedIP", "@IPAdd");
                    AddColumnSetIfExists(setParts, source.Columns, "IPAdd", "@IPAdd");

                    using (var command = new MySqlCommand("UPDATE `" + source.TableName + "` SET " + string.Join(", ", setParts) + " WHERE `" + source.BatchColumn + "` = @BatchNo", connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@BatchNo", scanValue);
                        command.Parameters.AddWithValue("@UserCode", string.IsNullOrWhiteSpace(userCode) ? "SYSTEM" : userCode);
                        command.Parameters.AddWithValue("@IPAdd", string.IsNullOrWhiteSpace(ipAddress) ? string.Empty : ipAddress);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (MySqlException)
            {
            }
        }
        public string LoadOriginalRfidByTagRfid(string tagRfid)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (ContainsHtmlInjection(tagRfid, scanValue) || !HasValidConnectionString || string.IsNullOrWhiteSpace(scanValue))
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("SELECT OrignalRFID FROM membertagging WHERE TagRFID = @TagRFID LIMIT 1", connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@TagRFID", scanValue);
                    connection.Open();
                    object value = command.ExecuteScalar();
                    return value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value);
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        public ExistingMemberForBatchMapping LoadExistingMemberForBatchMapping(string rfid)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(rfid);
            if (ContainsHtmlInjection(rfid, scanValue) || !HasValidConnectionString || string.IsNullOrWhiteSpace(scanValue))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("SELECT MemberCode, RFID, SKEWCode FROM membermaster WHERE RFID = @RFID AND UPPER(Active) = 'TRUE' LIMIT 1", connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@RFID", scanValue);
                    connection.Open();
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new ExistingMemberForBatchMapping
                        {
                            MemberCode = ReadString(reader, "MemberCode"),
                            Rfid = ReadString(reader, "RFID"),
                            SkuCode = ReadString(reader, "SKEWCode")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public MemberSaveResult SaveExistingMemberForBatchMapping(MemberMasterSaveRequest request, string userCode, string userName, string ipAddress)
        {
            if (!HasValidConnectionString || request == null)
            {
                return new MemberSaveResult { Message = "Unable to update member." };
            }
            if (ContainsHtmlInjection(request.MemberMode, request.QrCode, request.ExistingRfid, request.ExistingMemberCode, request.Sku, request.OldBatchNo, request.NewBatchNo, request.MemberTypeCode, request.MemberWeight, request.SkuMemberSize, request.ProductionLocation, request.ProductionLocationTitle, request.Remarks))
            {
                return new MemberSaveResult { Message = "HTML markup is not allowed in Member Master fields." };
            }

            string memberCode = string.IsNullOrWhiteSpace(request.ExistingMemberCode) ? string.Empty : request.ExistingMemberCode.Trim();
            string productionLocationCode = GetMemberProductionLocationCode(request);
            string productionLocationTitle = GetMemberProductionLocationTitle(request);
            string oldRfid = QrOcrHelper.NormalizeQrScanValue(request.ExistingRfid);
            string newRfid = QrOcrHelper.NormalizeQrScanValue(request.NewBatchNo);
            if (string.IsNullOrWhiteSpace(memberCode) || string.IsNullOrWhiteSpace(oldRfid) || string.IsNullOrWhiteSpace(newRfid))
            {
                return new MemberSaveResult { Message = "Member not found." };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int memberRows;
                            using (var command = new MySqlCommand(@"UPDATE membermaster
SET RFID = @NewRFID,
    BatchNo = @BatchNo,
    SKEWCode = @SKEWCode,
    MemberType = @MemberType,
    MemberWeight = @MemberWeight,
    MemberDimension = @MemberDimension,
    ProductionLoc = @ProductionLoc,
    CurrentLocation = @CurrentLocation,
    LocationTypeCode = @LocationTypeCode,
    Unit = @Unit,
    Remarks = @Remarks,
    OldRFID = @OldRFID,
    RFIDChangeReason = @RFIDChangeReason,
    RFIDChangeDt = NOW(),
    RFIDChangeUser = @UserCode,
    ModifiedBy = @UserCode,
    ModifiedDate = NOW(),
    ModifiedIP = @IPAdd
WHERE MemberCode = @MemberCode AND RFID = @OldRFID AND UPPER(Active) = 'TRUE'", connection, transaction))
                            {
                                command.CommandType = CommandType.Text;
                                command.Parameters.AddWithValue("@NewRFID", newRfid);
                                command.Parameters.AddWithValue("@BatchNo", newRfid);
                                command.Parameters.AddWithValue("@SKEWCode", request.Sku.Trim());
                                command.Parameters.AddWithValue("@MemberType", request.MemberTypeCode.Trim());
                                command.Parameters.AddWithValue("@MemberWeight", request.MemberWeight.Trim());
                                command.Parameters.AddWithValue("@MemberDimension", request.SkuMemberSize.Trim());
                                command.Parameters.AddWithValue("@ProductionLoc", productionLocationCode);
                                command.Parameters.AddWithValue("@CurrentLocation", productionLocationCode);
                                command.Parameters.AddWithValue("@LocationTypeCode", "MISC0000019");
                                command.Parameters.AddWithValue("@Unit", "MISC0000026");
                                command.Parameters.AddWithValue("@Remarks", "Batch No Mapping");
                                command.Parameters.AddWithValue("@OldRFID", oldRfid);
                                command.Parameters.AddWithValue("@RFIDChangeReason", "Batch No Mapping");
                                command.Parameters.AddWithValue("@UserCode", string.IsNullOrWhiteSpace(userCode) ? "SYSTEM" : userCode);
                                command.Parameters.AddWithValue("@IPAdd", string.IsNullOrWhiteSpace(ipAddress) ? string.Empty : ipAddress);
                                command.Parameters.AddWithValue("@MemberCode", memberCode);
                                memberRows = command.ExecuteNonQuery();
                            }

                            if (memberRows == 0)
                            {
                                transaction.Rollback();
                                return new MemberSaveResult { Message = "Existing Batch No not found in Member Master." };
                            }

                            using (var command = new MySqlCommand(@"UPDATE memberlastupdate
SET RFID = @NewRFID,
    LastLocation = @LocationTitle,
    LastEntryBy = @UserName,
    LastStatus = @LastStatus,
    LastTransferTo = @LocationTitle,
    LastTransactionId = @MemberCode,
    MemberId = @MemberCode
WHERE MemberId = @MemberCode OR LastTransactionId = @MemberCode OR RFID = @OldRFID", connection, transaction))
                            {
                                command.CommandType = CommandType.Text;
                                command.Parameters.AddWithValue("@NewRFID", newRfid);
                                command.Parameters.AddWithValue("@LocationTitle", productionLocationTitle);
                                command.Parameters.AddWithValue("@UserName", string.IsNullOrWhiteSpace(userName) ? userCode : userName);
                                command.Parameters.AddWithValue("@LastStatus", "Manufacture");
                                command.Parameters.AddWithValue("@MemberCode", memberCode);
                                command.Parameters.AddWithValue("@OldRFID", oldRfid);
                                command.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return new MemberSaveResult { MemberCode = memberCode, Message = "Member Updated Successfully" };
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                return new MemberSaveResult { Message = ex.Message };
            }
        }

        public bool UpdateMemberTaggingOriginalRfid(string oldRfid, string newRfid, out string message)
        {
            message = string.Empty;
            string oldValue = QrOcrHelper.NormalizeQrScanValue(oldRfid);
            string newValue = QrOcrHelper.NormalizeQrScanValue(newRfid);
            if (ContainsHtmlInjection(oldRfid, newRfid, oldValue, newValue))
            {
                message = "HTML markup is not allowed in Member Master fields.";
                return false;
            }
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(oldValue) || string.IsNullOrWhiteSpace(newValue))
            {
                message = "Member tagging details not found.";
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("UPDATE membertagging SET OrignalRFID = @NewRFID WHERE OrignalRFID = @OldRFID", connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@NewRFID", newValue);
                    command.Parameters.AddWithValue("@OldRFID", oldValue);
                    connection.Open();
                    if (command.ExecuteNonQuery() == 0)
                    {
                        message = "Member tagging details not found.";
                        return false;
                    }

                    return true;
                }
            }
            catch (MySqlException ex)
            {
                message = ex.Message;
                return false;
            }
        }
        public MemberSaveResult SaveMember(MemberMasterSaveRequest request, string userCode, string userName, string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return null;
            }
            if (request == null || ContainsHtmlInjection(request.MemberMode, request.QrCode, request.ExistingRfid, request.ExistingMemberCode, request.Sku, request.OldBatchNo, request.NewBatchNo, request.MemberTypeCode, request.MemberWeight, request.SkuMemberSize, request.ProductionLocation, request.ProductionLocationTitle, request.Remarks))
            {
                return new MemberSaveResult { Message = "HTML markup is not allowed in Member Master fields." };
            }

            string productionLocationCode = GetMemberProductionLocationCode(request);
            string productionLocationTitle = GetMemberProductionLocationTitle(request);
            DateTime currentDate = DateTime.Now;

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_IU_MemberMaster", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_MemberCode", "0");
                    command.Parameters.AddWithValue("@P_SKEWCode", request.Sku.Trim());
                    command.Parameters.AddWithValue("@P_BatchNo", string.IsNullOrWhiteSpace(request.NewBatchNo) ? string.Empty : request.NewBatchNo.Trim());
                    command.Parameters.AddWithValue("@P_BatchDate", currentDate);
                    command.Parameters.AddWithValue("@P_MemberType", request.MemberTypeCode.Trim());
                    command.Parameters.AddWithValue("@P_MemberCategory", string.Empty);
                    command.Parameters.AddWithValue("@P_MemberWeight", request.MemberWeight.Trim());
                    command.Parameters.AddWithValue("@P_ProductionLoc", productionLocationCode);
                    command.Parameters.AddWithValue("@P_ProductionDate", currentDate);
                    command.Parameters.AddWithValue("@P_MemberExpiryDate", currentDate);
                    command.Parameters.AddWithValue("@P_CurrentLocation", productionLocationCode);
                    command.Parameters.AddWithValue("@P_LocationTypeCode", "MISC0000019");
                    command.Parameters.AddWithValue("@P_LocationTitle", productionLocationTitle);
                    command.Parameters.AddWithValue("@P_RFID", QrOcrHelper.NormalizeQrScanValue(request.OldBatchNo));
                    command.Parameters.AddWithValue("@P_Dimention", request.SkuMemberSize.Trim());
                    command.Parameters.AddWithValue("@P_Unit", "MISC0000026");
                    command.Parameters.AddWithValue("@P_Remarks", string.IsNullOrWhiteSpace(request.Remarks) ? string.Empty : request.Remarks.Trim());
                    command.Parameters.AddWithValue("@P_Active", "True");
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    command.Parameters.AddWithValue("@P_UserName", userName);
                    command.Parameters.AddWithValue("@P_IPAdd", ipAddress);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new MemberSaveResult
                        {
                            MemberCode = ReadString(reader, "MemberCode"),
                            Message = ReadString(reader, "Message")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public List<PalletOrder.CustomerListItem> LoadCustomerList(string userCode)
        {
            var customers = new List<PalletOrder.CustomerListItem>();

            if (!HasValidConnectionString)
            {
                return customers;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_get_CustomerList", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            customers.Add(new PalletOrder.CustomerListItem
                            {
                                CustomerCode = ReadString(reader, "CustomerCode"),
                                CustName = ReadString(reader, "CustName"),
                                CustType = ReadString(reader, "CustType"),
                                LocationCode = ReadString(reader, "LocationCode")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return customers;
            }

            return customers;
        }

        public List<PalletAssemble.CustomerListItem> LoadPalletAssembleCustomerList(string userCode)
        {
            var customers = new List<PalletAssemble.CustomerListItem>();

            if (!HasValidConnectionString)
            {
                return customers;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_CustomerList", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            customers.Add(new PalletAssemble.CustomerListItem
                            {
                                CustomerCode = ReadString(reader, "CustomerCode"),
                                CustName = ReadString(reader, "CustName"),
                                CustType = ReadString(reader, "CustType"),
                                LocationCode = ReadString(reader, "LocationCode")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return customers;
            }

            return customers;
        }

        public List<PalletAssemble.PalletOrderDetailItem> LoadPalletAssembleOrderDetails(string customerCode)
        {
            var orders = new List<PalletAssemble.PalletOrderDetailItem>();

            if (!HasValidConnectionString)
            {
                return orders;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_PalletOrderDtls", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_CustCode", customerCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(new PalletAssemble.PalletOrderDetailItem
                            {
                                PalletOrderCode = ReadString(reader, "PalletOrderCode"),
                                PalletOrderdtlCode = ReadString(reader, "PalletOrderdtlCode"),
                                OrderCounts = ReadString(reader, "OrderCounts"),
                                AssembleCounts = ReadString(reader, "AssembleCounts"),
                                Customer = ReadString(reader, "Customer"),
                                PalletShape = ReadString(reader, "PalletShape"),
                                SheetWidth = ReadString(reader, "SheetWidth"),
                                SheetLength1 = ReadString(reader, "SheetLength1"),
                                SheetLength2 = ReadString(reader, "SheetLength2"),
                                CMSize = ReadString(reader, "CMSize"),
                                LM1Size = ReadString(reader, "LM1Size"),
                                LM2Size = ReadString(reader, "LM2Size"),
                                CMSizeQty = ReadString(reader, "CMSizeQty"),
                                LM1SizeQty = ReadString(reader, "LM1SizeQty"),
                                LM2SizeQty = ReadString(reader, "LM2SizeQty"),
                                TotalMemQty = ReadString(reader, "Total_MemQty"),
                                PendingCount = ReadString(reader, "Pending_Count")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return orders;
            }

            return orders;
        }

        public PalletAssemble.AssembleMemberValidationResult ValidatePalletAssembleMember(string tagRfid, string currentLocation)
        {
            string rfid = LoadRfidBySticker(tagRfid);
            if (string.IsNullOrWhiteSpace(rfid))
            {
                return new PalletAssemble.AssembleMemberValidationResult
                {
                    IsValid = false,
                    Message = "Member not found or tag is not mapped."
                };
            }

            PalletAssemble.AssembleMemberValidationResult member = LoadPalletAssembleCurrentMember(rfid);
            bool isValid = member != null && !string.IsNullOrWhiteSpace(member.Rfid);

            if (!isValid)
            {
                return new PalletAssemble.AssembleMemberValidationResult
                {
                    IsValid = false,
                    Rfid = rfid,
                    Message = "Member status not valid."
                };
            }

            string lastLocation = LoadMemberLastLocation(rfid);
            string location = string.IsNullOrWhiteSpace(currentLocation) ? string.Empty : currentLocation.Trim();
            bool locationMismatch = !string.IsNullOrWhiteSpace(location)
                && !string.IsNullOrWhiteSpace(lastLocation)
                && !string.Equals(lastLocation.Trim(), location, StringComparison.OrdinalIgnoreCase);
            string lastStatus = LoadMemberLatestStatus(rfid);
            bool isPalletAssembleStatus = string.Equals(lastStatus, "Pallet Assemble", StringComparison.OrdinalIgnoreCase);
            PalletPreDispatch.PreDispatchValidationResult dateValidation = LoadPalletAssembleDateValidation(rfid.Trim());
            bool isWithinThreeDayWindow = dateValidation != null
                && string.Equals(dateValidation.PalletStatus, "ALLOWED", StringComparison.OrdinalIgnoreCase);

            if (isPalletAssembleStatus && isWithinThreeDayWindow)
            {
                return new PalletAssemble.AssembleMemberValidationResult
                {
                    IsValid = false,
                    Rfid = rfid,
                    MemberCode = member.MemberCode,
                    MemberType = member.MemberType,
                    MemberSize = member.MemberSize,
                    LastLocation = lastLocation,
                    CurrentLocation = location,
                    LocationMismatch = locationMismatch,
                    Message = locationMismatch
                        ? "Pallet created within 3 days. Location mismatch bypass is not allowed."
                        : "Member already in Pallet Assemble status."
                };
            }

            if (!isPalletAssembleStatus && isWithinThreeDayWindow)
            {
                return new PalletAssemble.AssembleMemberValidationResult
                {
                    IsValid = false,
                    Rfid = rfid,
                    MemberCode = member.MemberCode,
                    MemberType = member.MemberType,
                    MemberSize = member.MemberSize,
                    LastLocation = lastLocation,
                    CurrentLocation = location,
                    LocationMismatch = locationMismatch,
                    Message = "Pallet already created within 3 days"
                };
            }

            return new PalletAssemble.AssembleMemberValidationResult
            {
                IsValid = true,
                Message = "Member validated successfully.",
                Rfid = member.Rfid,
                MemberCode = member.MemberCode,
                MemberType = member.MemberType,
                MemberSize = member.MemberSize,
                LastLocation = lastLocation,
                CurrentLocation = location,
                LocationMismatch = locationMismatch,
                RequiresBypass = isPalletAssembleStatus
            };
        }

        public PalletAssemble.AssembleBypassResult ContinuePalletAssembleBypass(
            string rfid,
            string palletOrderdtlCode,
            string currentLocation,
            string userCode,
            string userName)
        {
            if (string.IsNullOrWhiteSpace(rfid))
            {
                return new PalletAssemble.AssembleBypassResult { Success = false, Message = "Member RFID is required." };
            }

            PalletPreDispatch.PreDispatchValidationResult dateValidation = LoadPalletAssembleDateValidation(rfid.Trim());
            if (dateValidation != null
                && string.Equals(dateValidation.PalletStatus, "ALLOWED", StringComparison.OrdinalIgnoreCase))
            {
                return new PalletAssemble.AssembleBypassResult { Success = false, Message = "Pallet created within 3 days. Pallet Assemble is not allowed." };
            }

            string location = string.IsNullOrWhiteSpace(currentLocation) ? string.Empty : currentLocation.Trim();
            ExecuteMemberViolationMark(rfid.Trim(), location, userName);
            string updateMessage = ExecutePalletAssembleUpdate(rfid.Trim(), palletOrderdtlCode, userCode, userName, location);

            return new PalletAssemble.AssembleBypassResult
            {
                Success = true,
                Message = string.IsNullOrWhiteSpace(updateMessage) ? "Member bypass completed successfully." : updateMessage
            };
        }

        public PalletAssemble.AssembleSaveResult SavePalletAssemble(PalletAssemble.AssembleSaveRequest request, string userCode, string userName, string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return new PalletAssemble.AssembleSaveResult { Success = false, Message = "Database connection is not configured." };
            }

            if (request == null || string.IsNullOrWhiteSpace(request.ClientLocation))
            {
                return new PalletAssemble.AssembleSaveResult { Success = false, Message = "Client location is required." };
            }

            if (string.IsNullOrWhiteSpace(request.PalletOrderCode) || string.IsNullOrWhiteSpace(request.PalletOrderdtlCode))
            {
                return new PalletAssemble.AssembleSaveResult { Success = false, Message = "Pallet order details are required." };
            }

            List<PalletAssemble.AssembleSaveMemberInput> members = CleanPalletAssembleMembers(request.Members);
            if (!members.Any())
            {
                return new PalletAssemble.AssembleSaveResult { Success = false, Message = "Please scan at least one member." };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            using (var command = new MySqlCommand("WebApp_Iu_palletAssemble", connection, transaction))
                            {
                                command.CommandType = CommandType.StoredProcedure;
                                command.Parameters.AddWithValue("@P_PalletCode", "0");
                                command.Parameters.AddWithValue("@P_TotalMember", string.IsNullOrWhiteSpace(request.TotalMember) ? "0" : request.TotalMember.Trim());
                                command.Parameters.AddWithValue("@P_ClientLocation", request.ClientLocation.Trim());
                                command.Parameters.AddWithValue("@P_Active", "True");
                                command.Parameters.AddWithValue("@P_CreatedBy", string.IsNullOrWhiteSpace(userCode) ? string.Empty : userCode.Trim());
                                command.Parameters.AddWithValue("@P_IPAdd", string.IsNullOrWhiteSpace(ipAddress) ? string.Empty : ipAddress.Trim());
                                command.Parameters.AddWithValue("@P_PalletOrderCode", request.PalletOrderCode.Trim());
                                command.Parameters.AddWithValue("@P_PalletOrderdtlCode", request.PalletOrderdtlCode.Trim());
                                command.Parameters.AddWithValue("@P_SheetWidth", string.IsNullOrWhiteSpace(request.SheetWidth) ? string.Empty : request.SheetWidth.Trim());
                                command.Parameters.AddWithValue("@P_SheetLength1", string.IsNullOrWhiteSpace(request.SheetLength1) ? string.Empty : request.SheetLength1.Trim());
                                command.Parameters.AddWithValue("@P_SheetLength2", string.IsNullOrWhiteSpace(request.SheetLength2) ? string.Empty : request.SheetLength2.Trim());

                                using (MySqlDataReader reader = command.ExecuteReader())
                                {
                                    string palletCode = reader.Read() ? ReadString(reader, "PalletCode") : string.Empty;
                                    if (string.IsNullOrWhiteSpace(palletCode))
                                    {
                                        transaction.Rollback();
                                        return new PalletAssemble.AssembleSaveResult { Success = false, Message = "Unable to save Pallet Assemble." };
                                    }

                                    reader.Close();

                                    string detailMessage;
                                    bool detailSaved = SavePalletAssembleDetails(
                                        connection,
                                        transaction,
                                        palletCode,
                                        request.PalletOrderdtlCode,
                                        members,
                                        userCode,
                                        string.IsNullOrWhiteSpace(userName) ? userCode : userName,
                                        ipAddress,
                                        request.ToLocation,
                                        out detailMessage);

                                    if (!detailSaved)
                                    {
                                        transaction.Rollback();
                                        return new PalletAssemble.AssembleSaveResult
                                        {
                                            Success = false,
                                            PalletCode = palletCode,
                                            Message = string.IsNullOrWhiteSpace(detailMessage) ? "Unable to save Pallet Assemble details." : detailMessage
                                        };
                                    }

                                    IncrementPalletAssembleCount(connection, transaction, request.PalletOrderdtlCode);
                                    UpdateCompletedPalletOrderDetailStatus(connection, transaction, request.PalletOrderdtlCode);

                                    transaction.Commit();
                                    return new PalletAssemble.AssembleSaveResult
                                    {
                                        Success = true,
                                        PalletCode = palletCode,
                                        Message = "Pallet Assemble Saved Successfully"
                                    };
                                }
                            }
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                return new PalletAssemble.AssembleSaveResult { Success = false, Message = ex.Message };
            }
        }

        public List<InternalTransfer.LocationItem> LoadInternalTransferLocationList(string locationType, string userCode)
        {
            var locations = new List<InternalTransfer.LocationItem>();

            if (!HasValidConnectionString)
            {
                return locations;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_LocationByType", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_LocationType", locationType);
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            locations.Add(new InternalTransfer.LocationItem
                            {
                                LocationCode = ReadString(reader, "LocationCode"),
                                CustomerCode = ReadString(reader, "CustomerCode"),
                                LocationTitle = ReadString(reader, "LocationTitle"),
                                LocationCity = ReadString(reader, "LocationCity"),
                                LocationType = ReadString(reader, "LocationType"),
                                LocationTypeName = ReadString(reader, "LocationTypeName")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return locations;
            }

            return locations;
        }

        public List<InternalTransfer.CustomerItem> LoadInternalTransferCustomerList(string userCode)
        {
            var customers = new List<InternalTransfer.CustomerItem>();

            if (!HasValidConnectionString)
            {
                return customers;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_get_CustomerList", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            customers.Add(new InternalTransfer.CustomerItem
                            {
                                CustomerCode = ReadString(reader, "CustomerCode"),
                                CustName = ReadString(reader, "CustName"),
                                CustType = ReadString(reader, "CustType"),
                                LocationCode = ReadString(reader, "LocationCode")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return customers;
            }

            return customers;
        }

        public List<MemberReceipt.LocationItem> LoadMemberReceiptLocationList(string locationType, string userCode)
        {
            var locations = new List<MemberReceipt.LocationItem>();

            if (!HasValidConnectionString)
            {
                return locations;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Webapp_get_LocationByType", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_LocationType", locationType);
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            locations.Add(new MemberReceipt.LocationItem
                            {
                                LocationCode = ReadString(reader, "LocationCode"),
                                CustomerCode = ReadString(reader, "CustomerCode"),
                                LocationTitle = ReadString(reader, "LocationTitle"),
                                LocationCity = ReadString(reader, "LocationCity"),
                                LocationType = ReadString(reader, "LocationType"),
                                LocationTypeName = ReadString(reader, "LocationTypeName")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return locations;
            }

            return locations;
        }

        public List<MemberReceipt.CustomerItem> LoadMemberReceiptCustomerList(string userCode)
        {
            var customers = new List<MemberReceipt.CustomerItem>();

            if (!HasValidConnectionString)
            {
                return customers;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_get_CustomerList", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            customers.Add(new MemberReceipt.CustomerItem
                            {
                                CustomerCode = ReadString(reader, "CustomerCode"),
                                CustName = ReadString(reader, "CustName"),
                                CustType = ReadString(reader, "CustType"),
                                LocationCode = ReadString(reader, "LocationCode")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return customers;
            }

            return customers;
        }

        public MemberReceipt.ReceiveValidationResult ValidateMemberReceiptMember(string tagRfid, string currentLocationTitle)
        {
            string rfid = LoadRfidBySticker(tagRfid);
            if (string.IsNullOrWhiteSpace(rfid))
            {
                return new MemberReceipt.ReceiveValidationResult
                {
                    Success = false,
                    Message = "Member Not Tag / Member Not Found",
                    Members = new List<MemberReceipt.ReceiveMemberItem>()
                };
            }

            List<MemberReceipt.ReceiveMemberItem> members = LoadMemberReceiptReceiveData(rfid);
            if (members.Count == 0)
            {
                string message = IsMemberReceiptLocationMismatch(rfid, currentLocationTitle)
                    ? "Member Location Not Match"
                    : "Member Status Not Match";

                return new MemberReceipt.ReceiveValidationResult
                {
                    Success = false,
                    Rfid = rfid,
                    Message = message,
                    Members = members
                };
            }

            if (IsMemberReceiptReceiveDataLocationMismatch(members, currentLocationTitle))
            {
                return new MemberReceipt.ReceiveValidationResult
                {
                    Success = false,
                    Rfid = rfid,
                    Message = "Member Location Not Match",
                    Members = new List<MemberReceipt.ReceiveMemberItem>()
                };
            }

            return new MemberReceipt.ReceiveValidationResult
            {
                Success = true,
                Rfid = rfid,
                Message = "Member validated successfully.",
                Members = members
            };
        }


        private static bool IsMemberReceiptReceiveDataLocationMismatch(IEnumerable<MemberReceipt.ReceiveMemberItem> members, string currentLocationTitle)
        {
            string selectedLocation = NormalizeCompareValue(currentLocationTitle);
            if (string.IsNullOrWhiteSpace(selectedLocation) || members == null)
            {
                return false;
            }

            return members.Any(member =>
            {
                string lastTransferTo = NormalizeCompareValue(member == null ? string.Empty : member.LastTransferTo);
                return !string.IsNullOrWhiteSpace(lastTransferTo)
                    && !string.Equals(lastTransferTo, selectedLocation, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool IsMemberReceiptLocationMismatch(string rfid, string currentLocationTitle)
        {
            if (string.IsNullOrWhiteSpace(rfid) || string.IsNullOrWhiteSpace(currentLocationTitle))
            {
                return false;
            }

            string latestStatus = NormalizeCompareValue(LoadMemberLatestStatus(rfid));
            if (latestStatus != "Internal Transfer" && latestStatus != "Internal Transfer - Damage")
            {
                return false;
            }

            string lastTransferTo = NormalizeCompareValue(LoadMemberLatestTransferTo(rfid));
            string selectedLocation = NormalizeCompareValue(currentLocationTitle);
            return !string.IsNullOrWhiteSpace(lastTransferTo)
                && !string.Equals(lastTransferTo, selectedLocation, StringComparison.OrdinalIgnoreCase);
        }

        public List<MemberReceipt.ReceiveMemberItem> LoadMemberReceiptReceiveData(string rfid)
        {
            var members = new List<MemberReceipt.ReceiveMemberItem>();

            if (!HasValidConnectionString)
            {
                return members;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_ReceiveDataforSingle", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFId", (rfid ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            members.Add(new MemberReceipt.ReceiveMemberItem
                            {
                                Rfid = ReadString(reader, "RFID"),
                                MemberCode = ReadString(reader, "MemberCode"),
                                MemberType = ReadString(reader, "MemberType"),
                                MemberSize = ReadString(reader, "MemberSize"),
                                MatDispCode = ReadString(reader, "MatDispCode"),
                                TruckNo = ReadString(reader, "TruckNo"),
                                LastTransferTo = ReadString(reader, "LastTransferTo")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return members;
            }

            return members;
        }

        public MemberReceipt.ReceiveSaveResult SaveMemberReceipt(MemberReceipt.ReceiveSaveRequest request, string userCode, string userName, string ipAddress)
        {
            List<MemberReceipt.ReceiveMemberInput> members = CleanMemberReceiptMembers(request == null ? null : request.Members);
            if (request == null || !members.Any())
            {
                return new MemberReceipt.ReceiveSaveResult { Success = false, Message = "Please select member." };
            }

            string dispatchCode = (members[0].MatDispCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dispatchCode))
            {
                return new MemberReceipt.ReceiveSaveResult { Success = false, Message = "Dispatch code not found." };
            }

            string materialRecCode = SaveMemberReceiptMaster(request, dispatchCode, members.Count, userCode, ipAddress);
            if (string.IsNullOrWhiteSpace(materialRecCode))
            {
                return new MemberReceipt.ReceiveSaveResult { Success = false, Message = "Unable to save Member Receipt." };
            }

            string detailError;
            int detailCount = SaveMemberReceiptDetails(
                materialRecCode,
                members,
                string.IsNullOrWhiteSpace(userName) ? userCode : userName,
                request.ReceiptLocationName,
                out detailError);

            if (detailCount == 0)
            {
                return new MemberReceipt.ReceiveSaveResult
                {
                    Success = false,
                    MaterialRecCode = materialRecCode,
                    Message = string.IsNullOrWhiteSpace(detailError) ? "Unable to save Member Receipt details." : detailError
                };
            }

            return new MemberReceipt.ReceiveSaveResult
            {
                Success = true,
                MaterialRecCode = materialRecCode,
                DetailCount = detailCount,
                Message = "Member Receipt Saved Successfully"
            };
        }

        public InternalTransfer.MemberValidationResult ValidateInternalTransferMember(string tagRfid, string currentLocation)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(scanValue))
            {
                return new InternalTransfer.MemberValidationResult { IsValid = false, Message = "Member Not Tag / Member Not Found" };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Validate_InternalTransferMember", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_TagRFID", scanValue);
                    command.Parameters.AddWithValue("@P_CurrentLocation", string.IsNullOrWhiteSpace(currentLocation) ? string.Empty : currentLocation.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return new InternalTransfer.MemberValidationResult { IsValid = false, Message = "Unable to validate member." };
                        }

                        string rfid = HasColumn(reader, "RFID") ? ReadString(reader, "RFID") : string.Empty;
                        bool isValid = HasColumn(reader, "IsValid") && ReadInt32(reader, "IsValid") == 1;
                        if (!isValid)
                        {
                            return new InternalTransfer.MemberValidationResult
                            {
                                IsValid = false,
                                Rfid = rfid,
                                Message = string.IsNullOrWhiteSpace(rfid) ? "Member Not Tag / Member Not Found" : "Member status should be Manufacture or Received."
                            };
                        }

                        return new InternalTransfer.MemberValidationResult
                        {
                            IsValid = true,
                            Rfid = rfid,
                            MemberType = ReadString(reader, "MemberType"),
                            MemberSize = ReadString(reader, "MemberSize"),
                            MemberCode = ReadString(reader, "MemberCode"),
                            MemberWeight = ReadString(reader, "MemberWeight"),
                            MemberDimension = ReadString(reader, "MemberDimension"),
                            Active = ReadString(reader, "Active"),
                            Message = "Member validated successfully."
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return new InternalTransfer.MemberValidationResult { IsValid = false, Message = "Unable to validate member." };
            }
        }

        public List<InternalTransfer.TransportItem> LoadTransportList()
        {
            var transports = new List<InternalTransfer.TransportItem>();

            if (!HasValidConnectionString)
            {
                return transports;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_list_transport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            transports.Add(new InternalTransfer.TransportItem
                            {
                                TransportCode = ReadString(reader, "TransportCode"),
                                VehicleNo = ReadString(reader, "VehicleNo"),
                                TransporterName = ReadString(reader, "TransporterName"),
                                DriverName = ReadString(reader, "DriverName"),
                                MobileNo = ReadString(reader, "MobileNo"),
                                Active = ReadString(reader, "Active")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return transports;
            }

            return transports;
        }

        public InternalTransfer.CollectionBulkValidationResult ValidateCollectionInternalTransferMember(string tagRfid, string selectedTruckCode, string selectedTruckNo)
        {
            string rfid = LoadRfidBySticker(tagRfid);
            if (string.IsNullOrWhiteSpace(rfid))
            {
                return new InternalTransfer.CollectionBulkValidationResult
                {
                    IsValid = false,
                    Members = new List<InternalTransfer.TransferMemberInput>(),
                    Message = "Member Not Tag / Member Not Found"
                };
            }

            InternalTransfer.MemberValidationResult member = LoadCollectionInternalTransferCurrentMember(rfid);
            bool isValid = member != null && !string.IsNullOrWhiteSpace(member.Rfid);

            if (!isValid)
            {
                string latestStatus = LoadMemberLatestStatus(rfid);
                string normalizedStatus = NormalizeCompareValue(latestStatus);
                if (normalizedStatus == "INTERNALTRANSFER" || normalizedStatus == "INTERNALTRANSFER-DAMAGE")
                {
                    return new InternalTransfer.CollectionBulkValidationResult
                    {
                        IsValid = false,
                        BypassRequired = false,
                        Rfid = rfid,
                        Members = new List<InternalTransfer.TransferMemberInput>(),
                        Message = "Member Already In Internal Transfer"
                    };
                }

                return new InternalTransfer.CollectionBulkValidationResult
                {
                    IsValid = false,
                    BypassRequired = true,
                    Rfid = rfid,
                    Members = new List<InternalTransfer.TransferMemberInput>(),
                    Message = "Member is not in Member Collection status. Do you want to continue?"
                };
            }

            string memberTruck = LoadCollectionMemberTruck(rfid);
            if (!IsCollectionTruckMatch(memberTruck, selectedTruckCode, selectedTruckNo))
            {
                return new InternalTransfer.CollectionBulkValidationResult
                {
                    IsValid = false,
                    Rfid = rfid,
                    Members = new List<InternalTransfer.TransferMemberInput>(),
                    Message = "Truck Mismatch, Member Were Collected in different Truck. Please Select The Correct Truck"
                };
            }

            List<InternalTransfer.TransferMemberInput> members = LoadCollectionInternalTransferBulkMembers(rfid);
            if (!members.Any())
            {
                members.Add(new InternalTransfer.TransferMemberInput
                {
                    Rfid = member.Rfid,
                    TagRfid = tagRfid,
                    MemberCode = member.MemberCode,
                    MemberType = member.MemberType,
                    MemberSize = member.MemberSize,
                    MemberWeight = member.MemberWeight,
                    MemberDimension = member.MemberDimension
                });
            }

            return new InternalTransfer.CollectionBulkValidationResult
            {
                IsValid = true,
                Rfid = rfid,
                Members = members,
                Message = "Member validated successfully."
            };
        }

        public InternalTransfer.CollectionBulkValidationResult ContinueCollectionInternalTransferBypass(
            string rfid,
            string selectedTruckCode,
            string selectedTruckNo,
            string userName)
        {
            if (string.IsNullOrWhiteSpace(rfid))
            {
                return new InternalTransfer.CollectionBulkValidationResult
                {
                    IsValid = false,
                    Message = "Member RFID is required.",
                    Members = new List<InternalTransfer.TransferMemberInput>()
                };
            }

            InternalTransfer.MemberValidationResult member = LoadCollectionInternalTransferAnyMember(rfid.Trim());
            if (member == null || string.IsNullOrWhiteSpace(member.Rfid))
            {
                return new InternalTransfer.CollectionBulkValidationResult
                {
                    IsValid = false,
                    Rfid = rfid,
                    Message = "Member details not found.",
                    Members = new List<InternalTransfer.TransferMemberInput>()
                };
            }

            string location = !string.IsNullOrWhiteSpace(selectedTruckNo) ? selectedTruckNo.Trim() : (selectedTruckCode ?? string.Empty).Trim();
            string entryBy = string.IsNullOrWhiteSpace(userName) ? "SYSTEM" : userName.Trim();
            ExecuteCollectionInternalTransferViolationMark(member.Rfid, location, entryBy, member.MemberType, member.MemberSize);

            return new InternalTransfer.CollectionBulkValidationResult
            {
                IsValid = true,
                Rfid = member.Rfid,
                Message = "Member bypass completed.",
                Members = new List<InternalTransfer.TransferMemberInput>
                {
                    new InternalTransfer.TransferMemberInput
                    {
                        Rfid = member.Rfid,
                        TagRfid = rfid,
                        MemberCode = member.MemberCode,
                        MemberType = member.MemberType,
                        MemberSize = member.MemberSize,
                        MemberWeight = member.MemberWeight,
                        MemberDimension = member.MemberDimension
                    }
                }
            };
        }

        public InternalTransfer.DispatchSaveResult SaveInternalTransferDispatch(
            InternalTransfer.DispatchSaveRequest request,
            string userCode,
            string ipAddress)
        {
            return SaveMemberDispatchCore(request, userCode, ipAddress, "Internal Transfer Saved Successfully");
        }

        public InternalTransfer.TransportItem LoadMemberCollectionTruckDetails(string truckNo)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(truckNo))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand(@"SELECT DriverName,
       DriverNo,
       TransportCode AS TransporterName
FROM membercollection
WHERE TruckNo = @TruckNo
LIMIT 1", connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@TruckNo", truckNo.Trim());
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new InternalTransfer.TransportItem
                        {
                            VehicleNo = truckNo.Trim(),
                            DriverName = ReadString(reader, "DriverName"),
                            MobileNo = ReadString(reader, "DriverNo"),
                            TransporterName = ReadString(reader, "TransporterName")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }
        public InternalTransfer.DispatchSaveResult SaveCollectionInternalTransferDispatch(
            InternalTransfer.DispatchSaveRequest request,
            string userCode,
            string ipAddress)
        {
            List<InternalTransfer.TransferMemberInput> members = CleanInternalTransferMembers(request == null ? null : request.Members);
            if (request == null || !members.Any())
            {
                return new InternalTransfer.DispatchSaveResult { Success = false, Message = "Please select member." };
            }

            if (!HasValidConnectionString)
            {
                return new InternalTransfer.DispatchSaveResult { Success = false, Message = "Unable to save Collection Internal Transfer." };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Bulk_IU_CollectionInternalTransfer", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = InternalTransferCommandTimeoutSeconds;
                    command.Parameters.AddWithValue("@P_RFId", (members[0].Rfid ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_MatDispCode", "0");
                    command.Parameters.AddWithValue("@P_FromLocationType", MapInternalTransferLocationType(request.UserLocationType));
                    command.Parameters.AddWithValue("@P_FromLocation", (request.UserLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_ToLocationType", MapInternalTransferLocationType(request.ToLocationType));
                    command.Parameters.AddWithValue("@P_ToLocation", (request.ToLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_CustomerCode", string.Empty);
                    command.Parameters.AddWithValue("@P_LOCode", string.Empty);
                    command.Parameters.AddWithValue("@P_LOQty", string.Empty);
                    command.Parameters.AddWithValue("@P_TruckNo", (request.TruckNumber ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverName", (request.DriverName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverPhoneNo", (request.DriverContactNo ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_TransportorName", (request.TransporterName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_Dispacher", userCode);
                    command.Parameters.AddWithValue("@P_TotalDispatch", members.Count);
                    command.Parameters.AddWithValue("@P_DispatchClosed", "False");
                    command.Parameters.AddWithValue("@P_GenerateChallan", request.GenerateChallan ? "True" : "False");
                    command.Parameters.AddWithValue("@P_Active", "True");
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    command.Parameters.AddWithValue("@P_IPAdd", ipAddress);
                    command.Parameters.AddWithValue("@P_Data", BuildInternalTransferDetailJson("0", members));
                    connection.Open();

                    string matDispCode;
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        matDispCode = reader.Read() ? ReadString(reader, "MatDispCode") : string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(matDispCode))
                    {
                        return new InternalTransfer.DispatchSaveResult { Success = false, Message = "Unable to save Collection Internal Transfer." };
                    }

                    return new InternalTransfer.DispatchSaveResult
                    {
                        Success = true,
                        MatDispCode = matDispCode,
                        TotalRecords = members.Count,
                        Message = "Collection Internal Transfer Saved Successfully"
                    };
                }
            }
            catch (MySqlException)
            {
                return new InternalTransfer.DispatchSaveResult { Success = false, Message = "Unable to save Collection Internal Transfer." };
            }
        }

        private static InternalTransfer.DispatchSaveResult SaveMemberDispatchCore(
            InternalTransfer.DispatchSaveRequest request,
            string userCode,
            string ipAddress,
            string successMessage)
        {
            List<InternalTransfer.TransferMemberInput> members = CleanInternalTransferMembers(request == null ? null : request.Members);
            if (request == null || !members.Any())
            {
                return new InternalTransfer.DispatchSaveResult { Success = false, Message = "Please select member." };
            }

            string matDispCode = SaveInternalTransferMaster(request, members[0].Rfid, members.Count, userCode, ipAddress);
            if (string.IsNullOrWhiteSpace(matDispCode))
            {
                return new InternalTransfer.DispatchSaveResult { Success = false, Message = "Unable to save Internal Transfer." };
            }

            string detailError;
            bool detailsSaved = SaveInternalTransferDetails(matDispCode, request, members, userCode, out detailError);
            if (!detailsSaved)
            {
                return new InternalTransfer.DispatchSaveResult
                {
                    Success = false,
                    MatDispCode = matDispCode,
                    Message = string.IsNullOrWhiteSpace(detailError) ? "Unable to save Internal Transfer details." : detailError
                };
            }

            return new InternalTransfer.DispatchSaveResult
            {
                Success = true,
                MatDispCode = matDispCode,
                TotalRecords = members.Count,
                Message = successMessage
            };
        }

        public PalletDisassembleClient.ValidationResult ValidatePalletDisassembleClientMember(string tagRfid, string userCode)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return new PalletDisassembleClient.ValidationResult
                {
                    Success = false,
                    Message = "Please scan member.",
                    Members = new List<PalletDisassembleClient.DisassembleMemberItem>()
                };
            }

            PalletDisassembleClient.MemberLastUpdateItem status = LoadPalletDisassembleClientStatus(scanValue, userCode);
            if (status == null || string.IsNullOrWhiteSpace(status.Rfid))
            {
                return new PalletDisassembleClient.ValidationResult
                {
                    Success = false,
                    Rfid = scanValue,
                    Message = "Member status should be Pallet Assemble.",
                    Members = new List<PalletDisassembleClient.DisassembleMemberItem>()
                };
            }

            List<PalletDisassembleClient.DisassembleMemberItem> members = LoadPalletDisassembleClientMembers(status.Rfid);
            if (!members.Any())
            {
                return new PalletDisassembleClient.ValidationResult
                {
                    Success = false,
                    Rfid = status.Rfid,
                    Message = "Pallet details not found.",
                    Members = members
                };
            }

            return new PalletDisassembleClient.ValidationResult
            {
                Success = true,
                Rfid = status.Rfid,
                PalletCode = members.First().PalletCode,
                Message = "Member validated successfully.",
                Members = members
            };
        }

        public PalletDisassembleClient.SaveResult SavePalletDisassembleClient(
            PalletDisassembleClient.SaveRequest request,
            string userCode,
            string userName,
            string ipAddress)
        {
            List<PalletDisassembleClient.DisassembleMemberInput> members = CleanPalletDisassembleClientMembers(request == null ? null : request.Members);
            string palletCode = (request == null ? string.Empty : request.PalletCode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(palletCode))
            {
                palletCode = (members.FirstOrDefault() == null ? string.Empty : members.First().PalletCode ?? string.Empty).Trim();
            }

            if (!members.Any())
            {
                return new PalletDisassembleClient.SaveResult { Success = false, Message = "Please select member." };
            }

            if (string.IsNullOrWhiteSpace(palletCode))
            {
                return new PalletDisassembleClient.SaveResult { Success = false, Message = "Pallet code not found." };
            }

            string dismentleCode = SavePalletDisassembleClientMaster(palletCode, members.Count, userCode, ipAddress);
            if (string.IsNullOrWhiteSpace(dismentleCode))
            {
                return new PalletDisassembleClient.SaveResult { Success = false, Message = "Unable to save Pallet Disassemble." };
            }

            string detailError;
            int detailCount = SavePalletDisassembleClientDetails(
                dismentleCode,
                members,
                userCode,
                ipAddress,
                string.IsNullOrWhiteSpace(userName) ? userCode : userName,
                members.Count,
                out detailError);

            if (detailCount == 0)
            {
                return new PalletDisassembleClient.SaveResult
                {
                    Success = false,
                    DismentleCode = dismentleCode,
                    Message = string.IsNullOrWhiteSpace(detailError) ? "Unable to save Pallet Disassemble details." : detailError
                };
            }

            return new PalletDisassembleClient.SaveResult
            {
                Success = true,
                DismentleCode = dismentleCode,
                DetailCount = detailCount,
                Message = "Pallet Disassemble Saved Successfully"
            };
        }

        public PalletDisassembleCustomer.ValidationResult ValidatePalletDisassembleCustomerMember(string currentLocation, string tagRfid)
        {
            string rfid = LoadRfidBySticker(tagRfid);
            if (string.IsNullOrWhiteSpace(rfid))
            {
                return new PalletDisassembleCustomer.ValidationResult
                {
                    Success = false,
                    Message = "Member Not Tag / Member Not Found",
                    Members = new List<PalletDisassembleCustomer.DispatchDetailItem>()
                };
            }

            if (string.Equals(currentLocation, "Other", StringComparison.OrdinalIgnoreCase))
            {
                return ValidatePalletDisassembleCustomerOtherMember(rfid);
            }

            PalletDisassembleCustomer.ValidationResult member = LoadPalletDisassembleCustomerDispatchMember(rfid);
            if (member == null || string.IsNullOrWhiteSpace(member.Rfid))
            {
                return new PalletDisassembleCustomer.ValidationResult
                {
                    Success = false,
                    Rfid = rfid,
                    Message = "Member status should be Pallet Dispatch.",
                    Members = new List<PalletDisassembleCustomer.DispatchDetailItem>()
                };
            }

            List<PalletDisassembleCustomer.DispatchDetailItem> members = LoadPalletDisassembleCustomerPalletDetails(rfid);
            if (!members.Any())
            {
                return new PalletDisassembleCustomer.ValidationResult
                {
                    Success = false,
                    Rfid = rfid,
                    Message = "Pallet dispatch details not found.",
                    Members = members
                };
            }

            return new PalletDisassembleCustomer.ValidationResult
            {
                Success = true,
                Rfid = rfid,
                PalletCode = members.First().PalletCode,
                Message = "Member validated successfully.",
                Members = members
            };
        }

        private PalletDisassembleCustomer.ValidationResult ValidatePalletDisassembleCustomerOtherMember(string rfid)
        {
            PalletDisassembleCustomer.MemberLastUpdateItem lastUpdate = LoadPalletDisassembleCustomerLastUpdate(rfid);
            string lastStatus = lastUpdate == null ? string.Empty : lastUpdate.LastStatus;

            if (string.Equals(lastStatus, "Pallet Disassemble from Customer", StringComparison.OrdinalIgnoreCase))
            {
                return new PalletDisassembleCustomer.ValidationResult
                {
                    Success = false,
                    Rfid = rfid,
                    Message = "Member is in Pallet Disassemble from Customer",
                    Members = new List<PalletDisassembleCustomer.DispatchDetailItem>()
                };
            }

            if (string.Equals(lastStatus, "Pallet Dispatch", StringComparison.OrdinalIgnoreCase))
            {
                return new PalletDisassembleCustomer.ValidationResult
                {
                    Success = false,
                    Rfid = rfid,
                    Message = "Member in Pallet Dispatch, Please select the pallet dispatch option to add the member",
                    Members = new List<PalletDisassembleCustomer.DispatchDetailItem>()
                };
            }

            PalletDisassembleCustomer.DispatchDetailItem member = LoadPalletDisassembleCustomerMemberSizeType(rfid);
            if (member == null || string.IsNullOrWhiteSpace(member.Rfid))
            {
                return new PalletDisassembleCustomer.ValidationResult
                {
                    Success = false,
                    Rfid = rfid,
                    Message = "Member not manufactured",
                    Members = new List<PalletDisassembleCustomer.DispatchDetailItem>()
                };
            }

            return new PalletDisassembleCustomer.ValidationResult
            {
                Success = true,
                Rfid = rfid,
                PalletCode = string.Empty,
                Message = "Member validated successfully.",
                Members = new List<PalletDisassembleCustomer.DispatchDetailItem> { member }
            };
        }

        public PalletDisassembleCustomer.TempSaveResult SavePalletDisassembleCustomerTemp(
            PalletDisassembleCustomer.TempSaveRequest request,
            string userCode)
        {
            List<PalletDisassembleCustomer.TempSaveMemberInput> members = CleanPalletDisassembleCustomerTempMembers(request == null ? null : request.Members);
            string custDismentleCode = (request == null ? string.Empty : request.CustDismentleCode ?? string.Empty).Trim();

            if (!members.Any())
            {
                return new PalletDisassembleCustomer.TempSaveResult { Success = false, Message = "Please select member." };
            }

            if (string.IsNullOrWhiteSpace(custDismentleCode))
            {
                custDismentleCode = "Dismantle" + DateTime.Now.ToString("yyyyMMddHHmmss");
            }

            if (!HasValidConnectionString)
            {
                return new PalletDisassembleCustomer.TempSaveResult { Success = false, Message = "Database connection is not configured." };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Bulk_IU_PalletDismentleCustomerdtTemp", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 120;
                    command.Parameters.AddWithValue("@P_Data", BuildPalletDisassembleCustomerTempJson(custDismentleCode, members, userCode));
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string message = ReadString(reader, "Message");
                            bool success = ReadInt32(reader, "Success") == 1;

                            return new PalletDisassembleCustomer.TempSaveResult
                            {
                                Success = success,
                                Message = string.IsNullOrWhiteSpace(message) ? (success ? "Members Inserted" : "Member Already Exist!") : message,
                                TotalRecords = success ? members.Count : 0
                            };
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                return new PalletDisassembleCustomer.TempSaveResult { Success = false, Message = ex.Message };
            }

            return new PalletDisassembleCustomer.TempSaveResult { Success = false, Message = "Unable to save Pallet Dismantle Customer." };
        }

        public List<PalletDisassembleCustomer.TempDetailItem> LoadPalletDisassembleCustomerTempDetails(string custDismentleCode)
        {
            var items = new List<PalletDisassembleCustomer.TempDetailItem>();

            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(custDismentleCode))
            {
                return items;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_PalletDismentleCustomerTemp_ByCode", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_CustDismentleCode", custDismentleCode.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PalletDisassembleCustomer.TempDetailItem
                            {
                                CustDismentleCode = ReadString(reader, "CustDismentleCode"),
                                Rfid = ReadString(reader, "RFIDNo"),
                                MemberCode = ReadString(reader, "MemberCode"),
                                CreatedDate = ReadString(reader, "CreatedDate")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return items;
            }

            return items;
        }

        public PalletDisassembleCustomer.FinalSaveResult SavePalletDisassembleCustomerFinal(
            PalletDisassembleCustomer.FinalSaveRequest request,
            string userCode,
            string ipAddress)
        {
            List<PalletDisassembleCustomer.FinalSaveMemberInput> members = CleanPalletDisassembleCustomerFinalMembers(
                request == null ? null : request.Groups);
            string customerCode = (request == null ? string.Empty : request.CustomerCode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(customerCode))
            {
                return new PalletDisassembleCustomer.FinalSaveResult { Success = false, Message = "Please select To Customer." };
            }

            if (!members.Any())
            {
                return new PalletDisassembleCustomer.FinalSaveResult { Success = false, Message = "Please disassemble pallet first." };
            }

            string custDismentleCode = SavePalletDisassembleCustomerMaster(customerCode, members.Count, userCode, ipAddress);
            if (string.IsNullOrWhiteSpace(custDismentleCode))
            {
                return new PalletDisassembleCustomer.FinalSaveResult { Success = false, Message = "Unable to save Pallet Dismantle Customer." };
            }

            string detailError;
            bool detailsSaved = SavePalletDisassembleCustomerDetails(custDismentleCode, members, userCode, ipAddress, out detailError);
            if (!detailsSaved)
            {
                return new PalletDisassembleCustomer.FinalSaveResult
                {
                    Success = false,
                    CustDismentleCode = custDismentleCode,
                    Message = string.IsNullOrWhiteSpace(detailError) ? "Unable to save Pallet Dismantle Customer details." : detailError
                };
            }

            ClearPalletDisassembleCustomerTemp(userCode);

            return new PalletDisassembleCustomer.FinalSaveResult
            {
                Success = true,
                CustDismentleCode = custDismentleCode,
                TotalRecords = members.Count,
                Message = "Pallet Dismantle Customer Saved Successfully"
            };
        }

        public List<PalletOrder.MiscParameterItem> LoadPalletShapeList()
        {
            var shapes = new List<PalletOrder.MiscParameterItem>();

            if (!HasValidConnectionString)
            {
                return shapes;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("usp_ddweb_miscparameter", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_ParameterType", "BOMShape");
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            shapes.Add(new PalletOrder.MiscParameterItem
                            {
                                ParameterCode = ReadString(reader, "ParameterCode"),
                                ParameterName = ReadString(reader, "ParameterName"),
                                ParameterType = ReadString(reader, "ParameterType"),
                                TransactionID = ReadString(reader, "TransactionID")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return shapes;
            }

            return shapes;
        }

        public List<PalletOrder.PalletSkuItem> LoadPalletSkuList(string shape, string length, string width)
        {
            var skus = new List<PalletOrder.PalletSkuItem>();

            if (!HasValidConnectionString)
            {
                return skus;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_PalletSkuMaster", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_Shape", shape);
                    command.Parameters.AddWithValue("@P_FGLength", length);
                    command.Parameters.AddWithValue("@P_FGWidth", width);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            skus.Add(new PalletOrder.PalletSkuItem
                            {
                                PalletSkewCode = ReadString(reader, "PalletSkewCode"),
                                Shape = ReadString(reader, "Shape"),
                                SheetWidth = ReadString(reader, "SheetWidth"),
                                SheetLength1 = ReadString(reader, "SheetLength1"),
                                SheetLength2 = ReadString(reader, "SheetLength2"),
                                Stacking = ReadString(reader, "Stacking"),
                                CMSize = ReadString(reader, "CMSize"),
                                LM1Size = ReadString(reader, "LM1Size"),
                                LM2Size = ReadString(reader, "LM2Size"),
                                CMSizeQty = ReadString(reader, "CMSizeQty"),
                                LM1SizeQty = ReadString(reader, "LM1SizeQty"),
                                LM2SizeQty = ReadString(reader, "LM2SizeQty"),
                                Remarks = ReadString(reader, "Remarks")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return skus;
            }

            return skus;
        }



        public PalletOrder.PalletOrderSaveResult SavePalletOrder(string customerCode, string locationCode, IEnumerable<PalletOrder.PalletOrderLineItem> selectedSkus, string userCode, string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return null;
            }

            string palletOrderCode = string.Empty;

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();

                    using (var command = new MySqlCommand("WebApp_IU_PalletOrder", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@P_PalletOrderCode", "0");
                        command.Parameters.AddWithValue("@P_PalletOrderDate", DateTime.Now);
                        command.Parameters.AddWithValue("@P_CustomerCode", customerCode);
                        command.Parameters.AddWithValue("@P_LocationCode", locationCode);
                        command.Parameters.AddWithValue("@P_CREATEDBY", userCode);
                        command.Parameters.AddWithValue("@P_IPADDRESS", ipAddress);

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                palletOrderCode = ReadString(reader, "PalletOrderCode");
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(palletOrderCode))
                    {
                        return null;
                    }

                    int attemptedDetailCount = 0;
                    foreach (PalletOrder.PalletOrderLineItem item in selectedSkus ?? Enumerable.Empty<PalletOrder.PalletOrderLineItem>())
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.SkuCode))
                        {
                            continue;
                        }

                        attemptedDetailCount += 1;
                        string instruction = string.IsNullOrWhiteSpace(item.Instruction) ? string.Empty : item.Instruction.Trim();
                        if (instruction.Length > 100)
                        {
                            instruction = instruction.Substring(0, 100);
                        }

                        string detailCode;
                        bool detailSaved = SavePalletOrderDetail(connection, palletOrderCode, item, instruction, userCode, out detailCode);
                        if (!detailSaved || !PalletOrderDetailExists(connection, palletOrderCode, detailCode))
                        {
                            SavePalletOrderDetailFallback(connection, palletOrderCode, item, instruction, userCode, true, out detailCode);
                        }
                    }

                    int detailCount = CountPalletOrderDetails(connection, palletOrderCode);
                    return new PalletOrder.PalletOrderSaveResult
                    {
                        PalletOrderCode = palletOrderCode,
                        DetailCount = detailCount,
                        Message = detailCount > 0 ? string.Empty : "Unable to submit pallet order details. Detail rows were not saved in palletorderdtl."
                    };
                }
            }
            catch (MySqlException ex)
            {
                return new PalletOrder.PalletOrderSaveResult { Message = ex.Message };
            }
        }
        private static bool SavePalletOrderDetail(MySqlConnection connection, string palletOrderCode, PalletOrder.PalletOrderLineItem item, string instruction, string userCode, out string detailCode)
        {
            detailCode = string.Empty;

            try
            {
                using (var detailCommand = new MySqlCommand("WebApp_IU_PalletOrderdtl", connection))
                {
                    detailCommand.CommandType = CommandType.StoredProcedure;
                    detailCommand.Parameters.AddWithValue("@P_PalletOrderdtlCode", "0");
                    detailCommand.Parameters.AddWithValue("@P_PalletOrderCode", palletOrderCode);
                    detailCommand.Parameters.AddWithValue("@P_PalletSkewCode", item.SkuCode);
                    detailCommand.Parameters.AddWithValue("@P_Width", SafeInt32(item.Width));
                    detailCommand.Parameters.AddWithValue("@P_Length1", SafeInt32(item.Length1));
                    detailCommand.Parameters.AddWithValue("@P_Length2", SafeInt32(item.Length2));
                    detailCommand.Parameters.AddWithValue("@P_Instructions", instruction);
                    detailCommand.Parameters.AddWithValue("@P_Counts", string.IsNullOrWhiteSpace(item.Qty) ? "0" : item.Qty.Trim());
                    detailCommand.Parameters.AddWithValue("@P_CreatedBy", userCode);

                    using (MySqlDataReader detailReader = detailCommand.ExecuteReader())
                    {
                        if (detailReader.Read())
                        {
                            detailCode = ReadString(detailReader, "PalletOrderdtlCode");
                        }
                    }
                }

                return !string.IsNullOrWhiteSpace(detailCode);
            }
            catch (MySqlException ex)
            {
                if (IsUnknownColumn(ex, "Length1"))
                {
                    return SavePalletOrderDetailFallback(connection, palletOrderCode, item, instruction, userCode, true, out detailCode);
                }

                throw;
            }
        }

        private static bool PalletOrderDetailExists(MySqlConnection connection, string palletOrderCode, string detailCode)
        {
            if (connection == null || string.IsNullOrWhiteSpace(palletOrderCode) || string.IsNullOrWhiteSpace(detailCode))
            {
                return false;
            }

            using (var command = new MySqlCommand("SELECT COUNT(1) FROM palletorderdtl WHERE PalletOrderCode = @PalletOrderCode AND PalletOrderdtlCode = @PalletOrderdtlCode", connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.AddWithValue("@PalletOrderCode", palletOrderCode.Trim());
                command.Parameters.AddWithValue("@PalletOrderdtlCode", detailCode.Trim());
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static int CountPalletOrderDetails(MySqlConnection connection, string palletOrderCode)
        {
            if (connection == null || string.IsNullOrWhiteSpace(palletOrderCode))
            {
                return 0;
            }

            using (var command = new MySqlCommand("SELECT COUNT(1) FROM palletorderdtl WHERE PalletOrderCode = @PalletOrderCode", connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.AddWithValue("@PalletOrderCode", palletOrderCode.Trim());
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
        private static bool SavePalletOrderDetailFallback(MySqlConnection connection, string palletOrderCode, PalletOrder.PalletOrderLineItem item, string instruction, string userCode, bool includeInstruction, out string detailCode)
        {
            detailCode = string.Empty;

            try
            {
                string columnList = includeInstruction
                    ? "PalletOrderdtlCode, PalletOrderCode, PalletSkewCode, SheetWidth, SheetLength1, SheetLength2, Instructions, Counts, ACTIVE, CREATEDBY, CREATEDDATE, CreatedIP"
                    : "PalletOrderdtlCode, PalletOrderCode, PalletSkewCode, SheetWidth, SheetLength1, SheetLength2, Counts, ACTIVE, CREATEDBY, CREATEDDATE, CreatedIP";
                string valueList = includeInstruction
                    ? "'0', @PalletOrderCode, @PalletSkewCode, @SheetWidth, @SheetLength1, @SheetLength2, @Instructions, @Counts, 'True', @CreatedBy, SYSDATE(), 'MobApp'"
                    : "'0', @PalletOrderCode, @PalletSkewCode, @SheetWidth, @SheetLength1, @SheetLength2, @Counts, 'True', @CreatedBy, SYSDATE(), 'MobApp'";

                using (var insertCommand = new MySqlCommand("INSERT INTO palletorderdtl (" + columnList + ") VALUES (" + valueList + ")", connection))
                {
                    insertCommand.CommandType = CommandType.Text;
                    insertCommand.Parameters.AddWithValue("@PalletOrderCode", palletOrderCode);
                    insertCommand.Parameters.AddWithValue("@PalletSkewCode", item.SkuCode);
                    insertCommand.Parameters.AddWithValue("@SheetWidth", SafeInt32(item.Width));
                    insertCommand.Parameters.AddWithValue("@SheetLength1", SafeInt32(item.Length1));
                    insertCommand.Parameters.AddWithValue("@SheetLength2", SafeInt32(item.Length2));
                    if (includeInstruction)
                    {
                        insertCommand.Parameters.AddWithValue("@Instructions", instruction);
                    }
                    insertCommand.Parameters.AddWithValue("@Counts", string.IsNullOrWhiteSpace(item.Qty) ? "0" : item.Qty.Trim());
                    insertCommand.Parameters.AddWithValue("@CreatedBy", userCode);
                    insertCommand.ExecuteNonQuery();
                }

                long transactionId;
                using (var identityCommand = new MySqlCommand("SELECT LAST_INSERT_ID()", connection))
                {
                    transactionId = Convert.ToInt64(identityCommand.ExecuteScalar());
                }

                using (var updateCommand = new MySqlCommand("UPDATE palletorderdtl SET PalletOrderdtlCode = CONCAT('POD', LPAD(TransactionID,10,'0')) WHERE TransactionID = @TransactionID", connection))
                {
                    updateCommand.Parameters.AddWithValue("@TransactionID", transactionId);
                    updateCommand.ExecuteNonQuery();
                }

                using (var selectCommand = new MySqlCommand("SELECT PalletOrderdtlCode FROM palletorderdtl WHERE TransactionID = @TransactionID LIMIT 1", connection))
                {
                    selectCommand.Parameters.AddWithValue("@TransactionID", transactionId);
                    detailCode = Convert.ToString(selectCommand.ExecuteScalar());
                }

                return !string.IsNullOrWhiteSpace(detailCode);
            }
            catch (MySqlException ex)
            {
                if (includeInstruction && IsUnknownColumn(ex, "Instructions"))
                {
                    return SavePalletOrderDetailFallback(connection, palletOrderCode, item, string.Empty, userCode, false, out detailCode);
                }

                throw;
            }
        }

        private static bool IsUnknownColumn(MySqlException ex, string columnName)
        {
            return ex != null
                && ex.Number == 1054
                && ex.Message != null
                && ex.Message.IndexOf("Unknown column", StringComparison.OrdinalIgnoreCase) >= 0
                && ex.Message.IndexOf(columnName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int SafeInt32(string value)
        {
            int parsedValue;
            return int.TryParse(Convert.ToString(value), out parsedValue) ? parsedValue : 0;
        }


        public PalletPreDispatch.PreDispatchValidationResult ValidatePreDispatchMember(string rfid)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(rfid);
            string resolvedRfid = LoadRfidBySticker(scanValue);
            if (string.IsNullOrWhiteSpace(resolvedRfid))
            {
                resolvedRfid = scanValue;
            }

            int isValidStatus = CheckCurrentMemberStatus(resolvedRfid, "Pallet Assemble");
            if (isValidStatus <= 0)
            {
                return new PalletPreDispatch.PreDispatchValidationResult
                {
                    IsAllowed = false,
                    Message = "Member is not available at Pallet Assemble."
                };
            }

            PalletPreDispatch.PreDispatchValidationResult dateValidation = null;

            if (HasValidConnectionString)
            {
                try
                {
                    using (var connection = new MySqlConnection(ConnectionString))
                    using (var command = new MySqlCommand("WebApp_Validation_PreDispatch_In_3Days", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@P_RFID", resolvedRfid);
                        connection.Open();

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                dateValidation = new PalletPreDispatch.PreDispatchValidationResult
                                {
                                    PalletCode = ReadString(reader, "PalletCode"),
                                    PalletCreatedDate = ReadString(reader, "PalletCreatedDate"),
                                    RfidNo = ReadString(reader, "RFIDNo"),
                                    PalletStatus = ReadString(reader, "PalletStatus")
                                };
                            }
                        }
                    }
                }
                catch (MySqlException)
                {
                    dateValidation = null;
                }
            }

            if (dateValidation == null)
            {
                return new PalletPreDispatch.PreDispatchValidationResult
                {
                    IsAllowed = false,
                    Message = "Member assemble date validation not found."
                };
            }

            if (string.IsNullOrWhiteSpace(dateValidation.RfidNo))
            {
                dateValidation.RfidNo = resolvedRfid;
            }

            dateValidation.IsAllowed = string.Equals(dateValidation.PalletStatus, "ALLOWED", StringComparison.OrdinalIgnoreCase);
            dateValidation.Message = dateValidation.IsAllowed
                ? "Member validated successfully."
                : "Member assembled more than 3 days ago. Pre-Dispatch not allowed.";

            return dateValidation;
        }

        public PalletPreDispatch.PalletOrderDimensions LoadPalletOrderDimensionsByPalletCode(string palletCode)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(palletCode))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_PalletOrderByPalletCode", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_PalletCode", palletCode.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new PalletPreDispatch.PalletOrderDimensions
                        {
                            Width = ReadString(reader, "Width"),
                            Length1 = ReadString(reader, "Length1"),
                            Length2 = ReadString(reader, "Length2")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }
        public PalletPreDispatch.ExistingPreDispatchItem LoadExistingPreDispatch(string palletCode, string clientBatchNo)
        {
            if (!HasValidConnectionString)
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebAPP_Get_PalletPreDispatchByBatchNo_OR_PalletCode", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_PalletCode", palletCode);
                    command.Parameters.AddWithValue("@P_ClientBatchNo", clientBatchNo);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapExistingPreDispatchItem(reader) : null;
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public string SavePreDispatchEntry(string palletCode, string clientBatchNo, string sheetLength, string width, string weight, string thikness, string storageLocation, string userCode, string ipAddress, string UserName)
        {
            if (!HasValidConnectionString)
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_IU_palletpredispatch", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_PalletPreDispatchCode", "0");
                    command.Parameters.AddWithValue("@P_PalletCode", palletCode);
                    command.Parameters.AddWithValue("@P_ClientBatchNo", clientBatchNo);
                    command.Parameters.AddWithValue("@P_SheetLength", sheetLength);
                    command.Parameters.AddWithValue("@P_Width", width);
                    command.Parameters.AddWithValue("@P_Weight", weight);
                    command.Parameters.AddWithValue("@P_Thikness", thikness);
                    command.Parameters.AddWithValue("@P_StorageLocation", (storageLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_CreatedBy", userCode);
                    command.Parameters.AddWithValue("@P_IPAdd", ipAddress);
                    command.Parameters.AddWithValue("@P_CreatedByName", UserName);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return string.Empty;
                        }

                        string code = ReadString(reader, "PalletPreDispatchCode");
                        return !string.IsNullOrWhiteSpace(code) ? code : ReadString(reader, "Message");
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        public List<PalletDispatcch.ShipToLocationItem> LoadShipToLocationList(string userCode)
        {
            var locations = new List<PalletDispatcch.ShipToLocationItem>();

            if (!HasValidConnectionString)
            {
                return locations;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Webapp_get_CustShipToLocMaster", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            locations.Add(new PalletDispatcch.ShipToLocationItem
                            {
                                ShipToLocCode = ReadString(reader, "ShipToLocCode"),
                                ShipToLocTitle = ReadString(reader, "ShipToLocTitle"),
                                ShipToLocCompany = ReadString(reader, "ShipToLocCompany"),
                                LocationAddress = ReadString(reader, "LocationAddress"),
                                EmailId = ReadString(reader, "EmailId")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return locations;
            }

            return locations;
        }

        public List<PalletDispatcch.DispatchBatchItem> LoadDispatchBatches(IEnumerable<string> clientBatchNos)
        {
            var items = new List<PalletDispatcch.DispatchBatchItem>();

            if (!HasValidConnectionString)
            {
                return items;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebAPP_Get_PalletPreDispatchByMultipleBatchNo", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_ClientBatchNo", BuildClientBatchJson(clientBatchNos));
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapDispatchBatchItem(reader));
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return items;
            }

            HashSet<string> validPalletCodes = LoadPreDispatchPalletCodes(items.Select(item => item.PalletCode));

            return items
                .Where(item => validPalletCodes.Contains(item.PalletCode ?? string.Empty))
                .ToList();
        }

        public string SaveDispatchMaster(PalletDispatcch.DispatchMasterInput request, string userCode, string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_IU_palletdispatched", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_PalletDispCode", "0");
                    command.Parameters.AddWithValue("@P_ShipToLocation", (request.ShipToLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_Consignee", (request.Consignee ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_FromLocation", (request.FromLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_TransportCode", (request.TransporterName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_TruckNo", (request.TruckNumber ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverName", (request.DriverName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverNo", (request.DriverContactNo ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_Dispatcher", userCode);
                    command.Parameters.AddWithValue("@P_GenerateChallan", request.GenerateChallan ? "True" : "False");
                    command.Parameters.AddWithValue("@P_Active", "True");
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    command.Parameters.AddWithValue("@P_IPAdd", ipAddress);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "PalletDispCode") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        public bool HasAlreadyDispatchedMembers(IEnumerable<string> palletCodes)
        {
            List<string> codes = CleanPalletCodes(palletCodes);
            if (!codes.Any())
            {
                return false;
            }

            object result = ExecuteTextScalar(BuildAlreadyDispatchQuery(codes.Count), BuildPalletCodeParameters(codes));
            return result != null && Convert.ToInt32(result) > 0;
        }

        public PalletDispatcch.BulkDispatchSaveResult SaveDispatchDetails(string palletDispCode, PalletDispatcch.DispatchMasterInput request, IEnumerable<PalletDispatcch.DispatchDetailInput> dispatchItems, string userCode)
        {
            if (!HasValidConnectionString)
            {
                return new PalletDispatcch.BulkDispatchSaveResult { Success = false, Message = "Unable to save Pallet Dispatch details." };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Bulk_IU_PalletDispDtl", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_Data", BuildDispatchDetailJson(palletDispCode, request, dispatchItems, userCode));
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            if (reader.FieldCount == 0 || !reader.Read())
                            {
                                continue;
                            }

                            if (HasColumn(reader, "Success") && ReadInt32(reader, "Success") == 0)
                            {
                                return new PalletDispatcch.BulkDispatchSaveResult
                                {
                                    Success = false,
                                    Message = reader.FieldCount > 1 ? ReadString(reader, 1) : "Already Dispatch"
                                };
                            }

                            if (HasColumn(reader, "PalletDispCode"))
                            {
                                return new PalletDispatcch.BulkDispatchSaveResult
                                {
                                    Success = true,
                                    PalletDispCode = ReadString(reader, "PalletDispCode"),
                                    TotalRecords = HasColumn(reader, "TotalRecords") ? ReadInt32(reader, "TotalRecords") : 0,
                                    Message = "Pallet Dispatch Saved Successfully"
                                };
                            }
                        }
                        while (reader.NextResult());
                    }

                    return new PalletDispatcch.BulkDispatchSaveResult { Success = false, Message = "Unable to save Pallet Dispatch details." };
                }
            }
            catch (MySqlException)
            {
                return new PalletDispatcch.BulkDispatchSaveResult { Success = false, Message = "Unable to save Pallet Dispatch details." };
            }
        }

        public PalletDispatcch.DispatchValidationResult ValidateDispatchMember(string rfid)
        {
            int isValid = CheckCurrentMemberStatus(rfid, "Pre Dispatch");
            PalletDispatcch.DispatchBatchItem item = isValid > 0 ? LoadDispatchBatchByRfid(rfid) : null;
            bool hasItem = item != null && !string.IsNullOrWhiteSpace(item.ClientBatchNo);

            return new PalletDispatcch.DispatchValidationResult
            {
                IsValid = isValid > 0 && hasItem,
                LastStatus = isValid > 0 ? "Pre Dispatch" : string.Empty,
                Message = isValid > 0 && hasItem
                    ? "Members validated successfully."
                    : "Members Not in PreDispatch.",
                Item = item
            };
        }

        public static string ReadString(IDataRecord record, string name)
        {
            if (!HasColumn(record, name))
            {
                return string.Empty;
            }

            int ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? string.Empty : Convert.ToString(record.GetValue(ordinal));
        }

        public static string ReadString(IDataRecord record, int ordinal)
        {
            return record.IsDBNull(ordinal) ? string.Empty : Convert.ToString(record.GetValue(ordinal));
        }

        private static string ReadFirstString(IDataRecord record, params string[] names)
        {
            foreach (string name in names ?? Enumerable.Empty<string>())
            {
                string value = ReadString(record, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        public static int ReadInt32(IDataRecord record, string name)
        {
            if (!HasColumn(record, name))
            {
                return 0;
            }

            int ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? 0 : Convert.ToInt32(record.GetValue(ordinal));
        }

        public static decimal? ReadNullableDecimal(IDataRecord record, string name)
        {
            if (!HasColumn(record, name))
            {
                return null;
            }

            int ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? (decimal?)null : Convert.ToDecimal(record.GetValue(ordinal));
        }

        public static bool HasColumn(IDataRecord record, string name)
        {
            for (int index = 0; index < record.FieldCount; index += 1)
            {
                if (string.Equals(record.GetName(index), name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static BatchNumberUploadSource ResolveBatchNumberUploadSource(MySqlConnection connection, string batchNo, out bool hasUploadTable)
        {
            hasUploadTable = false;
            foreach (string tableName in new[] { "membernewbatchnumber2026", "batchnumbermaster", "batchnumbertable2" })
            {
                HashSet<string> columns = LoadTableColumns(connection, tableName);
                if (columns.Count == 0)
                {
                    continue;
                }

                string batchColumn = ResolveColumn(columns, "NewBatchNumber", "NewBatchNo", "BatchNo", "BatchNumber", "NewBatchNumber", "RFID", "Code");
                if (string.IsNullOrWhiteSpace(batchColumn))
                {
                    continue;
                }

                hasUploadTable = true;
                using (var command = new MySqlCommand("SELECT COUNT(1) FROM `" + tableName + "` WHERE `" + batchColumn + "` = @BatchNo", connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@BatchNo", batchNo);
                    if (Convert.ToInt32(command.ExecuteScalar()) > 0)
                    {
                        return new BatchNumberUploadSource
                        {
                            TableName = tableName,
                            Columns = columns,
                            BatchColumn = batchColumn
                        };
                    }
                }
            }

            return null;
        }
        private static HashSet<string> LoadTableColumns(MySqlConnection connection, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName", connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.AddWithValue("@TableName", tableName);
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(Convert.ToString(reader.GetValue(0)));
                    }
                }
            }

            return columns;
        }

        private static string ResolveColumn(HashSet<string> columns, params string[] names)
        {
            if (columns == null || names == null)
            {
                return string.Empty;
            }

            foreach (string name in names)
            {
                if (columns.Contains(name))
                {
                    return columns.First(column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            return string.Empty;
        }

        private static void AddColumnSetIfExists(ICollection<string> setParts, HashSet<string> columns, string columnName, string valueExpression)
        {
            string resolvedColumn = ResolveColumn(columns, columnName);
            if (!string.IsNullOrWhiteSpace(resolvedColumn))
            {
                setParts.Add("`" + resolvedColumn + "` = " + valueExpression);
            }
        }

        private static bool IsBatchUsedValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = value.Trim();
            return string.Equals(normalizedValue, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedValue, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedValue, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedValue, "used", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedValue, "inuse", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsTrueValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = value.Trim();
            return string.Equals(normalizedValue, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedValue, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedValue, "1", StringComparison.OrdinalIgnoreCase);
        }
        private static int CheckCurrentMemberStatus(string rfid, string currentStatus)
        {
            if (!HasValidConnectionString)
            {
                return 0;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_usp_CheckCurrentStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid);
                    command.Parameters.AddWithValue("@P_CurrentLocation", string.Empty);
                    command.Parameters.AddWithValue("@P_CurrentStatus", currentStatus);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadInt32(reader, "IsValid") : 0;
                    }
                }
            }
            catch (MySqlException)
            {
                return 0;
            }
        }

        private static string LoadRfidBySticker(string tagRfid)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(scanValue))
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_Get_RFIDBySticker", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_TagRFID", scanValue);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "RFID") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static string LoadMemberLastLocation(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("SELECT LastLocation FROM memberlastupdate WHERE RFID = @RFID OR TaggMember = @RFID ORDER BY LastUpdatedDate DESC LIMIT 1", connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@RFID", rfid.Trim());
                    connection.Open();
                    object value = command.ExecuteScalar();
                    return value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value);
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static PalletPreDispatch.PreDispatchValidationResult LoadPalletAssembleDateValidation(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Validation_PreDispatch_In_3Days", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new PalletPreDispatch.PreDispatchValidationResult
                        {
                            PalletCode = ReadString(reader, "PalletCode"),
                            PalletCreatedDate = ReadString(reader, "PalletCreatedDate"),
                            RfidNo = ReadString(reader, "RFIDNo"),
                            PalletStatus = ReadString(reader, "PalletStatus")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static void ExecuteMemberViolationMark(string rfid, string location, string entryBy)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_IU_MemberViolationMark", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    command.Parameters.AddWithValue("@P_Location", string.IsNullOrWhiteSpace(location) ? string.Empty : location.Trim());
                    command.Parameters.AddWithValue("@P_EntryBy", string.IsNullOrWhiteSpace(entryBy) ? string.Empty : entryBy.Trim());
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (MySqlException)
            {
                return;
            }
        }

        private static string ExecutePalletAssembleUpdate(string rfid, string palletOrderdtlCode, string userCode, string userName, string location)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Update_Pallet", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    command.Parameters.AddWithValue("@P_PalletOrderdtlCode", string.IsNullOrWhiteSpace(palletOrderdtlCode) ? string.Empty : palletOrderdtlCode.Trim());
                    command.Parameters.AddWithValue("@P_CreatedBy", string.IsNullOrWhiteSpace(userCode) ? string.Empty : userCode.Trim());
                    command.Parameters.AddWithValue("@P_UserName", string.IsNullOrWhiteSpace(userName) ? string.Empty : userName.Trim());
                    command.Parameters.AddWithValue("@P_Location", string.IsNullOrWhiteSpace(location) ? string.Empty : location.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "Message") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static bool SavePalletAssembleDetails(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string palletCode,
            string palletOrderdtlCode,
            IEnumerable<PalletAssemble.AssembleSaveMemberInput> members,
            string userCode,
            string userName,
            string ipAddress,
            string toLocation,
            out string message)
        {
            message = string.Empty;
            string jsonData = BuildPalletAssembleDetailJson(palletCode, palletOrderdtlCode, members, userCode, ipAddress);
            string lastTransferTo = LoadMemberLastTransferTo(connection, transaction, members);

            using (var command = new MySqlCommand("WebApp_Bulk_PalletAssembleDt", connection, transaction))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@P_JsonData", jsonData);
                command.Parameters.AddWithValue("@P_UserName", string.IsNullOrWhiteSpace(userName) ? string.Empty : userName.Trim());
                command.Parameters.AddWithValue("@P_Location", lastTransferTo);
                command.Parameters.AddWithValue("@P_ToLocation", string.IsNullOrWhiteSpace(toLocation) ? string.Empty : toLocation.Trim());

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        message = "Unable to save Pallet Assemble details.";
                        return false;
                    }

                    string success = ReadString(reader, "Success");
                    message = ReadString(reader, "Message");
                    return string.Equals(success, "1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(success, "True", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        // Increment only after pallet details are saved so a failed detail transaction cannot inflate the order count.
        private static void IncrementPalletAssembleCount(MySqlConnection connection, MySqlTransaction transaction, string palletOrderdtlCode)
        {
            if (connection == null || string.IsNullOrWhiteSpace(palletOrderdtlCode))
            {
                return;
            }

            using (var updateCommand = new MySqlCommand(
                "UPDATE PalletOrderDtl SET AssembleCounts = COALESCE(AssembleCounts, 0) + 1 WHERE PalletOrderdtlCode = @PalletOrderdtlCode",
                connection,
                transaction))
            {
                updateCommand.CommandType = CommandType.Text;
                updateCommand.Parameters.AddWithValue("@PalletOrderdtlCode", palletOrderdtlCode.Trim());
                updateCommand.ExecuteNonQuery();
            }
        }
        private static void UpdateCompletedPalletOrderDetailStatus(MySqlConnection connection, MySqlTransaction transaction, string palletOrderdtlCode)
        {
            if (connection == null || string.IsNullOrWhiteSpace(palletOrderdtlCode))
            {
                return;
            }

            int orderCount = 0;
            int assembleCount = 0;

            using (var countCommand = new MySqlCommand(
                "SELECT Counts, AssembleCounts FROM PalletOrderDtl WHERE PalletOrderdtlCode = @PalletOrderdtlCode LIMIT 1",
                connection,
                transaction))
            {
                countCommand.CommandType = CommandType.Text;
                countCommand.Parameters.AddWithValue("@PalletOrderdtlCode", palletOrderdtlCode.Trim());

                using (MySqlDataReader reader = countCommand.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return;
                    }

                    orderCount = ReadInt32(reader, "Counts");
                    assembleCount = ReadInt32(reader, "AssembleCounts");
                }
            }

            if (orderCount <= 0 || assembleCount < orderCount)
            {
                return;
            }

            using (var updateCommand = new MySqlCommand(
                "UPDATE PalletOrderDtl SET Status = @Status WHERE PalletOrderdtlCode = @PalletOrderdtlCode",
                connection,
                transaction))
            {
                updateCommand.CommandType = CommandType.Text;
                updateCommand.Parameters.AddWithValue("@Status", "Completed");
                updateCommand.Parameters.AddWithValue("@PalletOrderdtlCode", palletOrderdtlCode.Trim());
                updateCommand.ExecuteNonQuery();
            }
        }

        private static string LoadMemberLastTransferTo(MySqlConnection connection, MySqlTransaction transaction, IEnumerable<PalletAssemble.AssembleSaveMemberInput> members)
        {
            List<string> rfids = (members ?? Enumerable.Empty<PalletAssemble.AssembleSaveMemberInput>())
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.Rfid))
                .Select(member => member.Rfid.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!rfids.Any())
            {
                return string.Empty;
            }

            string parameterList = string.Join(",", rfids.Select((rfid, index) => "@RFID" + index));
            using (var command = new MySqlCommand(
                "SELECT LastTransferTo FROM memberlastupdate WHERE RFID IN (" + parameterList + ") OR TaggMember IN (" + parameterList + ") ORDER BY LastUpdatedDate DESC LIMIT 1",
                connection,
                transaction))
            {
                command.CommandType = CommandType.Text;
                for (int index = 0; index < rfids.Count; index += 1)
                {
                    command.Parameters.AddWithValue("@RFID" + index, rfids[index]);
                }

                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value);
            }
        }

        private static PalletDisassembleClient.MemberLastUpdateItem LoadPalletDisassembleClientStatus(string rfid, string userCode)
        {
            if (!HasValidConnectionString)
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_MemberLastUpdateNotInPreDispatch", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", (rfid ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_UserCode", (userCode ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new PalletDisassembleClient.MemberLastUpdateItem
                        {
                            Rfid = ReadString(reader, "RFID"),
                            LastStatus = ReadString(reader, "LastStatus"),
                            LastTransferTo = ReadString(reader, "LastTransferTo"),
                            LastTransactionId = ReadString(reader, "LastTransactionId")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static List<PalletDisassembleClient.DisassembleMemberItem> LoadPalletDisassembleClientMembers(string rfid)
        {
            var members = new List<PalletDisassembleClient.DisassembleMemberItem>();

            if (!HasValidConnectionString)
            {
                return members;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_PalletDetails_ByRFID", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", (rfid ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            if (!HasColumn(reader, "MemberCode") || !HasColumn(reader, "MemberId"))
                            {
                                continue;
                            }

                            while (reader.Read())
                            {
                                members.Add(new PalletDisassembleClient.DisassembleMemberItem
                                {
                                    Rfid = ReadString(reader, "MemberId"),
                                    MemberCode = ReadString(reader, "MemberCode"),
                                    MemberType = ReadString(reader, "MemberType"),
                                    MemberSize = ReadString(reader, "MemberSize"),
                                    BatchNo = ReadString(reader, "BatchNo"),
                                    PalletCode = ReadString(reader, "PalletCode"),
                                    SheetWidth = ReadString(reader, "SheetWidth"),
                                    SheetLength1 = ReadString(reader, "SheetLength1"),
                                    SheetLength2 = ReadString(reader, "SheetLength2")
                                });
                            }
                        }
                        while (reader.NextResult());
                    }
                }
            }
            catch (MySqlException)
            {
                return members;
            }

            return members;
        }

        private static string SavePalletDisassembleClientMaster(string palletCode, int totalMember, string userCode, string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Iu_PalletDismantleClient", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 120;
                    command.Parameters.AddWithValue("@P_DismentleCode", "0");
                    command.Parameters.AddWithValue("@P_PalletCode", (palletCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_TotalMember", Convert.ToString(totalMember));
                    command.Parameters.AddWithValue("@P_Active", "True");
                    command.Parameters.AddWithValue("@P_CreatedBy", (userCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_IPAdd", (ipAddress ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "DismentleCode") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static int SavePalletDisassembleClientDetails(
            string dismentleCode,
            IEnumerable<PalletDisassembleClient.DisassembleMemberInput> members,
            string userCode,
            string ipAddress,
            string userName,
            int expectedCount,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!HasValidConnectionString)
            {
                errorMessage = "Database connection is not configured.";
                return 0;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_IU_PalletDismentleDT_Bulk", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 180;
                    command.Parameters.AddWithValue("@P_DismentleCode", (dismentleCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_JsonData", BuildPalletDisassembleClientDetailJson(members));
                    command.Parameters.AddWithValue("@P_CreatedBy", (userCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_IPAdd", (ipAddress ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_UserName", (userName ?? string.Empty).Trim());
                    connection.Open();
                    command.ExecuteNonQuery();
                    return expectedCount;
                }
            }
            catch (MySqlException ex)
            {
                errorMessage = ex.Message;
                return 0;
            }
        }

        private static List<PalletDisassembleCustomer.DispatchDetailItem> LoadPalletDisassembleCustomerPalletDetails(string rfid)
        {
            var items = new List<PalletDisassembleCustomer.DispatchDetailItem>();

            if (!HasValidConnectionString)
            {
                return items;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_PalletDetails_ByRFID", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", (rfid ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            if (!HasColumn(reader, "MemberId") || !HasColumn(reader, "MemberCode"))
                            {
                                continue;
                            }

                            while (reader.Read())
                            {
                                items.Add(new PalletDisassembleCustomer.DispatchDetailItem
                                {
                                    MemberCode = ReadString(reader, "MemberCode"),
                                    MemberType = ReadString(reader, "MemberType"),
                                    MemberSize = ReadString(reader, "MemberSize"),
                                    ClientBatchNo = ReadFirstString(reader, "ClientBatchNo", "BatchNo"),
                                    PalletCode = ReadString(reader, "PalletCode"),
                                    Rfid = ReadString(reader, "MemberId"),
                                    SheetWidth = ReadString(reader, "SheetWidth"),
                                    SheetLength1 = ReadString(reader, "SheetLength1"),
                                    SheetLength2 = ReadString(reader, "SheetLength2"),
                                    Discrepancy = "No"
                                });
                            }
                        }
                        while (reader.NextResult());
                    }
                }
            }
            catch (MySqlException)
            {
                return items;
            }

            return items;
        }

        private static PalletDisassembleCustomer.MemberLastUpdateItem LoadPalletDisassembleCustomerLastUpdate(string rfid)
        {
            if (!HasValidConnectionString)
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_MemberLastUpdate", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 60;
                    command.Parameters.AddWithValue("@P_RFID", (rfid ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new PalletDisassembleCustomer.MemberLastUpdateItem
                        {
                            Rfid = ReadString(reader, "RFID"),
                            LastLocation = ReadString(reader, "LastLocation"),
                            LastStatus = ReadString(reader, "LastStatus"),
                            LastTransferTo = ReadString(reader, "LastTransferTo"),
                            LastTransactionId = ReadString(reader, "LastTransactionId")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static PalletDisassembleCustomer.DispatchDetailItem LoadPalletDisassembleCustomerMemberSizeType(string rfid)
        {
            if (!HasValidConnectionString)
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_MemberSizeType", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 60;
                    command.Parameters.AddWithValue("@P_RFID", (rfid ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new PalletDisassembleCustomer.DispatchDetailItem
                        {
                            MemberCode = ReadString(reader, "MemberCode"),
                            MemberType = ReadString(reader, "MemberType"),
                            MemberSize = ReadString(reader, "MemberSize"),
                            Rfid = ReadString(reader, "RFID"),
                            Discrepancy = "No"
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static string SavePalletDisassembleCustomerMaster(string customerCode, int totalMember, string userCode, string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_iu_palletdismentlecustomer", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 120;
                    command.Parameters.AddWithValue("@P_CustDismentleCode", "0");
                    command.Parameters.AddWithValue("@P_CustCode", (customerCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_TotalMember", Convert.ToString(totalMember));
                    command.Parameters.AddWithValue("@P_Active", "True");
                    command.Parameters.AddWithValue("@P_CreatedBy", (userCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_IPAdd", (ipAddress ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "CustDismentleCode") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static bool SavePalletDisassembleCustomerDetails(
            string custDismentleCode,
            IEnumerable<PalletDisassembleCustomer.FinalSaveMemberInput> members,
            string userCode,
            string ipAddress,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!HasValidConnectionString)
            {
                errorMessage = "Database connection is not configured.";
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Bulk_IU_PalletDismentleCustomerdt", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 180;
                    command.Parameters.AddWithValue("@P_Data", BuildPalletDisassembleCustomerFinalJson(custDismentleCode, members, userCode, ipAddress));
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (HasColumn(reader, "Success") && ReadInt32(reader, "Success") == 0)
                            {
                                errorMessage = ReadString(reader, "Message");
                                return false;
                            }

                            return true;
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                errorMessage = ex.Message;
                return false;
            }

            return false;
        }

        public bool ClearPalletDisassembleCustomerTempForUser(string userCode)
        {
            return ClearPalletDisassembleCustomerTemp(userCode);
        }
        private static bool ClearPalletDisassembleCustomerTemp(string userCode)
        {
            if (!HasValidConnectionString)
            {
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Clear_PalletDismentleCustomerTempTable", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", (userCode ?? string.Empty).Trim());
                    connection.Open();
                    command.ExecuteNonQuery();
                    return true;
                }
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        private static string SaveMemberReceiptMaster(MemberReceipt.ReceiveSaveRequest request, string dispatchCode, int receivedQty, string userCode, string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_iu_MemberReceived", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_MaterialRecCode", "0");
                    command.Parameters.AddWithValue("@P_DispatchCode", (dispatchCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_RecLoctype", MapMemberReceiptLocationType(request.ReceiptFrom));
                    command.Parameters.AddWithValue("@P_CustomerCode", string.Empty);
                    command.Parameters.AddWithValue("@P_ReceivedQty", Convert.ToString(receivedQty));
                    command.Parameters.AddWithValue("@P_RecLoc", (request.ReceiptLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_Receiver", userCode);
                    command.Parameters.AddWithValue("@P_Active", "True");
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    command.Parameters.AddWithValue("@P_IPAdd", ipAddress);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "MaterialRecCode") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static int SaveMemberReceiptDetails(
            string materialRecCode,
            IEnumerable<MemberReceipt.ReceiveMemberInput> members,
            string userName,
            string lastTransfer,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!HasValidConnectionString)
            {
                errorMessage = "Database connection is not configured.";
                return 0;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Bulk_IU_MemberReceiveddtl", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_MaterialRecCode", (materialRecCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_UserName", (userName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_LastTransfer", (lastTransfer ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_Data", BuildMemberReceiptDetailJson(members));
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return HasColumn(reader, "TotalRecords") ? ReadInt32(reader, "TotalRecords") : 0;
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                errorMessage = ex.Message;
                return 0;
            }

            return 0;
        }

        private static string BuildMemberReceiptDetailJson(IEnumerable<MemberReceipt.ReceiveMemberInput> members)
        {
            var data = new JArray();

            foreach (MemberReceipt.ReceiveMemberInput member in members ?? Enumerable.Empty<MemberReceipt.ReceiveMemberInput>())
            {
                if (member == null || string.IsNullOrWhiteSpace(member.Rfid))
                {
                    continue;
                }

                data.Add(new JObject
                {
                    ["MemberCode"] = (member.MemberCode ?? string.Empty).Trim(),
                    ["RFID"] = (member.Rfid ?? string.Empty).Trim(),
                    ["ActionType"] = string.IsNullOrWhiteSpace(member.ActionType) ? "Y" : member.ActionType.Trim(),
                    ["Comment"] = (member.Comment ?? string.Empty).Trim(),
                    ["MatDispCode"] = (member.MatDispCode ?? string.Empty).Trim()
                });
            }

            return data.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static List<MemberReceipt.ReceiveMemberInput> CleanMemberReceiptMembers(IEnumerable<MemberReceipt.ReceiveMemberInput> members)
        {
            return (members ?? Enumerable.Empty<MemberReceipt.ReceiveMemberInput>())
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.Rfid))
                .Select(member => new MemberReceipt.ReceiveMemberInput
                {
                    Rfid = (member.Rfid ?? string.Empty).Trim(),
                    MemberCode = (member.MemberCode ?? string.Empty).Trim(),
                    MemberType = (member.MemberType ?? string.Empty).Trim(),
                    MemberSize = (member.MemberSize ?? string.Empty).Trim(),
                    MatDispCode = (member.MatDispCode ?? string.Empty).Trim(),
                    TruckNo = (member.TruckNo ?? string.Empty).Trim(),
                    ActionType = string.IsNullOrWhiteSpace(member.ActionType) ? "Y" : member.ActionType.Trim(),
                    Comment = (member.Comment ?? string.Empty).Trim()
                })
                .GroupBy(member => member.Rfid, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<PalletAssemble.AssembleSaveMemberInput> CleanPalletAssembleMembers(IEnumerable<PalletAssemble.AssembleSaveMemberInput> members)
        {
            return (members ?? Enumerable.Empty<PalletAssemble.AssembleSaveMemberInput>())
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.Rfid))
                .Select(member => new PalletAssemble.AssembleSaveMemberInput
                {
                    Rfid = (member.Rfid ?? string.Empty).Trim(),
                    MemberCode = (member.MemberCode ?? string.Empty).Trim()
                })
                .GroupBy(member => member.Rfid, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static string BuildPalletAssembleDetailJson(
            string palletCode,
            string palletOrderdtlCode,
            IEnumerable<PalletAssemble.AssembleSaveMemberInput> members,
            string userCode,
            string ipAddress)
        {
            var data = new JArray();

            foreach (PalletAssemble.AssembleSaveMemberInput member in members ?? Enumerable.Empty<PalletAssemble.AssembleSaveMemberInput>())
            {
                if (member == null || string.IsNullOrWhiteSpace(member.Rfid))
                {
                    continue;
                }

                data.Add(new JArray
                {
                    member.Rfid.Trim(),
                    string.IsNullOrWhiteSpace(palletCode) ? string.Empty : palletCode.Trim(),
                    string.IsNullOrWhiteSpace(palletOrderdtlCode) ? string.Empty : palletOrderdtlCode.Trim(),
                    string.IsNullOrWhiteSpace(userCode) ? string.Empty : userCode.Trim(),
                    string.IsNullOrWhiteSpace(ipAddress) ? string.Empty : ipAddress.Trim()
                });
            }

            return data.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string MapMemberReceiptLocationType(string receiptFrom)
        {
            string value = (receiptFrom ?? string.Empty).Trim();

            if (value.StartsWith("MISC", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            if (string.Equals(value, "Factory", StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000019";
            }

            if (string.Equals(value, "Warehouse", StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000020";
            }

            if (string.Equals(value, "Client", StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000072";
            }

            return string.Empty;
        }

        private static InternalTransfer.MemberValidationResult LoadInternalTransferCurrentMember(string rfid, string currentLocation)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_usp_CheckCurrentMultiStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    command.Parameters.AddWithValue("@P_CurrentLocation", string.IsNullOrWhiteSpace(currentLocation) ? string.Empty : currentLocation.Trim());
                    command.Parameters.AddWithValue("@P_CurrentStatus", "Manufacture,Received,Pallet Disassemble From Client");
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || HasColumn(reader, "IsValid"))
                        {
                            return null;
                        }

                        return new InternalTransfer.MemberValidationResult
                        {
                            Rfid = ReadString(reader, "RFID"),
                            MemberType = ReadString(reader, "MemberType"),
                            MemberSize = ReadString(reader, "MemberSize"),
                            MemberCode = ReadString(reader, "MemberCode"),
                            MemberWeight = ReadString(reader, "MemberWeight"),
                            MemberDimension = ReadString(reader, "MemberDimension"),
                            Active = ReadString(reader, "Active")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static PalletDisassembleCustomer.ValidationResult LoadPalletDisassembleCustomerDispatchMember(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_usp_CheckCurrentMultiStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    command.Parameters.AddWithValue("@P_CurrentLocation", string.Empty);
                    command.Parameters.AddWithValue("@P_CurrentStatus", "Pallet Dispatch");
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || HasColumn(reader, "IsValid"))
                        {
                            return null;
                        }

                        return new PalletDisassembleCustomer.ValidationResult
                        {
                            Success = true,
                            Rfid = ReadString(reader, "RFID"),
                            Message = "Member validated successfully.",
                            Members = new List<PalletDisassembleCustomer.DispatchDetailItem>()
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static InternalTransfer.MemberValidationResult LoadCollectionInternalTransferCurrentMember(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_usp_CheckCurrentMultiStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    command.Parameters.AddWithValue("@P_CurrentLocation", string.Empty);
                    command.Parameters.AddWithValue("@P_CurrentStatus", "Member Collection,Member Collection - Damage");
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || HasColumn(reader, "IsValid"))
                        {
                            return null;
                        }

                        return new InternalTransfer.MemberValidationResult
                        {
                            Rfid = ReadString(reader, "RFID"),
                            MemberType = ReadString(reader, "MemberType"),
                            MemberSize = ReadString(reader, "MemberSize"),
                            MemberCode = ReadString(reader, "MemberCode"),
                            MemberWeight = ReadString(reader, "MemberWeight"),
                            MemberDimension = ReadString(reader, "MemberDimension"),
                            Active = ReadString(reader, "Active")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static InternalTransfer.MemberValidationResult LoadCollectionInternalTransferAnyMember(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_usp_CheckCurrentMultiStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    command.Parameters.AddWithValue("@P_CurrentLocation", string.Empty);
                    command.Parameters.AddWithValue("@P_CurrentStatus", string.Empty);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || HasColumn(reader, "IsValid"))
                        {
                            return null;
                        }

                        return new InternalTransfer.MemberValidationResult
                        {
                            Rfid = ReadString(reader, "RFID"),
                            MemberType = ReadString(reader, "MemberType"),
                            MemberSize = ReadString(reader, "MemberSize"),
                            MemberCode = ReadString(reader, "MemberCode"),
                            MemberWeight = ReadString(reader, "MemberWeight"),
                            MemberDimension = ReadString(reader, "MemberDimension"),
                            Active = ReadString(reader, "Active")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static void ExecuteCollectionInternalTransferViolationMark(
            string rfid,
            string location,
            string entryBy,
            string memberType,
            string memberSize)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_IU_MemberViolation_inCollectionInternalTransfer", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    command.Parameters.AddWithValue("@P_Location", string.IsNullOrWhiteSpace(location) ? string.Empty : location.Trim());
                    command.Parameters.AddWithValue("@P_EntryBy", string.IsNullOrWhiteSpace(entryBy) ? string.Empty : entryBy.Trim());
                    command.Parameters.AddWithValue("@P_Type", string.IsNullOrWhiteSpace(memberType) ? string.Empty : memberType.Trim());
                    command.Parameters.AddWithValue("@P_Size", string.IsNullOrWhiteSpace(memberSize) ? string.Empty : memberSize.Trim());
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (MySqlException)
            {
                return;
            }
        }

        private static List<InternalTransfer.TransferMemberInput> LoadCollectionInternalTransferBulkMembers(string rfid)
        {
            var members = new List<InternalTransfer.TransferMemberInput>();

            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return members;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_InternalTranfer_BulkEntry", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string memberRfid = ReadString(reader, "RFID");
                            if (string.IsNullOrWhiteSpace(memberRfid))
                            {
                                continue;
                            }

                            members.Add(new InternalTransfer.TransferMemberInput
                            {
                                Rfid = memberRfid,
                                TagRfid = memberRfid,
                                MemberCode = ReadString(reader, "MemberCode"),
                                MemberType = ReadString(reader, "MemberType"),
                                MemberSize = ReadString(reader, "MemberSize")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return members;
            }

            return members
                .GroupBy(member => member.Rfid, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static string LoadCollectionMemberTruck(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand(
                    @"SELECT LastTransferTo
                      FROM memberlastupdate
                      WHERE RFID = @P_RFID OR TaggMember = @P_RFID
                      ORDER BY LastUpdatedDate DESC
                      LIMIT 1",
                    connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "LastTransferTo") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }


        private static string LoadMemberLatestTransferTo(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand(
                    @"SELECT LastTransferTo
                      FROM memberlastupdate
                      WHERE RFID = @RFID OR TaggMember = @RFID
                      ORDER BY LastUpdatedDate DESC
                      LIMIT 1",
                    connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@RFID", rfid.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "LastTransferTo") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static string LoadMemberLatestStatus(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand(
                    @"SELECT LastStatus
                      FROM memberlastupdate
                      WHERE RFID = @P_RFID OR TaggMember = @P_RFID
                      ORDER BY LastUpdatedDate DESC
                      LIMIT 1",
                    connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "LastStatus") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static bool IsCollectionTruckMatch(string memberTruck, string selectedTruckCode, string selectedTruckNo)
        {
            string memberValue = NormalizeCompareValue(memberTruck);
            if (string.IsNullOrWhiteSpace(memberValue))
            {
                return true;
            }

            return memberValue == NormalizeCompareValue(selectedTruckCode)
                || memberValue == NormalizeCompareValue(selectedTruckNo);
        }

        private static string NormalizeCompareValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", string.Empty).ToUpperInvariant();
        }

        private static PalletAssemble.AssembleMemberValidationResult LoadPalletAssembleCurrentMember(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_usp_CheckCurrentMultiStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    command.Parameters.AddWithValue("@P_CurrentLocation", string.Empty);
                    command.Parameters.AddWithValue("@P_CurrentStatus", "Received,Received - Damage,Internal Transfer,Pre Dispatch,Pallet Dispatch,Pallet Disassemble From Client,Pallet Assemble");
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || HasColumn(reader, "IsValid"))
                        {
                            return null;
                        }

                        return new PalletAssemble.AssembleMemberValidationResult
                        {
                            IsValid = true,
                            Rfid = ReadString(reader, "RFID"),
                            MemberType = ReadString(reader, "MemberType"),
                            MemberSize = ReadString(reader, "MemberSize"),
                            MemberCode = ReadString(reader, "MemberCode")
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static string SaveInternalTransferMaster(
            InternalTransfer.DispatchSaveRequest request,
            string rfid,
            int totalDispatch,
            string userCode,
            string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_IU_MemberDispatch", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = InternalTransferCommandTimeoutSeconds;
                    command.Parameters.AddWithValue("@P_RFId", (rfid ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_MatDispCode", "0");
                    command.Parameters.AddWithValue("@P_FromLocationType", MapInternalTransferLocationType(request.UserLocationType));
                    command.Parameters.AddWithValue("@P_FromLocation", (request.UserLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_ToLocationType", MapInternalTransferLocationType(request.ToLocationType));
                    command.Parameters.AddWithValue("@P_ToLocation", (request.ToLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_CustomerCode", string.Empty);
                    command.Parameters.AddWithValue("@P_LOCode", string.Empty);
                    command.Parameters.AddWithValue("@P_LOQty", string.Empty);
                    command.Parameters.AddWithValue("@P_TruckNo", (request.TruckNumber ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverName", (request.DriverName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverPhoneNo", (request.DriverContactNo ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_TransportorName", (request.TransporterName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_Dispacher", userCode);
                    command.Parameters.AddWithValue("@P_TotalDispatch", totalDispatch);
                    command.Parameters.AddWithValue("@P_DispatchClosed", "False");
                    command.Parameters.AddWithValue("@P_GenerateChallan", request.GenerateChallan ? "True" : "False");
                    command.Parameters.AddWithValue("@P_Active", "True");
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    command.Parameters.AddWithValue("@P_IPAdd", ipAddress);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "MatDispCode") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }

        private static bool SaveInternalTransferDetails(
            string matDispCode,
            InternalTransfer.DispatchSaveRequest request,
            IEnumerable<InternalTransfer.TransferMemberInput> members,
            string userCode,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!HasValidConnectionString)
            {
                errorMessage = "Database connection is not configured.";
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Bulk_IU_MemberDispatchDtl", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = InternalTransferCommandTimeoutSeconds;
                    command.Parameters.AddWithValue("@P_Data", BuildInternalTransferDetailJson(matDispCode, members));
                    connection.Open();
                    command.ExecuteNonQuery();
                    return true;
                }
            }
            catch (MySqlException ex)
            {
                if (HasInternalTransferDetails(matDispCode))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                if (IsInternalTransferBulkGroupByError(ex)
                    && SaveInternalTransferDetailsDirect(matDispCode, request, members, userCode))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = IsFatalMySqlCommandMessage(ex) ? "Unable to confirm Internal Transfer details. Please try again." : ex.Message;
                return false;
            }
        }

        private static bool IsInternalTransferBulkGroupByError(MySqlException ex)
        {
            if (ex == null || string.IsNullOrWhiteSpace(ex.Message))
            {
                return false;
            }

            return ex.Message.IndexOf("only_full_group_by", StringComparison.OrdinalIgnoreCase) >= 0
                && ex.Message.IndexOf("MatDispDTCode", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool SaveInternalTransferDetailsDirect(
            string matDispCode,
            InternalTransfer.DispatchSaveRequest request,
            IEnumerable<InternalTransfer.TransferMemberInput> members,
            string userCode)
        {
            List<InternalTransfer.TransferMemberInput> cleanMembers = CleanInternalTransferMembers(members);
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(matDispCode) || cleanMembers.Count == 0)
            {
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            for (int index = 0; index < cleanMembers.Count; index++)
                            {
                                InternalTransfer.TransferMemberInput member = cleanMembers[index];
                                string detailCode = matDispCode.Trim() + "D" + (index + 1).ToString("000");

                                using (var command = new MySqlCommand(
                                    @"INSERT INTO memberdispatchdtl
    (MatDispDTCode, MatDispCode, MemberCode, RFID)
SELECT @MatDispDTCode, @MatDispCode, @MemberCode, @RFID
WHERE NOT EXISTS (
    SELECT 1
    FROM memberdispatchdtl
    WHERE MatDispCode = @MatDispCode AND MemberCode = @MemberCode
)", connection, transaction))
                                {
                                    command.CommandType = CommandType.Text;
                                    command.CommandTimeout = InternalTransferCommandTimeoutSeconds;
                                    command.Parameters.AddWithValue("@MatDispDTCode", detailCode);
                                    command.Parameters.AddWithValue("@MatDispCode", matDispCode.Trim());
                                    command.Parameters.AddWithValue("@MemberCode", (member.MemberCode ?? string.Empty).Trim());
                                    command.Parameters.AddWithValue("@RFID", (member.Rfid ?? string.Empty).Trim());
                                    command.ExecuteNonQuery();
                                }

                                UpdateInternalTransferMemberLastUpdateDirect(connection, transaction, request, member, matDispCode, userCode);
                            }

                            transaction.Commit();
                            return HasInternalTransferDetails(matDispCode);
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        private static void UpdateInternalTransferMemberLastUpdateDirect(
            MySqlConnection connection,
            MySqlTransaction transaction,
            InternalTransfer.DispatchSaveRequest request,
            InternalTransfer.TransferMemberInput member,
            string matDispCode,
            string userCode)
        {
            string toLocation = request == null ? string.Empty : (request.ToLocation ?? string.Empty).Trim();

            using (var command = new MySqlCommand(
                @"UPDATE memberlastupdate
SET LastStatus = 'Internal Transfer',
    LastLocation = @ToLocation,
    LastTransferTo = @ToLocation,
    LastTransactionId = @MatDispCode,
    LastEntryBy = @UserCode,
    LastUpdatedDate = NOW()
WHERE RFID = @RFID OR MemberId = @MemberCode OR LastTransactionId = @MemberCode", connection, transaction))
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = InternalTransferCommandTimeoutSeconds;
                command.Parameters.AddWithValue("@ToLocation", toLocation);
                command.Parameters.AddWithValue("@MatDispCode", matDispCode.Trim());
                command.Parameters.AddWithValue("@UserCode", string.IsNullOrWhiteSpace(userCode) ? "SYSTEM" : userCode.Trim());
                command.Parameters.AddWithValue("@RFID", (member.Rfid ?? string.Empty).Trim());
                command.Parameters.AddWithValue("@MemberCode", (member.MemberCode ?? string.Empty).Trim());
                command.ExecuteNonQuery();
            }
        }

        private static bool IsFatalMySqlCommandMessage(MySqlException ex)
        {
            return ex != null
                && !string.IsNullOrWhiteSpace(ex.Message)
                && ex.Message.IndexOf("Fatal error encountered", StringComparison.OrdinalIgnoreCase) >= 0;
        }



        private static bool HasInternalTransferDetails(string matDispCode)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(matDispCode))
            {
                return false;
            }

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (var connection = new MySqlConnection(ConnectionString))
                    using (var command = new MySqlCommand(
                        "SELECT COUNT(1) FROM memberdispatchdtl WHERE MatDispCode = @P_MatDispCode",
                        connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.CommandTimeout = InternalTransferCommandTimeoutSeconds;
                        command.Parameters.AddWithValue("@P_MatDispCode", matDispCode.Trim());
                        connection.Open();

                        object count = command.ExecuteScalar();
                        if (count != null && Convert.ToInt32(count) > 0)
                        {
                            return true;
                        }
                    }
                }
                catch (MySqlException)
                {
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
        }

        private static PalletDispatcch.DispatchBatchItem LoadDispatchBatchByRfid(string rfid)
        {
            if (!HasValidConnectionString)
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_MemberLastUpdateforDispatchByRFID", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapDispatchBatchItem(reader) : null;
                    }
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static HashSet<string> LoadPreDispatchPalletCodes(IEnumerable<string> palletCodes)
        {
            List<string> codes = CleanPalletCodes(palletCodes);
            var validCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!codes.Any())
            {
                return validCodes;
            }

            ExecuteTextReader(
                BuildPreDispatchPalletStatusQuery(codes.Count),
                BuildPalletCodeParameters(codes),
                reader =>
                {
                    validCodes.Add(ReadString(reader, "PalletCode"));
                });

            return validCodes;
        }

        private static PalletPreDispatch.ExistingPreDispatchItem MapExistingPreDispatchItem(IDataRecord reader)
        {
            return new PalletPreDispatch.ExistingPreDispatchItem
            {
                PalletPreDispatchCode = ReadString(reader, "PalletPreDispatchCode"),
                PalletCode = ReadString(reader, "PalletCode"),
                ClientBatchNo = ReadString(reader, "ClientBatchNo"),
                SheetLength = ReadString(reader, "SheetLength"),
                Width = ReadString(reader, "Width"),
                Weight = ReadString(reader, "Weight"),
                Thikness = ReadString(reader, "Thikness"),
                ClientLocation = ReadString(reader, "ClientLocation")
            };
        }

        private static PalletDispatcch.DispatchBatchItem MapDispatchBatchItem(IDataRecord reader)
        {
            return new PalletDispatcch.DispatchBatchItem
            {
                PreDispatchCode = ReadString(reader, "PalletPreDispatchCode"),
                PalletCode = ReadString(reader, "PalletCode"),
                ClientBatchNo = ReadString(reader, "ClientBatchNo"),
                SheetLength = ReadString(reader, "SheetLength"),
                Width = ReadString(reader, "Width"),
                Weight = ReadString(reader, "Weight"),
                Thikness = ReadString(reader, "Thikness"),
                ClientLocation = ReadString(reader, "ClientLocation")
            };
        }

        private static List<string> CleanPalletCodes(IEnumerable<string> palletCodes)
        {
            return (palletCodes ?? Enumerable.Empty<string>())
                .Select(code => (code ?? string.Empty).Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code) && !string.Equals(code, "-", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildClientBatchJson(IEnumerable<string> clientBatchNos)
        {
            return new JArray((clientBatchNos ?? Enumerable.Empty<string>())
                .Select(batch => batch.Trim())
                .Where(batch => !string.IsNullOrWhiteSpace(batch)))
                .ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string BuildPreDispatchPalletStatusQuery(int palletCount)
        {
            var parameters = Enumerable.Range(0, palletCount)
                .Select(index => "@PalletCode" + index);

            return "SELECT PCD.PalletCode " +
                   "FROM palletconfigdt PCD " +
                   "INNER JOIN MemberLastUpdate MLU ON MLU.RFID = PCD.RFIDNO " +
                   "WHERE PCD.PalletCode IN (" + string.Join(",", parameters) + ") " +
                   "GROUP BY PCD.PalletCode " +
                   "HAVING COUNT(DISTINCT REPLACE(MLU.LastStatus, ' ', '')) = 1 " +
                   "AND MAX(REPLACE(MLU.LastStatus, ' ', '')) = 'PreDispatch';";
        }

        private static string BuildAlreadyDispatchQuery(int palletCount)
        {
            var parameters = Enumerable.Range(0, palletCount)
                .Select(index => "@PalletCode" + index);

            return "SELECT COUNT(1) " +
                   "FROM memberlastupdate MLU " +
                   "INNER JOIN palletconfigdt PCD ON MLU.RFID = PCD.RFIDNO " +
                   "WHERE MLU.LastStatus = 'Pallet Dispatch' " +
                   "AND PCD.PalletCode IN (" + string.Join(",", parameters) + ") " +
                   "LIMIT 1;";
        }

        private static Dictionary<string, object> BuildPalletCodeParameters(IList<string> palletCodes)
        {
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < palletCodes.Count; index += 1)
            {
                parameters["@PalletCode" + index] = palletCodes[index];
            }

            return parameters;
        }

        private static string BuildDispatchDetailJson(string palletDispCode, PalletDispatcch.DispatchMasterInput request, IEnumerable<PalletDispatcch.DispatchDetailInput> dispatchItems, string userCode)
        {
            var data = new JArray();

            foreach (PalletDispatcch.DispatchDetailInput item in dispatchItems ?? Enumerable.Empty<PalletDispatcch.DispatchDetailInput>())
            {
                data.Add(new JArray
                {
                    palletDispCode,
                    (item.PalletCode ?? string.Empty).Trim(),
                    (item.ClientBatchNo ?? string.Empty).Trim(),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    (request.FromLocation ?? string.Empty).Trim(),
                    (request.Consignee ?? string.Empty).Trim(),
                    string.Empty,
                    userCode
                });
            }

            return data.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static List<InternalTransfer.TransferMemberInput> CleanInternalTransferMembers(IEnumerable<InternalTransfer.TransferMemberInput> members)
        {
            return (members ?? Enumerable.Empty<InternalTransfer.TransferMemberInput>())
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.Rfid) && !string.IsNullOrWhiteSpace(member.MemberCode))
                .GroupBy(member => member.Rfid.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<PalletDisassembleClient.DisassembleMemberInput> CleanPalletDisassembleClientMembers(IEnumerable<PalletDisassembleClient.DisassembleMemberInput> members)
        {
            return (members ?? Enumerable.Empty<PalletDisassembleClient.DisassembleMemberInput>())
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.Rfid) && !string.IsNullOrWhiteSpace(member.MemberCode))
                .Select(member => new PalletDisassembleClient.DisassembleMemberInput
                {
                    Rfid = (member.Rfid ?? string.Empty).Trim(),
                    MemberCode = (member.MemberCode ?? string.Empty).Trim(),
                    MemberType = (member.MemberType ?? string.Empty).Trim(),
                    MemberSize = (member.MemberSize ?? string.Empty).Trim(),
                    PalletCode = (member.PalletCode ?? string.Empty).Trim()
                })
                .GroupBy(member => member.Rfid, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<PalletDisassembleCustomer.TempSaveMemberInput> CleanPalletDisassembleCustomerTempMembers(IEnumerable<PalletDisassembleCustomer.TempSaveMemberInput> members)
        {
            return (members ?? Enumerable.Empty<PalletDisassembleCustomer.TempSaveMemberInput>())
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.Rfid) && !string.IsNullOrWhiteSpace(member.MemberCode))
                .Select(member => new PalletDisassembleCustomer.TempSaveMemberInput
                {
                    Rfid = (member.Rfid ?? string.Empty).Trim(),
                    MemberCode = (member.MemberCode ?? string.Empty).Trim(),
                    MemberSize = (member.MemberSize ?? string.Empty).Trim(),
                    PalletCode = (member.PalletCode ?? string.Empty).Trim()
                })
                .GroupBy(member => member.Rfid, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static string BuildPalletDisassembleCustomerTempJson(
            string custDismentleCode,
            IEnumerable<PalletDisassembleCustomer.TempSaveMemberInput> members,
            string userCode)
        {
            var data = new JArray();

            foreach (PalletDisassembleCustomer.TempSaveMemberInput member in members ?? Enumerable.Empty<PalletDisassembleCustomer.TempSaveMemberInput>())
            {
                data.Add(new JArray
                {
                    (custDismentleCode ?? string.Empty).Trim(),
                    (member.Rfid ?? string.Empty).Trim(),
                    (member.MemberCode ?? string.Empty).Trim(),
                    (userCode ?? string.Empty).Trim()
                });
            }

            return data.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static List<PalletDisassembleCustomer.FinalSaveMemberInput> CleanPalletDisassembleCustomerFinalMembers(IEnumerable<PalletDisassembleCustomer.FinalSaveGroupInput> groups)
        {
            return (groups ?? Enumerable.Empty<PalletDisassembleCustomer.FinalSaveGroupInput>())
                .Where(group => group != null && group.Members != null)
                .SelectMany(group => group.Members.Select(member => new PalletDisassembleCustomer.FinalSaveMemberInput
                {
                    Rfid = (member.Rfid ?? string.Empty).Trim(),
                    MemberCode = (member.MemberCode ?? string.Empty).Trim(),
                    PalletCode = string.IsNullOrWhiteSpace(member.PalletCode) ? (group.PalletCode ?? string.Empty).Trim() : member.PalletCode.Trim(),
                    ClientBatchNo = (member.ClientBatchNo ?? string.Empty).Trim(),
                    ActionType = string.IsNullOrWhiteSpace(member.ActionType) ? "Y" : member.ActionType.Trim(),
                    Comment = (member.Comment ?? string.Empty).Trim()
                }))
                .Where(member => !string.IsNullOrWhiteSpace(member.Rfid))
                .GroupBy(member => member.Rfid, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static string BuildPalletDisassembleCustomerFinalJson(
            string custDismentleCode,
            IEnumerable<PalletDisassembleCustomer.FinalSaveMemberInput> members,
            string userCode,
            string ipAddress)
        {
            var data = new JArray();

            foreach (PalletDisassembleCustomer.FinalSaveMemberInput member in members ?? Enumerable.Empty<PalletDisassembleCustomer.FinalSaveMemberInput>())
            {
                data.Add(new JArray
                {
                    "0",
                    (custDismentleCode ?? string.Empty).Trim(),
                    (member.PalletCode ?? string.Empty).Trim(),
                    (member.ClientBatchNo ?? string.Empty).Trim(),
                    (member.Rfid ?? string.Empty).Trim(),
                    string.IsNullOrWhiteSpace(member.ActionType) ? "Y" : member.ActionType.Trim(),
                    (member.Comment ?? string.Empty).Trim(),
                    (userCode ?? string.Empty).Trim(),
                    (ipAddress ?? string.Empty).Trim()
                });
            }

            return data.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string BuildPalletDisassembleClientDetailJson(IEnumerable<PalletDisassembleClient.DisassembleMemberInput> members)
        {
            var data = new JArray();

            foreach (PalletDisassembleClient.DisassembleMemberInput member in members ?? Enumerable.Empty<PalletDisassembleClient.DisassembleMemberInput>())
            {
                if (member == null || string.IsNullOrWhiteSpace(member.Rfid) || string.IsNullOrWhiteSpace(member.MemberCode))
                {
                    continue;
                }

                data.Add(new JObject
                {
                    ["RFID"] = (member.Rfid ?? string.Empty).Trim(),
                    ["MemberCode"] = (member.MemberCode ?? string.Empty).Trim()
                });
            }

            return data.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string BuildInternalTransferDetailJson(string matDispCode, IEnumerable<InternalTransfer.TransferMemberInput> members)
        {
            var data = new JArray();

            foreach (InternalTransfer.TransferMemberInput member in members ?? Enumerable.Empty<InternalTransfer.TransferMemberInput>())
            {
                data.Add(new JArray
                {
                    "0",
                    matDispCode,
                    (member.MemberCode ?? string.Empty).Trim(),
                    (member.Rfid ?? string.Empty).Trim()
                });
            }

            return data.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string MapInternalTransferLocationType(string locationType)
        {
            if (string.Equals(locationType, "Factory", StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000019";
            }

            if (string.Equals(locationType, "Warehouse", StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000020";
            }

            if (string.Equals(locationType, "Client", StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000072";
            }

            if (string.Equals(locationType, "Collection Truck", StringComparison.OrdinalIgnoreCase)
                || string.Equals(locationType, "CollectionTruck", StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000074";
            }
            if (!string.IsNullOrWhiteSpace(locationType) && locationType.Trim().StartsWith("MISC", StringComparison.OrdinalIgnoreCase))
            {
                return locationType.Trim();
            }

            return string.Empty;
        }

        public class LoginUser
        {
            public string UserCode { get; set; }

            public string UserFullName { get; set; }

            public string UserType { get; set; }

            public string UserName { get; set; }

            public string UserRole { get; set; }

            public List<MenuAccessItem> MenuAccess { get; set; }
        }

        public class MenuAccessItem
        {
            public string MenuDTCode { get; set; }

            public string MenuDTName { get; set; }

            public string Type { get; set; }

            public string TransactionID { get; set; }
        }

        private class BatchNumberUploadSource
        {
            public string TableName { get; set; }

            public HashSet<string> Columns { get; set; }

            public string BatchColumn { get; set; }
        }
        public class NewBatchNumberStatus
        {
            public bool Exists { get; set; }

            public bool Active { get; set; }

            public bool Used { get; set; }

            public bool TableAvailable { get; set; }

            public string Message { get; set; }
        }

        public class ExistingMemberForBatchMapping
        {
            public string MemberCode { get; set; }

            public string Rfid { get; set; }

            public string SkuCode { get; set; }
        }
        public class MemberSaveResult
        {
            public string MemberCode { get; set; }

            public string Message { get; set; }
        }

        private static string GetConnectionString()
        {
            string connectionString = SecretSettingReader.Read("APTS_CONNECTION_STRING", string.Empty);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return NormalizeMySqlConnectionString(connectionString);
            }

            ConnectionStringSettings connectionSetting = ConfigurationManager.ConnectionStrings["ConnectionString"];
            return connectionSetting == null
                ? string.Empty
                : NormalizeMySqlConnectionString(SecretSettingReader.NormalizeSecretValue(connectionSetting.ConnectionString));
        }

        private static string NormalizeMySqlConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return string.Empty;
            }

            var builder = new MySqlConnectionStringBuilder(connectionString)
            {
                AllowPublicKeyRetrieval = true
            };

            return builder.ConnectionString;
        }

        private static void AddParameters(MySqlCommand command, IDictionary<string, object> parameters)
        {
            if (parameters == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
            }
        }

        public List<MemberCollection.CustomerShipToLocationItem> LoadMemberCollectionCustomerList(string userCode)
        {
            var locations = new List<MemberCollection.CustomerShipToLocationItem>();

            if (!HasValidConnectionString)
            {
                return locations;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_get_CustShipToLocMaster", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            locations.Add(new MemberCollection.CustomerShipToLocationItem
                            {
                                ShipToLocCode = ReadString(reader, "ShipToLocCode"),
                                ShipToLocTitle = ReadString(reader, "ShipToLocTitle"),
                                ShipToLocCompany = ReadString(reader, "ShipToLocCompany"),
                                LocationAddress = ReadString(reader, "LocationAddress"),
                                EmailId = ReadString(reader, "EmailId")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return locations;
            }

            return locations;
        }

        public List<MemberCollection.TransportItem> LoadTransportListInMemColl()
        {
            var transports = new List<MemberCollection.TransportItem>();

            if (!HasValidConnectionString)
            {
                return transports;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_list_transport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            transports.Add(new MemberCollection.TransportItem
                            {
                                TransportCode = ReadString(reader, "TransportCode"),
                                VehicleNo = ReadString(reader, "VehicleNo"),
                                TransporterName = ReadString(reader, "TransporterName"),
                                DriverName = ReadString(reader, "DriverName"),
                                MobileNo = ReadString(reader, "MobileNo"),
                                Active = ReadString(reader, "Active")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return transports;
            }

            return transports;
        }

        public List<MemberCollection.CollectionMemberItem> LoadMemberCollectionItemsByRfid(string rfid)
        {

            var items = new List<MemberCollection.CollectionMemberItem>();

            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return items;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_getPalletDismentleCustDt_ByRFID", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFID", rfid.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new MemberCollection.CollectionMemberItem
                            {
                                CustDismentleCode = ReadString(reader, "CustDismentleCode"),
                                CustCode = ReadString(reader, "CustCode"),
                                RFIDNo = ReadString(reader, "RFIDNo"),
                                MemberType = ReadString(reader, "MemberType"),
                                MemberSize = ReadString(reader, "MemberSize"),
                                ActionType = ReadString(reader, "ActionType")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return items;
            }

            return items;
        }

        public MemberCollection.MemberCollectionSaveResult SaveMemberCollection(
            MemberCollection.MemberCollectionSaveRequest request,
            string userCode,
            string userName,
            string ipAddress)
        {
            List<MemberCollection.CollectionMemberItem> members = CleanMemberCollectionMembers(request == null ? null : request.Members);
            if (request == null || !members.Any())
            {
                return new MemberCollection.MemberCollectionSaveResult
                {
                    Success = false,
                    Message = "Please add at least one member."
                };
            }

            List<string> dismantleCodes = members
                .Select(item => (item.CustDismentleCode ?? string.Empty).Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!dismantleCodes.Any() || !HasValidConnectionString)
            {
                return new MemberCollection.MemberCollectionSaveResult
                {
                    Success = false,
                    Message = dismantleCodes.Any()
                        ? "Unable to save Member Collection."
                        : "Customer dismantle code not found."
                };
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Bulk_IU_MemberCollection", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_FromLocation", (request.FromLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_TruckNo", (request.TruckNo ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverName", (request.DriverName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverNo", (request.DriverContactNo ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_TransportCode", (request.TransportCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_CollectorName", string.IsNullOrWhiteSpace(userName) ? userCode : userName);
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    command.Parameters.AddWithValue("@P_IPAdd", ipAddress);
                    command.Parameters.AddWithValue("@P_DismantleCodes", new JArray(dismantleCodes).ToString(Newtonsoft.Json.Formatting.None));
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return new MemberCollection.MemberCollectionSaveResult
                            {
                                Success = false,
                                Message = "Unable to save Member Collection."
                            };
                        }

                        string collectionCode = ReadString(reader, "CollectionCode");
                        if (string.IsNullOrWhiteSpace(collectionCode))
                        {
                            return new MemberCollection.MemberCollectionSaveResult
                            {
                                Success = false,
                                Message = "Unable to save Member Collection."
                            };
                        }

                        return new MemberCollection.MemberCollectionSaveResult
                        {
                            Success = true,
                            CollectionCode = collectionCode,
                            TotalRecords = HasColumn(reader, "TotalRecords") ? ReadInt32(reader, "TotalRecords") : members.Count,
                            Message = "Member Collection Saved Successfully"
                        };
                    }
                }
            }
            catch (MySqlException)
            {
                return new MemberCollection.MemberCollectionSaveResult
                {
                    Success = false,
                    Message = "Unable to save Member Collection."
                };
            }
        }

        private static List<MemberCollection.CollectionMemberItem> CleanMemberCollectionMembers(
            IEnumerable<MemberCollection.CollectionMemberItem> members)
        {
            return (members ?? Enumerable.Empty<MemberCollection.CollectionMemberItem>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.RFIDNo))
                .GroupBy(item => item.RFIDNo.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static string SaveMemberCollectionMaster(
            MemberCollection.MemberCollectionSaveRequest request,
            string userCode,
            string userName,
            string ipAddress)
        {
            if (!HasValidConnectionString)
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_iu_memberCollection", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_CollectionCode", "0");
                    command.Parameters.AddWithValue("@P_FromLocationType", "MISC0000073");
                    command.Parameters.AddWithValue("@P_FromLocation", (request.FromLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_CustomerCode", (request.FromLocation ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_ToLocationType", "MISC0000074");
                    command.Parameters.AddWithValue("@P_ToLocation", (request.TruckNo ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_CollectionDate", DateTime.Now);
                    command.Parameters.AddWithValue("@P_CollectorName", string.IsNullOrWhiteSpace(userName) ? userCode : userName);
                    command.Parameters.AddWithValue("@P_TruckNo", (request.TruckNo ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverName", (request.DriverName ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_DriverNo", (request.DriverContactNo ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_GenerateChallan", "True");
                    command.Parameters.AddWithValue("@P_TransportCode", (request.TransportCode ?? string.Empty).Trim());
                    command.Parameters.AddWithValue("@P_Active", "True");
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    command.Parameters.AddWithValue("@P_IPAdd", ipAddress);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            if (reader.Read())
                            {
                                string code = Convert.ToString(reader.GetValue(0));
                                if (!string.IsNullOrWhiteSpace(code))
                                {
                                    return code.Trim();
                                }
                            }
                        }
                        while (reader.NextResult());
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static bool SaveMemberCollectionDetailGroup(
            string collectionCode,
            string dismantleCode,
            string userCode,
            string lastTransferTo)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(collectionCode) || string.IsNullOrWhiteSpace(dismantleCode))
            {
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Bulk_MemberCollDtInsert", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_CollectionCode", collectionCode);
                    command.Parameters.AddWithValue("@P_CustDismentleCode", dismantleCode);
                    command.Parameters.AddWithValue("@P_CreatedUser", userCode);
                    command.Parameters.AddWithValue("@P_LastTransferTo", (lastTransferTo ?? string.Empty).Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            while (reader.Read())
                            {
                            }
                        }
                        while (reader.NextResult());
                    }
                }

                return true;
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        private static bool FreeMemberCollectionModules(string collectionCode, string dismantleCode)
        {
            if (!HasValidConnectionString
                || string.IsNullOrWhiteSpace(collectionCode)
                || string.IsNullOrWhiteSpace(dismantleCode))
            {
                return false;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Free_MemberCollectionModules_Optimized", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_CollectionCode", collectionCode.Trim());
                    command.Parameters.AddWithValue("@P_CustDismentleCode", dismantleCode.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            while (reader.Read())
                            {
                            }
                        }
                        while (reader.NextResult());
                    }
                }

                return true;
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        public List<MemberTransactionReport.ReportRow> LoadMemberTransactionReport(string locationCode, DateTime fromDate, DateTime toDate)
        {
            var rows = new List<MemberTransactionReport.ReportRow>();

            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(locationCode))
            {
                return rows;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Rpt_MemberStatusReport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@Location", locationCode.Trim());
                    command.Parameters.AddWithValue("@FromDate", fromDate.Date);
                    command.Parameters.AddWithValue("@ToDate", toDate.Date);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new MemberTransactionReport.ReportRow
                            {
                                FromLocation = ReadString(reader, "FromLocation"),
                                ToLocation = HasColumn(reader, "ToLocation") ? ReadString(reader, "ToLocation") : ReadString(reader, "ToLoaction"),
                                Status = ReadString(reader, "status"),
                                TotalMember = ReadInt32(reader, "TotalMember")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return rows;
            }

            return rows;
        }

        public List<CurrentStockReport.DropdownItem> LoadCurrentStockClients(string userCode)
        {
            var customers = new List<CurrentStockReport.DropdownItem>();

            if (!HasValidConnectionString)
            {
                return customers;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Get_CustomerList", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            customers.Add(new CurrentStockReport.DropdownItem
                            {
                                Value = ReadString(reader, "CustomerCode"),
                                Text = ReadString(reader, "CustName")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return customers;
            }

            return customers;
        }

        public HighAgeReport.ReportResult LoadHighAgeReport(string highAgeType, string locationCode, string totalDays)
        {
            var result = new HighAgeReport.ReportResult();

            if (!HasValidConnectionString)
            {
                return result;
            }

            bool isCustomer = string.Equals(highAgeType, "Customer", StringComparison.OrdinalIgnoreCase);

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Rpt_HighAgePallete", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@ClientLocationCode", isCustomer ? (object)DBNull.Value : locationCode);
                    command.Parameters.AddWithValue("@CustLocationCode", isCustomer ? (object)locationCode : DBNull.Value);
                    command.Parameters.AddWithValue("@P_days", totalDays);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read() && reader.FieldCount > 0)
                        {
                            result.Count = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                        }

                        if (reader.NextResult())
                        {
                            while (reader.Read())
                            {
                                result.Rows.Add(isCustomer
                                    ? ReadHighAgeCustomerRow(reader)
                                    : ReadHighAgeClientRow(reader));
                            }
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return result;
            }

            return result;
        }

        private static HighAgeReport.ReportRow ReadHighAgeClientRow(IDataRecord reader)
        {
            return new HighAgeReport.ReportRow
            {
                LocationTitle = ReadString(reader, "LocationTitle"),
                PreDispatchDate = ReadString(reader, "PreDispatchdate"),
                TotalMember = ReadString(reader, "TotalMember"),
                Status = reader.FieldCount > 3 ? ReadString(reader, 3) : "PreDispatch",
                Ageing = ReadString(reader, "Ageing")
            };
        }

        private static HighAgeReport.ReportRow ReadHighAgeCustomerRow(IDataRecord reader)
        {
            return new HighAgeReport.ReportRow
            {
                ClientBatchNo = ReadString(reader, "ClientBatchNo"),
                FromLocation = ReadString(reader, "FromLocation"),
                ToLocation = ReadString(reader, "ToLocation"),
                DispatchDate = ReadString(reader, "DispatchDate"),
                Members = ReadString(reader, "Members"),
                Status = reader.FieldCount > 5 ? ReadString(reader, 5) : "PalletDisp",
                Ageing = ReadString(reader, "Ageing")
            };
        }

        public List<HighAgeReport.DropdownItem> LoadHighAgeClientLocations(string userCode)
        {
            var clients = new List<HighAgeReport.DropdownItem>();

            if (!HasValidConnectionString)
            {
                return clients;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_get_CustomerList", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            clients.Add(new HighAgeReport.DropdownItem
                            {
                                Value = ReadString(reader, "LocationCode"),
                                Text = ReadString(reader, "CustName")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return clients;
            }

            return clients;
        }

        public List<HighAgeReport.DropdownItem> LoadHighAgeCustomerLocations(string userCode)
        {
            var locations = new List<HighAgeReport.DropdownItem>();

            if (!HasValidConnectionString)
            {
                return locations;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_get_CustShipToLocMaster", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_UserCode", userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            locations.Add(new HighAgeReport.DropdownItem
                            {
                                Value = ReadString(reader, "ShipToLocCode"),
                                Text = ReadString(reader, "ShipToLocTitle")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return locations;
            }

            return locations;
        }


        public DiscrepancyReport.Result LoadDiscrepancyReport(string discrepancyType, DateTime fromDate, DateTime toDate)
        {
            var result = new DiscrepancyReport.Result();

            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(discrepancyType))
            {
                return result;
            }

            string procedureName = string.Equals(discrepancyType, "Received", StringComparison.OrdinalIgnoreCase)
                ? "WebApp_Rpt_Recived_DiscrepancyReport"
                : "WebApp_Rpt_Disassemble_DiscrepancyReport";

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@fromdate", fromDate.Date);
                    command.Parameters.AddWithValue("@ToDate", toDate.Date);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string actionType = ReadString(reader, "ActionType");
                            int count = ReadInt32(reader, "Count");

                            if (string.Equals(actionType, "Damage", StringComparison.OrdinalIgnoreCase))
                            {
                                result.DamageCount = count;
                            }
                            else if (string.Equals(actionType, "Loss", StringComparison.OrdinalIgnoreCase))
                            {
                                result.LossCount = count;
                            }
                        }

                        if (reader.NextResult())
                        {
                            ReadDiscrepancyRows(reader, result.Rows);
                        }

                        if (reader.NextResult())
                        {
                            ReadDiscrepancyRows(reader, result.Rows);
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return result;
            }

            return result;
        }

        private static void ReadDiscrepancyRows(IDataReader reader, List<DiscrepancyReport.Row> rows)
        {
            while (reader.Read())
            {
                rows.Add(new DiscrepancyReport.Row
                {
                    RFIDNo = ReadString(reader, "RFIDNo"),
                    FromLocation = ReadString(reader, "fromLocation"),
                    ToLocation = HasColumn(reader, "ToLocation") ? ReadString(reader, "ToLocation") : ReadString(reader, "ToLoaction"),
                    LastEntryBy = ReadString(reader, "LastEntryBy"),
                    ActionType = ReadString(reader, "ActionType"),
                    Date = ReadString(reader, "Date")
                });
            }
        }

        public List<MemberByClientBatchNoReport.ReportRow> LoadMemberByClientBatchNoReport(string clientBatchNo)
        {
            if (!HasValidConnectionString)
            {
                throw new InvalidOperationException("Database connection is not configured.");
            }

            var rows = new List<MemberByClientBatchNoReport.ReportRow>();
            using (var connection = new MySqlConnection(ConnectionString))
            using (var command = new MySqlCommand("WebApp_RPT_MemberLastUpdateByClientBatchNo", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@p_ClientBatchNo", MySqlDbType.VarChar, 50).Value = clientBatchNo.Trim();
                connection.Open();

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new MemberByClientBatchNoReport.ReportRow
                        {
                            RFID = ReadString(reader, "RFID"),
                            LastEntryBy = ReadString(reader, "LastEntryBy"),
                            LastUpdatedDate = ReadString(reader, "LastUpdatedDate"),
                            LastTransferTo = ReadString(reader, "LastTransferTo"),
                            LastStatus = ReadString(reader, "LastStatus")
                        });
                    }
                }
            }

            return rows;
        }

        public QueryReport.Result LoadQueryReport(string rfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(rfid))
            {
                return null;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Rpt_QueryScreen", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_RFId", rfid.Trim());
                    connection.Open();

                    QueryReport.Result result;

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        result = new QueryReport.Result
                        {
                            RFID = ReadString(reader, "RFID"),
                            AllTagRFIDs = ReadString(reader, "AllTagRFIDs"),
                            FromLocation = ReadString(reader, "fromLocation"),
                            ToLocation = ReadString(reader, "ToLocation"),
                            MemberType = ReadFirstString(reader, "MemberType", "MemberTypeName"),
                            MemberSize = ReadFirstString(reader, "MemberSize", "MemberDimension"),
                            LastEntryBy = ReadString(reader, "LastEntryBy"),
                            LastUpdatedDate = ReadString(reader, "LastUpdatedDate"),
                            LastStatus = ReadString(reader, "LastStatus"),
                            ClientBatchNo = ReadString(reader, "ClientBatchNo")
                        };
                    }

                    PopulateQueryReportMemberDetails(connection, result, rfid);
                    return result;
                }
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        private static void PopulateQueryReportMemberDetails(MySqlConnection connection, QueryReport.Result result, string scannedRfid)
        {
            if (connection == null || result == null
                || (!string.IsNullOrWhiteSpace(result.MemberType) && !string.IsNullOrWhiteSpace(result.MemberSize)))
            {
                return;
            }

            string lookupRfid = !string.IsNullOrWhiteSpace(result.RFID) ? result.RFID : scannedRfid;

            using (var command = new MySqlCommand(
                @"SELECT COALESCE(MP.ParameterName, MM.MemberType, '') AS MemberType,
       COALESCE(SK.MemberSize, MM.MemberDimension, '') AS MemberSize
FROM membermaster MM
LEFT JOIN skewmaster SK ON SK.SKEWCode = MM.SKEWCode
LEFT JOIN miscparameter MP ON MP.ParameterCode = MM.MemberType
LEFT JOIN membertagging MT ON MT.OrignalRFID = MM.RFID
WHERE MM.RFID = @RFID
   OR MM.MemberCode = @RFID
   OR MT.TagRFID = @RFID
   OR MT.OrignalRFID = @RFID
LIMIT 1",
                connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.AddWithValue("@RFID", (lookupRfid ?? string.Empty).Trim());

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(result.MemberType))
                    {
                        result.MemberType = ReadString(reader, "MemberType");
                    }

                    if (string.IsNullOrWhiteSpace(result.MemberSize))
                    {
                        result.MemberSize = ReadString(reader, "MemberSize");
                    }
                }
            }
        }

        public List<CurrentStockReport.ReportRow> LoadCurrentStockReport(string locationCode, DateTime fromDate, DateTime toDate)
        {
            var rows = new List<CurrentStockReport.ReportRow>();

            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(locationCode))
            {
                return rows;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_Rpt_MemberCurrentStock", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_LocationCode", locationCode.Trim());
                    command.Parameters.AddWithValue("@P_FromDt", fromDate.Date);
                    command.Parameters.AddWithValue("@P_ToDt", toDate.Date);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new CurrentStockReport.ReportRow
                            {
                                ParameterName = ReadString(reader, 0),
                                MemberSize = ReadString(reader, 1),
                                Count = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2))
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return rows;
            }

            return rows;
        }


        public List<CurrentStockReport.DropdownItem> LoadCurrentStockTrucks()
        {
            var trucks = new List<CurrentStockReport.DropdownItem>();

            if (!HasValidConnectionString)
            {
                return trucks;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_list_transport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string truckNo = ReadString(reader, "VehicleNo");

                            if (!string.IsNullOrWhiteSpace(truckNo))
                            {
                                trucks.Add(new CurrentStockReport.DropdownItem
                                {
                                    Value = truckNo,
                                    Text = truckNo
                                });
                            }
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return trucks;
            }

            return trucks;
        }

        public List<CurrentStockReport.DropdownItem> LoadCurrentStockLocationsByType(string locationType, string userCode)
        {
            var locations = new List<CurrentStockReport.DropdownItem>();

            if (!HasValidConnectionString)
            {
                return locations;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("WebApp_get_LocationByType", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_LocationType", string.IsNullOrWhiteSpace(locationType) ? (object)DBNull.Value : locationType.Trim());
                    command.Parameters.AddWithValue("@P_UserCode", string.IsNullOrWhiteSpace(userCode) ? (object)DBNull.Value : userCode);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            locations.Add(new CurrentStockReport.DropdownItem
                            {
                                Value = ReadString(reader, "LocationCode"),
                                Text = ReadString(reader, "LocationTitle")
                            });
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                return locations;
            }

            return locations;
        }

        public string LoadRfidByStickerMemCollection(string tagRfid)
        {
            if (!HasValidConnectionString || string.IsNullOrWhiteSpace(tagRfid))
            {
                return string.Empty;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand("Gen_Get_RFIDBySticker", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@P_TagRFID", tagRfid.Trim());
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadString(reader, "RFID") : string.Empty;
                    }
                }
            }
            catch (MySqlException)
            {
                return string.Empty;
            }
        }
        public DataSet GetDispatchDetails(string dispatchCode)
        {
            return ExecuteStoredProcedureDataSet("WebApp_Challan_InternalTransferDetails",
                new Dictionary<string, object> { { "P_DispatchCode", dispatchCode } });
        }

        public DataSet GetDispatchMemberDetails(string dispatchCode)
        {
            return ExecuteStoredProcedureDataSet(
                "WebApp_Get_dispatchMemberSKUWise",
                new Dictionary<string, object> { { "P_MatDispCode", dispatchCode } });
        }

        public DataSet GetMemberCollectionDetails(string collectionCode)
        {
            DataSet dataSet = ExecuteStoredProcedureDataSet(
                "WebApp_Get_CollectionDetails",
                new Dictionary<string, object> { { "P_CollectionCode", collectionCode } });

            ReplaceMemberCollectionChallanDetails(dataSet, collectionCode);
            return dataSet;
        }

        public DataSet GetPalletDispatchDetailsForChallan(string dispatchCode)
        {
            return ExecuteStoredProcedureDataSet("WebApp_Get_PalletDispatchChallanDtForEmail", 
                new Dictionary<string, object> { { "P_PalletDispCode", dispatchCode } });
        }

        public DataSet GetPalletDismentleDetails(string custDismentleCode)
        {
            return ExecuteStoredProcedureDataSet(
                "WebApp_Get_PalletDismentleChallanDtForEmail",
                new Dictionary<string, object> { { "P_CustDismentleCode", custDismentleCode } });
        }

        public DataSet GetPalletOrderChallanDetails(string palletOrderCode)
        {
            return ExecuteStoredProcedureDataSet(
                "WebApp_Challan_PalletOrderDetails",
                new Dictionary<string, object> { { "P_PalletOrderCode", palletOrderCode } });
        }

        private static void ReplaceDispatchChallanMemberSummary(DataSet dataSet, string dispatchCode)
        {
            if (dataSet == null || dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0
                || !HasValidConnectionString || string.IsNullOrWhiteSpace(dispatchCode))
            {
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    DataTable memberSummary = LoadDispatchChallanMemberSummary(connection, dispatchCode);
                    DataTable totalSummary = LoadDispatchChallanTotalSummary(connection, dispatchCode);

                    while (dataSet.Tables.Count > 1)
                    {
                        dataSet.Tables.RemoveAt(1);
                    }

                    dataSet.Tables.Add(memberSummary);
                    dataSet.Tables.Add(totalSummary);
                }
            }
            catch (MySqlException)
            {
                return;
            }
        }

        private static DataTable LoadDispatchChallanMemberSummary(MySqlConnection connection, string dispatchCode)
        {
            using (var command = new MySqlCommand(
                @"SELECT COALESCE(MP.ParameterName, '') AS MemberType,
       COALESCE(SK.MemberSize, '') AS MemberWeight,
       COUNT(*) AS TOTAL
FROM memberdispatchdtl MDD
INNER JOIN membermaster MM ON MM.MemberCode = MDD.MemberCode
LEFT JOIN skewmaster SK ON SK.SKEWCode = MM.SKEWCode
LEFT JOIN miscparameter MP ON MP.ParameterCode = MM.MemberType
WHERE MDD.MatDispCode = @DispatchCode
GROUP BY MP.ParameterName, SK.MemberSize
ORDER BY MP.ParameterName, SK.MemberSize",
                connection))
            using (var adapter = new MySqlDataAdapter(command))
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 1600;
                command.Parameters.AddWithValue("@DispatchCode", dispatchCode.Trim());

                var table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }

        private static DataTable LoadDispatchChallanTotalSummary(MySqlConnection connection, string dispatchCode)
        {
            using (var command = new MySqlCommand(
                @"SELECT COUNT(*) AS TotalQty
FROM memberdispatchdtl
WHERE MatDispCode = @DispatchCode",
                connection))
            using (var adapter = new MySqlDataAdapter(command))
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 1600;
                command.Parameters.AddWithValue("@DispatchCode", dispatchCode.Trim());

                var table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }
        private static void ReplaceMemberCollectionChallanDetails(DataSet dataSet, string collectionCode)
        {
            if (dataSet == null || dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0
                || !HasValidConnectionString || string.IsNullOrWhiteSpace(collectionCode))
            {
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();

                    DataTable palletDetails = LoadMemberCollectionPalletDetails(connection, collectionCode);
                    DataTable memberDetails = LoadMemberCollectionMemberDetails(connection, collectionCode);
                    DataTable discrepancyDetails = LoadMemberCollectionDiscrepancyDetails(connection, collectionCode);
                    DataTable totalDetails = LoadMemberCollectionTotalDetails(connection, collectionCode);

                    while (dataSet.Tables.Count > 1)
                    {
                        dataSet.Tables.RemoveAt(1);
                    }

                    dataSet.Tables.Add(palletDetails);
                    dataSet.Tables.Add(memberDetails);
                    dataSet.Tables.Add(discrepancyDetails);
                    dataSet.Tables.Add(totalDetails);
                }
            }
            catch (MySqlException)
            {
                return;
            }
        }

        private static DataTable LoadMemberCollectionPalletDetails(MySqlConnection connection, string collectionCode)
        {
            return ExecuteMemberCollectionTextTable(
                connection,
                @"SELECT S.ClientBatchNo,
       GROUP_CONCAT(S.MemberSize ORDER BY S.SizeDisplayOrder, S.MemberSize SEPARATOR 'X') AS PalletMemberSize,
       GROUP_CONCAT(S.size_count ORDER BY S.SizeDisplayOrder, S.MemberSize SEPARATOR 'X') AS PalletMemberQty,
       MC.FromLocation AS DispatchShipToCode,
       COALESCE(ST.ShipToLocTitle, CM.CustName, '') AS DispatchedShipToName
FROM (
    SELECT MCD.CollectionCode, PDD.ClientBatchNo, SK.MemberSize, COUNT(*) AS size_count,
           CASE
               WHEN UPPER(COALESCE(MP.ParameterName, '')) LIKE 'CROSS%' THEN 1
               WHEN UPPER(COALESCE(MP.ParameterName, '')) LIKE 'CM%' THEN 1
               WHEN UPPER(COALESCE(MP.ParameterName, '')) LIKE 'LONG%' THEN 2
               WHEN UPPER(COALESCE(MP.ParameterName, '')) LIKE 'LM%' THEN 2
               ELSE 9
           END AS SizeDisplayOrder
    FROM membercollectiondtl MCD
    INNER JOIN palletdismentlecustomerdt PDD ON PDD.CustDismentleCode = MCD.CustDismentleCode AND PDD.MemberCode = MCD.MemberCode
    INNER JOIN membermaster MM ON MM.MemberCode = MCD.MemberCode
    INNER JOIN skewmaster SK ON SK.SKEWCode = MM.SKEWCode
    LEFT JOIN miscparameter MP ON MP.ParameterCode = MM.MemberType
    WHERE MCD.CollectionCode = @CollectionCode AND PDD.ActionType = 'Y'
    GROUP BY MCD.CollectionCode, PDD.ClientBatchNo, SK.MemberSize, SizeDisplayOrder
) S
INNER JOIN membercollection MC ON MC.CollectionCode = S.CollectionCode
LEFT JOIN shiptolocation ST ON ST.ShipToLocCode = MC.FromLocation
LEFT JOIN customermaster CM ON CM.CustomerCode = MC.CustomerCode
GROUP BY S.ClientBatchNo, MC.FromLocation, ST.ShipToLocTitle, CM.CustName
ORDER BY S.ClientBatchNo",
                collectionCode);
        }

        private static DataTable LoadMemberCollectionMemberDetails(MySqlConnection connection, string collectionCode)
        {
            return ExecuteMemberCollectionTextTable(
                connection,
                @"SELECT CONCAT(MP.ParameterName, ' ', SK.MemberSize) AS MemberSKU,
       COUNT(*) AS Qty
FROM membercollectiondtl MCD
INNER JOIN membermaster MM ON MM.MemberCode = MCD.MemberCode
INNER JOIN skewmaster SK ON SK.SKEWCode = MM.SKEWCode
LEFT JOIN miscparameter MP ON MP.ParameterCode = MM.MemberType
WHERE MCD.CollectionCode = @CollectionCode
GROUP BY MP.ParameterName, SK.MemberSize
ORDER BY MP.ParameterName, SK.MemberSize",
                collectionCode);
        }

        private static DataTable LoadMemberCollectionDiscrepancyDetails(MySqlConnection connection, string collectionCode)
        {
            return ExecuteMemberCollectionTextTable(
                connection,
                @"SELECT S.ClientBatchNo,
       GROUP_CONCAT(S.MemberSize ORDER BY S.SizeDisplayOrder, S.MemberSize SEPARATOR 'X') AS PalletMemberSize,
       GROUP_CONCAT(S.size_count ORDER BY S.SizeDisplayOrder, S.MemberSize SEPARATOR 'X') AS PalletMemberQty,
       COALESCE(ST.ShipToLocTitle, CM.CustName, '') AS DispatchedShipToName,
       S.ActionType
FROM (
    SELECT MCD.CollectionCode, PDD.ClientBatchNo, PDD.ActionType, SK.MemberSize, COUNT(*) AS size_count,
           CASE
               WHEN UPPER(COALESCE(MP.ParameterName, '')) LIKE 'CROSS%' THEN 1
               WHEN UPPER(COALESCE(MP.ParameterName, '')) LIKE 'CM%' THEN 1
               WHEN UPPER(COALESCE(MP.ParameterName, '')) LIKE 'LONG%' THEN 2
               WHEN UPPER(COALESCE(MP.ParameterName, '')) LIKE 'LM%' THEN 2
               ELSE 9
           END AS SizeDisplayOrder
    FROM membercollectiondtl MCD
    INNER JOIN palletdismentlecustomerdt PDD ON PDD.CustDismentleCode = MCD.CustDismentleCode AND PDD.MemberCode = MCD.MemberCode
    INNER JOIN membermaster MM ON MM.MemberCode = MCD.MemberCode
    INNER JOIN skewmaster SK ON SK.SKEWCode = MM.SKEWCode
    LEFT JOIN miscparameter MP ON MP.ParameterCode = MM.MemberType
    WHERE MCD.CollectionCode = @CollectionCode AND PDD.ActionType <> 'Y'
    GROUP BY MCD.CollectionCode, PDD.ClientBatchNo, PDD.ActionType, SK.MemberSize, SizeDisplayOrder
) S
INNER JOIN membercollection MC ON MC.CollectionCode = S.CollectionCode
LEFT JOIN shiptolocation ST ON ST.ShipToLocCode = MC.FromLocation
LEFT JOIN customermaster CM ON CM.CustomerCode = MC.CustomerCode
GROUP BY S.ClientBatchNo, S.ActionType, ST.ShipToLocTitle, CM.CustName
ORDER BY S.ClientBatchNo",
                collectionCode);
        }

        private static DataTable LoadMemberCollectionTotalDetails(MySqlConnection connection, string collectionCode)
        {
            return ExecuteMemberCollectionTextTable(
                connection,
                @"SELECT COUNT(*) AS TotalQty
FROM membercollectiondtl
WHERE CollectionCode = @CollectionCode",
                collectionCode);
        }

        private static DataTable ExecuteMemberCollectionTextTable(MySqlConnection connection, string commandText, string collectionCode)
        {
            using (var command = new MySqlCommand(commandText, connection))
            using (var adapter = new MySqlDataAdapter(command))
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 1600;
                command.Parameters.AddWithValue("@CollectionCode", collectionCode.Trim());

                var table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }
        private static DataSet ExecuteStoredProcedureDataSet(string procedureName, IDictionary<string, object> parameters)
        {
            var dataSet = new DataSet();
            if (!HasValidConnectionString)
            {
                return dataSet;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                using (var command = new MySqlCommand(procedureName, connection))
                using (var adapter = new MySqlDataAdapter(command))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 1600;
                    AddParameters(command, parameters);
                    adapter.Fill(dataSet);
                }
            }
            catch (MySqlException)
            {
                return new DataSet();
            }

            return dataSet;
        }



    }
}















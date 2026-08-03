using System;
using System.Configuration;

namespace APTS_WEB_APP.Helpers
{
    public static class SecretSettingReader
    {
        private const string EncryptedPrefix = "ENC:";

        public static string Read(string environmentVariableName, string appSettingName)
        {
            string value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(appSettingName))
            {
                value = ConfigurationManager.AppSettings[appSettingName];
            }

            return NormalizeSecretValue(value);
        }

        public static string NormalizeSecretValue(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (!value.StartsWith(EncryptedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            string encryptedValue = value.Substring(EncryptedPrefix.Length);
            return new EncryptDecrypt().DecryptQueryString(encryptedValue);
        }
    }
}
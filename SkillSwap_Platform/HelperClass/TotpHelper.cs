using OtpNet;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using static System.Net.WebRequestMethods;

namespace SkillSwap_Platform.HelperClass
{
    public static class TotpHelper
    {

        /// ✅ **Generate a Secure Secret Key**
        public static string GenerateSecretKey()
        {
            var key = KeyGeneration.GenerateRandomKey(20);
            string generatedSecret = Base32Encoding.ToString(key).Trim().ToUpperInvariant();
            return generatedSecret; // 🔥 Store in plain Base32
        }

        /// ✅ **Generate a Secure QR Code URL**
        public static string GenerateQrCodeUrl(string secretKey, string userEmail)
        {
            string issuer = "SkillSwap";
            string formattedSecret = secretKey.Trim().ToUpperInvariant();
            string otpAuthUri = $"otpauth://totp/{issuer}:{userEmail}?secret={formattedSecret}&issuer={issuer}";
            return $"https://quickchart.io/qr?text={WebUtility.UrlEncode(otpAuthUri)}&size=200";
        }

        /// ✅ **Verify the TOTP Code Securely**
        public static bool VerifyTotpCode(string secretKey, string userCode, out string failureReason)
        {
            if (string.IsNullOrWhiteSpace(userCode))
            {
                failureReason = "No code provided.";
                return false;
            }

            // Convert the Base32 secret to bytes.
            byte[] secretBytes = Base32Encoding.ToBytes(secretKey.Trim().ToUpperInvariant());

            // Create a Totp instance (30s step, 6-digit, SHA1).
            var totp = new Totp(secretBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);

            // Get current UTC time.
            DateTime utcNow = DateTime.UtcNow;

            var window = new VerificationWindow(previous: 1, future: 1);
            bool isValid = totp.VerifyTotp(userCode.Trim(), out _, window);
            if (!isValid)
            {
                failureReason = "Invalid code or clock skew. Please ensure your authenticator app’s clock is set correctly.";
                return false;
            }

            failureReason = null;
            return true;
        }
    }
}
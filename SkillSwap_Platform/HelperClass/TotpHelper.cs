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
            Debug.WriteLine($"[DEBUG] Generated Secret: {generatedSecret}");
            return generatedSecret; // 🔥 Store in plain Base32
        }

        /// ✅ **Generate a Secure QR Code URL**
        public static string GenerateQrCodeUrl(string secretKey, string userEmail)
        {
            string issuer = "SkillSwap";
            string formattedSecret = secretKey.Trim().ToUpperInvariant();
            string otpAuthUri = $"otpauth://totp/{issuer}:{userEmail}?secret={formattedSecret}&issuer={issuer}";
            Debug.WriteLine($"[DEBUG] OTP Auth URI: {otpAuthUri}");
            Debug.WriteLine($"[DEBUG] OTP Auth URI: otpauth://totp/SkillSwap:{userEmail}?secret={secretKey}&issuer=SkillSwap");
            return $"https://quickchart.io/qr?text={WebUtility.UrlEncode(otpAuthUri)}&size=200";
        }

        /// ✅ **Verify the TOTP Code Securely**
        public static bool VerifyTotpCode(string secretKey, string userCode)
        {
            // Normalize the secret.
            string formattedSecret = secretKey.Trim().ToUpperInvariant();
            Debug.WriteLine($"[DEBUG] Formatted Secret: {formattedSecret}");

            // Convert the Base32 secret to bytes.
            byte[] secretBytes = Base32Encoding.ToBytes(formattedSecret);
            Debug.WriteLine($"[DEBUG] Secret Bytes: {BitConverter.ToString(secretBytes)}");

            // Create a Totp instance with parameters matching Google Authenticator:
            // 30-second time step, 6-digit code, SHA1.
            var totp = new Totp(secretBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);

            // Get current UTC time.
            DateTime utcNow = DateTime.UtcNow;
            Debug.WriteLine($"[DEBUG] Server UTC Time: {utcNow}");

            // Compute OTP for the current time, previous, and next time steps.
            string expectedOtpCurrent = totp.ComputeTotp(utcNow);
            string expectedOtpPrevious = totp.ComputeTotp(utcNow.AddSeconds(-30));
            string expectedOtpNext = totp.ComputeTotp(utcNow.AddSeconds(30));

            Debug.WriteLine($"[DEBUG] Expected OTP (Previous): {expectedOtpPrevious}");
            Debug.WriteLine($"[DEBUG] Expected OTP (Current): {expectedOtpCurrent}");
            Debug.WriteLine($"[DEBUG] Expected OTP (Next): {expectedOtpNext}");
            Debug.WriteLine($"[DEBUG] Entered OTP: {userCode}");

            // Allow a verification window of one time step before and after.
            var window = new VerificationWindow(previous: 1, future: 1);
            bool verified = totp.VerifyTotp(userCode, out long timeStepMatched, window);
            Debug.WriteLine($"[DEBUG] OTP Verified: {verified}, Time Step Matched: {timeStepMatched}");

            return verified;
        }
    }
}
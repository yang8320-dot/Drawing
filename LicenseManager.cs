using System;

namespace DrawingApp
{
    public static class LicenseManager
    {
        private static readonly DateTime ExpirationDate = new DateTime(2050, 12, 31);

        // 您指定的授權名單
        public static readonly string[] DefaultUsers = { "黃忠揚", "TJ7006571", "TJ700228", "TJ700533", "TJ204159" };

        public static bool VerifyLicense()
        {
            // 1. 檢查軟體使用期限
            if (DateTime.Today > ExpirationDate)
            {
                return false;
            }

            // 2. 取得當前電腦登入的 Windows 帳號
            string currentComputerUser = Environment.UserName.Trim();
            
            // 3. 檢查目前帳號是否在授權名單內 (不分大小寫)
            foreach (string allowedUser in DefaultUsers)
            {
                if (string.Equals(currentComputerUser, allowedUser, StringComparison.OrdinalIgnoreCase))
                {
                    return true; // 驗證通過
                }
            }

            // 驗證失敗
            return false;
        }
    }
}

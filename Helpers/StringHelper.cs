using System.Globalization;
using System.Text;


namespace QuanLyCotWeb.Helpers
{
    public static class StringHelper
    {
        public static string NormalizeString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            // Bỏ dấu tiếng Việt và chuyển thành chữ thường
            string normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).ToLower();
        }
    }
}

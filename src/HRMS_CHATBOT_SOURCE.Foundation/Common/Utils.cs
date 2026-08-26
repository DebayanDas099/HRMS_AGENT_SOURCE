using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;

namespace HRMS_CHATBOT_SOURCE.Foundation.Common;

public static class Utils
{
    public static bool IsProduction()
    {
        var production = true;
        try
        {
            var aspnetcoreEnvString = GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            production = aspnetcoreEnvString == null ? false : aspnetcoreEnvString.Equals("production", StringComparison.InvariantCultureIgnoreCase);
        }
        catch { }

        return production;
    }

    public static DateTime? ToStartOfDay(this DateTime? dateTime)
    {
        return dateTime != null ? new DateTime(dateTime.Value.Year, dateTime.Value.Month, dateTime.Value.Day, 0, 0, 0, dateTime.Value.Kind) : null;
    }
    public static DateTime ToStartOfDay(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0, dateTime.Kind);
    }
    public static bool StringEquals(this string? obj, string? value, StringComparison comparison)
    {
        return (string.IsNullOrEmpty(obj) && string.IsNullOrEmpty(value))
               || string.Equals(obj, value, comparison);
    }

    public static char GetUppercaseLetterByPosition(int position)
    {
        const int alphabetStartAscii = 65; // ASCII value for 'A'

        if (position < 1 || position > 26)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 1 and 26.");
        }

        return (char)(alphabetStartAscii + position - 1);
    }

    public static string GetEnvironmentVariable(string? key, bool isUnitTest = false)
    {
        if (isUnitTest) return String.Empty;

        if (string.IsNullOrWhiteSpace(key)) return String.Empty;

        var secret = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(secret))
            secret = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);

        return secret ?? string.Empty;
    }

    public static List<string> GetHeaderValues(this HttpRequest request, string headerName)
    {
        var headerValues = new List<string?>();

        if (request.Headers.TryGetValue(headerName, out var values))
        {
            foreach (var value in values)
            {
                headerValues.Add(value);
            }
        }

        return headerValues;
    }

    public static string GetMimeTypeFromFile(string? filePath)
    {
        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(filePath, out string? contentType)
            ? contentType
            : "application/octet-stream"; // Default if unknown
    }

    public static int GenerateRandomNumber(int min, int max, int[] excludeValues, int[] genearetedValues)
    {
        // Get the range of valid values
        var validValues = Enumerable.Range(min, max - min + 1).Except(excludeValues).Except(genearetedValues).ToArray();

        // Generate a random number from the valid values
        int randomValue = validValues[(new Random()).Next(validValues.Length)];

        return randomValue;
    }
    public static DateTime ConvertToIST(this DateTime date)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(date, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
    }

    public static string GetRandomString(int size, bool lowerCase)
    {
        var builder = new StringBuilder();
        Random random = new Random();
        char ch;
        for (int i = 0; i < size; i++)
        {
            ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
            builder.Append(ch);
        }
        if (lowerCase)
            return builder.ToString().ToLower();
        return builder.ToString();
    }

    public static bool HasData(this DataSet? ds, int tableCount)
    {
        bool result = false;

        if (ds != null && ds.Tables.Count > 0)
        {
            for (int i = 0; i < tableCount; i++)
            {
                if (ds.Tables[i] != null && ds.Tables[i].Rows.Count > 0)
                {
                    result = true;
                }
                else
                {
                    result = false;
                    break;
                }
            }
        }

        return result;

    }

    public static bool HasData(this DataTable? dt)
    {
        return (dt != null && dt.Rows.Count > 0);
    }

    public static bool In<T>(this T item, params T[] items)
    {
        if (items == null)
            throw new ArgumentNullException("items");

        return items.Contains(item);
    }

    public static T IIf<T>(bool expression, T truePart, T falsePart)
    {
        return expression ? truePart : falsePart;
    }

    public static bool IsNullOrEmpty<T>(this IEnumerable<T> arrlist)
    {
        return arrlist == null || !arrlist.Any();
    }

    public static object IIFStringOrDBNull(string? value)
    {
        return (string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value);
    }

    public static object IIFIntegerOrDBNull(int? value)
    {
        return (value == null || value == int.MinValue ? (object)DBNull.Value : value);
    }

    public static object IIFLongOrDBNull(long? value)
    {
        return (value == null || value == long.MinValue ? (object)DBNull.Value : value);
    }

    public static object IIFDecimalOrDBNull(decimal? value)
    {
        return (value == null || value == decimal.MinValue ? (object)DBNull.Value : value);
    }

    public static object IIFDateTimeOrDBNull(SqlDateTime value)
    {
        return (value == SqlDateTime.MinValue ? (object)DBNull.Value : value);
    }

    public static object IIFGuidOrDBNull(Guid? value)
    {
        return (value == null ? (object)DBNull.Value : value);
    }

    public static object IIFBooleanOrDBNull(bool? value)
    {
        return (value == null ? (object)DBNull.Value : value);
    }
    public static object IIFListOrDBNull(dynamic? value)
    {
        return (value != null ? JsonConvert.SerializeObject(value) : (object)DBNull.Value);
    }
    public static string GenerateOtp(int length)
    {
        var numbers = Enumerable.Range(0, 10).Select(i => i.ToString());
        var otp = Enumerable.Range(1, length).Select(i => numbers.ElementAt(new Random().Next(0, 10)));
        return string.Join("", otp);
    }

    public static SqlDateTime ConvertToNullableDateTimeFromSqlDateTime(DateTime? nullableDateTime)
    {
        SqlDateTime sqlDateTime;

        if (nullableDateTime.HasValue)
        {
            sqlDateTime = new SqlDateTime(nullableDateTime.Value);
        }
        else
        {
            sqlDateTime = SqlDateTime.Null;
        }

        return sqlDateTime;
    }

    public static bool Contains(this string source, string toCheck, StringComparison comp)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }

    public static string CreateNumericAlphabeticPassword(int length)
    {
        const string valid = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        StringBuilder res = new StringBuilder();
        Random rnd = new Random();
        while (0 < length--)
        {
            res.Append(valid[rnd.Next(valid.Length)]);
        }
        return res.ToString();
    }

    public static string CreateAlphanumericPassword(int length)
    {
        const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        StringBuilder res = new StringBuilder();
        Random rnd = new Random();
        while (0 < length--)
        {
            res.Append(valid[rnd.Next(valid.Length)]);
        }
        return res.ToString();
    }

    public static string CreateSpecialCharactersPassword(int length)
    {
        const string valid = "@#$^&*-_";
        StringBuilder res = new StringBuilder();
        Random rnd = new Random();
        while (0 < length--)
        {
            res.Append(valid[rnd.Next(valid.Length)]);
        }
        return res.ToString();
    }

    public static string CreateNewPassword()
    {
        return Convert.ToString(CreateAlphanumericPassword(4) + CreateSpecialCharactersPassword(1) + CreateNumericAlphabeticPassword(5));
    }

    public static async Task RetryTaskAsync(Func<Task> operation, int numRetries, int delay)
    {
        for (int i = 0; i < numRetries; i++)
        {
            try
            {
                await operation();
                return;
            }
            catch
            {
                if (i == numRetries - 1)
                {
                    throw;
                }

                await Task.Delay(delay);
            }
        }
    }

    public static decimal? ConvertDecimal(decimal? num)
    {
        decimal regularDecimal = num.GetValueOrDefault(0.0M);
        decimal? wholePart = Math.Floor(regularDecimal);
        decimal? decimalPart = num - wholePart;
        decimal? convertedDecimal;

        if (decimalPart > 0.00M && decimalPart <= 0.25M)
        {
            convertedDecimal = wholePart + 0.25M;
        }
        else if (decimalPart > 0.25M && decimalPart <= 0.50M)
        {
            convertedDecimal = wholePart + 0.50M;
        }
        else if (decimalPart > 0.50M && decimalPart <= 0.75M)
        {
            convertedDecimal = wholePart + 0.75M;
        }
        else if (decimalPart > 0.75M)
        {
            convertedDecimal = wholePart + 1;
        }
        else
        {
            convertedDecimal = num;
        }

        return convertedDecimal;
    }

    public static DateTime? ConvertDate(string? inputDate)
    {
        DateTime? outputDate = null;
        if (!string.IsNullOrEmpty(inputDate))
        {
            //// Parse the input date
            //DateTime originalDate = DateTime.ParseExact(inputDate, "dd-MM-yyyy", null);

            //// Format the date as yyyy-mm-dd
            //outputDate = DateTime.ParseExact(originalDate.ToString("yyyy-MM-dd"), "yyyy-MM-dd", null);

            // Try parsing with "d/M/yyyy" format
            if (DateTime.TryParseExact(inputDate, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                // Format the date as yyyy-mm-dd
                outputDate = DateTime.ParseExact(parsedDate.ToString("yyyy-MM-dd"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            // If the first attempt fails, try parsing with "dd/MM/yyyy" format
            else if (DateTime.TryParseExact(inputDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
            {
                // Format the date as yyyy-mm-dd
                outputDate = DateTime.ParseExact(parsedDate.ToString("yyyy-MM-dd"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else if (DateTime.TryParseExact(inputDate, "dd/MMM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
            {
                // Format the date as yyyy-mm-dd
                outputDate = DateTime.ParseExact(parsedDate.ToString("yyyy-MM-dd"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else if (DateTime.TryParseExact(inputDate, "dd-MMM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
            {
                // Format the date as yyyy-mm-dd
                outputDate = DateTime.ParseExact(parsedDate.ToString("yyyy-MM-dd"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else if (DateTime.TryParseExact(inputDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
            {
                // Format the date as yyyy-mm-dd
                outputDate = DateTime.ParseExact(parsedDate.ToString("yyyy-MM-dd"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else if (DateTime.TryParseExact(inputDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
            {
                // Format the date as yyyy-mm-dd
                outputDate = DateTime.ParseExact(parsedDate.ToString("yyyy-MM-dd"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

        }

        return outputDate;

    }
    public static string ExtractFileId(string url)
    {
        string keyword = "TempUpload/";
        int startIndex = url.IndexOf(keyword) + keyword.Length; // Find where TempUpload/ ends
        int endIndex = url.IndexOf(".", startIndex); // Find the next period (.) after the startIndex

        if (startIndex > -1 && endIndex > -1)
        {
            return url.Substring(startIndex, endIndex - startIndex); // Extract the file ID
        }

        return string.Empty; // Return empty string if no match
    }

    public static async Task<string?> DownloadAndSaveFileFromUrl(string? fileUrl, string? rootSubFolder, string? subFolder, IConfiguration? configuration, string? newFileName = "")
    {
        if (string.IsNullOrWhiteSpace(fileUrl) || string.IsNullOrWhiteSpace(subFolder) || configuration == null || string.IsNullOrWhiteSpace(rootSubFolder))
        {
            throw new Exception("Invalid Arguments.");
        }

        var baseFolderPath = Convert.ToString(configuration["AppSettings:DocFolderAbsolutePath"]);

        if (string.IsNullOrWhiteSpace(baseFolderPath))
        {
            throw new Exception("Invalid Base Folder Path.");
        }

        var destinationFolder = Path.Combine(baseFolderPath, rootSubFolder, subFolder);

        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        var fileName = string.IsNullOrWhiteSpace(newFileName)
            ? Path.GetFileName(new Uri(fileUrl).LocalPath)
            : newFileName;

        var destinationPath = Path.Combine(destinationFolder, fileName);

        if (!File.Exists(destinationPath))
        {
            using (HttpClient client = new HttpClient())
            using (var response = await client.GetAsync(fileUrl))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
        }

        return $"{subFolder}/{fileName}";
    }


    public static string GetContentTypeFromFileName(string fileName)
    {
        string contentType = string.Empty;

        if (!string.IsNullOrEmpty(fileName))
        {
            string? extension = Path.GetExtension(fileName)?.ToLowerInvariant();

            if (!string.IsNullOrEmpty(extension))
            {
                switch (extension)
                {
                    case ".txt":
                        contentType = "text/plain";
                        break;
                    case ".pdf":
                        contentType = "application/pdf";
                        break;
                    case ".doc":
                    case ".docx":
                        contentType = "application/msword";
                        break;
                    case ".xls":
                    case ".xlsx":
                    case ".xlsb":
                        contentType = "application/vnd.ms-excel";
                        break;
                    case ".ppt":
                    case ".pptx":
                        contentType = "application/vnd.ms-powerpoint";
                        break;
                    case ".jpg":
                    case ".jpeg":
                        contentType = "image/jpeg";
                        break;
                    case ".png":
                        contentType = "image/png";
                        break;
                    case ".gif":
                        contentType = "image/gif";
                        break;
                    case ".bmp":
                        contentType = "image/bmp";
                        break;
                    case ".zip":
                        contentType = "application/zip";
                        break;
                    case ".rar":
                        contentType = "application/x-rar-compressed";
                        break;
                    case ".tar":
                        contentType = "application/x-tar";
                        break;
                    case ".7z":
                        contentType = "application/x-7z-compressed";
                        break;
                    default:
                        contentType = "application/octet-stream";
                        break;
                }
            }
        }

        return contentType;
    }

}

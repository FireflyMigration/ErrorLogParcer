using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ErrorLogParcer
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Please provide a file name as an argument.");
                return;
            }

            string inputFileName = args[0];
            string outputFileName = Path.ChangeExtension(inputFileName, ".csv");

            try
            {
                using (StreamReader reader = new StreamReader(inputFileName))
                using (StreamWriter writer = new StreamWriter(outputFileName))
                {

                    writer.WriteLine("Date,Day of Week,Time,ABIMM Version,Classification,Error,stack,InnerError,innerStack,ISO Date,Line");

                    string line;
                    string lastErrorDate = "";
                    string lastErrorMessage = "";
                    string lastVersion = ""; // Temporary storage for version information

                    string innerError = "";
                    string firstStackLine = "";
                    string firstInnerStackLine = "";

                    int lineNumber = 0;
                    void writeCsv()
                    {
                        var classification = ClassifyError(lastErrorMessage, innerError); // Determine classification
                        DateTime parsedDate = DateTime.ParseExact(lastErrorDate, "M/d/yyyy h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
                        writer.WriteLine($"{parsedDate.ToString("yyyy-MM-dd")},{parsedDate.DayOfWeek.ToString()},{GetHourPlus15MinuteInterval(parsedDate)},{EscapeForCsv(lastVersion)},{EscapeForCsv(classification)},{EscapeForCsv(lastErrorMessage)},{EscapeForCsv(firstStackLine)},{EscapeForCsv(innerError)},{EscapeForCsv(firstInnerStackLine)},{parsedDate.ToString("o")},{lineNumber}");
                    }

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        if (line.Contains(">> ERROR >>"))
                        {
                            // Before processing a new error, write the previous error and version to the CSV
                            if (!string.IsNullOrEmpty(lastErrorDate))
                            {
                                writeCsv();
                            }

                            var errorParts = line.Split(new string[] { " - >> ERROR >>" }, StringSplitOptions.None);
                            lastErrorDate = errorParts[0].Trim(); // Keep the date string for parsing later
                            lastErrorMessage = errorParts[1].Trim();

                            // Reset version for the new error block
                            lastVersion = "";
                            innerError = "";
                            firstStackLine = "";
                            firstInnerStackLine = "";
                        }
                        else if (innerError == "" && line.Contains("Inner Exception:"))
                        {
                            innerError = line;
                        }
                        else if (line.Contains("ABIMM_SM Version:"))
                        {
                            lastVersion = line.Substring("ABIMM_SM Version:".Length).Trim();
                        }
                        else if (line.Contains(">> ERROR >>") && innerError != "")
                        {
                            lastErrorMessage = innerError;

                            // Reset version for the new error block
                            lastVersion = "";
                            innerError = "";
                        }
                        else if (line.Contains("Inner Error : "))
                        {
                            innerError = line.Split(new string[] { "Inner Error : " }, StringSplitOptions.None)[1];
                        }
                        else if (line.StartsWith("ABIMM_SM Version:"))
                        {
                            lastVersion = line.Substring("ABIMM_SM Version:".Length).Trim();
                        }
                        else if (line.StartsWith("   at "))
                        {
                            if (string.IsNullOrEmpty(innerError))
                            {
                                if (string.IsNullOrEmpty(firstStackLine))
                                {
                                    firstStackLine = line.Substring(6);
                                }
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(firstInnerStackLine))
                                {
                                    firstInnerStackLine = line.Substring(6);
                                }
                            }
                        }
                    }

                    // Write the last error and version if the file ends after an error description
                    if (!string.IsNullOrEmpty(lastErrorDate))
                    {
                        writeCsv();
                    }
                }

                Console.WriteLine($"Output written to {outputFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        static Dictionary<string, string> errorMappings = new Dictionary<string, string>
    {
        { "Lock request time", "LockTimeout" },
        { "RowDoesNotExist", "RowDoesNotExist" },
        { "insert duplicate", "UniqueConstraint" },
        {" constrained to be unique","UniqueConstraint" },
        { "RowWasChanged", "RowWasChanged" },
        { "Failed to send Email", "EmailFailed" },
        { "No printer", "NoPrinter" },
        { "The process cannot access the file", "FileAccessError" },
        { "Login failed for user", "SqlLoginFailed" },
        { "login failed", "SqlLoginFailed" },
        { "DirectoryNotFound", "DirectoryNotFound" },
        { "SQL Server. The server was not found or was not accessible.", "SqlConnectionServerNotFound" },
        { "The connection's current state is closed.", "SqlConnectionClosed" },
            {"because the session is in the kill state.","SqlConnectionClosed" },
            {"The connection is closed.","SqlConnectionClosed" },
            {"Connection Timeout Expired","SqlConnectionTimeout" },
        { "A transport-level error", "SqlTransportLevelError" },
        { " varchar data type to a datetime", "VarcharToDatetimeConversion" },
        { "NullReference", "NullReference" },
        { " due to a transient failure", "SqlEFTransientFailure" },
        { "The specified printer has been deleted", "PrinterDeleted" },
            {"semaphore","semaphore" },
            {"OutOfMemoryException","OutOfMemoryException" },
            {" ReadOnlyEntityUpdate"," ReadOnlyEntityUpdate" },
            {"UnauthorizedAccessException","UnauthorizedIOAccessException" }



        // Add more mappings as needed
    };
        // Method to determine the classification based on the error message
        static string ClassifyError(string errorMessage, string innerError)
        {
            if (innerError.Contains("Login failed") || innerError.Contains("login failed"))
                return "SqlLoginFailed";

            foreach (var mapping in errorMappings)
            {
                if (errorMessage.Contains(mapping.Key))
                {
                    return mapping.Value;
                }
            }

            return "other"; // Default classification
        }

        // Method to escape special characters for CSV
        static string EscapeForCsv(string input)
        {
            if (input.Contains("\"") || input.Contains(",") || input.Contains("\n"))
            {
                // Escape quotes and wrap the input in quotes
                return $"\"{input.Replace("\"", "\"\"")}\"";
            }
            else
            {
                return input;
            }
        }


        static string GetHourPlus15MinuteInterval(DateTime date)
        {

            int minutes = (date.Minute / 15) * 15;
            return date.ToString($"HH{minutes:D2}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_STANDALONE || UNITY_EDITOR
using SFB; // StandaloneFileBrowser package needs to be added to unity. Download from git hub
#endif

public class QuestionSetImportUI : MonoBehaviour
{
    private enum StatusType { Info, Success, Warning, Error }

    [Header("UI")]
    [SerializeField] private TMP_InputField setNameInput;
    [SerializeField] private Button importCsvButton;

    // NEW: Button to open QuestionSets folder
    [SerializeField] private Button openQuestionSetsFolderButton;

    [SerializeField] private TMP_Text warningText;

    [Header("CSV Settings")]
    [Tooltip("Expected columns: Question, Category, Correct, Answer2, Answer3, Answer4, ImageName")]
    [SerializeField] private bool skipFirstRowAsHeader = false;

    // A-F required (6). G optional (imageName).
    private const int MinColumns = 6;
    private const int ExpectedColumns = 7;

    private void Awake()
    {
        if (importCsvButton != null)
            importCsvButton.onClick.AddListener(OnImportCsvClicked);

        if (openQuestionSetsFolderButton != null)
            openQuestionSetsFolderButton.onClick.AddListener(OnOpenQuestionSetsFolderClicked);

        SetStatus("", StatusType.Info);
    }

    private void OnDestroy()
    {
        if (importCsvButton != null)
            importCsvButton.onClick.RemoveListener(OnImportCsvClicked);

        if (openQuestionSetsFolderButton != null)
            openQuestionSetsFolderButton.onClick.RemoveListener(OnOpenQuestionSetsFolderClicked);
    }

    private void OnOpenQuestionSetsFolderClicked()
    {
        try
        {
            string root = QuestionSetStorage.GetQuestionSetsRoot();
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);

            OpenFolderInExplorer(root);
            SetStatus("Opened QuestionSets folder.", StatusType.Info);
        }
        catch (Exception ex)
        {
            SetStatus("Could not open folder: " + ex.Message, StatusType.Error);
            Debug.LogError(ex);
        }
    }

    private void OnImportCsvClicked()
    {
        SetStatus("", StatusType.Info);

        string rawSetName = setNameInput != null ? setNameInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(rawSetName))
        {
            SetStatus("Please enter a question set name.", StatusType.Error);
            return;
        }

        string setName = SanitizeFolderName(rawSetName);
        if (string.IsNullOrWhiteSpace(setName))
        {
            SetStatus("That set name contains invalid characters. Try a simpler name.", StatusType.Error);
            return;
        }

        // Ensure QuestionSets exists (safety net)
        string root = QuestionSetStorage.GetQuestionSetsRoot();
        if (!Directory.Exists(root))
            Directory.CreateDirectory(root);

        // Make set folder
        string setFolder = QuestionSetStorage.GetSetFolder(setName);

        // If folder already exists, block (safer for beginners)
        if (Directory.Exists(setFolder))
        {
            SetStatus($"A question set named '{setName}' already exists. Choose a different name or delete the old folder.", StatusType.Error);
            return;
        }

        Directory.CreateDirectory(setFolder);
        Directory.CreateDirectory(Path.Combine(setFolder, "images"));
        string jsonPath = Path.Combine(setFolder, "questions.json");
        string logPath = Path.Combine(setFolder, "import_log.txt");

        // Pick CSV
        string csvPath = OpenCsvFilePicker();
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            SetStatus("CSV import canceled.", StatusType.Info);
            TryDeleteDirectory(setFolder);
            return;
        }

        try
        {
            var warnings = new List<string>();
            var skippedQuestionNumbers = new List<int>();

            QuestionSetFile setFile = ConvertCsvToQuestionSetFile(
                csvPath,
                setName,
                warnings,
                skippedQuestionNumbers
            );

            if (setFile == null || setFile.questions == null || setFile.questions.Count == 0)
                throw new Exception("No valid questions were imported. Please check your CSV formatting.");

            string json = JsonUtility.ToJson(setFile, true);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));

            // Write log always (helps non-technical users)
            WriteImportLog(logPath, csvPath, setName, setFile.questions.Count, skippedQuestionNumbers, warnings);

            // Status message + color rules:
            // - Green only if no warnings AND no skipped
            // - Yellow if imported but had warnings/skips
            // - Red on failure
            bool hasIssues = skippedQuestionNumbers.Count > 0 || warnings.Count > 0;

            string photosTip = "\nMake sure to add your photos to the images folder.";

            if (hasIssues)
            {
                string skippedList = skippedQuestionNumbers.Count > 0
                    ? FormatNumberList(skippedQuestionNumbers, 12)
                    : "None";

                SetStatus(
                    $"Import finished: {setFile.questions.Count} questions.\nSkipped questions: {skippedList}\nSee import_log.txt for details.{photosTip}",
                    StatusType.Warning
                );
            }
            else
            {
                SetStatus(
                    $"Import successful! {setFile.questions.Count} questions created.\n(Set folder: {setName}){photosTip}",
                    StatusType.Success
                );
            }

            Debug.Log($"[CSV Import] Wrote JSON: {jsonPath}");
            Debug.Log($"[CSV Import] Wrote log:  {logPath}");
        }
        catch (Exception ex)
        {
            TryDeleteDirectory(setFolder);
            SetStatus("Import failed: " + ex.Message, StatusType.Error);
            Debug.LogError(ex);
        }
    }

    private string OpenCsvFilePicker()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        try
        {
            var extensions = new[]
            {
                new ExtensionFilter("CSV Files", "csv"),
                new ExtensionFilter("All Files", "*")
            };

            string[] paths = StandaloneFileBrowser.OpenFilePanel("Select Questions CSV", "", extensions, false);
            if (paths != null && paths.Length > 0)
                return paths[0];

            return null;
        }
        catch (Exception)
        {
            // fall through
        }
#endif

        SetStatus("File picker not available. Install StandaloneFileBrowser (SFB) to choose a CSV file.", StatusType.Error);
        return null;
    }

    // ---------- CSV -> QuestionSetFile (robust) ----------

    private QuestionSetFile ConvertCsvToQuestionSetFile(
        string csvPath,
        string setName,
        List<string> warnings,
        List<int> skippedQuestionNumbers)
    {
        if (!File.Exists(csvPath))
            throw new FileNotFoundException("CSV file not found.", csvPath);

        // Read with encoding fallback (fixes many Excel CSV cases)
        string encodingWarning;
        string csvText = ReadCsvTextSmart(csvPath, out encodingWarning);
        if (!string.IsNullOrEmpty(encodingWarning))
            warnings.Add(encodingWarning);

        if (string.IsNullOrWhiteSpace(csvText))
            throw new Exception("CSV file is empty.");

        // Detect delimiter (comma vs semicolon)
        char delimiter = DetectDelimiter(csvText);
        if (delimiter == ';')
            warnings.Add("Detected semicolon-delimited CSV. (This is common in some regions.)");

        // Parse properly (handles quoted commas + quoted newlines)
        List<List<string>> rows = ParseCsv(csvText, delimiter);

        if (rows.Count == 0)
            throw new Exception("CSV contains no readable rows.");

        int startRow = skipFirstRowAsHeader ? 1 : 0;

        var setFile = new QuestionSetFile
        {
            setName = setName,
            questions = new List<QuestionFile>()
        };

        int questionNumber = 0; // 1-based count of data questions (excluding header)

        for (int r = startRow; r < rows.Count; r++)
        {
            questionNumber++;

            List<string> colsRaw = rows[r];

            if (colsRaw == null || colsRaw.All(string.IsNullOrWhiteSpace))
                continue;

            if (!TryNormalizeColumns(colsRaw, delimiter, out string[] cols, out string normalizeError))
            {
                skippedQuestionNumbers.Add(questionNumber);
                warnings.Add($"Question {questionNumber}: {normalizeError}");
                continue;
            }

            string question = cols[0].Trim();
            string category = cols[1].Trim();
            string correct = cols[2].Trim();
            string a2 = cols[3].Trim();
            string a3 = cols[4].Trim();
            string a4 = cols[5].Trim();
            string imageName = cols[6].Trim();

            var rowIssues = new List<string>();

            if (string.IsNullOrWhiteSpace(question))
                rowIssues.Add("Question text is empty (Column A).");

            if (string.IsNullOrWhiteSpace(correct))
                rowIssues.Add("Correct answer is empty (Column C).");

            if (string.IsNullOrWhiteSpace(a2) || string.IsNullOrWhiteSpace(a3) || string.IsNullOrWhiteSpace(a4))
                rowIssues.Add("One or more wrong answers are empty (Columns D/E/F).");

            if (rowIssues.Count > 0)
            {
                skippedQuestionNumbers.Add(questionNumber);
                warnings.Add($"Question {questionNumber} skipped: " + string.Join(" ", rowIssues));
                continue;
            }

            // Soft warnings (still import)
            string[] wrong = { a2, a3, a4 };

            if (wrong.Any(w => string.Equals(w, correct, StringComparison.OrdinalIgnoreCase)))
                warnings.Add($"Question {questionNumber}: Correct answer also appears in wrong answers.");

            if (wrong.Distinct(StringComparer.OrdinalIgnoreCase).Count() != wrong.Length)
                warnings.Add($"Question {questionNumber}: Duplicate wrong answers detected.");

            // Sanitize image name (store as images/<file>)
            string imageField = "";
            if (!string.IsNullOrWhiteSpace(imageName))
            {
                string fileOnly = Path.GetFileName(imageName.Replace('\\', '/'));
                fileOnly = SanitizeFileName(fileOnly);

                if (string.IsNullOrWhiteSpace(fileOnly))
                {
                    warnings.Add($"Question {questionNumber}: ImageName was provided but invalid. Ignoring image.");
                    imageField = "";
                }
                else
                {
                    imageField = $"images/{fileOnly}";
                }
            }

            var q = new QuestionFile
            {
                id = MakeQuestionId(setName, setFile.questions.Count + 1),
                question = question,
                category = category,
                correct = correct,
                wrong = new string[3] { a2, a3, a4 },
                image = imageField
            };

            setFile.questions.Add(q);
        }

        return setFile;
    }

    // ---------- CSV parsing helpers ----------

    private static string ReadCsvTextSmart(string csvPath, out string encodingWarning)
    {
        encodingWarning = null;

        byte[] bytes = File.ReadAllBytes(csvPath);

        // Try strict UTF-8 first
        try
        {
            string utf8 = new UTF8Encoding(false, true).GetString(bytes);

            if (utf8.Contains('\uFFFD'))
                encodingWarning = "Warning: CSV may not be UTF-8. If you see weird characters, re-save as 'CSV UTF-8' in Excel.";

            return utf8;
        }
        catch
        {
            encodingWarning = "Warning: CSV was not UTF-8. For best results, re-save as 'CSV UTF-8' in Excel.";
            return Encoding.Default.GetString(bytes);
        }
    }

    private static char DetectDelimiter(string text)
    {
        int comma = 0;
        int semi = 0;
        bool inQuotes = false;

        int limit = Mathf.Min(text.Length, 5000);
        for (int i = 0; i < limit; i++)
        {
            char c = text[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < limit && text[i + 1] == '"')
                {
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }

            if (!inQuotes)
            {
                if (c == ',') comma++;
                else if (c == ';') semi++;
            }
        }

        return (semi > comma) ? ';' : ',';
    }

    private static List<List<string>> ParseCsv(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();

        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else if (c == '\r')
                {
                    // ignore
                }
                else
                {
                    field.Append(c);
                }
            }
        }

        row.Add(field.ToString());
        rows.Add(row);

        return rows;
    }

    private static bool TryNormalizeColumns(List<string> colsRaw, char delimiter, out string[] cols, out string error)
    {
        cols = null;
        error = null;

        var colsClean = colsRaw.Select(c => (c ?? "").Trim()).ToList();

        if (colsClean.Count == 6)
        {
            colsClean.Add("");
            cols = colsClean.ToArray();
            return true;
        }

        if (colsClean.Count == 7)
        {
            cols = colsClean.ToArray();
            return true;
        }

        if (colsClean.Count < 6)
        {
            error = $"Too few columns ({colsClean.Count}). Expected 6 or 7. Check for missing commas or malformed quotes.";
            return false;
        }

        if (colsClean.Count > 7)
        {
            int tailCount = 6;
            int questionParts = colsClean.Count - tailCount;

            if (questionParts < 1)
            {
                error = "Row has an unexpected number of columns and can't be repaired.";
                return false;
            }

            string q = string.Join(delimiter.ToString(), colsClean.Take(questionParts));
            string category = colsClean[questionParts + 0];
            string correct = colsClean[questionParts + 1];
            string a2 = colsClean[questionParts + 2];
            string a3 = colsClean[questionParts + 3];
            string a4 = colsClean[questionParts + 4];
            string img = colsClean[questionParts + 5];

            cols = new[] { q, category, correct, a2, a3, a4, img };
            return true;
        }

        error = "Unknown CSV formatting issue.";
        return false;
    }

    // ---------- IDs / Sanitizers / Logging ----------

    private static string MakeQuestionId(string setName, int number)
    {
        string safe = SanitizeIdPart(setName);
        return $"{safe}_{number:0000}";
    }

    private static string SanitizeIdPart(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "set";
        s = s.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", "_");
        s = Regex.Replace(s, @"[^a-z0-9_]+", "");
        if (string.IsNullOrWhiteSpace(s)) s = "set";
        return s;
    }

    private static string SanitizeFolderName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "");

        name = Regex.Replace(name.Trim(), @"\s+", " ");
        return name;
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c.ToString(), "");

        return fileName.Trim();
    }

    private static void WriteImportLog(
        string logPath,
        string csvPath,
        string setName,
        int importedCount,
        List<int> skippedQuestionNumbers,
        List<string> warnings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Question Set Import Log ===");
        sb.AppendLine($"Time: {DateTime.Now}");
        sb.AppendLine($"CSV:  {csvPath}");
        sb.AppendLine($"Set:  {setName}");
        sb.AppendLine($"Imported questions: {importedCount}");
        sb.AppendLine($"Skipped questions:  {(skippedQuestionNumbers.Count == 0 ? "None" : string.Join(", ", skippedQuestionNumbers))}");
        sb.AppendLine();

        if (warnings.Count > 0)
        {
            sb.AppendLine("Warnings / Notes:");
            for (int i = 0; i < warnings.Count; i++)
                sb.AppendLine("- " + warnings[i]);
        }
        else
        {
            sb.AppendLine("Warnings / Notes: None");
        }

        File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static string FormatNumberList(List<int> nums, int maxToShow)
    {
        if (nums == null || nums.Count == 0) return "None";

        nums = nums.Distinct().OrderBy(n => n).ToList();
        if (nums.Count <= maxToShow) return string.Join(", ", nums);

        return string.Join(", ", nums.Take(maxToShow)) + $" ... (+{nums.Count - maxToShow} more)";
    }

    private static void TryDeleteDirectory(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
        catch { }
    }

    private void SetStatus(string msg, StatusType type)
    {
        if (warningText == null) return;

        warningText.text = msg;

        // You said the text is red by default in Unity.
        // We'll override it based on status.
        switch (type)
        {
            case StatusType.Success:
                warningText.color = Color.green;
                break;
            case StatusType.Warning:
                warningText.color = new Color(1f, 0.85f, 0.2f); // yellow-ish
                break;
            case StatusType.Error:
                warningText.color = Color.red;
                break;
            default:
                // Info/neutral: keep your default or set white
                // If you want to keep "whatever is set in Inspector", comment the next line out.
                warningText.color = Color.white;
                break;
        }
    }

    private static void OpenFolderInExplorer(string folderPath)
    {
        folderPath = Path.GetFullPath(folderPath);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folderPath}\"",
            UseShellExecute = true
        });
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        System.Diagnostics.Process.Start("open", $"\"{folderPath}\"");
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        System.Diagnostics.Process.Start("xdg-open", $"\"{folderPath}\"");
#else
        Application.OpenURL("file://" + folderPath);
#endif
    }
}
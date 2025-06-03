using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

public class CSVtoSO
{
    private static string questionCSVPath = "/Editor/CSVs/Questions.csv";
    private static string questionsPath = "Assets/Resources/Questions/";
    private static int numberOfAnswers = 4;

    [MenuItem("Utilities/Generate Questions")]
    public static void GenerateQuestions()
    {
        Debug.Log("Generating Questions...");

        // Read all lines and process with regex CSV parsing
        string[] allLines = File.ReadAllLines(Application.dataPath + questionCSVPath);

        foreach (string line in allLines)
        {
            // Regex to split while respecting quoted fields
            string[] splitData = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

            // Trim quotes from each field
            for (int i = 0; i < splitData.Length; i++)
            {
                splitData[i] = splitData[i].Trim('"');
            }

            QuestionData questionData = ScriptableObject.CreateInstance<QuestionData>();
            questionData.question = splitData[0];
            questionData.category = splitData[1];
            questionData.answers = new string[numberOfAnswers];

            // Ensure we have enough columns
            if (splitData.Length < 2 + numberOfAnswers)
            {
                Debug.LogError($"Insufficient data in line: {line}");
                continue;
            }

            // Process answers
            for (int i = 0; i < numberOfAnswers; i++)
            {
                questionData.answers[i] = splitData[2 + i];
            }

            // Create clean filename
            string cleanName = CleanFileName(questionData.question);
            questionData.name = cleanName;

            // Ensure directory exists
            if (!Directory.Exists(questionsPath))
            {
                Directory.CreateDirectory(questionsPath);
            }

            // Generate unique asset path to handle duplicates
            string assetPath = $"{questionsPath}/{cleanName}.asset";
            int duplicateCount = 1;
            while (File.Exists(assetPath))
            {
                assetPath = $"{questionsPath}/{cleanName}_{duplicateCount}.asset";
                duplicateCount++;
            }

            // Save asset
            AssetDatabase.CreateAsset(questionData, assetPath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Question generation completed!");
    }

    private static string CleanFileName(string input)
    {
        // Remove special characters and extra spaces
        string cleaned = Regex.Replace(input, @"[\?*,\-:""!@]", "");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
    }
}

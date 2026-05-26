using System;
using System.IO;
using UnityEngine;

//script to pull and store questions from the json.

public static class QuestionSetStorage
{
    public static string GetQuestionSetsRoot()
    {
        // Folder next to the .exe (or in the same folder as Assets, Packages in unity folder)
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string setsRoot = Path.Combine(root, "QuestionSets");
        if (!Directory.Exists(setsRoot))
            Directory.CreateDirectory(setsRoot);
        return setsRoot;
    }

    public static string GetSetFolder(string setName)
    {
        return Path.Combine(GetQuestionSetsRoot(), setName);
    }

    public static string GetSetJsonPath(string setName)
    {
        // "questions.json" is the file name looked for to load questions
        return Path.Combine(GetSetFolder(setName), "questions.json");
    }

    public static bool TryLoadSet(string setName, out QuestionSetFile setFile, out string error)
    {
        setFile = null;
        error = null;

        try
        {
            string path = GetSetJsonPath(setName);
            if (!File.Exists(path))
            {
                error = $"Missing questions.json at: {path}";
                return false;
            }

            string json = File.ReadAllText(path);
            setFile = JsonUtility.FromJson<QuestionSetFile>(json);

            if (setFile == null || setFile.questions == null || setFile.questions.Count == 0)
            {
                error = "Set loaded but contains no questions.";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}
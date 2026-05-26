using System.Collections.Generic;
using System.IO;
using UnityEngine;

// this script builds runtime questions from the selected JSON set

public static class RuntimeQuestionBuilder
{
    public static List<RuntimeQuestion> BuildFromSet(QuestionSetFile setFile, string setFolder)
    {
        var result = new List<RuntimeQuestion>(setFile.questions.Count);

        foreach (var q in setFile.questions)
        {
            var rq = new RuntimeQuestion();
            rq.question = q.question;
            rq.category = q.category;

            rq.answers = new string[4];
            rq.answers[0] = q.correct;
            rq.answers[1] = q.wrong[0];
            rq.answers[2] = q.wrong[1];
            rq.answers[3] = q.wrong[2];

            rq.imageSprite = LoadSpriteIfAny(setFolder, q.image);

            result.Add(rq);
        }

        return result;
    }

    private static Sprite LoadSpriteIfAny(string setFolder, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        string fullPath = Path.Combine(setFolder, relativePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"Image not found: {fullPath}");
            return null;
        }

        byte[] data = File.ReadAllBytes(fullPath);

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(data))
        {
            Debug.LogWarning($"Failed to LoadImage: {fullPath}");
            Object.Destroy(tex);
            return null;
        }

        tex.name = Path.GetFileName(fullPath);

        return Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
        );
    }
}
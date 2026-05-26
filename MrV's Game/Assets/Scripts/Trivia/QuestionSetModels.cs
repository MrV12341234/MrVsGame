using System.Collections.Generic;
// this script is a runtime data model to help setup the json files. Its a wrapper object to use unitys JsonUtility.

[System.Serializable]
public class QuestionSetFile
{
    public string setName;
    public List<QuestionFile> questions = new();
}

[System.Serializable]
public class QuestionFile
{
    public string id;          // unique string, like "geo_0001"
    public string question;
    public string category;
    public string correct;     // correct answer (what used to be answers[0])
    public string[] wrong = new string[3]; // 3 wrong answers
    public string image;       // relative path like "images/q_0001.jpg" or "" if none
}

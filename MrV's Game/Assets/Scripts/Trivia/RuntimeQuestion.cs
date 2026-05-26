using UnityEngine;
// this script is a helper, called from QuestionSetup.cs (GetQuestionAssets method) and AnswerButton.cs at runtime. Helps understand what is pulled from the json files.
// it's a runtime "question" struct used by gameplay.
public class RuntimeQuestion
{
    public string question;
    public string category;

    // correct answer MUST be answers[0] (same rule you already use)
    public string[] answers;     // length 4

    public Sprite imageSprite;   // can be null

    public string correctAnswer
        => (answers != null && answers.Length > 0) ? answers[0] : "";
}
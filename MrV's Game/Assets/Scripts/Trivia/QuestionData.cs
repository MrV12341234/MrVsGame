using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// this script used to be called in QuestionSetup.cs when the trivia questions were pulled from the scriptable objects in unity. It was changed on 2.28.2026 if you need to find old script in GitHub
[CreateAssetMenu(fileName = "Question", menuName = "ScriptableObjects/Question", order = 1)]
public class QuestionData : ScriptableObject
{
    public string question;
    public string category;
  

    [Tooltip("correct answer should always be listed first here, they are randomized later")]
    public string[] answers;
   
    public Sprite questionImage;
   
}
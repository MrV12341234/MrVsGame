using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;


[CreateAssetMenu(fileName = "Question", menuName = "ScriptableObjects/Question", order = 1)]
public class QuestionData : ScriptableObject
{
    public string question;
    public string category;
  

    [Tooltip("correct answer should always be listed first here, they are randomized later")]
    public string[] answers;
   
    public Sprite questionImage;
   
}
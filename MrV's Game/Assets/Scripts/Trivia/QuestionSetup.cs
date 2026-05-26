using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestionSetup : MonoBehaviour
{
    public List<RuntimeQuestion> questions;
    private RuntimeQuestion currentQuestion;
    
    private static List<RuntimeQuestion> unusedQuestions; // persists per client.Tracks questions not used
    private static bool sInitialized = false;          // init guard
    
    [SerializeField]
    private TextMeshProUGUI questionText;
    [SerializeField]
    private TextMeshProUGUI categoryText;
    [SerializeField]
    public AnswerButton[] answerButtons;
    [SerializeField] 
    public TextMeshProUGUI feedbackText; // correct answer text

    [SerializeField]
    private int correctAnswerChoice;

    [SerializeField] 
    private Image questionImage;

    private void Awake()
    {
        LoadSelectedQuestionSet();
        
        // Only initialize the unused pool once per client session
        if (!sInitialized || unusedQuestions == null || unusedQuestions.Count == 0)
        {
            ResetUnusedQuestions(); // Initialize the unused questions list
            sInitialized = true;
        }
    }
    
    private void LoadSelectedQuestionSet()
    {
        // For now, pick from PlayerPrefs. Later your host dropdown will set this.
        string setName = PlayerPrefs.GetString("SelectedQuestionSet", "Default");

        if (!QuestionSetStorage.TryLoadSet(setName, out var setFile, out var error))
        {
            Debug.LogError($"Failed to load set '{setName}': {error}");
            questions = new List<RuntimeQuestion>();
            ResetUnusedQuestions();
            sInitialized = true;
            return;
        }

        string folder = QuestionSetStorage.GetSetFolder(setName);
        questions = RuntimeQuestionBuilder.BuildFromSet(setFile, folder);
        
        // If you change question sets between matches, you should reset the pool whenever you load a set:
        ResetUnusedQuestions();
        sInitialized = true;
    }
    public void InitializeNewQuestion()
    {
        // Clear any previous feedback
        if(feedbackText != null) feedbackText.text = "";

        SelectNewQuestion();
        SetQuestionValues();
        SetAnswerValues();
    }

    private void ResetUnusedQuestions()
    {
        unusedQuestions = new List<RuntimeQuestion>(questions);
    }

    public void SelectNewQuestion()
    {
        // If we've used all questions, reset the unused list
        if (unusedQuestions.Count == 0)
        {
            ResetUnusedQuestions();
        }

        // Pick a random question from the unused list
        int randomQuestionIndex = Random.Range(0, unusedQuestions.Count);
        currentQuestion = unusedQuestions[randomQuestionIndex];
        
        // Remove the selected question so it won't be asked again
        unusedQuestions.RemoveAt(randomQuestionIndex);
    }

    public void SetQuestionValues()
    {
        questionText.text = currentQuestion.question;
        categoryText.text = currentQuestion.category;

        // RuntimeQuestion.cs uses imageSprite (not questionImage like before when we used QuestionData.cs and scriptable objects)
        if (currentQuestion.imageSprite != null)
        {
            questionImage.sprite = currentQuestion.imageSprite;
            questionImage.gameObject.SetActive(true);
        }
        else
        {
            questionImage.gameObject.SetActive(false);
        }
    }

    public void SetAnswerValues()
    {
        List<string> answers = RandomizeAnswers(new List<string>(currentQuestion.answers));

        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool isCorrect = (i == correctAnswerChoice);
            answerButtons[i].SetIsCorrect(isCorrect);
            answerButtons[i].SetAnswerText(answers[i]);
        }
    }

    private List<string> RandomizeAnswers(List<string> originalList)
    {
        bool correctAnswerChosen = false;
        List<string> newList = new List<string>();

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int random = Random.Range(0, originalList.Count);
            // If the chosen index is 0, treat that as the correct answer (only once)
            if (random == 0 && !correctAnswerChosen)
            {
                correctAnswerChoice = i;
                correctAnswerChosen = true;
            }
            newList.Add(originalList[random]);
            originalList.RemoveAt(random);
        }

        return newList;
    }
    // Helper method to get the correct answer text from the current question
    public string GetCorrectAnswerText()
    {
// The QuestionData states that the correct answer is always the first in the original array,
// you can use that. Alternatively, you could retrieve the answer from the answerButton which was flagged correct.
        return currentQuestion.correctAnswer;
    }

}
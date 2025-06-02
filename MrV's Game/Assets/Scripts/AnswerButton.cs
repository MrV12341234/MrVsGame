using System.Collections;
using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Pun.UtilityScripts;

public class AnswerButton : MonoBehaviour
{
    private bool isCorrect;
    [SerializeField] private TextMeshProUGUI answerText;
    [SerializeField] public QuestionSetup questionSetup;

    private bool isInDelay = false;

    public void SetAnswerText(string newText)
    {
        answerText.text = newText;
    }

    public string GetAnswerText()
    {
        return answerText.text;
    }

    public void SetIsCorrect(bool newBool)
    {
        isCorrect = newBool;
    }

    public void OnClick()
    {
        if (isInDelay) return;

        if (GameMode.IsLAN)
        {
            HandleLanAnswer();
        }
        else
        {
            HandlePhotonAnswer();
        }
    }

    private void HandlePhotonAnswer()
    {
        if (isCorrect)
        {
            Debug.Log("Correct Answer [PHOTON]");
            RoomManager.Instance.getCorrectAnswer();
            PhotonNetwork.LocalPlayer.AddScore(100);

            if (questionSetup.questions.Count > 0)
            {
                questionSetup.InitializeNewQuestion();
            }
        }
        else
        {
            isInDelay = true;
            Debug.Log("Wrong Answer [PHOTON]");
            RoomManager.Instance.getWrongAnswer();
            PhotonNetwork.LocalPlayer.AddScore(-100);

            string correctAnswer = questionSetup.GetCorrectAnswerText();
            if (questionSetup.feedbackText != null)
                questionSetup.feedbackText.text = "Correct Answer: " + correctAnswer;

            StartCoroutine(WrongAnswerDelay());
        }
    }
    private void HandleLanAnswer()
    {
        if (isCorrect)
        {
            Debug.Log("Correct Answer [LAN]");
            RoomManagerLan.Instance.getCorrectAnswer();
            PlayerPrefs.SetInt("LAN_Score", PlayerPrefs.GetInt("LAN_Score", 0) + 100);

            if (questionSetup.questions.Count > 0 && RoomManagerLan.Instance.correctAnswerCounter < 3)
            {
                questionSetup.InitializeNewQuestion();
            }
        }
        else
        {
            isInDelay = true;
            Debug.Log("Wrong Answer [LAN]");
            RoomManagerLan.Instance.getWrongAnswer();
            PlayerPrefs.SetInt("LAN_Score", PlayerPrefs.GetInt("LAN_Score", 0) - 100);

            string correctAnswer = questionSetup.GetCorrectAnswerText();
            if (questionSetup.feedbackText != null)
                questionSetup.feedbackText.text = "Correct Answer: " + correctAnswer;

            StartCoroutine(WrongAnswerDelay());
        }
    }

    private IEnumerator WrongAnswerDelay()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        yield return new WaitForSeconds(5f);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (questionSetup.questions.Count > 0)
        {
            if (questionSetup.feedbackText != null)
                questionSetup.feedbackText.text = "";

            questionSetup.InitializeNewQuestion();
        }

        isInDelay = false;
    }
}

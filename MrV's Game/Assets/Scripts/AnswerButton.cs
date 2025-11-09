using System.Collections;
using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Unity.Netcode;

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
            // Update leaderboard score if game on photon server
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
            // Update leaderboard score if game on photon server
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
            RoomManagerLan.Instance.getCorrectAnswer();
            //update leaderboard score if LAN game. This point / score is updated in LeaderboardManagerLAN (or in the inspector inside each map)
            LeaderboardManagerLAN.Instance?.ReportCorrectAnswerServerRpc(NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            isInDelay = true;
            
            RoomManagerLan.Instance.getWrongAnswer();
            //update leaderboard score if LAN game
            LeaderboardManagerLAN.Instance?.ReportWrongAnswerServerRpc(NetworkManager.Singleton.LocalClientId);

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